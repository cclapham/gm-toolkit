using GmToolkit.Core.Repositories;
using GmToolkit.Core.Systems;

namespace GmToolkit.Core.Import;

/// <summary>
/// Orchestrates importing a whole <see cref="CampaignExportDto"/> against a database that may
/// already have a campaign of the same name (issue #130's "Conflict resolution: Overwrite existing
/// / Merge / Skip"), composing <see cref="ICampaignRepository"/>/<see cref="IPlayerCharacterRepository"/>/
/// <see cref="INpcRepository"/>'s already-existing methods rather than adding any new repository
/// surface -- see this class's remarks for exactly how each <see cref="ImportConflictResolution"/>
/// maps onto them.
/// </summary>
/// <remarks>
/// <para>
/// <b>No new Data-layer code needed.</b> Every branch below reuses exactly what #129 already built:
/// <see cref="ICampaignRepository.ImportCampaignAsync"/>'s own <c>overwrite</c> flag *is*
/// "Overwrite existing" (replace the whole campaign, cascading its old player characters/NPCs away
/// first) versus "no conflict, create fresh" when there isn't one;
/// <see cref="IPlayerCharacterRepository.ImportCharactersAsync"/>/<see cref="INpcRepository.ImportCharactersAsync"/>'s
/// own per-entry overwrite-by-name semantics *is* "Merge" (add whatever's new, replace same-named
/// entries in place, leave everything else in the existing campaign untouched). This class's only
/// real job is picking which of those calls to make based on whether a same-named campaign already
/// exists and which <see cref="ImportConflictResolution"/> the GM chose.
/// </para>
/// <para>
/// <b>"Merge" is not fully transactional the way "Overwrite"/"no conflict" are.</b>
/// <see cref="ICampaignRepository.ImportCampaignAsync"/>'s all-or-nothing transaction only ever
/// covers *replacing* a whole campaign or creating a brand-new one; merging into an
/// already-existing campaign's player characters/NPCs one at a time is
/// <see cref="IPlayerCharacterRepository.ImportCharactersAsync"/>/<see cref="INpcRepository.ImportCharactersAsync"/>'s
/// own best-effort-per-entry contract (see <see cref="BulkImportResult{T}"/>'s remarks), which this
/// class inherits rather than working around -- a merge that fails partway through leaves whatever
/// already succeeded in place, which is the friendlier outcome for a batch of otherwise-independent
/// characters/NPCs anyway.
/// </para>
/// </remarks>
public sealed class CampaignImportOrchestrator(
    ICampaignRepository campaignRepository,
    IPlayerCharacterRepository playerCharacterRepository,
    INpcRepository npcRepository)
{
    /// <param name="dto">The parsed, not-yet-validated import file contents.</param>
    /// <param name="resolution">How to resolve a name conflict, if there is one -- ignored entirely
    /// when there isn't (see <see cref="ImportConflictResolution"/>'s remarks).</param>
    /// <param name="systemRegistry">Passed straight through to <see cref="ImportValidator.ValidateCampaign"/>
    /// -- optional, per that method's own remarks on why.</param>
    public async Task<CampaignImportOutcome> ImportAsync(
        CampaignExportDto dto,
        ImportConflictResolution resolution,
        ICharacterSystemRegistry? systemRegistry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var validation = ImportValidator.ValidateCampaign(dto, systemRegistry);
        if (!validation.IsValid)
        {
            return CampaignImportOutcome.Failed(validation);
        }

        var existing = (await campaignRepository.GetAllAsync(cancellationToken))
            .FirstOrDefault(campaign => string.Equals(campaign.Name, dto.Name, StringComparison.Ordinal));

        if (existing is null)
        {
            return await CreateAsync(dto, cancellationToken);
        }

        return resolution switch
        {
            ImportConflictResolution.Skip => CampaignImportOutcome.Skipped(dto.Name),
            ImportConflictResolution.Overwrite => await CreateAsync(dto, cancellationToken, overwrite: true),
            ImportConflictResolution.Merge => await MergeAsync(existing.Id, dto, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "Unknown import conflict resolution."),
        };
    }

    private async Task<CampaignImportOutcome> CreateAsync(CampaignExportDto dto, CancellationToken cancellationToken, bool overwrite = false)
    {
        var result = await campaignRepository.ImportCampaignAsync(dto, overwrite, cancellationToken);
        return result.Succeeded
            ? CampaignImportOutcome.Created(result.Campaign!, dto.PlayerCharacters.Count, dto.Npcs.Count)
            : CampaignImportOutcome.Failed(result.Validation);
    }

    private async Task<CampaignImportOutcome> MergeAsync(Guid campaignId, CampaignExportDto dto, CancellationToken cancellationToken)
    {
        var characters = await playerCharacterRepository.ImportCharactersAsync(campaignId, dto.PlayerCharacters, overwrite: true, cancellationToken);
        var npcs = await npcRepository.ImportCharactersAsync(campaignId, dto.Npcs, overwrite: true, cancellationToken);

        // Re-fetch rather than trust the pre-merge `existing` reference (or splice the bulk results
        // into it) -- the campaign passed back to the caller must reflect what's actually in the
        // database now, including any player characters/NPCs the merge left untouched.
        var campaign = await campaignRepository.GetAsync(campaignId, cancellationToken)
            ?? throw new InvalidOperationException($"Campaign {campaignId} disappeared mid-merge.");

        return CampaignImportOutcome.Merged(campaign, characters, npcs);
    }
}