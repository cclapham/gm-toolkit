using GmToolkit.Core.Import;
using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;

namespace GmToolkit.UI.Tests.Fakes;

/// <summary>In-memory <see cref="ICampaignRepository"/> for constructing a real
/// <see cref="GmToolkit.Core.Services.ActiveCampaignContext"/> in tests without SQLite.</summary>
internal sealed class FakeCampaignRepository(params Campaign[] campaigns) : ICampaignRepository
{
    private readonly List<Campaign> _campaigns = [.. campaigns];

    public List<Campaign> UpdatedCampaigns { get; } = [];

    /// <summary>When set, <see cref="GetAllAsync"/> throws this instead of returning -- for
    /// exercising a load failure (e.g. <c>CampaignsViewModel</c>'s error state) without needing a
    /// real broken database.</summary>
    public Exception? ThrowOnGetAll { get; set; }

    /// <summary>When set, <see cref="AddAsync"/> throws this instead of adding -- for exercising a
    /// save failure (issue #32, e.g. <c>CampaignFormViewModel.SaveAsync</c>'s
    /// <c>catch (Exception ex)</c> path) without needing a real broken database. Mirrors
    /// <see cref="ThrowOnGetAll"/>.</summary>
    public Exception? ThrowOnAdd { get; set; }

    /// <summary>When set, <see cref="GetAllAsync"/> waits for this to complete before returning --
    /// mirrors <see cref="FakeNpcRepository.GetByCampaignGate"/>'s identical purpose.</summary>
    public TaskCompletionSource? GetAllGate { get; set; }

    public Task<Campaign?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_campaigns.FirstOrDefault(c => c.Id == id));

    public async Task<IReadOnlyList<Campaign>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (GetAllGate is not null)
        {
            await GetAllGate.Task;
        }

        if (ThrowOnGetAll is not null)
        {
            throw ThrowOnGetAll;
        }

        return [.. _campaigns];
    }

    public Task AddAsync(Campaign campaign, CancellationToken cancellationToken = default)
    {
        if (ThrowOnAdd is not null)
        {
            return Task.FromException(ThrowOnAdd);
        }

        _campaigns.Add(campaign);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken = default)
    {
        UpdatedCampaigns.Add(campaign);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _campaigns.RemoveAll(c => c.Id == id);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Real (not <see cref="NotSupportedException"/>-throwing) in-memory implementation, so
    /// <see cref="GmToolkit.UI.ViewModels.CampaignImportViewModel"/>/<see cref="GmToolkit.UI.ViewModels.CampaignExportViewModel"/>
    /// (issues #130/#131) can be tested against this fake the same way every other view model in
    /// this project is -- mirrors <c>GmToolkit.Data.Repositories.CampaignRepository</c>'s own
    /// name-conflict/overwrite semantics closely enough for those view models' own branching logic,
    /// without needing a real SQLite file (that coverage already exists in
    /// <c>GmToolkit.Data.Tests.CampaignExportImportRoundTripTests</c>/<c>CampaignImportOrchestratorTests</c>).
    /// </summary>
    public Task<CampaignExportDto?> ExportCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var campaign = _campaigns.FirstOrDefault(c => c.Id == campaignId);
        return Task.FromResult(campaign is null ? null : CampaignExportMapper.ToDto(campaign));
    }

    /// <inheritdoc cref="ExportCampaignAsync"/>
    public Task<CampaignImportResult> ImportCampaignAsync(CampaignExportDto dto, bool overwrite, CancellationToken cancellationToken = default)
    {
        var validation = ImportValidator.ValidateCampaign(dto);
        if (!validation.IsValid)
        {
            return Task.FromResult(CampaignImportResult.Failure(validation));
        }

        var existing = _campaigns.FirstOrDefault(c => c.Name == dto.Name);
        if (existing is not null && !overwrite)
        {
            return Task.FromResult(CampaignImportResult.Failure($"A campaign named '{dto.Name}' already exists."));
        }

        if (existing is not null)
        {
            _campaigns.Remove(existing);
        }

        var campaign = CampaignExportMapper.ToModel(dto);
        campaign.PlayerCharacters.AddRange(dto.PlayerCharacters.Select(pc => PlayerCharacterExportMapper.ToModel(pc, campaign.Id)));
        campaign.Npcs.AddRange(dto.Npcs.Select(npc => NpcExportMapper.ToModel(npc, campaign.Id)));
        _campaigns.Add(campaign);

        return Task.FromResult(CampaignImportResult.Success(campaign));
    }
}