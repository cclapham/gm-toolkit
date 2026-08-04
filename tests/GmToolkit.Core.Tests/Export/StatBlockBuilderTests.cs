using GmToolkit.Core.Export;
using GmToolkit.Core.Systems;

namespace GmToolkit.Core.Tests.Export;

public class StatBlockBuilderTests
{
    [Fact]
    public void Build_without_fields_falls_back_to_every_stat_sorted_by_key()
    {
        var stats = new Dictionary<string, string> { ["STR"] = "16", ["DEX"] = "12" };

        var blocks = StatBlockBuilder.Build(stats, null);

        Assert.Equal(2, blocks.Count);
        Assert.Equal("DEX: 12", blocks[0].Text);
        Assert.Equal("STR: 16", blocks[1].Text);
    }

    [Fact]
    public void Build_without_fields_skips_blank_values()
    {
        var stats = new Dictionary<string, string> { ["STR"] = "16", ["Notes"] = "" };

        var blocks = StatBlockBuilder.Build(stats, null);

        Assert.Single(blocks);
        Assert.Equal("STR: 16", blocks[0].Text);
    }

    [Fact]
    public void Build_with_fields_uses_schema_labels_and_order()
    {
        var fields = new List<StatFieldDefinition>
        {
            new() { Key = "dex", Label = "Dexterity", Type = StatFieldTypes.Number },
            new() { Key = "str", Label = "Strength", Type = StatFieldTypes.Number },
        };
        var stats = new Dictionary<string, string> { ["str"] = "16", ["dex"] = "12" };

        var blocks = StatBlockBuilder.Build(stats, fields);

        Assert.Equal(["Dexterity: 12", "Strength: 16"], blocks.Select(b => b.Text));
    }

    [Fact]
    public void Build_with_fields_omits_fields_with_no_value()
    {
        var fields = new List<StatFieldDefinition>
        {
            new() { Key = "str", Label = "Strength", Type = StatFieldTypes.Number },
        };

        var blocks = StatBlockBuilder.Build(new Dictionary<string, string>(), fields);

        Assert.Empty(blocks);
    }

    [Fact]
    public void Build_expands_repeating_group_rows_under_a_subheading()
    {
        var fields = new List<StatFieldDefinition>
        {
            new()
            {
                Key = "equipment",
                Label = "Equipment",
                Type = StatFieldTypes.RepeatingGroup,
                ItemFields =
                [
                    new StatFieldDefinition { Key = "item", Label = "Item", Type = StatFieldTypes.Text },
                    new StatFieldDefinition { Key = "qty", Label = "Qty", Type = StatFieldTypes.Text },
                ],
            },
        };
        var rows = new List<IReadOnlyDictionary<string, string>>
        {
            new Dictionary<string, string> { ["item"] = "Longsword", ["qty"] = "1" },
            new Dictionary<string, string> { ["item"] = "Rope (50ft)", ["qty"] = "2" },
        };
        var stats = new Dictionary<string, string> { ["equipment"] = RepeatingGroupCodec.Serialize(rows) };

        var blocks = StatBlockBuilder.Build(stats, fields);

        Assert.Equal(PdfBlockStyle.SubHeading, blocks[0].Style);
        Assert.Equal("Equipment", blocks[0].Text);
        Assert.Equal("Item: Longsword  |  Qty: 1", blocks[1].Text);
        Assert.Equal("Item: Rope (50ft)  |  Qty: 2", blocks[2].Text);
    }

    [Fact]
    public void Build_omits_a_repeating_group_with_no_rows()
    {
        var fields = new List<StatFieldDefinition>
        {
            new()
            {
                Key = "equipment",
                Label = "Equipment",
                Type = StatFieldTypes.RepeatingGroup,
                ItemFields = [new StatFieldDefinition { Key = "item", Label = "Item", Type = StatFieldTypes.Text }],
            },
        };

        var blocks = StatBlockBuilder.Build(new Dictionary<string, string>(), fields);

        Assert.Empty(blocks);
    }
}