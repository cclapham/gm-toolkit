using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;

namespace GmToolkit.UI.Tests.Fakes;

/// <summary>In-memory <see cref="INpcRepository"/> for testing
/// <see cref="GmToolkit.UI.ViewModels.NpcsViewModel"/> without SQLite -- mirrors
/// <see cref="FakePlayerCharacterRepository"/>/<see cref="FakeCampaignRepository"/>.</summary>
internal sealed class FakeNpcRepository(params Npc[] npcs) : INpcRepository
{
    private readonly List<Npc> _npcs = [.. npcs];

    /// <summary>When set, <see cref="GetByCampaignAsync"/> throws this instead of returning -- for
    /// exercising a load failure without needing a real broken database.</summary>
    public Exception? ThrowOnGetByCampaign { get; set; }

    /// <summary>When set, <see cref="AddAsync"/> throws this instead of adding -- mirrors
    /// <see cref="FakeCampaignRepository.ThrowOnAdd"/> (issue #32).</summary>
    public Exception? ThrowOnAdd { get; set; }

    /// <summary>When set, <see cref="GetByCampaignAsync"/> waits for this to complete before
    /// returning -- every other member of this class (and every other fake in this project) completes
    /// synchronously, which is fine for asserting a load's final state but can't show a caller
    /// anything about the view model's state while that load is genuinely still in flight. Setting
    /// this lets a test hold a call open, assert on the view model mid-load, then release it -- mirrors
    /// <see cref="FakeCampaignRepository.GetAllGate"/>/<see cref="FakePlayerCharacterRepository.GetByCampaignGate"/>.</summary>
    public TaskCompletionSource? GetByCampaignGate { get; set; }

    public Task<Npc?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_npcs.FirstOrDefault(n => n.Id == id));

    public async Task<IReadOnlyList<Npc>> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        if (GetByCampaignGate is not null)
        {
            await GetByCampaignGate.Task;
        }

        if (ThrowOnGetByCampaign is not null)
        {
            throw ThrowOnGetByCampaign;
        }

        return [.. _npcs.Where(n => n.CampaignId == campaignId)];
    }

    public Task AddAsync(Npc npc, CancellationToken cancellationToken = default)
    {
        if (ThrowOnAdd is not null)
        {
            return Task.FromException(ThrowOnAdd);
        }

        _npcs.Add(npc);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Npc npc, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _npcs.RemoveAll(n => n.Id == id);
        return Task.CompletedTask;
    }
}