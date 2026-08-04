namespace GmToolkit.Core.Import;

/// <summary>
/// How <see cref="CampaignImportOrchestrator"/> should resolve an import whose
/// <see cref="CampaignExportDto.Name"/> already matches an existing campaign (issue #130's
/// "Conflict resolution: Overwrite existing / Merge / Skip"). Meaningless -- never consulted -- when
/// there's no conflict at all: an import whose name doesn't match any existing campaign is always
/// just created fresh.
/// </summary>
public enum ImportConflictResolution
{
    /// <summary>Replace the existing campaign (and every one of its player characters/NPCs)
    /// entirely with the imported one -- <see cref="Repositories.ICampaignRepository.ImportCampaignAsync"/>'s
    /// own <c>overwrite: true</c>.</summary>
    Overwrite,

    /// <summary>Keep the existing campaign, adding every imported player character/NPC that isn't
    /// already present (matched by name) and replacing in place any that is --
    /// <see cref="Repositories.IPlayerCharacterRepository.ImportCharactersAsync"/>/
    /// <see cref="Repositories.INpcRepository.ImportCharactersAsync"/>'s own per-entry overwrite
    /// semantics. See <see cref="CampaignImportOrchestrator"/>'s remarks on why this isn't fully
    /// transactional the way <see cref="Overwrite"/> is.</summary>
    Merge,

    /// <summary>Leave the existing campaign untouched and import nothing.</summary>
    Skip,
}