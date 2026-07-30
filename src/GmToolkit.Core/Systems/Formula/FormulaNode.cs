namespace GmToolkit.Core.Systems.Formula;

/// <summary>
/// Base type for the derived-formula grammar's AST. SYSTEMS.md's own claim: a hand-rolled
/// recursive-descent parser over this grammar produces an AST of exactly these four node kinds and
/// "cannot be coerced into doing anything a four-function calculator couldn't" — there is
/// deliberately no fifth node kind (no function call, no conditional).
/// </summary>
public abstract class FormulaNode
{
    /// <summary>
    /// Collects every <see cref="FieldRefNode.Key"/> reachable from <paramref name="node"/>, in no
    /// particular order (duplicates possible). Recursive, but safely bounded: the deepest an AST
    /// built by <see cref="FormulaParser"/> can ever be is on the order of the 500-character
    /// maximum formula length (each level of nesting or each additional chained operator consumes
    /// at least one character of the source string), which is nowhere near enough C# call frames to
    /// threaten the stack — unlike the derived-field dependency graph itself (see
    /// <see cref="DerivedFieldGraph"/>), whose size is bounded by field count, not formula length,
    /// and which is therefore walked iteratively instead.
    /// </summary>
    public static IEnumerable<string> CollectFieldReferences(FormulaNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        switch (node)
        {
            case LiteralNode:
                yield break;

            case FieldRefNode fieldRef:
                yield return fieldRef.Key;
                yield break;

            case UnaryNegateNode unary:
                foreach (var key in CollectFieldReferences(unary.Operand))
                {
                    yield return key;
                }

                yield break;

            case BinaryOpNode binary:
                foreach (var key in CollectFieldReferences(binary.Left))
                {
                    yield return key;
                }

                foreach (var key in CollectFieldReferences(binary.Right))
                {
                    yield return key;
                }

                yield break;

            default:
                throw new NotSupportedException($"Unknown {nameof(FormulaNode)} subtype '{node.GetType()}'.");
        }
    }
}

/// <summary>A literal <c>number-literal</c> — always non-negative per the grammar (negation is <see cref="UnaryNegateNode"/>).</summary>
public sealed class LiteralNode(decimal value) : FormulaNode
{
    public decimal Value { get; } = value;
}

/// <summary>A bare <c>field-reference</c> — the key of another field visible in scope.</summary>
public sealed class FieldRefNode(string key) : FormulaNode
{
    public string Key { get; } = key;
}

/// <summary>A binary <c>+</c>/<c>-</c>/<c>*</c>/<c>/</c> operation.</summary>
public sealed class BinaryOpNode(char op, FormulaNode left, FormulaNode right) : FormulaNode
{
    public char Operator { get; } = op;

    public FormulaNode Left { get; } = left;

    public FormulaNode Right { get; } = right;
}

/// <summary>Unary <c>-factor</c>.</summary>
public sealed class UnaryNegateNode(FormulaNode operand) : FormulaNode
{
    public FormulaNode Operand { get; } = operand;
}