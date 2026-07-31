namespace GmToolkit.Core.Models;

public class Npc
{
    public const int NameMaxLength = 200;

    private string _name = string.Empty;

    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CampaignId { get; init; }

    public required string Name
    {
        get => _name;
        set => _name = ValidateName(value);
    }

    public string Role { get; set; } = string.Empty;
    public string Faction { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Appearance { get; set; } = string.Empty;
    public string Mannerism { get; set; } = string.Empty;
    public string Motivation { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool KnownToPlayers { get; set; }
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// True when this NPC came out of the generator. Deliberately not a separate type —
    /// a hand-written NPC and a generated one are indistinguishable afterwards other than
    /// this flag.
    /// </summary>
    public bool WasGenerated { get; set; }

    /// <summary>
    /// System-agnostic key/value bag, matching <see cref="PlayerCharacter.Stats"/>'s pattern —
    /// the GM (or an attached <see cref="Campaign.CharacterSystemId"/> schema) defines the keys.
    /// Empty for NPCs with no stat block at all, which is the common case for minor/incidental
    /// NPCs.
    /// </summary>
    public Dictionary<string, string> Stats { get; init; } = [];

    private static string ValidateName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("NPC name is required.", nameof(value));
        }

        if (value.Length > NameMaxLength)
        {
            throw new ArgumentException($"NPC name cannot exceed {NameMaxLength} characters.", nameof(value));
        }

        return value;
    }
}