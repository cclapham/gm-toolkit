namespace GmToolkit.Core.Systems;

/// <summary>
/// One field in a <see cref="CharacterSystem"/>'s <see cref="CharacterSystem.PcFields"/>/
/// <see cref="CharacterSystem.NpcFields"/> (or, one level down, a <c>repeating-group</c>'s
/// <see cref="ItemFields"/>). See SYSTEMS.md's "Field types" section for the full shape and
/// per-type rules.
/// </summary>
/// <remarks>
/// A single flat POCO rather than seven type-specific subclasses, because that's the JSON shape
/// every field is actually written in — a <see cref="Type"/> discriminator alongside whichever of
/// these properties that type happens to use (see SYSTEMS.md's worked examples). Which properties
/// each <see cref="Type"/> may or must set is enforced by <see cref="CharacterSystemLoader"/>'s
/// load-time validation, not by the shape of this type itself.
/// </remarks>
public sealed class StatFieldDefinition
{
    /// <summary>
    /// Stable identifier: <c>^[a-zA-Z_][a-zA-Z0-9_]*$</c>. Both the dictionary key a stat value is
    /// stored under and the identifier a <c>derived</c> formula references. Never renamed once a
    /// pack ships.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>Display name shown to the GM/player.</summary>
    public required string Label { get; init; }

    /// <summary>Discriminator: one of the <see cref="StatFieldTypes"/> constants.</summary>
    public required string Type { get; init; }

    /// <summary>Optional short GM-facing description of what the field means. Not load-bearing for validation.</summary>
    public string? HelpText { get; init; }

    /// <summary>
    /// <c>number</c>: an inclusive validation range — a value outside it is rejected as invalid
    /// input. <c>derived</c>: a clamp on the computed result — a value outside it is pulled to the
    /// nearest bound, never rejected. Same property/shape, deliberately different operations; see
    /// SYSTEMS.md's <c>derived</c> section for why the two are stated separately even though they
    /// look identical.
    /// </summary>
    public decimal? Min { get; init; }

    /// <summary>See <see cref="Min"/>.</summary>
    public decimal? Max { get; init; }

    /// <summary>
    /// Decimal places the value is stored/displayed at. Defaults to <c>0</c> when unset. Used by
    /// both <c>number</c> (presentation only) and <c>derived</c> (see
    /// <c>GmToolkit.Core.Systems.Formula.DerivedFieldEvaluator</c>).
    /// </summary>
    public int? Precision { get; init; }

    /// <summary><c>number</c> only: a UI increment hint (e.g. spinner step). Not a validation rule.</summary>
    public decimal? Step { get; init; }

    /// <summary><c>number</c> only: the value a freshly-created character starts with.</summary>
    public decimal? Default { get; init; }

    /// <summary>
    /// <c>text</c>/<c>free-text-block</c>: maximum stored/displayed length. Type-specific defaults
    /// apply when unset (500 for <c>text</c>, 4000 for <c>free-text-block</c> — see
    /// <see cref="CharacterSystemLoader"/>); the engine enforces a 10,000-character hard ceiling
    /// regardless of what a pack declares.
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>
    /// <c>text</c> only: a regex (must compile under <see cref="System.Text.RegularExpressions.RegexOptions.NonBacktracking"/>)
    /// the value must match. A field that sets this must also set <see cref="MaxLength"/>. Max 200
    /// characters.
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary><c>enum</c> only: the fixed, closed list of valid values, in display order. Required, non-empty.</summary>
    public IReadOnlyList<string>? Options { get; init; }

    /// <summary>
    /// <c>derived</c> only: the formula text — see SYSTEMS.md's "The derived-formula grammar" and
    /// <c>GmToolkit.Core.Systems.Formula.FormulaParser</c>. Required.
    /// </summary>
    public string? Formula { get; init; }

    /// <summary>
    /// <c>derived</c> only: how the clamped result is rounded to <see cref="Precision"/> decimal
    /// places — one of <see cref="RoundingModes"/>' constants. Defaults to
    /// <see cref="RoundingModes.None"/> when unset.
    /// </summary>
    public string? Rounding { get; init; }

    /// <summary>
    /// <c>repeating-group</c> only: the field definitions describing one row. Required, non-empty.
    /// May not contain another <c>repeating-group</c>, nor a <c>derived</c> field (which is
    /// top-level-only — see SYSTEMS.md's "Scope resolution").
    /// </summary>
    public IReadOnlyList<StatFieldDefinition>? ItemFields { get; init; }

    /// <summary><c>repeating-group</c> only: minimum row count. Optional; an empty list is valid when unset.</summary>
    public int? MinItems { get; init; }

    /// <summary>
    /// <c>repeating-group</c> only: maximum row count. Optional; the engine defaults to 100 when
    /// unset and enforces a 1,000-row hard ceiling regardless of what a pack declares.
    /// </summary>
    public int? MaxItems { get; init; }
}