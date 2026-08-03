namespace GmToolkit.Core.Import;

/// <summary>
/// The wire shape of an <see cref="Models.Npc"/> inside a <see cref="CampaignExportDto"/>, per
/// <c>Resources/Schemas/campaign-export-v1.schema.json</c>.
/// </summary>
/// <remarks>
/// Carries no <see cref="Models.Npc.Id"/>, <see cref="Models.Npc.CampaignId"/>, or
/// <see cref="Models.Npc.CreatedUtc"/> — see <see cref="PlayerCharacterExportDto"/>'s remarks
/// (including why this is a primary-constructor <c>record</c>, not init-only auto-properties),
/// which this mirrors. <see cref="Name"/> is the stable identifier used for conflict detection on
/// re-import.
/// </remarks>
public sealed record NpcExportDto(
    string Name = "",
    string Role = "",
    string Faction = "",
    string Location = "",
    string Appearance = "",
    string Mannerism = "",
    string Motivation = "",
    string Secret = "",
    string Notes = "",
    bool KnownToPlayers = false,
    bool WasGenerated = false,
    Dictionary<string, string>? Stats = null)
{
    /// <summary>System-agnostic key/value bag, matching <see cref="Models.Npc.Stats"/>.</summary>
    public Dictionary<string, string> Stats { get; init; } = Stats ?? [];
}