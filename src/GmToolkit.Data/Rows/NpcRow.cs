using SQLite;

namespace GmToolkit.Data.Rows;

/// <summary>SQLite row shape for <see cref="Core.Models.Npc"/>.</summary>
[Table("Npcs")]
public class NpcRow
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid CampaignId { get; set; }

    [Indexed]
    [NotNull]
    public string Name { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
    public string Faction { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Appearance { get; set; } = string.Empty;
    public string Mannerism { get; set; } = string.Empty;
    public string Motivation { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool KnownToPlayers { get; set; }
    public DateTime CreatedUtc { get; set; }
    public bool WasGenerated { get; set; }

    /// <summary>
    /// JSON-serialized Dictionary&lt;string, string&gt; — matches
    /// <see cref="PlayerCharacterRow.StatsJson"/>'s pattern. Serialization lives in the repository
    /// (#11). Added in schema v2 (#88).
    /// </summary>
    public string StatsJson { get; set; } = "{}";
}