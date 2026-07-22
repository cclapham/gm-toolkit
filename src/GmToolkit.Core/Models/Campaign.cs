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