using GmToolkit.Core.Export;
using GmToolkit.Core.Import;

namespace GmToolkit.Core.Tests.Export;

public class CampaignCsvExporterTests
{
    [Fact]
    public void Export_writes_header_row_with_fixed_columns_plus_union_of_stat_keys()
    {
        var dto = new CampaignExportDto
        {
            Name = "Wandering Souls",
            PlayerCharacters =
            [
                new PlayerCharacterExportDto { CharacterName = "Brannigan", Stats = new() { ["STR"] = "16" } },
                new PlayerCharacterExportDto { CharacterName = "Elowen", Stats = new() { ["DEX"] = "14" } },
            ],
        };

        var csv = CampaignCsvExporter.Export(dto);
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Name,Player,Ancestry,Class,Level,Notes,DEX,STR", lines[0]);
    }

    [Fact]
    public void Export_writes_one_row_per_player_character_with_blank_for_missing_stat_keys()
    {
        var dto = new CampaignExportDto
        {
            Name = "Wandering Souls",
            PlayerCharacters =
            [
                new PlayerCharacterExportDto
                {
                    CharacterName = "Brannigan",
                    PlayerName = "Alice",
                    Ancestry = "Human",
                    Class = "Fighter",
                    Level = 3,
                    Notes = "Missing an eye",
                    Stats = new() { ["STR"] = "16" },
                },
                new PlayerCharacterExportDto { CharacterName = "Elowen", Stats = new() { ["DEX"] = "14" } },
            ],
        };

        var csv = CampaignCsvExporter.Export(dto);
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.Equal("Brannigan,Alice,Human,Fighter,3,Missing an eye,,16", lines[1]);
        Assert.Equal("Elowen,,,,1,,14,", lines[2]);
    }

    [Fact]
    public void Export_does_not_include_npcs()
    {
        var dto = new CampaignExportDto
        {
            Name = "Wandering Souls",
            Npcs = [new NpcExportDto { Name = "Old Marta" }],
        };

        var csv = CampaignCsvExporter.Export(dto);

        Assert.DoesNotContain("Old Marta", csv);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("has,comma", "\"has,comma\"")]
    [InlineData("has\"quote", "\"has\"\"quote\"")]
    [InlineData("multi\nline", "\"multi\nline\"")]
    public void Export_quotes_fields_containing_commas_quotes_or_newlines(string notes, string expectedField)
    {
        var dto = new CampaignExportDto
        {
            Name = "Wandering Souls",
            PlayerCharacters = [new PlayerCharacterExportDto { CharacterName = "Brannigan", Notes = notes }],
        };

        var csv = CampaignCsvExporter.Export(dto);

        Assert.Contains($"Brannigan,,,,1,{expectedField}", csv);
    }

    [Fact]
    public void Export_with_no_player_characters_still_writes_the_header_row()
    {
        var dto = new CampaignExportDto { Name = "Wandering Souls" };

        var csv = CampaignCsvExporter.Export(dto);
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(lines);
        Assert.Equal("Name,Player,Ancestry,Class,Level,Notes", lines[0]);
    }

    [Fact]
    public void Export_throws_for_null_dto()
    {
        Assert.Throws<ArgumentNullException>(() => CampaignCsvExporter.Export(null!));
    }
}