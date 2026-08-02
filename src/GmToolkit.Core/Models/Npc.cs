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
    /// NPCs. When <see cref="HasMalformedStats"/> is <c>true</c>, this is empty (nothing could be
    /// parsed from the persisted row), not the NPC's real stats — see that property's remarks.
    /// </summary>
    public Dictionary<string, string> Stats { get; init; } = [];

    /// <summary>
    /// <c>true</c> when this NPC's persisted stats couldn't be parsed as JSON (hand-edited
    /// directly in the database file, truncated by some other failure, etc.) — <see cref="Stats"/>
    /// is left empty in that case rather than propagating the parse failure and making the whole
    /// NPC inaccessible (see <c>GmToolkit.Data.Mapping.NpcMapper</c>'s remarks), but saving this
    /// NPC back must not silently discard whatever the original, unparseable bytes actually were —
    /// see <see cref="MalformedStatsJson"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately derived rather than a flag set once at load and never cleared: as soon as the
    /// GM edits <see cref="Stats"/> (even to an empty dictionary on purpose), this must flip back to
    /// <c>false</c> so <c>ToRow</c> serializes the edited <see cref="Stats"/> instead of silently
    /// overwriting them with the stale <see cref="MalformedStatsJson"/> bytes on save — a plain
    /// settable flag that stayed <c>true</c> forever would otherwise discard every subsequent edit
    /// to a row that was ever malformed.
    /// </remarks>
    public bool HasMalformedStats => MalformedStatsJson is not null && Stats.Count == 0;

    /// <summary>
    /// The original, unparseable <c>StatsJson</c> bytes this NPC was loaded with, when
    /// <see cref="HasMalformedStats"/> is <c>true</c>; <c>null</c> otherwise. The mapper's write
    /// path writes this back verbatim instead of re-serializing <see cref="Stats"/> (which is just
    /// empty, not the NPC's real data) — round-tripping through an empty <see cref="Stats"/> would
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
            throw new ArgumentException("NPC name is required.", nameof(value));
        }

        if (value.Length > NameMaxLength)
        {
            throw new ArgumentException($"NPC name cannot exceed {NameMaxLength} characters.", nameof(value));
        }

        return value;
    }
}