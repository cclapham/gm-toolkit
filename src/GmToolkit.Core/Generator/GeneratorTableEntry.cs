namespace GmToolkit.Core.Generator;

/// <summary>
/// A single weighted entry in a <see cref="GeneratorTable"/>. Picking logic (issue #26) is not
/// implemented here — this type only describes the data shape loaded from JSON.
/// </summary>
public sealed class GeneratorTableEntry
{
    /// <summary>The text produced when this entry is selected. Required, non-empty.</summary>
    public required string Value { get; init; }

    /// <summary>
    /// Relative weight for random selection. Defaults to 1 when omitted from the source JSON.
    /// Must be greater than zero.
    /// </summary>
    public double Weight { get; init; } = 1;

    /// <summary>Optional free-form tags (e.g. "given", "surname", "criminal") for filtering.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}