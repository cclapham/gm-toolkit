using GmToolkit.Core.Systems;
using GmToolkit.Core.Systems.Formula;

namespace GmToolkit.Core.Tests.Systems;

/// <summary>
/// Covers issue #86's acceptance criteria against the real embedded
/// <c>Resources/CharacterSystems/pathfinder2e.json</c> pack: it loads and validates cleanly under
/// the real <see cref="CharacterSystemLoader"/>, and its shape reflects PF2e's actual
/// proficiency-rank system rather than a relabeled D&D 5e profile -- proficiency expressed as an
/// enum rank (not a flat bonus), Perception as its own standalone field, Class DC, Hero Points,
/// ancestry/heritage, Bulk-based encumbrance, and Resistances/Weaknesses with a per-damage-type
/// numeric value (distinct from 5e's flat resistances/immunities/vulnerabilities lines).
/// </summary>
public class Pathfinder2eCharacterSystemTests
{
    private const string ResourceName = "GmToolkit.Core.CharacterSystems.pathfinder2e.json";

    private static readonly string[] ProficiencyRanks = ["Untrained", "Trained", "Expert", "Master", "Legendary"];

    private static CharacterSystem LoadSystem()
        => CharacterSystemLoader.LoadResource(typeof(CharacterSystemLoader).Assembly, ResourceName);

    [Fact]
    public void The_pack_loads_and_validates_cleanly_against_the_real_engine()
    {
        var ex = Record.Exception(LoadSystem);

        Assert.Null(ex);
    }

    [Fact]
    public void The_pack_is_discovered_by_LoadAll_and_registered_under_its_own_id()
    {
        var systems = CharacterSystemLoader.LoadAll();

        Assert.Contains(systems, s => s.Id == "pathfinder2e");
    }

    [Fact]
    public void FromEmbeddedSystems_includes_pathfinder2e_alongside_the_generic_system()
    {
        var registry = CharacterSystemRegistry.FromEmbeddedSystems();

        Assert.True(registry.TryGetById("pathfinder2e", out var system));
        Assert.Equal("Pathfinder 2e", system!.Name);
    }

    // ---- Proficiency ranks: an enum, not a flat bonus ----

    [Theory]
    [InlineData("perceptionProficiencyRank")]
    [InlineData("fortitudeProficiencyRank")]
    [InlineData("reflexProficiencyRank")]
    [InlineData("willProficiencyRank")]
    [InlineData("classDcProficiencyRank")]
    [InlineData("armorProficiencyRank")]
    [InlineData("weaponProficiencyRank")]
    public void PcFields_expose_the_five_proficiency_ranks_as_a_closed_enum(string key)
    {
        var system = LoadSystem();

        var field = Assert.Single(system.PcFields, f => f.Key == key);
        Assert.Equal(StatFieldTypes.Enum, field.Type);
        Assert.Equal(ProficiencyRanks, field.Options);
    }

    [Theory]
    [InlineData("perceptionProficiencyRank")]
    [InlineData("fortitudeProficiencyRank")]
    [InlineData("reflexProficiencyRank")]
    [InlineData("willProficiencyRank")]
    public void NpcFields_expose_save_and_perception_proficiency_ranks_as_a_closed_enum(string key)
    {
        var system = LoadSystem();

        var field = Assert.Single(system.NpcFields, f => f.Key == key);
        Assert.Equal(StatFieldTypes.Enum, field.Type);
        Assert.Equal(ProficiencyRanks, field.Options);
    }

    [Fact]
    public void Skills_repeating_groups_carry_their_own_proficiency_rank_per_row_on_both_pc_and_npc_sides()
    {
        var system = LoadSystem();

        var pcSkills = Assert.Single(system.PcFields, f => f.Key == "skills");
        var pcRank = Assert.Single(pcSkills.ItemFields!, f => f.Key == "proficiencyRank");
        Assert.Equal(ProficiencyRanks, pcRank.Options);

        var npcSkills = Assert.Single(system.NpcFields, f => f.Key == "skills");
        var npcRank = Assert.Single(npcSkills.ItemFields!, f => f.Key == "proficiencyRank");
        Assert.Equal(ProficiencyRanks, npcRank.Options);
    }

    // ---- Perception is standalone, not derived from Wisdom ----

    [Fact]
    public void Perception_is_a_standalone_plain_number_field_not_a_formula_over_wisdom()
    {
        var system = LoadSystem();

        var perception = Assert.Single(system.PcFields, f => f.Key == "perception");
        Assert.Equal(StatFieldTypes.Number, perception.Type);

        // Distinct from D&D's passivePerception, which is `derived` from `wisMod` -- PF2e's
        // Perception has no such formula; it's entered directly (SYSTEMS.md's PF2e sketch).
        Assert.DoesNotContain(system.PcFields, f => f.Key == "passivePerception");

        var npcPerception = Assert.Single(system.NpcFields, f => f.Key == "perception");
        Assert.Equal(StatFieldTypes.Number, npcPerception.Type);
    }

    // ---- PF2e-only PC concepts absent from any 5e profile ----

    [Fact]
    public void PcFields_include_class_dc_hero_points_ancestry_and_heritage()
    {
        var system = LoadSystem();

        Assert.Contains(system.PcFields, f => f.Key == "classDc" && f.Type == StatFieldTypes.Derived);
        Assert.Contains(system.PcFields, f => f.Key == "heroPoints");
        Assert.Contains(system.PcFields, f => f.Key == "ancestry");
        Assert.Contains(system.PcFields, f => f.Key == "heritage");
    }

    [Fact]
    public void PcFields_track_bulk_based_encumbrance_derived_from_strength()
    {
        var system = LoadSystem();

        Assert.Contains(system.PcFields, f => f.Key == "bulkCarried" && f.Type == StatFieldTypes.Number);

        var encumbered = Assert.Single(system.PcFields, f => f.Key == "encumberedThreshold");
        Assert.Equal(StatFieldTypes.Derived, encumbered.Type);
        Assert.Equal("5 + strMod", encumbered.Formula);

        var maxBulk = Assert.Single(system.PcFields, f => f.Key == "maximumBulk");
        Assert.Equal(StatFieldTypes.Derived, maxBulk.Type);
        Assert.Equal("10 + strMod", maxBulk.Formula);
    }

    // ---- NPC/monster block: level, resistances/weaknesses with a value, reactions ----

    [Fact]
    public void NpcFields_use_level_not_challenge_rating()
    {
        var system = LoadSystem();

        Assert.Contains(system.NpcFields, f => f.Key == "level" && f.Type == StatFieldTypes.Number);
        Assert.DoesNotContain(system.NpcFields, f => f.Key == "challengeRating");
    }

    [Fact]
    public void NpcFields_store_ability_modifiers_directly_not_scores()
    {
        // Real PF2e bestiary stat blocks print ability modifiers directly (e.g. "Str +4"), never a
        // score -- distinct from both the PC side of this same pack and every 5e npcFields sketch.
        var system = LoadSystem();

        foreach (var key in new[] { "strengthModifier", "dexterityModifier", "constitutionModifier", "intelligenceModifier", "wisdomModifier", "charismaModifier" })
        {
            var field = Assert.Single(system.NpcFields, f => f.Key == key);
            Assert.Equal(StatFieldTypes.Number, field.Type);
        }

        Assert.DoesNotContain(system.NpcFields, f => f.Key == "strength");
    }

    [Fact]
    public void NpcFields_use_resistances_and_weaknesses_repeating_groups_with_a_per_type_value_not_flat_5e_text_lines()
    {
        var system = LoadSystem();

        var resistances = Assert.Single(system.NpcFields, f => f.Key == "resistances");
        Assert.Equal(StatFieldTypes.RepeatingGroup, resistances.Type);
        Assert.Contains(resistances.ItemFields!, f => f.Key == "damageType" && f.Type == StatFieldTypes.Enum);
        Assert.Contains(resistances.ItemFields!, f => f.Key == "value" && f.Type == StatFieldTypes.Number);

        var weaknesses = Assert.Single(system.NpcFields, f => f.Key == "weaknesses");
        Assert.Equal(StatFieldTypes.RepeatingGroup, weaknesses.Type);
        Assert.Contains(weaknesses.ItemFields!, f => f.Key == "damageType" && f.Type == StatFieldTypes.Enum);
        Assert.Contains(weaknesses.ItemFields!, f => f.Key == "value" && f.Type == StatFieldTypes.Number);

        // 5e's flat text fields must not appear on this profile -- resistances/weaknesses here are
        // structured (per-type value), not a comma-separated line.
        Assert.DoesNotContain(system.NpcFields, f => f.Key == "damageResistances");
        Assert.DoesNotContain(system.NpcFields, f => f.Key == "damageVulnerabilities");

        // Immunities has no numeric value in PF2e either, so it stays a flat list, same shape as 5e.
        Assert.Contains(system.NpcFields, f => f.Key == "immunities" && f.Type == StatFieldTypes.Text);
    }

    [Fact]
    public void NpcFields_actions_repeating_group_tracks_reactions_as_their_own_action_type()
    {
        var system = LoadSystem();

        var actions = Assert.Single(system.NpcFields, f => f.Key == "actions");
        Assert.Equal(StatFieldTypes.RepeatingGroup, actions.Type);

        var actionType = Assert.Single(actions.ItemFields!, f => f.Key == "actionType");
        Assert.Equal(StatFieldTypes.Enum, actionType.Type);
        Assert.Contains("Reaction", actionType.Options!);
        Assert.Contains("Single Action", actionType.Options!);
    }

    [Fact]
    public void NpcFields_include_senses_and_languages()
    {
        var system = LoadSystem();

        Assert.Contains(system.NpcFields, f => f.Key == "senses");
        Assert.Contains(system.NpcFields, f => f.Key == "languages");
    }

    // ---- No function-call syntax: the grammar has never supported it ----

    [Fact]
    public void No_formula_in_the_pack_uses_function_call_syntax()
    {
        var system = LoadSystem();

        foreach (var field in system.PcFields.Concat(system.NpcFields))
        {
            if (field.Type == StatFieldTypes.Derived)
            {
                Assert.DoesNotContain("floor(", field.Formula, StringComparison.Ordinal);
                Assert.DoesNotContain("min(", field.Formula, StringComparison.Ordinal);
                Assert.DoesNotContain("max(", field.Formula, StringComparison.Ordinal);
            }
        }
    }

    // ---- Derived-field math: ability modifiers and the proficiency-rank-driven fields ----

    [Fact]
    public void Pc_derived_fields_compute_correct_pf2e_values_end_to_end()
    {
        var system = LoadSystem();
        var graph = DerivedFieldGraph.Build(system.PcFields);

        var rawValues = new Dictionary<string, string>
        {
            ["strength"] = "18",
            ["dexterity"] = "14",
            ["constitution"] = "16",
            ["intelligence"] = "10",
            ["wisdom"] = "12",
            ["charisma"] = "8",
            ["level"] = "5",
            ["classDcKeyAbilityModifier"] = "4", // copy of strMod for a Str-based class
            ["classDcProficiencyBonus"] = "7", // Trained at level 5: level(5) + 2 = 7
            ["weaponProficiencyBonus"] = "7", // Trained at level 5: level(5) + 2 = 7
        };

        var results = DerivedFieldEvaluator.EvaluateAll(system.PcFields, graph.EvaluationOrder, rawValues);

        Assert.Equal(4m, results["strMod"]); // floor((18-10)/2) = 4
        Assert.Equal(2m, results["dexMod"]); // floor((14-10)/2) = 2
        Assert.Equal(3m, results["conMod"]); // floor((16-10)/2) = 3
        Assert.Equal(0m, results["intMod"]); // floor((10-10)/2) = 0
        Assert.Equal(1m, results["wisMod"]); // floor((12-10)/2) = 1
        Assert.Equal(-1m, results["chaMod"]); // floor((8-10)/2) = -1

        // The proficiency-rank system expressed as a derived formula: 10 + key ability mod + rank bonus.
        Assert.Equal(21m, results["classDc"]); // 10 + 4 + 7

        // strMod + weaponProficiencyBonus (rank-to-bonus lookup).
        Assert.Equal(11m, results["strikeAttackBonus"]); // 4 + 7

        // PF2e's actual Bulk formulas.
        Assert.Equal(9m, results["encumberedThreshold"]); // 5 + strMod(4)
        Assert.Equal(14m, results["maximumBulk"]); // 10 + strMod(4)
    }

    [Fact]
    public void Pc_derived_fields_compute_correctly_on_a_freshly_created_character_with_unset_adjustment_fields()
    {
        // A brand-new character has only the six ability scores collected up front --
        // classDcKeyAbilityModifier, classDcProficiencyBonus, and weaponProficiencyBonus (adjustment
        // fields) aren't in rawValues yet, but all declare a "default", so the derived fields that
        // reference them must still resolve using those defaults rather than failing closed.
        var system = LoadSystem();
        var graph = DerivedFieldGraph.Build(system.PcFields);

        var rawValues = new Dictionary<string, string>
        {
            ["strength"] = "10",
            ["dexterity"] = "10",
            ["constitution"] = "10",
            ["intelligence"] = "10",
            ["wisdom"] = "10",
            ["charisma"] = "10",
        };

        var results = DerivedFieldEvaluator.EvaluateAll(system.PcFields, graph.EvaluationOrder, rawValues);

        Assert.Equal(0m, results["strMod"]); // floor((10-10)/2) = 0
        Assert.Equal(12m, results["classDc"]); // 10 + classDcKeyAbilityModifier default(0) + classDcProficiencyBonus default(2)
        Assert.Equal(2m, results["strikeAttackBonus"]); // strMod(0) + weaponProficiencyBonus default(2)
        Assert.Equal(5m, results["encumberedThreshold"]); // 5 + strMod(0)
        Assert.Equal(10m, results["maximumBulk"]); // 10 + strMod(0)
    }

    [Fact]
    public void Npc_derived_fields_compute_correct_pf2e_values_end_to_end()
    {
        // The NPC side has no ability-score fields at all (see
        // NpcFields_store_ability_modifiers_directly_not_scores) -- there is nothing to derive on
        // the NPC side of this pack, which is itself a deliberate PF2e-accurate difference from the
        // PC side. This test documents that rather than asserting a formula that doesn't exist.
        var system = LoadSystem();
        var graph = DerivedFieldGraph.Build(system.NpcFields);

        Assert.Empty(graph.EvaluationOrder);
    }
}