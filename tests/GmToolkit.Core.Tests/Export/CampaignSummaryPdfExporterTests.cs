using System.Text;

using GmToolkit.Core.Export;
using GmToolkit.Core.Models;

namespace GmToolkit.Core.Tests.Export;

public class CampaignSummaryPdfExporterTests
{
    [Fact]
    public void Export_includes_campaign_name_system_and_description()
    {
        var campaign = new Campaign
        {
            Name = "Wandering Souls",
            GameSystem = "D&D 5e",
            Description = "A haunted coastal town.",
        };

        var text = Encoding.Latin1.GetString(CampaignSummaryPdfExporter.Export(campaign));

        Assert.Contains("(Wandering Souls) Tj", text, StringComparison.Ordinal);
        Assert.Contains("Game system: D&D 5e", text, StringComparison.Ordinal);
        Assert.Contains("A haunted coastal town.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_lists_every_player_character_and_npc()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        campaign.PlayerCharacters.Add(new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Brannigan", Class = "Fighter", Level = 3 });
        campaign.Npcs.Add(new Npc { CampaignId = campaign.Id, Name = "Old Marta", Role = "Innkeeper", Secret = "Was once a pirate." });

        var text = Encoding.Latin1.GetString(CampaignSummaryPdfExporter.Export(campaign));

        Assert.Contains("Player Characters \\(1\\)", text, StringComparison.Ordinal);
        Assert.Contains("(Brannigan) Tj", text, StringComparison.Ordinal);
        Assert.Contains("Class: Fighter", text, StringComparison.Ordinal);
        Assert.Contains("NPCs \\(1\\)", text, StringComparison.Ordinal);
        Assert.Contains("(Old Marta) Tj", text, StringComparison.Ordinal);
        Assert.Contains("Role: Innkeeper", text, StringComparison.Ordinal);
        Assert.Contains("Secret: Was once a pirate.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_with_no_player_characters_or_npcs_shows_zero_counts()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };

        var text = Encoding.Latin1.GetString(CampaignSummaryPdfExporter.Export(campaign));

        Assert.Contains("Player Characters \\(0\\)", text, StringComparison.Ordinal);
        Assert.Contains("NPCs \\(0\\)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_throws_for_null_campaign()
    {
        Assert.Throws<ArgumentNullException>(() => CampaignSummaryPdfExporter.Export(null!));
    }
}