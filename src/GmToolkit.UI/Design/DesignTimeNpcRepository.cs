using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using GmToolkit.Core.Import;
using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;

namespace GmToolkit.UI.Design;

/// <summary>
/// Always-empty, no-op <see cref="INpcRepository"/> used only to construct
/// <see cref="ViewModels.NpcsViewModel"/> for the XAML previewer's <c>Design.DataContext</c> --
/// mirrors <see cref="DesignTimePlayerCharacterRepository"/>. Never used at runtime; both real
/// heads resolve <see cref="INpcRepository"/> from the DI container instead.
/// </summary>
internal sealed class DesignTimeNpcRepository : INpcRepository
{
    public Task<Npc?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<Npc?>(null);

    public Task<IReadOnlyList<Npc>> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Npc>>([]);

    public Task AddAsync(Npc npc, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpdateAsync(Npc npc, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<BulkImportResult<Npc>> ImportCharactersAsync(
        Guid campaignId, IReadOnlyList<NpcExportDto> dtos, bool overwrite, CancellationToken cancellationToken = default) =>
        Task.FromResult(new BulkImportResult<Npc>([], []));
}