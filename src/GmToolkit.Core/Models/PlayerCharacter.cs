namespace GmToolkit.Core.Models;

public class PlayerCharacter
{
    public const int NameMaxLength = 200;

    private string _characterName = string.Empty;

    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CampaignId { get; init; }

    public required string CharacterName
    {
        get => _characterName;
        set => _characterName = ValidateName(value);
    }

    public string PlayerName { get; set; } = string.Empty;
    public string Ancestry { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// System-agnostic key/value bag — the GM defines the keys (STR/DEX for D&D, SAN/skills
    /// for Call of Cthulhu, whatever a homebrew system needs). Passive values a GM looks up
    /// mid-session (perception, AC, languages) are just well-known keys in this same bag, not
    /// their own fields — giving them dedicated fields would bake in D&D-specific concepts that
    /// don't exist in other systems. Which keys get pinned for at-a-glance display on the
    /// roster view is a UI concern (issue #20), not a data-model one.
    /// </summary>
    public Dictionary<string, string> Stats { get; init; } = [];

    private static string ValidateName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Character name is required.", nameof(value));
        }

        if (value.Length > NameMaxLength)
        {
            throw new ArgumentException($"Character name cannot exceed {NameMaxLength} characters.", nameof(value));
        }

        return value;
    }
}