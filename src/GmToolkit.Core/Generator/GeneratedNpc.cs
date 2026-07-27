namespace GmToolkit.Core.Generator;

/// <summary>
/// The result of generating an NPC: a lightweight, generator-specific DTO rather than
/// <see cref="Models.Npc"/> itself. Two reasons: (1) <see cref="Models.Npc.CampaignId"/> isn't
/// known until #29's save step — there's no sensible value to put there mid-generation; (2)
/// <see cref="Models.Npc.Name"/>'s setter enforces required/max-length validation meant for a
/// persisted domain root, which doesn't fit an in-progress, field-by-field, possibly-partially-blank
/// generation result. Mapping a <see cref="GeneratedNpc"/> onto a real <see cref="Models.Npc"/>
/// (setting <c>CampaignId</c>, <c>WasGenerated = true</c>, and optionally <c>Role</c>/
/// <c>Faction</c>/<c>Location</c>) is #29's job.
/// </summary>
/// <remarks>
/// Properties are mutable (not init-only) so a caller — e.g. #28's per-field reroll — can replace
/// one field's value in place via <see cref="INpcGenerator.GenerateField"/> without reconstructing
/// the whole object or touching any other field.
/// </remarks>
public sealed class GeneratedNpc
{
    public string Name { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Appearance { get; set; } = string.Empty;

    public string Mannerism { get; set; } = string.Empty;

    public string Motivation { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;
}