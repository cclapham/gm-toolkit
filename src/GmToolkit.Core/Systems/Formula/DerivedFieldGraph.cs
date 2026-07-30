namespace GmToolkit.Core.Systems.Formula;

/// <summary>
/// The dependency graph of every top-level <c>derived</c> field in one scope (<c>pcFields</c> or
/// <c>npcFields</c> — the two are always checked independently, per SYSTEMS.md's "Scope
/// resolution"). A node is a <c>derived</c> field's key; an edge from A to B means A's formula
/// references B, and B is itself <c>derived</c> (a reference to a plain <c>number</c>/etc. field is
/// a data read, not a dependency — it has no formula of its own that has to run first).
/// </summary>
/// <remarks>
/// Built with Kahn's algorithm — deliberately iterative (a queue-driven in-degree count-down), not
/// a recursive depth-first cycle check. SYSTEMS.md's "Resource limits" calls this out explicitly:
/// a naive recursive DFS cycle-check has the same unbounded-call-stack problem formula nesting does,
/// just one level up (a graph is bounded by field count rather than character count, but nothing
/// stops a hostile pack from declaring the maximum). Kahn's algorithm also happens to compute a
/// valid topological evaluation order and detect a cycle in the same pass, which is exactly what
/// <see cref="DerivedFieldEvaluator"/> needs to evaluate every field at most once.
/// </remarks>
public sealed class DerivedFieldGraph
{
    /// <summary>SYSTEMS.md's "Resource limits": maximum length of the longest dependency chain.</summary>
    public const int MaxChainDepth = 64;

    private DerivedFieldGraph(IReadOnlyList<string> evaluationOrder, int longestChainLength)
    {
        EvaluationOrder = evaluationOrder;
        LongestChainLength = longestChainLength;
    }

    /// <summary>
    /// Every <c>derived</c> field key in this scope, in a fixed topological order: a field always
    /// appears after every other <c>derived</c> field its own formula references. Safe to evaluate
    /// in this order in a single memoized pass — see <see cref="DerivedFieldEvaluator"/>.
    /// </summary>
    public IReadOnlyList<string> EvaluationOrder { get; }

    /// <summary>The number of fields on the longest dependency chain found in the graph.</summary>
    public int LongestChainLength { get; }

    /// <summary>
    /// Builds the dependency graph of every <c>derived</c> field in <paramref name="topLevelFields"/>.
    /// Throws <see cref="DerivedFieldGraphException"/> if any <c>derived</c> field's formula fails
    /// to parse (defense in depth — <see cref="CharacterSystemLoader"/> should already have
    /// rejected this), if the graph contains a cycle (a self-reference or a mutual reference, both
    /// load-time rejections per SYSTEMS.md's "Scope resolution"), or if its longest chain exceeds
    /// <see cref="MaxChainDepth"/>.
    /// </summary>
    public static DerivedFieldGraph Build(IReadOnlyList<StatFieldDefinition> topLevelFields)
    {
        ArgumentNullException.ThrowIfNull(topLevelFields);

        var derivedFields = topLevelFields.Where(f => f.Type == StatFieldTypes.Derived).ToList();
        var derivedKeys = derivedFields.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);

        // dependencies[key] = the set of other *derived* keys `key`'s own formula references.
        var dependencies = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        // dependents[key] = every derived field that depends on `key` (the reverse edges Kahn's
        // algorithm walks as each node's in-degree reaches zero).
        var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var key in derivedKeys)
        {
            dependencies[key] = [];
            dependents[key] = [];
        }

        foreach (var field in derivedFields)
        {
            FormulaNode ast;
            try
            {
                ast = FormulaParser.Parse(field.Formula ?? string.Empty);
            }
            catch (FormulaParseException ex)
            {
                throw new DerivedFieldGraphException($"derived field '{field.Key}' has an invalid formula: {ex.Message}", ex);
            }

            foreach (var referencedKey in FormulaNode.CollectFieldReferences(ast))
            {
                if (derivedKeys.Contains(referencedKey))
                {
                    dependencies[field.Key].Add(referencedKey);
                }
            }
        }

        foreach (var (key, deps) in dependencies)
        {
            foreach (var dep in deps)
            {
                dependents[dep].Add(key);
            }
        }

        // Kahn's algorithm: repeatedly dequeue a node with zero remaining (unsatisfied)
        // dependencies, append it to the evaluation order, then decrement the in-degree of
        // everything that depends on it. Entirely loop-driven -- no recursion, so no call-stack
        // growth regardless of field count or chain shape.
        var remainingDependencyCount = dependencies.ToDictionary(kv => kv.Key, kv => kv.Value.Count, StringComparer.Ordinal);
        var queue = new Queue<string>(remainingDependencyCount.Where(kv => kv.Value == 0).Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal));
        var order = new List<string>(derivedKeys.Count);
        var chainLength = new Dictionary<string, int>(StringComparer.Ordinal);

        while (queue.Count > 0)
        {
            var key = queue.Dequeue();
            order.Add(key);

            var currentChain = chainLength.TryGetValue(key, out var existingChain) ? existingChain : 1;
            chainLength[key] = currentChain;

            foreach (var dependent in dependents[key])
            {
                remainingDependencyCount[dependent]--;

                var candidateChain = currentChain + 1;
                if (!chainLength.TryGetValue(dependent, out var dependentChain) || candidateChain > dependentChain)
                {
                    chainLength[dependent] = candidateChain;
                }

                if (remainingDependencyCount[dependent] == 0)
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        if (order.Count != derivedKeys.Count)
        {
            var unresolved = derivedKeys.Except(order).OrderBy(k => k, StringComparer.Ordinal);
            throw new DerivedFieldGraphException(
                $"derived field dependency graph contains a cycle (a self-reference or a mutual reference) involving: {string.Join(", ", unresolved)}.");
        }

        var longestChain = chainLength.Count == 0 ? 0 : chainLength.Values.Max();
        if (longestChain > MaxChainDepth)
        {
            throw new DerivedFieldGraphException(
                $"derived field dependency chain is {longestChain} fields deep, exceeding the {MaxChainDepth}-field maximum.");
        }

        return new DerivedFieldGraph(order, longestChain);
    }
}