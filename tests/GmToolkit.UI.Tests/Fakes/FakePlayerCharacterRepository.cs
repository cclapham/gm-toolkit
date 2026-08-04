using GmToolkit.Core.Import;
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

    /// <summary>When set, <see cref="AddAsync"/> throws this instead of adding -- mirrors
    /// <see cref="FakeCampaignRepository.ThrowOnAdd"/> (issue #32).</summary>
    public Exception? ThrowOnAdd { get; set; }

    /// <summary>When set, <see cref="GetByCampaignAsync"/> waits for this to complete before
    /// returning -- mirrors <see cref="FakeNpcRepository.GetByCampaignGate"/>'s identical purpose.</summary>
    public TaskCompletionSource? GetByCampaignGate { get; set; }

    public Task<PlayerCharacter?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_playerCharacters.FirstOrDefault(p => p.Id == id));

    public async Task<IReadOnlyList<PlayerCharacter>> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        if (GetByCampaignGate is not null)
        {
            await GetByCampaignGate.Task;
        }

        if (ThrowOnGetByCampaign is not null)
        {
            throw ThrowOnGetByCampaign;
        }

        return [.. _playerCharacters.Where(p => p.CampaignId == campaignId)];
    }

    public Task AddAsync(PlayerCharacter playerCharacter, CancellationToken cancellationToken = default)
    {
        if (ThrowOnAdd is not null)
        {
            return Task.FromException(ThrowOnAdd);
        }

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

    /// <summary>
    /// Real (not <see cref="NotSupportedException"/>-throwing) in-memory implementation, matching
    /// <c>GmToolkit.Data.Repositories.PlayerCharacterRepository.ImportCharactersAsync</c>'s own
    /// per-entry overwrite-by-name semantics -- see <see cref="FakeCampaignRepository.ImportCampaignAsync"/>'s
    /// identical remark on why this fake supports it directly rather than throwing.
    /// </summary>
    public Task<BulkImportResult<PlayerCharacter>> ImportCharactersAsync(
        Guid campaignId, IReadOnlyList<PlayerCharacterExportDto> dtos, bool overwrite, CancellationToken cancellationToken = default)
    {
        var existingByName = _playerCharacters
            .Where(pc => pc.CampaignId == campaignId)
            .ToDictionary(pc => pc.CharacterName, StringComparer.Ordinal);

        var imported = new List<PlayerCharacter>();
        var errors = new List<ImportItemError>();

        for (var index = 0; index < dtos.Count; index++)
        {
            var dto = dtos[index];
            var validation = ImportValidator.ValidatePlayerCharacter(dto);
            if (!validation.IsValid)
            {
                errors.Add(new ImportItemError(index, dto.CharacterName, validation.Errors));
                continue;
            }

            if (existingByName.TryGetValue(dto.CharacterName, out var existing))
            {
                if (!overwrite)
                {
                    errors.Add(new ImportItemError(
                        index, dto.CharacterName, [$"A player character named '{dto.CharacterName}' already exists in this campaign."]));
                    continue;
                }

                _playerCharacters.Remove(existing);
                var updated = PlayerCharacterExportMapper.ToModel(dto, campaignId, existing.Id);
                _playerCharacters.Add(updated);
                imported.Add(updated);
            }
            else
            {
                var created = PlayerCharacterExportMapper.ToModel(dto, campaignId);
                _playerCharacters.Add(created);
                imported.Add(created);
            }
        }

        return Task.FromResult(new BulkImportResult<PlayerCharacter>(imported, errors));
    }
}