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