using System.Reflection;

using GmToolkit.Core.Generator;

namespace GmToolkit.Core.Tests.Generator;

public class GeneratorTableLoaderTests
{
    private const int MinimumEntriesPerTable = 30;

    /// <summary>
    /// Startup-equivalent smoke test: exercises the exact code path the app runs at launch to
    /// seed generator tables, and asserts it succeeds against the real embedded resources.
    /// </summary>
    [Fact]
    public void LoadAll_loads_every_embedded_table_without_error()
    {
        var tables = GeneratorTableLoader.LoadAll();

        Assert.NotEmpty(tables);
    }

    [Fact]
    public void LoadAll_includes_one_table_per_expected_category()
    {
        var tables = GeneratorTableLoader.LoadAll();

        var categories = tables.Select(t => t.Category).Distinct().ToHashSet();

        Assert.Contains("names", categories);
        Assert.Contains("appearance", categories);
        Assert.Contains("mannerism", categories);
        Assert.Contains("motivation", categories);
        Assert.Contains("occupation", categories);
        Assert.Contains("secret", categories);
    }

    [Fact]
    public void Every_table_has_at_least_the_minimum_entry_count()
    {
        var tables = GeneratorTableLoader.LoadAll();

        foreach (var table in tables)
        {
            Assert.True(
                table.Entries.Count >= MinimumEntriesPerTable,
                $"Table '{table.Id}' has {table.Entries.Count} entries, expected at least {MinimumEntriesPerTable}.");
        }
    }

    [Fact]
    public void Every_entry_in_every_table_satisfies_the_schema()
    {
        var tables = GeneratorTableLoader.LoadAll();

        foreach (var table in tables)
        {
            foreach (var entry in table.Entries)
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.Value), $"Table '{table.Id}' has an entry with an empty value.");
                Assert.True(entry.Weight > 0, $"Table '{table.Id}' entry '{entry.Value}' has a non-positive weight.");
                Assert.NotNull(entry.Tags);
            }
        }
    }

    [Fact]
    public void Names_category_covers_at_least_two_distinct_cultures()
    {
        var tables = GeneratorTableLoader.LoadAll();

        var namesCultures = tables
            .Where(t => t.Category == "names")
            .Select(t => t.Culture)
            .ToList();

        Assert.All(namesCultures, culture => Assert.False(string.IsNullOrWhiteSpace(culture)));

        var distinctCultures = namesCultures.Distinct().ToList();
        Assert.True(distinctCultures.Count >= 2, $"Expected at least 2 distinct name cultures, found {distinctCultures.Count}.");
    }

    [Fact]
    public void Core_assembly_actually_embeds_the_expected_table_resources()
    {
        var coreAssembly = typeof(GeneratorTableLoader).Assembly;

        var resourceNames = GeneratorTableLoader.GetTableResourceNames(coreAssembly);

        Assert.Contains(GeneratorTableLoader.ResourceNamePrefix + "appearance.json", resourceNames);
        Assert.Contains(GeneratorTableLoader.ResourceNamePrefix + "mannerism.json", resourceNames);
        Assert.Contains(GeneratorTableLoader.ResourceNamePrefix + "motivation.json", resourceNames);
        Assert.Contains(GeneratorTableLoader.ResourceNamePrefix + "occupation.json", resourceNames);
        Assert.Contains(GeneratorTableLoader.ResourceNamePrefix + "secret.json", resourceNames);
        Assert.True(
            resourceNames.Count(n => n.StartsWith(GeneratorTableLoader.ResourceNamePrefix + "names-", StringComparison.Ordinal)) >= 2,
            "Expected at least 2 embedded names-*.json culture tables.");
    }

    [Fact]
    public void LoadResource_throws_a_clear_error_for_a_missing_resource()
    {
        var coreAssembly = typeof(GeneratorTableLoader).Assembly;

        var ex = Assert.Throws<GeneratorTableLoadException>(
            () => GeneratorTableLoader.LoadResource(coreAssembly, "GmToolkit.Core.GeneratorTables.does-not-exist.json"));

        Assert.Contains("was not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("GmToolkit.Core.Tests.GeneratorTableTestData.malformed-syntax.json", "malformed JSON")]
    [InlineData("GmToolkit.Core.Tests.GeneratorTableTestData.missing-value.json", "malformed JSON")]
    [InlineData("GmToolkit.Core.Tests.GeneratorTableTestData.blank-value.json", "value")]
    [InlineData("GmToolkit.Core.Tests.GeneratorTableTestData.zero-weight.json", "weight")]
    [InlineData("GmToolkit.Core.Tests.GeneratorTableTestData.empty-entries.json", "no entries")]
    [InlineData("GmToolkit.Core.Tests.GeneratorTableTestData.blank-id.json", "'id'")]
    [InlineData("GmToolkit.Core.Tests.GeneratorTableTestData.blank-category.json", "'category'")]
    [InlineData("GmToolkit.Core.Tests.GeneratorTableTestData.names-missing-given.json", "'given'")]
    [InlineData("GmToolkit.Core.Tests.GeneratorTableTestData.names-missing-surname.json", "'surname'")]
    public void LoadResource_throws_a_clear_specific_error_for_malformed_tables(string resourceName, string expectedMessageFragment)
    {
        var testAssembly = Assembly.GetExecutingAssembly();

        var ex = Assert.Throws<GeneratorTableLoadException>(
            () => GeneratorTableLoader.LoadResource(testAssembly, resourceName));

        Assert.Contains(resourceName, ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedMessageFragment, ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}