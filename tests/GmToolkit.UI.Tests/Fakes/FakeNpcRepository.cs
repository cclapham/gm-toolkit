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

    public Task<Npc?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_npcs.FirstOrDefault(n => n.Id == id));

    public Task<IReadOnlyList<Npc>> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        ThrowOnGetByCampaign is not null
            ? Task.FromException<IReadOnlyList<Npc>>(ThrowOnGetByCampaign)
            : Task.FromResult<IReadOnlyList<Npc>>([.. _npcs.Where(n => n.CampaignId == campaignId)]);

    public Task AddAsync(Npc npc, CancellationToken cancellationToken = default)
    {
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