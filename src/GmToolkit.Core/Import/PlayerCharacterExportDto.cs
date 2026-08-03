namespace GmToolkit.Core.Import;

/// <summary>
/// The wire shape of a <see cref="Models.PlayerCharacter"/> inside a
/// <see cref="CampaignExportDto"/>, per <c>Resources/Schemas/campaign-export-v1.schema.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately carries no <see cref="Models.PlayerCharacter.Id"/>,
/// <see cref="Models.PlayerCharacter.CampaignId"/>, or timestamp — export uses
/// <see cref="CharacterName"/> as the stable identifier and import always mints a fresh
/// <see cref="Guid"/> (see <see cref="PlayerCharacterExportMapper"/>). Every property is a plain,
/// unvalidated string/primitive rather than routing through <see cref="Models.PlayerCharacter"/>'s
/// own validating setters — a DTO deserialized from an untrusted file has to be constructible even
/// when its data is invalid, so <see cref="ImportValidator"/> can inspect it and report readable
/// errors instead of a deserialization-time exception. Flat by design (no nested arrays besides
/// <see cref="Stats"/>) so it also serves as the row shape for the CSV export planned in #131.
/// </para>
/// <para>
/// <b>A <c>record</c> with a primary constructor, every parameter defaulted, rather than
/// init-only auto-properties with field initializers.</b> Not a style choice — a plain class whose
/// <c>init</c> properties rely on <c>= someDefault</c> field initializers silently loses those
/// defaults under source-generated <see cref="System.Text.Json.JsonSerializer"/> deserialization
/// (<see cref="CampaignExportJsonContext"/>'s reflection-free contract): when a JSON payload omits
/// a property, the source generator constructs the instance via
/// <see cref="System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject"/> to allow
/// setting <c>init</c> members after construction, which skips the type's own constructor (and
/// therefore every field initializer) entirely — confirmed empirically while building this type;
/// the reflection-based serializer doesn't have this problem, but the source-generated one does. A
/// primary-constructor parameter default doesn't have this failure mode: the generator recognizes
/// and calls the parameterized constructor, supplying each defaulted argument for whatever the JSON
/// didn't include. <see cref="Stats"/> still needs its own body-level property (re-declared under
/// the same name as its constructor parameter, initialized from it with <c>??</c>) since a
/// <see cref="Dictionary{TKey,TValue}"/> default can't itself be a compile-time constant parameter
/// default.
/// </para>
/// </remarks>
public sealed record PlayerCharacterExportDto(
    string CharacterName = "",
    string PlayerName = "",
    string Ancestry = "",
    string Class = "",
    int Level = 1,
    string Notes = "",
    Dictionary<string, string>? Stats = null)
{
    /// <summary>System-agnostic key/value bag, matching <see cref="Models.PlayerCharacter.Stats"/>.</summary>
    public Dictionary<string, string> Stats { get; init; } = Stats ?? [];
}