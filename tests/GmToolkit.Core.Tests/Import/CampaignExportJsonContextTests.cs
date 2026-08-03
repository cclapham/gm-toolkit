using System.Text.Json;

using GmToolkit.Core.Import;

namespace GmToolkit.Core.Tests.Import;

public class CampaignExportJsonContextTests
{
    [Fact]
    public void Serialize_then_deserialize_round_trips_a_full_campaign_export()
    {
        var dto = new CampaignExportDto
        {
            Name = "Wandering Souls",
            GameSystem = "D&D 5e",
            CharacterSystemId = "dnd5e-2024",
            Description = "A haunted coastal town.",
            PlayerCharacters =
            [
                new PlayerCharacterExportDto
                {
                    CharacterName = "Brannigan Thistlewood",
                    PlayerName = "Alex",
                    Ancestry = "Half-Elf",
                    Class = "Ranger",
                    Level = 4,
                    Notes = "Afraid of boats.",
                    Stats = new Dictionary<string, string> { ["STR"] = "14", ["Passive Perception"] = "16" },
                },
            ],
            Npcs =
            [
                new NpcExportDto
                {
                    Name = "Old Marta",
                    Role = "Innkeeper",
                    KnownToPlayers = true,
                    WasGenerated = false,
                    Stats = new Dictionary<string, string> { ["HP"] = "12" },
                },
            ],
        };

        var json = JsonSerializer.Serialize(dto, CampaignExportJsonContext.Default.CampaignExportDto);
        var roundTripped = JsonSerializer.Deserialize(json, CampaignExportJsonContext.Default.CampaignExportDto);

        Assert.NotNull(roundTripped);
        Assert.Equal(dto.Name, roundTripped.Name);
        Assert.Equal(dto.GameSystem, roundTripped.GameSystem);
        Assert.Equal(dto.CharacterSystemId, roundTripped.CharacterSystemId);
        Assert.Equal(dto.Description, roundTripped.Description);
        Assert.Equal(dto.FormatVersion, roundTripped.FormatVersion);

        var pc = Assert.Single(roundTripped.PlayerCharacters);
        Assert.Equal("Brannigan Thistlewood", pc.CharacterName);
        Assert.Equal("14", pc.Stats["STR"]);

        var npc = Assert.Single(roundTripped.Npcs);
        Assert.Equal("Old Marta", npc.Name);
        Assert.True(npc.KnownToPlayers);
    }

    [Fact]
    public void Serialize_uses_camelCase_property_names()
    {
        var dto = new CampaignExportDto { Name = "Wandering Souls" };

        var json = JsonSerializer.Serialize(dto, CampaignExportJsonContext.Default.CampaignExportDto);

        Assert.Contains("\"name\"", json, StringComparison.Ordinal);
        Assert.Contains("\"formatVersion\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Name\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_of_a_campaign_with_no_characters_or_npcs_produces_empty_lists_not_null()
    {
        const string json = """{"formatVersion":1,"name":"Wandering Souls","gameSystem":"","description":"","exportedUtc":"2026-01-01T00:00:00Z"}""";

        var dto = JsonSerializer.Deserialize(json, CampaignExportJsonContext.Default.CampaignExportDto);

        Assert.NotNull(dto);
        Assert.Empty(dto.PlayerCharacters);
        Assert.Empty(dto.Npcs);
    }
}