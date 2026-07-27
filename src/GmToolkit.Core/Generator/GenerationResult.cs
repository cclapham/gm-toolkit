namespace GmToolkit.Core.Generator;

/// <summary>
/// The outcome of one constrained generation draw (issue #27): the value produced, plus — only
/// when a requested constraint (a name culture, an occupation category tag) didn't match anything
/// and generation fell back to an unconstrained pick instead of throwing or returning an empty
/// string — a human-readable notice describing what happened and why.
/// </summary>
/// <remarks>
/// This is Core's half of #27's "never throw, never return empty, but tell the caller when a
/// fallback happened" requirement. There's no UI yet for surfacing <see cref="FallbackNotice"/> —
/// that's a future issue's (#28's constraint UI) job — but the information has to exist somewhere
/// for that UI to read, rather than being silently discarded the moment a fallback occurs. A plain
/// <c>string</c> return, as every unconstrained <see cref="IGenerator{TResult}"/> still uses today,
/// has nowhere to carry that notice, which is why constrained draws use this wrapper instead.
/// </remarks>
/// <param name="Value">The generated value. Always populated — never empty, even on fallback.</param>
/// <param name="FallbackNotice">
/// Null when no constraint was requested, or when the requested constraint was honored exactly.
/// Non-null only when a constraint was requested but nothing matched it, in which case it
/// describes what was requested and what was used instead.
/// </param>
public sealed record GenerationResult(string Value, string? FallbackNotice)
{
    /// <summary>True when a requested constraint could not be honored and a fallback pick was used instead.</summary>
    public bool FellBack => FallbackNotice is not null;

    /// <summary>Wraps a value produced with no constraint involved (or a constraint that was honored exactly) — never a fallback.</summary>
    public static GenerationResult Unconstrained(string value) => new(value, FallbackNotice: null);
}