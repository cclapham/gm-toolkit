using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;

namespace GmToolkit.UI.Tests.Fakes;

/// <summary>In-memory <see cref="IPlayerCharacterRepository"/> for testing
/// <see cref="GmToolkit.UI.ViewModels.CharactersViewModel"/>/<see cref="GmToolkit.UI.ViewModels.CharacterFormViewModel"/>
/// without SQLite -- mirrors <see cref="FakeCampaignRepository"/>.</summary>
internal sealed class FakePlayerCharacterRepository(params PlayerCharacter[] playerCharacters) : IPlayerCharacterRepository
{
    private readonly List<PlayerCharacter> _playerCharacters = [.. playerCharacters];

    public List<PlayerCharacter> UpdatedPlayerCharacters { get; } = [];

    /// <summary>When set, <see cref="GetByCampaignAsync"/> throws this instead of returning -- for
    /// exercising a load failure without needing a real broken database.</summary>
    public Exception? ThrowOnGetByCampaign { get; set; }

    public Task<PlayerCharacter?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_playerCharacters.FirstOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<PlayerCharacter>> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        ThrowOnGetByCampaign is not null
            ? Task.FromException<IReadOnlyList<PlayerCharacter>>(ThrowOnGetByCampaign)
            : Task.FromResult<IReadOnlyList<PlayerCharacter>>([.. _playerCharacters.Where(p => p.CampaignId == campaignId)]);

    public Task AddAsync(PlayerCharacter playerCharacter, CancellationToken cancellationToken = default)
    {
        _playerCharacters.Add(playerCharacter);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(PlayerCharacter playerCharacter, CancellationToken cancellationToken = default)
    {
        UpdatedPlayerCharacters.Add(playerCharacter);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _playerCharacters.RemoveAll(p => p.Id == id);
        return Task.CompletedTask;
    }
}