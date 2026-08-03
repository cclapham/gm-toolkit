using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using GmToolkit.Core.Import;
using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;

namespace GmToolkit.UI.Design;

/// <summary>
/// Always-empty, no-op <see cref="IPlayerCharacterRepository"/> used only to construct
/// <see cref="ViewModels.CharactersViewModel"/>/<see cref="ViewModels.CharacterFormViewModel"/> for
/// the XAML previewer's <c>Design.DataContext</c> -- mirrors
/// <see cref="DesignTimeCampaignRepository"/>. Never used at runtime; both real heads resolve
/// <see cref="IPlayerCharacterRepository"/> from the DI container instead.
/// </summary>
internal sealed class DesignTimePlayerCharacterRepository : IPlayerCharacterRepository
{
    public Task<PlayerCharacter?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<PlayerCharacter?>(null);

    public Task<IReadOnlyList<PlayerCharacter>> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PlayerCharacter>>([]);

    public Task AddAsync(PlayerCharacter playerCharacter, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpdateAsync(PlayerCharacter playerCharacter, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<BulkImportResult<PlayerCharacter>> ImportCharactersAsync(
        Guid campaignId, IReadOnlyList<PlayerCharacterExportDto> dtos, bool overwrite, CancellationToken cancellationToken = default) =>
        Task.FromResult(new BulkImportResult<PlayerCharacter>([], []));
}