using GmToolkit.Core.Models;

namespace GmToolkit.Core.Tests.Models;

public class NpcTests
{
    [Fact]
    public void Construction_with_valid_fields_succeeds()
    {
        var npc = new Npc
        {
            Name = "Old Marta",
            Role = "Innkeeper",
            Faction = "Neutral",
            Location = "The Wandering Boar",
            Appearance = "Stooped, flour-dusted apron",
            Mannerism = "Hums old sea shanties",
            Motivation = "Keep her regulars fed and out of trouble",
            Secret = "Used to run with pirates",
            KnownToPlayers = true,
        };

        Assert.Equal("Old Marta", npc.Name);
        Assert.True(npc.KnownToPlayers);
    }

    [Fact]
    public void Defaults_are_sensible()
    {
        var npc = new Npc { Name = "Old Marta" };

        Assert.NotEqual(Guid.Empty, npc.Id);
        Assert.False(npc.KnownToPlayers);
        Assert.False(npc.WasGenerated);
    }

    [Fact]
    public void Hand_written_and_generated_npcs_are_the_same_type()
    {
        var handWritten = new Npc { Name = "Old Marta", WasGenerated = false };
        var generated = new Npc { Name = "Brannigan Thistlewood", WasGenerated = true };

        Assert.IsType<Npc>(handWritten);
        Assert.IsType<Npc>(generated);
        Assert.NotEqual(handWritten.WasGenerated, generated.WasGenerated);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_or_whitespace_name_throws(string name)
    {
        Assert.Throws<ArgumentException>(() => new Npc { Name = name });
    }

    [Fact]
    public void Name_over_max_length_throws()
    {
        var tooLong = new string('a', Npc.NameMaxLength + 1);

        Assert.Throws<ArgumentException>(() => new Npc { Name = tooLong });
    }
}