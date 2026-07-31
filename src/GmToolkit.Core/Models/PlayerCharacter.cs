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
    /// roster view is a UI concern (issue #20), not a data-model one. When
    /// <see cref="HasMalformedStats"/> is <c>true</c>, this is empty (nothing could be parsed from
    /// the persisted row), not this PC's real stats — see that property's remarks.
    /// </summary>
    public Dictionary<string, string> Stats { get; init; } = [];

    /// <summary>
    /// <c>true</c> when this PC's persisted stats couldn't be parsed as JSON (hand-edited directly
    /// in the database file, truncated by some other failure, etc.) — set by
    /// <c>GmToolkit.Data.Mapping.PlayerCharacterMapper</c> when loading a row whose
    /// <c>StatsJson</c> fails to deserialize. <see cref="Stats"/> is left empty in that case rather
    /// than propagating the parse failure and making the whole PC inaccessible (see that mapper's
    /// remarks, mirroring <see cref="Npc.HasMalformedStats"/>), but saving this PC back must not
    /// silently discard whatever the original, unparseable bytes actually were — see
    /// <see cref="MalformedStatsJson"/>.
    /// </summary>
    public bool HasMalformedStats { get; internal set; }

    /// <summary>
    /// The original, unparseable <c>StatsJson</c> bytes this PC was loaded with, when
    /// <see cref="HasMalformedStats"/> is <c>true</c>; <c>null</c> otherwise. The mapper's write
    /// path writes this back verbatim instead of re-serializing <see cref="Stats"/> (which is just
    /// empty, not this PC's real data) — round-tripping through an empty <see cref="Stats"/> would
    /// permanently destroy whatever was there, turning a recoverable "this one row needs manual
    /// attention" problem into unrecoverable data loss the moment the app so much as loads and
    /// saves the campaign. Not a public setter: only the mapper that read the original bytes should
    /// ever set this.
    /// </summary>
    public string? MalformedStatsJson { get; internal set; }

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