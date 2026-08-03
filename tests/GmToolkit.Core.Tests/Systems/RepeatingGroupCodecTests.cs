using GmToolkit.Core.Systems;

namespace GmToolkit.Core.Tests.Systems;

/// <summary>Covers <see cref="RepeatingGroupCodec"/>'s round-trip and malformed-input handling --
/// SYSTEMS.md's "a `repeating-group`'s rows (serialized as a JSON array under that field's key)."</summary>
public class RepeatingGroupCodecTests
{
    [Fact]
    public void Serialize_then_Deserialize_round_trips_rows_in_order()
    {
        var rows = new List<IReadOnlyDictionary<string, string>>
        {
            new Dictionary<string, string> { ["skillName"] = "Stealth", ["skillBonus"] = "5" },
            new Dictionary<string, string> { ["skillName"] = "Perception", ["skillBonus"] = "3" },
        };

        var json = RepeatingGroupCodec.Serialize(rows);
        var roundTripped = RepeatingGroupCodec.Deserialize(json);

        Assert.Equal(2, roundTripped.Count);
        Assert.Equal("Stealth", roundTripped[0]["skillName"]);
        Assert.Equal("5", roundTripped[0]["skillBonus"]);
        Assert.Equal("Perception", roundTripped[1]["skillName"]);
    }

    [Fact]
    public void Serialize_of_an_empty_list_produces_an_empty_JSON_array()
    {
        var json = RepeatingGroupCodec.Serialize([]);

        Assert.Equal("[]", json);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Deserialize_of_blank_input_returns_an_empty_list(string? json)
    {
        Assert.Empty(RepeatingGroupCodec.Deserialize(json));
    }

    [Fact]
    public void Deserialize_of_malformed_JSON_returns_an_empty_list_instead_of_throwing()
    {
        var rows = RepeatingGroupCodec.Deserialize("{not valid json");

        Assert.Empty(rows);
    }

    [Fact]
    public void Deserialize_of_a_JSON_object_instead_of_an_array_returns_an_empty_list()
    {
        var rows = RepeatingGroupCodec.Deserialize("{\"skillName\":\"Stealth\"}");

        Assert.Empty(rows);
    }
}