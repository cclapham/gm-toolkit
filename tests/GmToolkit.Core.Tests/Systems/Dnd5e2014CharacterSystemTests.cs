using GmToolkit.Core.Systems;
using GmToolkit.Core.Systems.Formula;

namespace GmToolkit.Core.Tests.Systems;

/// <summary>
/// Covers the real embedded <c>Resources/CharacterSystems/dnd5e-2014.json</c> pack: it loads and
/// validates cleanly under the real <see cref="CharacterSystemLoader"/>, its terminology reflects
/// the original 2014 Player's Handbook/Monster Manual rather than a re-skinned 2024 profile (race
/// rather than species, no origin feat field, no weapon mastery property, and no explicit
/// Proficiency Bonus/Initiative lines on the NPC side the way the 2024 Monster Manual has), it's a
/// separate registry entry from <c>dnd5e-2024</c>, and its derived ability-modifier/proficiency-
/// bonus/initiative/passive-perception fields actually compute the correct 5e values end to end.
/// </summary>
public class Dnd5e2014CharacterSystemTests
{
    private const string ResourceName = "GmToolkit.Core.CharacterSystems.dnd5e-2014.json";

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

        Assert.Contains(systems, s => s.Id == "dnd5e-2014");
    }

    [Fact]
    public void The_pack_is_a_separate_registry_entry_from_dnd5e_2024()
    {
        var system = LoadSystem();

        Assert.Equal("dnd5e-2014", system.Id);
        Assert.NotEqual("dnd5e-2024", system.Id);
    }

    [Fact]
    public void FromEmbeddedSystems_includes_dnd5e_2014_alongside_dnd5e_2024_and_the_generic_system()
    {
        var registry = CharacterSystemRegistry.FromEmbeddedSystems();

        Assert.True(registry.TryGetById("dnd5e-2014", out var system));
        Assert.Equal("D&D 5e (2014)", system!.Name);

        Assert.True(registry.TryGetById("dnd5e-2024", out var system2024));
        Assert.NotSame(system, system2024);
    }

    // ---- Terminology: 2014 original, not a re-skinned 2024 profile ----

    [Fact]
    public void PcFields_use_race_not_species()
    {
        var system = LoadSystem();

        Assert.Contains(system.PcFields, f => f.Key == "race");
        Assert.DoesNotContain(system.PcFields, f => f.Key == "species");
    }

    [Fact]
    public void PcFields_have_no_origin_feat_field()
    {
        // Origin feats are a 2024-only concept granted by background; the 2014 rules have no
        // equivalent at character creation.
        var system = LoadSystem();

        Assert.DoesNotContain(system.PcFields, f => f.Key == "originFeat");

        var background = Assert.Single(system.PcFields, f => f.Key == "background");
        Assert.Contains("skill proficiencies", background.HelpText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("origin feat", background.HelpText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PcFields_weapons_repeating_group_has_no_mastery_property()
    {
        // Weapon mastery is a 2024-only addition; the 2014 rules have no such system.
        var system = LoadSystem();

        var weapons = Assert.Single(system.PcFields, f => f.Key == "weapons");
        Assert.Equal(StatFieldTypes.RepeatingGroup, weapons.Type);

        Assert.DoesNotContain(weapons.ItemFields!, f => f.Key == "masteryProperty");
    }

    [Fact]
    public void PcFields_add_an_initiative_proficiency_bonus_adjustment_to_the_dex_modifier()
    {
        var system = LoadSystem();

        Assert.Contains(system.PcFields, f => f.Key == "initiativeProficiencyBonus");
        var initiative = Assert.Single(system.PcFields, f => f.Key == "initiative");
        Assert.Equal("dexMod + initiativeProficiencyBonus", initiative.Formula);
    }

    [Fact]
    public void NpcFields_have_no_2024_only_terminology_additions()
    {
        var system = LoadSystem();

        // The 2014 Monster Manual has no explicit Gear line -- that's a 2024-only addition.
        Assert.DoesNotContain(system.NpcFields, f => f.Key == "gear");

        var proficiencyBonus = Assert.Single(system.NpcFields, f => f.Key == "proficiencyBonus");
        Assert.Contains("doesn't print Proficiency Bonus", proficiencyBonus.HelpText, StringComparison.OrdinalIgnoreCase);

        var initiativeAdjustment = Assert.Single(system.NpcFields, f => f.Key == "initiativeBonusAdjustment");
        Assert.Contains("no explicit Initiative line", initiativeAdjustment.HelpText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NpcFields_still_cover_the_shared_monster_block_shape()
    {
        var system = LoadSystem();

        var expectedKeys = new[]
        {
            "damageResistances", "damageImmunities", "damageVulnerabilities", "conditionImmunities",
            "senses", "languages", "challengeRating",
        };

        foreach (var key in expectedKeys)
        {
            Assert.Contains(system.NpcFields, f => f.Key == key);
        }

        Assert.Contains(system.NpcFields, f => f.Key == "traits" && f.Type == StatFieldTypes.RepeatingGroup);
        Assert.Contains(system.NpcFields, f => f.Key == "actions" && f.Type == StatFieldTypes.RepeatingGroup);
        Assert.Contains(system.NpcFields, f => f.Key == "legendaryActions" && f.Type == StatFieldTypes.RepeatingGroup);
    }

    // ---- No floor()-style function calls: the grammar has never supported function calls ----

    [Fact]
    public void No_formula_in_the_pack_uses_floor_or_other_function_call_syntax()
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

    // ---- Derived-field math: ability modifiers, proficiency bonus, initiative, passive perception ----

    [Fact]
    public void Pc_derived_fields_compute_correct_5e_values_end_to_end()
    {
        var system = LoadSystem();
        var graph = DerivedFieldGraph.Build(system.PcFields);

        var rawValues = new Dictionary<string, string>
        {
            ["strength"] = "16",
            ["dexterity"] = "14",
            ["constitution"] = "12",
            ["intelligence"] = "10",
            ["wisdom"] = "13",
            ["charisma"] = "8",
            ["level"] = "5",
            ["initiativeProficiencyBonus"] = "3",
            ["perceptionProficiencyBonus"] = "0",
        };

        var results = DerivedFieldEvaluator.EvaluateAll(system.PcFields, graph.EvaluationOrder, rawValues);

        Assert.Equal(3m, results["strMod"]); // floor((16-10)/2) = 3
        Assert.Equal(2m, results["dexMod"]); // floor((14-10)/2) = 2
        Assert.Equal(1m, results["conMod"]); // floor((12-10)/2) = 1
        Assert.Equal(0m, results["intMod"]); // floor((10-10)/2) = 0
        Assert.Equal(1m, results["wisMod"]); // floor((13-10)/2) = 1
        Assert.Equal(-1m, results["chaMod"]); // floor((8-10)/2) = -1
        Assert.Equal(3m, results["proficiencyBonus"]); // floor((5-1)/4) + 2 = 3
        Assert.Equal(5m, results["initiative"]); // dexMod(2) + initiativeProficiencyBonus(3)
        Assert.Equal(11m, results["passivePerception"]); // 10 + wisMod(1) + 0
    }

    [Fact]
    public void Pc_derived_fields_compute_correctly_on_a_freshly_created_character_with_unset_adjustment_fields()
    {
        var system = LoadSystem();
        var graph = DerivedFieldGraph.Build(system.PcFields);

        var rawValues = new Dictionary<string, string>
        {
            ["strength"] = "10",
            ["dexterity"] = "14",
            ["constitution"] = "10",
            ["intelligence"] = "10",
            ["wisdom"] = "14",
            ["charisma"] = "10",
            ["level"] = "1",
        };

        var results = DerivedFieldEvaluator.EvaluateAll(system.PcFields, graph.EvaluationOrder, rawValues);

        Assert.Equal(2m, results["dexMod"]); // floor((14-10)/2) = 2
        Assert.Equal(2m, results["wisMod"]); // floor((14-10)/2) = 2
        Assert.Equal(2m, results["initiative"]); // dexMod(2) + initiativeProficiencyBonus default(0)
        Assert.Equal(12m, results["passivePerception"]); // 10 + wisMod(2) + perceptionProficiencyBonus default(0)
    }

    [Fact]
    public void Npc_derived_fields_compute_correct_5e_values_end_to_end()
    {
        var system = LoadSystem();
        var graph = DerivedFieldGraph.Build(system.NpcFields);

        var rawValues = new Dictionary<string, string>
        {
            ["strength"] = "20",
            ["dexterity"] = "18",
            ["constitution"] = "16",
            ["intelligence"] = "6",
            ["wisdom"] = "14",
            ["charisma"] = "10",
            ["initiativeBonusAdjustment"] = "1",
        };

        var results = DerivedFieldEvaluator.EvaluateAll(system.NpcFields, graph.EvaluationOrder, rawValues);

        Assert.Equal(5m, results["strMod"]); // floor((20-10)/2) = 5
        Assert.Equal(4m, results["dexMod"]); // floor((18-10)/2) = 4
        Assert.Equal(3m, results["conMod"]); // floor((16-10)/2) = 3
        Assert.Equal(-2m, results["intMod"]); // floor((6-10)/2) = -2
        Assert.Equal(2m, results["wisMod"]); // floor((14-10)/2) = 2
        Assert.Equal(0m, results["chaMod"]); // floor((10-10)/2) = 0
        Assert.Equal(5m, results["initiative"]); // dexMod(4) + initiativeBonusAdjustment(1)
    }
}