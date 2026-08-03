using GmToolkit.Core.Import;
using GmToolkit.Core.Models;

namespace GmToolkit.Core.Tests.Import;

public class NpcExportMapperTests
{
    [Fact]
    public void ToDto_then_ToModel_round_trips_every_field_including_stats()
    {
        var campaignId = Guid.NewGuid();
        var original = new Npc
        {
            CampaignId = campaignId,
            Name = "Old Marta",
            Role = "Innkeeper",
            Faction = "The Dockside Guild",
            Location = "The Salty Gull",
            Appearance = "Weathered, one gold tooth.",
            Mannerism = "Taps the bar twice before speaking.",
            Motivation = "Protect her regulars.",
            Secret = "Used to run with smugglers.",
            Notes = "Knows everyone's business.",
            KnownToPlayers = true,
            WasGenerated = true,
            Stats = new Dictionary<string, string> { ["HP"] = "12" },
        };

        var dto = NpcExportMapper.ToDto(original);
        var reimported = NpcExportMapper.ToModel(dto, campaignId);

        Assert.Equal(original.Name, reimported.Name);
        Assert.Equal(original.Role, reimported.Role);
        Assert.Equal(original.Faction, reimported.Faction);
        Assert.Equal(original.Location, reimported.Location);
        Assert.Equal(original.Appearance, reimported.Appearance);
        Assert.Equal(original.Mannerism, reimported.Mannerism);
        Assert.Equal(original.Motivation, reimported.Motivation);
        Assert.Equal(original.Secret, reimported.Secret);
        Assert.Equal(original.Notes, reimported.Notes);
        Assert.Equal(original.KnownToPlayers, reimported.KnownToPlayers);
        Assert.Equal(original.WasGenerated, reimported.WasGenerated);
        Assert.Equal(original.Stats, reimported.Stats);
        Assert.Equal(campaignId, reimported.CampaignId);
    }

    [Fact]
    public void ToModel_mints_a_fresh_id_by_default()
    {
        var dto = new NpcExportDto { Name = "Old Marta" };

        var a = NpcExportMapper.ToModel(dto, Guid.NewGuid());
        var b = NpcExportMapper.ToModel(dto, Guid.NewGuid());

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void ToModel_reuses_the_supplied_id_when_given_one()
    {
        var dto = new NpcExportDto { Name = "Old Marta" };
        var existingId = Guid.NewGuid();

        var model = NpcExportMapper.ToModel(dto, Guid.NewGuid(), existingId);

        Assert.Equal(existingId, model.Id);
    }

    [Fact]
    public void ToModel_with_invalid_name_throws_the_same_way_direct_construction_would()
    {
        var dto = new NpcExportDto { Name = string.Empty };

        Assert.Throws<ArgumentException>(() => NpcExportMapper.ToModel(dto, Guid.NewGuid()));
    }
}