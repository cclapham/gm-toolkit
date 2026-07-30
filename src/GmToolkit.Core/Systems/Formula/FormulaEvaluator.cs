namespace GmToolkit.Core.Systems.Formula;

/// <summary>
/// Evaluates a parsed formula AST against a set of already-resolved field values. The numeric type
/// is <see cref="decimal"/> throughout (see SYSTEMS.md's "Numeric type"): unlike <see cref="double"/>,
/// <see cref="decimal"/> throws <see cref="OverflowException"/> on overflow and
/// <see cref="DivideByZeroException"/> on division by zero rather than silently producing an
/// infinite or NaN value that could sail through a clamp/round/serialize step looking like an
/// ordinary number.
/// </summary>
/// <remarks>
/// Deliberately does not catch <see cref="DivideByZeroException"/>, <see cref="OverflowException"/>,
/// or the <see cref="KeyNotFoundException"/> thrown for an unresolved <see cref="FieldRefNode"/>
/// itself — SYSTEMS.md's "Runtime failure semantics" says each of those fails a *field* closed, and
/// it's the caller (<see cref="DerivedFieldEvaluator"/>) that knows which field is being computed
/// and turns the exception into that field's <c>null</c> result, rather than this type guessing at
/// field-level context it doesn't have.
/// </remarks>
public static class FormulaEvaluator
{
    public static decimal Evaluate(FormulaNode node, IReadOnlyDictionary<string, decimal> resolvedValues)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(resolvedValues);

        return node switch
        {
            LiteralNode literal => literal.Value,
            FieldRefNode fieldRef => resolvedValues.TryGetValue(fieldRef.Key, out var value)
                ? value
                : throw new KeyNotFoundException($"Formula references field '{fieldRef.Key}', which has no resolved value for this character."),
            UnaryNegateNode unary => -Evaluate(unary.Operand, resolvedValues),
            BinaryOpNode binary => EvaluateBinary(binary, resolvedValues),
            _ => throw new NotSupportedException($"Unknown {nameof(FormulaNode)} subtype '{node.GetType()}'."),
        };
    }

    private static decimal EvaluateBinary(BinaryOpNode node, IReadOnlyDictionary<string, decimal> resolvedValues)
    {
        var left = Evaluate(node.Left, resolvedValues);
        var right = Evaluate(node.Right, resolvedValues);

        return node.Operator switch
        {
            '+' => left + right,
            '-' => left - right,
            '*' => left * right,
            '/' => left / right, // decimal division by zero always throws DivideByZeroException
            _ => throw new NotSupportedException($"Unknown binary operator '{node.Operator}'."),
        };
    }
}