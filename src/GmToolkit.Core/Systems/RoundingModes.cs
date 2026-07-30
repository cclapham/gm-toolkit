namespace GmToolkit.Core.Systems;

/// <summary>
/// The five <see cref="StatFieldDefinition.Rounding"/> values SYSTEMS.md's "Rounding is field
/// metadata" table defines, applied to a <c>derived</c> field's clamped result at
/// <see cref="StatFieldDefinition.Precision"/> decimal places. See
/// <c>GmToolkit.Core.Systems.Formula.DerivedFieldEvaluator</c> for exactly how each is applied.
/// </summary>
public static class RoundingModes
{
    /// <summary>Default. Round to nearest, ties to even (<see cref="MidpointRounding.ToEven"/>).</summary>
    public const string None = "none";

    /// <summary>Round to nearest, ties away from zero (<see cref="MidpointRounding.AwayFromZero"/>).</summary>
    public const string Round = "round";

    /// <summary>Always rounds down, toward negative infinity.</summary>
    public const string Floor = "floor";

    /// <summary>Always rounds up, toward positive infinity.</summary>
    public const string Ceiling = "ceiling";

    /// <summary>Rounds toward zero, dropping the excess digits regardless of sign.</summary>
    public const string Truncate = "truncate";

    /// <summary>Every recognized rounding value, for "is this a known value" load-time checks.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        None,
        Round,
        Floor,
        Ceiling,
        Truncate,
    };
}