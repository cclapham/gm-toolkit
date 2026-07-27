using GmToolkit.Core.Generator;

namespace GmToolkit.Core.Tests.Generator;

public class TableGeneratorTests
{
    private static GeneratorTable MakeTaggedTable()
    {
        return new GeneratorTable
        {
            Id = "occupation",
            Category = "occupation",
            Entries =
            [
                new GeneratorTableEntry { Value = "Blacksmith", Tags = ["trade"] },
                new GeneratorTableEntry { Value = "Weaver", Tags = ["trade"] },
                new GeneratorTableEntry { Value = "Smuggler", Tags = ["criminal"] },
                new GeneratorTableEntry { Value = "Fence for stolen goods", Tags = ["criminal"] },
                new GeneratorTableEntry { Value = "Innkeeper", Tags = ["common"] },
            ],
        };
    }

    [Fact]
    public void Generate_picks_from_the_whole_table_with_no_tag_filtering()
    {
        var table = MakeTaggedTable();
        var generator = new TableGenerator(table);
        var random = new SystemRandomSource(1);

        var value = generator.Generate(random);

        Assert.Contains(value, table.Entries.Select(e => e.Value));
    }

    [Fact]
    public void GenerateWithNotice_with_a_matching_tag_only_produces_entries_carrying_that_tag()
    {
        var table = MakeTaggedTable();
        var generator = new TableGenerator(table);
        var random = new SystemRandomSource(42);

        var tradeValues = table.Entries.Where(e => e.Tags.Contains("trade")).Select(e => e.Value).ToHashSet();

        for (var i = 0; i < 100; i++)
        {
            var result = generator.GenerateWithNotice(random, requiredTag: "trade");

            Assert.Contains(result.Value, tradeValues);
            Assert.False(result.FellBack);
            Assert.Null(result.FallbackNotice);
        }
    }

    [Fact]
    public void GenerateWithNotice_with_an_unrecognized_tag_falls_back_and_reports_a_notice()
    {
        var table = MakeTaggedTable();
        var generator = new TableGenerator(table);
        var random = new SystemRandomSource(1);

        var result = generator.GenerateWithNotice(random, requiredTag: "nonexistent-category");

        Assert.False(string.IsNullOrWhiteSpace(result.Value));
        Assert.Contains(result.Value, table.Entries.Select(e => e.Value));
        Assert.True(result.FellBack);
        Assert.NotNull(result.FallbackNotice);
        Assert.Contains("nonexistent-category", result.FallbackNotice, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateWithNotice_with_a_null_tag_behaves_exactly_like_Generate()
    {
        var table = MakeTaggedTable();
        var generator = new TableGenerator(table);
        var random = new SystemRandomSource(9);

        var result = generator.GenerateWithNotice(random, requiredTag: null);

        Assert.Contains(result.Value, table.Entries.Select(e => e.Value));
        Assert.False(result.FellBack);
    }
}