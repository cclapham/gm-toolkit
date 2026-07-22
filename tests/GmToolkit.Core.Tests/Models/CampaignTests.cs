using GmToolkit.Core.Models;

namespace GmToolkit.Core.Tests.Models;

public class CampaignTests
{
    [Fact]
    public void Construction_with_valid_name_succeeds()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };

        Assert.Equal("Wandering Souls", campaign.Name);
        Assert.NotEqual(Guid.Empty, campaign.Id);
        Assert.Empty(campaign.GameSystem);
        Assert.Empty(campaign.Description);
        Assert.Empty(campaign.PlayerCharacters);
        Assert.Empty(campaign.Npcs);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_or_whitespace_name_throws(string name)
    {
        Assert.Throws<ArgumentException>(() => new Campaign { Name = name });
    }

    [Fact]
    public void Name_over_max_length_throws()
    {
        var tooLong = new string('a', Campaign.NameMaxLength + 1);

        Assert.Throws<ArgumentException>(() => new Campaign { Name = tooLong });
    }

    [Fact]
    public void Name_at_max_length_is_allowed()
    {
        var atLimit = new string('a', Campaign.NameMaxLength);

        var campaign = new Campaign { Name = atLimit };

        Assert.Equal(atLimit, campaign.Name);
    }
}