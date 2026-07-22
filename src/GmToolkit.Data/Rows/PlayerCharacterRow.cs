using SQLite;

namespace GmToolkit.Data.Rows;

/// <summary>SQLite row shape for <see cref="Core.Models.PlayerCharacter"/>.</summary>
[Table("PlayerCharacters")]
public class PlayerCharacterRow
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid CampaignId { get; set; }

    [NotNull]
    public string CharacterName { get; set; } = string.Empty;

    public string PlayerName { get; set; } = string.Empty;
    public string Ancestry { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public int Level { get; set; }
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized Dictionary&lt;string, string&gt; — sqlite-net-pcl has no native map
    /// column type. Serialization lives in the repository (#11), not here.
    /// </summary>
    public string StatsJson { get; set; } = "{}";
}