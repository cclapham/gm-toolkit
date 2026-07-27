using GmToolkit.Core.Generator;

namespace GmToolkit.Core.Tests.Generator;

/// <summary>
/// Integration-style tests for issue #27 (name culture / occupation category constraints) driven
/// through the real embedded tables (<see cref="GeneratorRegistry.FromEmbeddedTables"/>), rather
/// than hand-built fakes, to prove the tags added to <c>occupation.json</c> and the existing
/// per-culture name tables actually work end-to-end.
/// </summary>
public class GeneratorConstraintsTests
{
    [Fact]
    public void Requesting_the_highland_name_culture_never_draws_from_coastal_across_many_draws()
    {
        var tables = GeneratorTableLoader.LoadAll();
        var highland = tables.Single(t => string.Equals(t.Culture, "highland", StringComparison.OrdinalIgnoreCase));
        var coastal = tables.Single(t => string.Equals(t.Culture, "coastal", StringComparison.OrdinalIgnoreCase));

        var registry = GeneratorRegistry.FromEmbeddedTables();
        var nameGenerator = registry.GetNameGenerator();
        var random = new SystemRandomSource(2027);

        var coastalGivenNames = coastal.Entries.Where(e => e.Tags.Contains("given", StringComparer.OrdinalIgnoreCase))
            .Select(e => e.Value).ToHashSet();
        var coastalSurnames = coastal.Entries.Where(e => e.Tags.Contains("surname", StringComparer.OrdinalIgnoreCase))
            .Select(e => e.Value).ToHashSet();
        var highlandGivenNames = highland.Entries.Where(e => e.Tags.Contains("given", StringComparer.OrdinalIgnoreCase))
            .Select(e => e.Value).ToHashSet();
        var highlandSurnames = highland.Entries.Where(e => e.Tags.Contains("surname", StringComparer.OrdinalIgnoreCase))
            .Select(e => e.Value).ToHashSet();

        for (var i = 0; i < 100; i++)
        {
            var result = nameGenerator.GenerateWithNotice(random, culture: "highland");
            Assert.False(result.FellBack);

            var parts = result.Value.Split(' ', 2);
            Assert.Equal(2, parts.Length);

            Assert.Contains(parts[0], highlandGivenNames);
            Assert.Contains(parts[1], highlandSurnames);
            Assert.DoesNotContain(parts[0], coastalGivenNames);
            Assert.DoesNotContain(parts[1], coastalSurnames);
        }
    }

    [Fact]
    public void Requesting_a_nonexistent_name_culture_falls_back_with_a_notice_instead_of_an_error()
    {
        var registry = GeneratorRegistry.FromEmbeddedTables();
        var nameGenerator = registry.GetNameGenerator();
        var random = new SystemRandomSource(4);

        var exception = Record.Exception(() => nameGenerator.GenerateWithNotice(random, culture: "atlantean"));

        Assert.Null(exception);

        var result = nameGenerator.GenerateWithNotice(new SystemRandomSource(4), culture: "atlantean");

        Assert.False(string.IsNullOrWhiteSpace(result.Value));
        Assert.True(result.FellBack);
        Assert.NotNull(result.FallbackNotice);
        Assert.Contains("atlantean", result.FallbackNotice, StringComparison.Ordinal);
    }

    [Fact]
    public void Requesting_the_trade_occupation_category_only_produces_entries_tagged_trade()
    {
        var tables = GeneratorTableLoader.LoadAll();
        var occupationTable = tables.Single(t => string.Equals(t.Category, "occupation", StringComparison.OrdinalIgnoreCase));
        var tradeValues = occupationTable.Entries.Where(e => e.Tags.Contains("trade", StringComparer.OrdinalIgnoreCase))
            .Select(e => e.Value).ToHashSet();
        Assert.NotEmpty(tradeValues);

        var registry = GeneratorRegistry.FromEmbeddedTables();
        var occupationGenerator = registry.GetTableGenerator("occupation");
        var random = new SystemRandomSource(2028);

        for (var i = 0; i < 100; i++)
        {
            var result = occupationGenerator.GenerateWithNotice(random, requiredTag: "trade");

            Assert.False(result.FellBack);
            Assert.Contains(result.Value, tradeValues);
        }
    }

    [Fact]
    public void Requesting_a_nonexistent_occupation_category_falls_back_with_a_notice_instead_of_an_error()
    {
        var registry = GeneratorRegistry.FromEmbeddedTables();
        var occupationGenerator = registry.GetTableGenerator("occupation");
        var random = new SystemRandomSource(6);

        var exception = Record.Exception(() => occupationGenerator.GenerateWithNotice(random, requiredTag: "space-pirate"));

        Assert.Null(exception);

        var result = occupationGenerator.GenerateWithNotice(new SystemRandomSource(6), requiredTag: "space-pirate");

        Assert.False(string.IsNullOrWhiteSpace(result.Value));
        Assert.True(result.FellBack);
        Assert.NotNull(result.FallbackNotice);
        Assert.Contains("space-pirate", result.FallbackNotice, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("trade")]
    [InlineData("common")]
    [InlineData("civic")]
    [InlineData("scholarly")]
    [InlineData("criminal")]
    public void Every_documented_occupation_category_tag_matches_at_least_one_real_embedded_entry(string tag)
    {
        var tables = GeneratorTableLoader.LoadAll();
        var occupationTable = tables.Single(t => string.Equals(t.Category, "occupation", StringComparison.OrdinalIgnoreCase));

        var matches = occupationTable.Entries.Where(e => e.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();

        Assert.NotEmpty(matches);
    }
}