using GmToolkit.Core.Models;

namespace GmToolkit.Core.Tests.Models;

public class PlayerCharacterTests
{
    [Fact]
    public void Can_represent_a_dnd_character_with_no_code_changes()
    {
        var pc = new PlayerCharacter
        {
            CharacterName = "Brannigan Thistlewood",
            PlayerName = "Alex",
            Ancestry = "Half-Elf",
            Class = "Ranger",
            Level = 4,
            Stats = new Dictionary<string, string>
            {
                ["STR"] = "14",
                ["DEX"] = "18",
                ["Passive Perception"] = "16",
                ["AC"] = "15",
                ["Languages"] = "Common, Elvish",
            },
        };

        Assert.Equal("16", pc.Stats["Passive Perception"]);
    }

    [Fact]
    public void Can_represent_a_call_of_cthulhu_character_with_no_code_changes()
    {
        var pc = new PlayerCharacter
        {
            CharacterName = "Eleanor Voss",
            PlayerName = "Jordan",
            Class = string.Empty, // Call of Cthulhu has no "class" concept
            Stats = new Dictionary<string, string>
            {
                ["SAN"] = "55",
                ["HP"] = "12",
                ["Library Use"] = "70%",
                ["Spot Hidden"] = "55%",
            },
        };

        Assert.Equal("55", pc.Stats["SAN"]);
    }

    [Fact]
    public void Can_represent_a_homebrew_character_with_arbitrary_stat_keys()
    {
        var pc = new PlayerCharacter
        {
            CharacterName = "Zix-9",
            Stats = new Dictionary<string, string>
            {
                ["Reactor Heat"] = "Low",
                ["Scrap Salvaged"] = "42",
            },
        };

        Assert.Equal("Low", pc.Stats["Reactor Heat"]);
    }

    [Fact]
    public void Defaults_are_sensible()
    {
        var pc = new PlayerCharacter { CharacterName = "Brannigan Thistlewood" };

        Assert.NotEqual(Guid.Empty, pc.Id);
        Assert.Equal(1, pc.Level);
        Assert.Empty(pc.Stats);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_or_whitespace_character_name_throws(string name)
    {
        Assert.Throws<ArgumentException>(() => new PlayerCharacter { CharacterName = name });
    }

    [Fact]
    public void Character_name_over_max_length_throws()
    {
        var tooLong = new string('a', PlayerCharacter.NameMaxLength + 1);

        Assert.Throws<ArgumentException>(() => new PlayerCharacter { CharacterName = tooLong });
    }
}