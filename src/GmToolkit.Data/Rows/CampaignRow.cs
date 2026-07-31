using SQLite;

namespace GmToolkit.Data.Rows;

/// <summary>
/// SQLite row shape for <see cref="Core.Models.Campaign"/>. A separate type rather than
/// attributes on the Core model — Core stays free of SQLite references (see
/// CONTRIBUTING.md); mapping between the two lives in the repository (#11).
/// </summary>
[Table("Campaigns")]
public class CampaignRow
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;

    public string GameSystem { get; set; } = string.Empty;

    /// <summary>
    /// Nullable — a campaign with no <see cref="Core.Systems.CharacterSystem"/> attached keeps
    /// today's fully freeform behavior. Added in schema v2 (#88).
    /// </summary>
    public string? CharacterSystemId { get; set; }

    public string Description { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime LastOpenedUtc { get; set; }
}