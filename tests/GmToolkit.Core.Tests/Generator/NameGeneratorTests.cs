using GmToolkit.Core.Generator;

namespace GmToolkit.Core.Tests.Generator;

public class NameGeneratorTests
{
    private static GeneratorTable MakeCultureTable(string culture, string[] givenNames, string[] surnames)
    {
        var entries = givenNames.Select(n => new GeneratorTableEntry { Value = n, Tags = ["given"] })
            .Concat(surnames.Select(n => new GeneratorTableEntry { Value = n, Tags = ["surname"] }))
            .ToList();

        return new GeneratorTable
        {
            Id = $"names-{culture}",
            Category = "names",
            Culture = culture,
            Entries = entries,
        };
    }

    [Fact]
    public void Generate_composes_a_given_name_and_surname_from_the_same_culture()
    {
        var highland = MakeCultureTable("highland", ["Brennic", "Torvald"], ["Stonevale", "Ironpeak"]);
        var coastal = MakeCultureTable("coastal", ["Marin", "Talia"], ["Wavecrest", "Saltmoor"]);
        var generator = new NameGenerator([highland, coastal]);
        var random = new SystemRandomSource(42);

        for (var i = 0; i < 100; i++)
        {
            var name = generator.Generate(random);
            var parts = name.Split(' ', 2);
            Assert.Equal(2, parts.Length);

            var given = parts[0];
            var surname = parts[1];

            var isHighlandPair = highland.Entries.Any(e => e.Value == given) && highland.Entries.Any(e => e.Value == surname);
            var isCoastalPair = coastal.Entries.Any(e => e.Value == given) && coastal.Entries.Any(e => e.Value == surname);

            Assert.True(isHighlandPair || isCoastalPair, $"'{name}' mixed given/surname across cultures.");
            Assert.False(isHighlandPair && isCoastalPair, $"'{name}' matched both cultures, which shouldn't be possible.");
        }
    }

    [Fact]
    public void Generate_with_an_explicit_culture_never_draws_from_another_culture()
    {
        var highland = MakeCultureTable("highland", ["Brennic"], ["Stonevale"]);
        var coastal = MakeCultureTable("coastal", ["Marin"], ["Wavecrest"]);
        var generator = new NameGenerator([highland, coastal]);
        var random = new SystemRandomSource(7);

        var name = generator.Generate(random, culture: "coastal");

        Assert.Equal("Marin Wavecrest", name);
    }

    [Fact]
    public void Generate_with_an_unrecognized_culture_throws()
    {
        var highland = MakeCultureTable("highland", ["Brennic"], ["Stonevale"]);
        var generator = new NameGenerator([highland]);
        var random = new SystemRandomSource(1);

        Assert.Throws<ArgumentException>(() => generator.Generate(random, culture: "nonexistent"));
    }

    [Fact]
    public void Generate_is_deterministic_given_identically_seeded_random_sources()
    {
        var highland = MakeCultureTable("highland", ["Brennic", "Torvald", "Ilsevet"], ["Stonevale", "Ironpeak", "Ashcairn"]);
        var coastal = MakeCultureTable("coastal", ["Marin", "Talia", "Corin"], ["Wavecrest", "Saltmoor", "Driftholm"]);

        var generatorA = new NameGenerator([highland, coastal]);
        var generatorB = new NameGenerator([highland, coastal]);
        var randomA = new SystemRandomSource(2026);
        var randomB = new SystemRandomSource(2026);

        var namesA = Enumerable.Range(0, 20).Select(_ => generatorA.Generate(randomA)).ToList();
        var namesB = Enumerable.Range(0, 20).Select(_ => generatorB.Generate(randomB)).ToList();

        Assert.Equal(namesA, namesB);
    }
}