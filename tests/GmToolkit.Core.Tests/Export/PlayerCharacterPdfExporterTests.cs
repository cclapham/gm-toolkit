using System.Text;

using GmToolkit.Core.Export;
using GmToolkit.Core.Models;
using GmToolkit.Core.Systems;

namespace GmToolkit.Core.Tests.Export;

public class PlayerCharacterPdfExporterTests
{
    private static readonly Campaign Campaign = new() { Name = "Wandering Souls" };

    [Fact]
    public void Export_includes_character_name_player_and_class_level()
    {
        var character = new PlayerCharacter
        {
            CampaignId = Campaign.Id,
            CharacterName = "Brannigan",
            PlayerName = "Alice",
            Ancestry = "Human",
            Class = "Fighter",
            Level = 3,
        };

        var bytes = PlayerCharacterPdfExporter.Export(character, Campaign);
        var text = Encoding.Latin1.GetString(bytes);

        Assert.Contains("(Brannigan) Tj", text, StringComparison.Ordinal);
        Assert.Contains("Played by Alice", text, StringComparison.Ordinal);
        Assert.Contains("Ancestry: Human", text, StringComparison.Ordinal);
        Assert.Contains("Class: Fighter", text, StringComparison.Ordinal);
        Assert.Contains("Level: 3", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_includes_stats_without_a_system()
    {
        var character = new PlayerCharacter { CampaignId = Campaign.Id, CharacterName = "Brannigan" };
        character.Stats["STR"] = "16";

        var bytes = PlayerCharacterPdfExporter.Export(character, Campaign);
        var text = Encoding.Latin1.GetString(bytes);

        Assert.Contains("STR: 16", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_labels_stats_using_the_attached_system()
    {
        var system = new CharacterSystem
        {
            FormatVersion = 1,
            Id = "test",
            Name = "Test System",
            PcFields = [new StatFieldDefinition { Key = "str", Label = "Strength", Type = StatFieldTypes.Number }],
        };
        var character = new PlayerCharacter { CampaignId = Campaign.Id, CharacterName = "Brannigan" };
        character.Stats["str"] = "16";

        var bytes = PlayerCharacterPdfExporter.Export(character, Campaign, system);
        var text = Encoding.Latin1.GetString(bytes);

        Assert.Contains("Strength: 16", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_includes_notes_when_present_and_omits_the_section_when_blank()
    {
        var withNotes = new PlayerCharacter { CampaignId = Campaign.Id, CharacterName = "Brannigan", Notes = "Missing an eye" };
        var withoutNotes = new PlayerCharacter { CampaignId = Campaign.Id, CharacterName = "Elowen" };

        var withNotesText = Encoding.Latin1.GetString(PlayerCharacterPdfExporter.Export(withNotes, Campaign));
        var withoutNotesText = Encoding.Latin1.GetString(PlayerCharacterPdfExporter.Export(withoutNotes, Campaign));

        Assert.Contains("Missing an eye", withNotesText, StringComparison.Ordinal);
        Assert.DoesNotContain("(Notes) Tj", withoutNotesText, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_throws_for_null_character_or_campaign()
    {
        var character = new PlayerCharacter { CampaignId = Campaign.Id, CharacterName = "Brannigan" };

        Assert.Throws<ArgumentNullException>(() => PlayerCharacterPdfExporter.Export(null!, Campaign));
        Assert.Throws<ArgumentNullException>(() => PlayerCharacterPdfExporter.Export(character, null!));
    }
}