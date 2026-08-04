using GmToolkit.Core.Models;

namespace GmToolkit.Core.Import;

/// <summary>The outcome of <see cref="CampaignImportOrchestrator.ImportAsync"/> -- see that type's
/// remarks for how each <see cref="ImportConflictResolution"/> maps to one of this record's factory
/// methods.</summary>
/// <param name="Succeeded">Whether anything was actually written.</param>
/// <param name="WasSkipped">True only for <see cref="ImportConflictResolution.Skip"/> on an actual
/// conflict -- distinct from an ordinary validation failure so a caller can show a neutral "nothing
/// imported" message rather than an error.</param>
/// <param name="Campaign">The resulting campaign (freshly created, the replacement from an
/// <see cref="ImportConflictResolution.Overwrite"/>, or the same existing campaign a
/// <see cref="ImportConflictResolution.Merge"/> added into) when <paramref name="Succeeded"/> is
/// <c>true</c>; <c>null</c> otherwise.</param>
/// <param name="CharactersImported">Count of player characters actually created/replaced.</param>
/// <param name="NpcsImported">Count of NPCs actually created/replaced.</param>
/// <param name="Validation">Blocking errors (if any) and non-blocking warnings -- see
/// <see cref="ImportValidator"/>.</param>
/// <param name="CharacterErrors">Per-entry skip reasons from a <see cref="ImportConflictResolution.Merge"/>'s
/// best-effort player-character import -- always empty for every other outcome.</param>
/// <param name="NpcErrors">See <see cref="CharacterErrors"/>, for NPCs.</param>
public sealed record CampaignImportOutcome(
    bool Succeeded,
    bool WasSkipped,
    Campaign? Campaign,
    int CharactersImported,
    int NpcsImported,
    ValidationResult Validation,
    IReadOnlyList<ImportItemError> CharacterErrors,
    IReadOnlyList<ImportItemError> NpcErrors)
{
    public static CampaignImportOutcome Created(Campaign campaign, int characters, int npcs) =>
        new(true, false, campaign, characters, npcs, ValidationResult.Success(), [], []);

    public static CampaignImportOutcome Merged(Campaign campaign, BulkImportResult<PlayerCharacter> characters, BulkImportResult<Npc> npcs) =>
        new(true, false, campaign, characters.Imported.Count, npcs.Imported.Count, ValidationResult.Success(), characters.Errors, npcs.Errors);

    public static CampaignImportOutcome Skipped(string campaignName) =>
        new(false, true, null, 0, 0, ValidationResult.Failure($"Skipped: a campaign named '{campaignName}' already exists."), [], []);

    public static CampaignImportOutcome Failed(ValidationResult validation) =>
        new(false, false, null, 0, 0, validation, [], []);
}