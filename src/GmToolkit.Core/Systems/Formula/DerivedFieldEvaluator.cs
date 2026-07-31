using System.Globalization;

namespace GmToolkit.Core.Systems.Formula;

/// <summary>
/// Evaluates every top-level <c>derived</c> field in one scope against a character's raw Stats bag
/// (a plain <c>Dictionary&lt;string, string&gt;</c> — see SYSTEMS.md's "Attachment point": a
/// <c>derived</c> field's value is never persisted, only recomputed). Single-pass and memoized per
/// SYSTEMS.md's "Resource limits": each field is evaluated using <paramref name="evaluationOrder"/>'s
/// fixed order (see <see cref="DerivedFieldGraph.Build"/>, computed once at
/// <see cref="CharacterSystem"/> load time) so no field's formula is ever re-run from scratch to
/// satisfy another field's reference to it — the difference between O(field count) and the
/// exponential blowup a naive "resolve a reference by re-running that field's own formula" approach
/// hits.
/// </summary>
public static class DerivedFieldEvaluator
{
    /// <summary>
    /// Computes every <c>derived</c> field's value in <paramref name="evaluationOrder"/>. A field
    /// whose formula fails at runtime — division by zero, decimal overflow, or an unresolved field
    /// reference (including one that failed closed earlier in the same pass) — evaluates to
    /// <c>null</c>: SYSTEMS.md's "Runtime failure semantics" fail-closed contract, applied
    /// transitively through the dependency chain, and never an uncaught exception.
    /// </summary>
    /// <param name="fields">Every top-level field in the scope (both <c>derived</c> and not) — needed so non-derived fields' raw values can be resolved as formula inputs.</param>
    /// <param name="evaluationOrder">The scope's <c>derived</c> fields in dependency order (see <see cref="DerivedFieldGraph.Build"/>).</param>
    /// <param name="rawValues">The character's raw Stats bag.</param>
    public static IReadOnlyDictionary<string, decimal?> EvaluateAll(
        IReadOnlyList<StatFieldDefinition> fields,
        IReadOnlyList<string> evaluationOrder,
        IReadOnlyDictionary<string, string> rawValues)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(evaluationOrder);
        ArgumentNullException.ThrowIfNull(rawValues);

        var fieldsByKey = fields.ToDictionary(f => f.Key, f => f, StringComparer.Ordinal);

        // Seed with every non-derived field whose stored raw value parses cleanly as a decimal.
        // SYSTEMS.md's "Scope resolution" places no type restriction on what a formula may
        // reference, so a stored `boolean`/`enum`/`text` value that doesn't parse as a plain
        // decimal is simply left unseeded here; any formula referencing it then fails closed as an
        // unresolved reference below -- exactly the same treatment a missing key gets, and exactly
        // SYSTEMS.md's "Runtime failure semantics" for runtime data that doesn't match its schema.
        //
        // A field with no stored raw value at all -- e.g. an adjustment field like
        // `initiativeProficiencyBonus` on a freshly-created character, which has never been
        // explicitly set -- falls back to its schema-declared `Default` instead of being left
        // unresolved. This is the same "input to the formula" operation as reading rawValues,
        // just from a different source: SYSTEMS.md's "adjustment field idiom" is that the default
        // contributes nothing (e.g. `0`) until explicitly set, not that the field is absent from
        // every formula that references it.
        var resolved = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (field.Type == StatFieldTypes.Derived)
            {
                continue;
            }

            if (rawValues.TryGetValue(field.Key, out var raw)
                && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                resolved[field.Key] = parsed;
            }
            else if (field.Default.HasValue)
            {
                resolved[field.Key] = field.Default.Value;
            }
        }

        var results = new Dictionary<string, decimal?>(StringComparer.Ordinal);
        foreach (var key in evaluationOrder)
        {
            var field = fieldsByKey[key];
            var value = EvaluateOne(field, resolved);
            results[key] = value;

            if (value.HasValue)
            {
                // Cached for reuse by every other derived field that references it -- this is the
                // entire memoization: a field is computed here at most once no matter how many
                // dependents reference it.
                resolved[key] = value.Value;
            }

            // When null, `key` is deliberately left out of `resolved`: any dependent's
            // FormulaEvaluator.Evaluate call then throws KeyNotFoundException for this key, which
            // EvaluateOne below catches and turns into that dependent's own null result -- so a
            // failed field's failure propagates transitively with no special-casing needed here.
        }

        return results;
    }

    private static decimal? EvaluateOne(StatFieldDefinition field, IReadOnlyDictionary<string, decimal> resolved)
    {
        try
        {
            var ast = FormulaParser.Parse(field.Formula ?? string.Empty);
            var value = FormulaEvaluator.Evaluate(ast, resolved);

            // Order of operations, per SYSTEMS.md: clamp first, *then* round the clamped value.
            if (field.Min.HasValue && value < field.Min.Value)
            {
                value = field.Min.Value;
            }

            if (field.Max.HasValue && value > field.Max.Value)
            {
                value = field.Max.Value;
            }

            var precision = field.Precision is > 0 ? field.Precision.Value : 0;
            return ApplyRounding(value, precision, field.Rounding ?? RoundingModes.None);
        }
        catch (DivideByZeroException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
        catch (FormulaParseException)
        {
            // Defense in depth only: CharacterSystemLoader validation already rejects any formula
            // that fails to parse before a character can ever be evaluated against this schema.
            return null;
        }
    }

    private static decimal ApplyRounding(decimal value, int precision, string rounding) => rounding switch
    {
        RoundingModes.Round => Math.Round(value, precision, MidpointRounding.AwayFromZero),
        RoundingModes.Floor => RoundAtPrecision(value, precision, Math.Floor),
        RoundingModes.Ceiling => RoundAtPrecision(value, precision, Math.Ceiling),
        RoundingModes.Truncate => RoundAtPrecision(value, precision, Math.Truncate),
        _ => Math.Round(value, precision, MidpointRounding.ToEven), // RoundingModes.None (default)
    };

    private static decimal RoundAtPrecision(decimal value, int precision, Func<decimal, decimal> integerRoundingFunction)
    {
        var factor = Pow10(precision);
        return integerRoundingFunction(value * factor) / factor;
    }

    private static decimal Pow10(int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++)
        {
            result *= 10m;
        }

        return result;
    }
}