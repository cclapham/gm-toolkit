using GmToolkit.Core.Generator;

namespace GmToolkit.Core.Tests.Generator;

public class GeneratorRegistryTests
{
    private static GeneratorTable MakeTable(string id, string category, params string[] values)
    {
        return new GeneratorTable
        {
            Id = id,
            Category = category,
            Entries = values.Select(v => new GeneratorTableEntry { Value = v }).ToList(),
        };
    }

    [Fact]
    public void GetGenerator_generates_from_a_brand_new_category_with_no_registry_code_changes()
    {
        // Stands in for a hypothetical future "loot" table: nothing about GeneratorRegistry's
        // implementation needs to change to support a new single-table category.
        var loot = MakeTable("loot", "loot", "Rusty dagger", "Bag of coins", "Ancient scroll");
        var registry = new GeneratorRegistry([loot]);
        var random = new SystemRandomSource(1);

        Assert.Contains("loot", registry.Categories);

        var generator = registry.GetGenerator("loot");
        var result = generator.Generate(random);

        Assert.Contains(result, loot.Entries.Select(e => e.Value));
    }

    [Fact]
    public void GetGenerator_throws_for_an_unregistered_category()
    {
        var registry = new GeneratorRegistry([MakeTable("occupation", "occupation", "Blacksmith")]);

        Assert.Throws<KeyNotFoundException>(() => registry.GetGenerator("does-not-exist"));
    }

    [Fact]
    public void TryGetGenerator_returns_false_and_a_null_out_param_for_an_unregistered_category()
    {
        var registry = new GeneratorRegistry([MakeTable("occupation", "occupation", "Blacksmith")]);

        var found = registry.TryGetGenerator("does-not-exist", out var generator);

        Assert.False(found);
        Assert.Null(generator);
    }

    [Fact]
    public void TryGetGenerator_returns_true_and_a_working_generator_for_a_registered_category()
    {
        var registry = new GeneratorRegistry([MakeTable("occupation", "occupation", "Blacksmith")]);
        var random = new SystemRandomSource(1);

        var found = registry.TryGetGenerator("occupation", out var generator);

        Assert.True(found);
        Assert.NotNull(generator);
        Assert.Equal("Blacksmith", generator.Generate(random));
    }

    [Fact]
    public void GetGenerator_for_names_composes_across_every_supplied_culture_table()
    {
        var highland = new GeneratorTable
        {
            Id = "names-highland",
            Category = "names",
            Culture = "highland",
            Entries =
            [
                new GeneratorTableEntry { Value = "Brennic", Tags = ["given"] },
                new GeneratorTableEntry { Value = "Stonevale", Tags = ["surname"] },
            ],
        };
        var coastal = new GeneratorTable
        {
            Id = "names-coastal",
            Category = "names",
            Culture = "coastal",
            Entries =
            [
                new GeneratorTableEntry { Value = "Marin", Tags = ["given"] },
                new GeneratorTableEntry { Value = "Wavecrest", Tags = ["surname"] },
            ],
        };
        var registry = new GeneratorRegistry([highland, coastal]);

        var nameGenerator = registry.GetNameGenerator();

        Assert.Equal(2, nameGenerator.Cultures.Count);
        Assert.Contains("highland", nameGenerator.Cultures);
        Assert.Contains("coastal", nameGenerator.Cultures);

        // GetGenerator("names") returns the same composed NameGenerator through the narrower
        // IGenerator<string> view.
        var viaGetGenerator = registry.GetGenerator("names");
        Assert.Same(nameGenerator, viaGetGenerator);
    }

    [Fact]
    public void GetNameGenerator_throws_when_no_names_tables_are_registered()
    {
        var registry = new GeneratorRegistry([MakeTable("occupation", "occupation", "Blacksmith")]);

        Assert.Throws<KeyNotFoundException>(() => registry.GetNameGenerator());
    }

    [Fact]
    public void Constructor_throws_when_a_non_names_category_has_more_than_one_table()
    {
        var tableA = MakeTable("occupation-a", "occupation", "Blacksmith");
        var tableB = MakeTable("occupation-b", "occupation", "Innkeeper");

        Assert.Throws<InvalidOperationException>(() => new GeneratorRegistry([tableA, tableB]));
    }

    [Fact]
    public void FromEmbeddedTables_builds_a_registry_that_serves_every_real_table_category()
    {
        var registry = GeneratorRegistry.FromEmbeddedTables();

        Assert.Contains("names", registry.Categories);
        Assert.Contains("occupation", registry.Categories);
        Assert.Contains("appearance", registry.Categories);
        Assert.Contains("mannerism", registry.Categories);
        Assert.Contains("motivation", registry.Categories);
        Assert.Contains("secret", registry.Categories);
    }
}