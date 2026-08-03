using GmToolkit.Core.Import;
using GmToolkit.Core.Models;

namespace GmToolkit.Core.Tests.Import;

public class PlayerCharacterExportMapperTests
{
    [Fact]
    public void ToDto_then_ToModel_round_trips_every_field_including_stats()
    {
        var campaignId = Guid.NewGuid();
        var original = new PlayerCharacter
        {
            CampaignId = campaignId,
            CharacterName = "Brannigan Thistlewood",
            PlayerName = "Alex",
            Ancestry = "Half-Elf",
            Class = "Ranger",
            Level = 4,
            Notes = "Afraid of boats.",
            Stats = new Dictionary<string, string> { ["STR"] = "14", ["Passive Perception"] = "16" },
        };

        var dto = PlayerCharacterExportMapper.ToDto(original);
        var reimported = PlayerCharacterExportMapper.ToModel(dto, campaignId);

        Assert.Equal(original.CharacterName, reimported.CharacterName);
        Assert.Equal(original.PlayerName, reimported.PlayerName);
        Assert.Equal(original.Ancestry, reimported.Ancestry);
        Assert.Equal(original.Class, reimported.Class);
        Assert.Equal(original.Level, reimported.Level);
        Assert.Equal(original.Notes, reimported.Notes);
        Assert.Equal(original.Stats, reimported.Stats);
        Assert.Equal(campaignId, reimported.CampaignId);
    }

    [Fact]
    public void ToModel_mints_a_fresh_id_by_default()
    {
        var dto = new PlayerCharacterExportDto { CharacterName = "Brannigan" };

        var a = PlayerCharacterExportMapper.ToModel(dto, Guid.NewGuid());
        var b = PlayerCharacterExportMapper.ToModel(dto, Guid.NewGuid());

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void ToModel_reuses_the_supplied_id_when_given_one()
    {
        var dto = new PlayerCharacterExportDto { CharacterName = "Brannigan" };
        var existingId = Guid.NewGuid();

        var model = PlayerCharacterExportMapper.ToModel(dto, Guid.NewGuid(), existingId);

        Assert.Equal(existingId, model.Id);
    }

    [Fact]
    public void ToModel_with_invalid_name_throws_the_same_way_direct_construction_would()
    {
        var dto = new PlayerCharacterExportDto { CharacterName = string.Empty };

        Assert.Throws<ArgumentException>(() => PlayerCharacterExportMapper.ToModel(dto, Guid.NewGuid()));
    }

    [Fact]
    public void ToDto_copies_the_stats_dictionary_rather_than_sharing_the_instance()
    {
        var original = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Brannigan" };
        original.Stats["STR"] = "14";

        var dto = PlayerCharacterExportMapper.ToDto(original);
        dto.Stats["STR"] = "mutated";

        Assert.Equal("14", original.Stats["STR"]);
    }
}