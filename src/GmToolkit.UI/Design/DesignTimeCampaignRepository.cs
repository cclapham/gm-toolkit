using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;

namespace GmToolkit.UI.Design;

/// <summary>
/// Always-empty, no-op <see cref="ICampaignRepository"/> used only to construct a real
/// <see cref="GmToolkit.Core.Services.ActiveCampaignContext"/> for the XAML previewer's
/// <c>Design.DataContext</c> (see <see cref="ViewModels.ShellViewModel"/>'s parameterless
/// constructor) — previewers have no running DI container or SQLite file to read from. Never
/// used at runtime; both real heads resolve <see cref="GmToolkit.Core.Services.ActiveCampaignContext"/>
/// from the DI container instead.
/// </summary>
internal sealed class DesignTimeCampaignRepository : ICampaignRepository
{
    public Task<Campaign?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<Campaign?>(null);

    public Task<IReadOnlyList<Campaign>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Campaign>>([]);

    public Task AddAsync(Campaign campaign, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
}