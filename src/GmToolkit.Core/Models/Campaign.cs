namespace GmToolkit.Core.Models;

public class Campaign
{
    public const int NameMaxLength = 200;

    private string _name = string.Empty;

    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name
    {
        get => _name;
        set => _name = ValidateName(value);
    }

    /// <summary>Free text on purpose — the MVP does not model rules systems.</summary>
    public string GameSystem { get; set; } = string.Empty;

    /// <summary>
    /// The <see cref="Systems.CharacterSystem.Id"/> of the typed stat schema attached to this
    /// campaign, or <c>null</c> for a campaign that keeps today's fully freeform
    /// <see cref="Dictionary{TKey, TValue}"/>-based stats (see <see cref="PlayerCharacter.Stats"/>,
    /// <see cref="Npc.Stats"/>) with no schema attached at all — unchanged behavior for existing
    /// campaigns. Not a database foreign key: which systems exist is a
    /// <see cref="Systems.ICharacterSystemRegistry"/> concern, not this data model's.
    /// </summary>
    public string? CharacterSystemId { get; set; }

    public string Description { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public DateTime LastOpenedUtc { get; set; } = DateTime.UtcNow;

    public List<PlayerCharacter> PlayerCharacters { get; init; } = [];
    public List<Npc> Npcs { get; init; } = [];

    private static string ValidateName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Campaign name is required.", nameof(value));
        }

        if (value.Length > NameMaxLength)
        {
            throw new ArgumentException($"Campaign name cannot exceed {NameMaxLength} characters.", nameof(value));
        }

        return value;
    }
}