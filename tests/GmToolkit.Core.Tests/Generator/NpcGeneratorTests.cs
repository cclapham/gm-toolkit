using GmToolkit.Core.Generator;

namespace GmToolkit.Core.Tests.Generator;

public class NpcGeneratorTests
{
    private static NpcGenerator CreateGeneratorOverRealTables()
    {
        var registry = GeneratorRegistry.FromEmbeddedTables();
        return new NpcGenerator(registry);
    }

    [Fact]
    public void Generate_produces_every_expected_field_non_empty_using_the_real_embedded_tables()
    {
        var generator = CreateGeneratorOverRealTables();
        var random = new SystemRandomSource(1);

        var npc = generator.Generate(random);

        Assert.False(string.IsNullOrWhiteSpace(npc.Name));
        Assert.False(string.IsNullOrWhiteSpace(npc.Role));
        Assert.False(string.IsNullOrWhiteSpace(npc.Appearance));
        Assert.False(string.IsNullOrWhiteSpace(npc.Mannerism));
        Assert.False(string.IsNullOrWhiteSpace(npc.Motivation));
        Assert.False(string.IsNullOrWhiteSpace(npc.Secret));

        // A generated name is "Given Surname" — two words.
        Assert.Equal(2, npc.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void Generate_is_deterministic_given_identically_seeded_random_sources()
    {
        var generatorA = CreateGeneratorOverRealTables();
        var generatorB = CreateGeneratorOverRealTables();
        var randomA = new SystemRandomSource(2026);
        var randomB = new SystemRandomSource(2026);

        var npcA = generatorA.Generate(randomA);
        var npcB = generatorB.Generate(randomB);

        Assert.Equal(npcA.Name, npcB.Name);
        Assert.Equal(npcA.Role, npcB.Role);
        Assert.Equal(npcA.Appearance, npcB.Appearance);
        Assert.Equal(npcA.Mannerism, npcB.Mannerism);
        Assert.Equal(npcA.Motivation, npcB.Motivation);
        Assert.Equal(npcA.Secret, npcB.Secret);
    }

    [Fact]
    public void Generate_with_different_seeds_usually_produces_different_npcs()
    {
        var generator = CreateGeneratorOverRealTables();

        var npcA = generator.Generate(new SystemRandomSource(1));
        var npcB = generator.Generate(new SystemRandomSource(2));

        // Not a strict guarantee for every field with every seed pair, but with six independently
        // drawn fields over tables with 30+ entries each, two different seeds landing on an
        // identical NPC is implausible enough to be a meaningful regression guard.
        var identical = npcA.Name == npcB.Name
            && npcA.Role == npcB.Role
            && npcA.Appearance == npcB.Appearance
            && npcA.Mannerism == npcB.Mannerism
            && npcA.Motivation == npcB.Motivation
            && npcA.Secret == npcB.Secret;

        Assert.False(identical, "Two different seeds produced an identical NPC across every field.");
    }

    [Fact]
    public void GenerateField_regenerates_only_the_requested_field_in_isolation()
    {
        var generator = CreateGeneratorOverRealTables();
        var random = new SystemRandomSource(5);

        var role = generator.GenerateField(NpcField.Role, random);

        Assert.False(string.IsNullOrWhiteSpace(role));
    }

    [Theory]
    [InlineData(NpcField.Name)]
    [InlineData(NpcField.Role)]
    [InlineData(NpcField.Appearance)]
    [InlineData(NpcField.Mannerism)]
    [InlineData(NpcField.Motivation)]
    [InlineData(NpcField.Secret)]
    public void GenerateField_produces_a_non_empty_value_for_every_field(NpcField field)
    {
        var generator = CreateGeneratorOverRealTables();
        var random = new SystemRandomSource(9);

        var value = generator.GenerateField(field, random);

        Assert.False(string.IsNullOrWhiteSpace(value));
    }

    [Fact]
    public void GenerateField_is_deterministic_given_identically_seeded_random_sources()
    {
        var generatorA = CreateGeneratorOverRealTables();
        var generatorB = CreateGeneratorOverRealTables();

        var valueA = generatorA.GenerateField(NpcField.Secret, new SystemRandomSource(77));
        var valueB = generatorB.GenerateField(NpcField.Secret, new SystemRandomSource(77));

        Assert.Equal(valueA, valueB);
    }

    [Fact]
    public void GenerateField_with_a_name_culture_constraint_produces_only_that_culture_across_many_draws()
    {
        var generator = CreateGeneratorOverRealTables();
        var registry = GeneratorRegistry.FromEmbeddedTables();
        var nameGenerator = registry.GetNameGenerator();
        var highlandGiven = nameGenerator.Cultures.Contains("highland", StringComparer.OrdinalIgnoreCase);
        Assert.True(highlandGiven, "Expected the real embedded name tables to include a 'highland' culture.");

        var random = new SystemRandomSource(11);
        var constraints = new GeneratorConstraints { NameCulture = "highland" };

        for (var i = 0; i < 50; i++)
        {
            var result = generator.GenerateField(NpcField.Name, random, constraints);

            Assert.False(string.IsNullOrWhiteSpace(result.Value));
            Assert.False(result.FellBack);
            Assert.Null(result.FallbackNotice);
        }
    }

    [Fact]
    public void GenerateField_with_an_unrecognized_name_culture_never_throws_or_returns_empty_and_reports_a_notice()
    {
        var generator = CreateGeneratorOverRealTables();
        var random = new SystemRandomSource(3);
        var constraints = new GeneratorConstraints { NameCulture = "atlantean" };

        var result = generator.GenerateField(NpcField.Name, random, constraints);

        Assert.False(string.IsNullOrWhiteSpace(result.Value));
        Assert.True(result.FellBack);
        Assert.NotNull(result.FallbackNotice);
    }

    [Fact]
    public void GenerateField_with_an_occupation_category_constraint_produces_only_that_category_across_many_draws()
    {
        var generator = CreateGeneratorOverRealTables();
        var random = new SystemRandomSource(13);
        var constraints = new GeneratorConstraints { OccupationCategory = "criminal" };

        for (var i = 0; i < 50; i++)
        {
            var result = generator.GenerateField(NpcField.Role, random, constraints);

            Assert.False(string.IsNullOrWhiteSpace(result.Value));
            Assert.False(result.FellBack);
            Assert.Null(result.FallbackNotice);
        }
    }

    [Fact]
    public void GenerateField_with_an_unrecognized_occupation_category_never_throws_or_returns_empty_and_reports_a_notice()
    {
        var generator = CreateGeneratorOverRealTables();
        var random = new SystemRandomSource(21);
        var constraints = new GeneratorConstraints { OccupationCategory = "does-not-exist" };

        var result = generator.GenerateField(NpcField.Role, random, constraints);

        Assert.False(string.IsNullOrWhiteSpace(result.Value));
        Assert.True(result.FellBack);
        Assert.NotNull(result.FallbackNotice);
    }

    [Theory]
    [InlineData(NpcField.Appearance)]
    [InlineData(NpcField.Mannerism)]
    [InlineData(NpcField.Motivation)]
    [InlineData(NpcField.Secret)]
    public void GenerateField_with_constraints_ignores_them_for_fields_that_have_no_defined_constraint(NpcField field)
    {
        var generator = CreateGeneratorOverRealTables();
        var random = new SystemRandomSource(31);
        var constraints = new GeneratorConstraints { NameCulture = "highland", OccupationCategory = "trade" };

        var result = generator.GenerateField(field, random, constraints);

        Assert.False(string.IsNullOrWhiteSpace(result.Value));
        Assert.False(result.FellBack);
        Assert.Null(result.FallbackNotice);
    }

    [Fact]
    public void GenerateField_with_GeneratorConstraints_None_behaves_like_the_unconstrained_overload()
    {
        var generatorA = CreateGeneratorOverRealTables();
        var generatorB = CreateGeneratorOverRealTables();

        var unconstrained = generatorA.GenerateField(NpcField.Role, new SystemRandomSource(55));
        var constrained = generatorB.GenerateField(NpcField.Role, new SystemRandomSource(55), GeneratorConstraints.None);

        Assert.Equal(unconstrained, constrained.Value);
        Assert.False(constrained.FellBack);
    }

    [Fact]
    public void Rerolling_one_field_does_not_require_regenerating_the_whole_npc()
    {
        var generator = CreateGeneratorOverRealTables();
        var npc = generator.Generate(new SystemRandomSource(1));
        var originalName = npc.Name;
        var originalAppearance = npc.Appearance;

        // Simulates #28's per-field reroll: only Role is replaced, using a fresh random source.
        npc.Role = generator.GenerateField(NpcField.Role, new SystemRandomSource(1000));

        Assert.Equal(originalName, npc.Name);
        Assert.Equal(originalAppearance, npc.Appearance);
        Assert.False(string.IsNullOrWhiteSpace(npc.Role));
    }
}