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
    public void Generate_with_an_unrecognized_culture_falls_back_instead_of_throwing()
    {
        // #27 changed this from throwing (#26's original behavior) to a sensible fallback: an
        // unrecognized culture should never prevent a name from being generated.
        var highland = MakeCultureTable("highland", ["Brennic"], ["Stonevale"]);
        var generator = new NameGenerator([highland]);
        var random = new SystemRandomSource(1);

        var name = generator.Generate(random, culture: "nonexistent");

        // Only one culture is registered, so the fallback has nowhere else to land.
        Assert.Equal("Brennic Stonevale", name);
    }

    [Fact]
    public void GenerateWithNotice_with_an_existing_culture_reports_no_fallback()
    {
        var highland = MakeCultureTable("highland", ["Brennic"], ["Stonevale"]);
        var coastal = MakeCultureTable("coastal", ["Marin"], ["Wavecrest"]);
        var generator = new NameGenerator([highland, coastal]);
        var random = new SystemRandomSource(7);

        var result = generator.GenerateWithNotice(random, culture: "coastal");

        Assert.Equal("Marin Wavecrest", result.Value);
        Assert.Null(result.FallbackNotice);
        Assert.False(result.FellBack);
    }

    [Fact]
    public void GenerateWithNotice_with_an_unrecognized_culture_falls_back_and_reports_a_notice()
    {
        var highland = MakeCultureTable("highland", ["Brennic"], ["Stonevale"]);
        var generator = new NameGenerator([highland]);
        var random = new SystemRandomSource(1);

        var result = generator.GenerateWithNotice(random, culture: "nonexistent");

        Assert.False(string.IsNullOrWhiteSpace(result.Value));
        Assert.Equal("Brennic Stonevale", result.Value);
        Assert.True(result.FellBack);
        Assert.NotNull(result.FallbackNotice);
        Assert.Contains("nonexistent", result.FallbackNotice, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_with_an_explicit_culture_never_draws_from_the_other_culture_across_many_draws()
    {
        var highland = MakeCultureTable("highland", ["Brennic", "Torvald", "Ilsevet"], ["Stonevale", "Ironpeak", "Ashcairn"]);
        var coastal = MakeCultureTable("coastal", ["Marin", "Talia", "Corin"], ["Wavecrest", "Saltmoor", "Driftholm"]);
        var generator = new NameGenerator([highland, coastal]);
        var random = new SystemRandomSource(42);

        var coastalGivenNames = coastal.Entries.Where(e => e.Tags.Contains("given")).Select(e => e.Value).ToHashSet();
        var coastalSurnames = coastal.Entries.Where(e => e.Tags.Contains("surname")).Select(e => e.Value).ToHashSet();

        for (var i = 0; i < 100; i++)
        {
            var name = generator.Generate(random, culture: "highland");
            var parts = name.Split(' ', 2);

            Assert.DoesNotContain(parts[0], coastalGivenNames);
            Assert.DoesNotContain(parts[1], coastalSurnames);
        }
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

    [Fact]
    public void Constructor_throws_when_given_no_culture_tables_at_all()
    {
        // Genuine gap caught during the skeptical review of PR #64: this explicit guard existed
        // since #26 but had no test asserting on it.
        var exception = Assert.Throws<ArgumentException>(() => new NameGenerator([]));
        Assert.Contains("At least one name culture table is required", exception.Message);
    }

    [Fact]
    public void Generate_throws_a_clear_error_when_a_culture_table_has_no_given_tagged_entries()
    {
        // Genuine gap caught during the skeptical review of PR #64: FilterByTag's no-match throw
        // existed since #26 but had no test asserting on it. GeneratorTableLoader doesn't validate
        // that a names table actually has given/surname entries (that's issue #61's separate,
        // already-filed gap) -- this test proves what happens today if one doesn't: a clear,
        // specific exception, not a confusing crash somewhere else.
        var surnameOnly = new GeneratorTable
        {
            Id = "names-broken",
            Category = "names",
            Culture = "broken",
            Entries = [new GeneratorTableEntry { Value = "Stonevale", Tags = ["surname"] }],
        };
        var generator = new NameGenerator([surnameOnly]);
        var random = new SystemRandomSource(1);

        var exception = Assert.Throws<InvalidOperationException>(() => generator.Generate(random, culture: "broken"));
        Assert.Contains("names-broken", exception.Message);
        Assert.Contains("given", exception.Message);
    }
}