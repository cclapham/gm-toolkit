using GmToolkit.Core.Import;
using GmToolkit.Core.Models;

namespace GmToolkit.Core.Tests.Import;

public class CampaignExportMapperTests
{
    [Fact]
    public void ToDto_maps_every_field_including_nested_characters_and_npcs()
    {
        var campaign = new Campaign
        {
            Name = "Wandering Souls",
            GameSystem = "D&D 5e",
            CharacterSystemId = "dnd5e-2024",
            Description = "A haunted coastal town.",
            PlayerCharacters =
            [
                new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Brannigan" },
            ],
            Npcs =
            [
                new Npc { CampaignId = Guid.NewGuid(), Name = "Old Marta" },
            ],
        };

        var dto = CampaignExportMapper.ToDto(campaign);

        Assert.Equal("Wandering Souls", dto.Name);
        Assert.Equal("D&D 5e", dto.GameSystem);
        Assert.Equal("dnd5e-2024", dto.CharacterSystemId);
        Assert.Equal("A haunted coastal town.", dto.Description);
        Assert.Equal(CampaignExportDto.CurrentFormatVersion, dto.FormatVersion);
        Assert.Single(dto.PlayerCharacters);
        Assert.Equal("Brannigan", dto.PlayerCharacters[0].CharacterName);
        Assert.Single(dto.Npcs);
        Assert.Equal("Old Marta", dto.Npcs[0].Name);
    }

    [Fact]
    public void ToDto_never_carries_a_database_id_or_timestamp()
    {
        // The DTO type itself has no Id/CreatedUtc/LastOpenedUtc properties to leak into -- this
        // test documents that as an explicit, checked assertion via reflection rather than relying
        // on "it compiles" to notice a regression if one were ever added back.
        var properties = typeof(CampaignExportDto).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("Id", properties);
        Assert.DoesNotContain("CreatedUtc", properties);
        Assert.DoesNotContain("LastOpenedUtc", properties);
    }

    [Fact]
    public void ToModel_mints_a_fresh_id_and_never_reuses_one_from_the_source_campaign()
    {
        var original = new Campaign { Name = "Wandering Souls" };
        var dto = CampaignExportMapper.ToDto(original);

        var reimported = CampaignExportMapper.ToModel(dto);

        Assert.NotEqual(original.Id, reimported.Id);
        Assert.Equal(original.Name, reimported.Name);
    }

    [Fact]
    public void ToModel_does_not_populate_player_characters_or_npcs()
    {
        var dto = new CampaignExportDto
        {
            Name = "Wandering Souls",
            PlayerCharacters = [new PlayerCharacterExportDto { CharacterName = "Brannigan" }],
            Npcs = [new NpcExportDto { Name = "Old Marta" }],
        };

        var campaign = CampaignExportMapper.ToModel(dto);

        Assert.Empty(campaign.PlayerCharacters);
        Assert.Empty(campaign.Npcs);
    }

    [Fact]
    public void ToModel_with_invalid_name_throws_the_same_way_direct_construction_would()
    {
        var dto = new CampaignExportDto { Name = string.Empty };

        Assert.Throws<ArgumentException>(() => CampaignExportMapper.ToModel(dto));
    }
}