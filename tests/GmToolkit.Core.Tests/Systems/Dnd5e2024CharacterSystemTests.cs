using GmToolkit.Core.Systems;
using GmToolkit.Core.Systems.Formula;

namespace GmToolkit.Core.Tests.Systems;

/// <summary>
/// Covers issue #85's acceptance criteria against the real embedded
/// <c>Resources/CharacterSystems/dnd5e-2024.json</c> pack: it loads and validates cleanly under the
/// real <see cref="CharacterSystemLoader"/> (proving the formula grammar in it is actually legal --
/// no <c>floor()</c>-style function calls, which SYSTEMS.md's grammar has never supported), its
/// terminology reflects the 2024 revision rather than a re-skinned 2014 profile (species not race,
/// an origin feat field, weapon mastery properties, an explicit proficiency-gated Initiative, and
/// 2024 Monster Manual terminology on the NPC side), and its derived ability-modifier/proficiency-
/// bonus/initiative/passive-perception fields actually compute the correct 5e values end to end.
/// </summary>
public class Dnd5e2024CharacterSystemTests
{
    private const string ResourceName = "GmToolkit.Core.CharacterSystems.dnd5e-2024.json";

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

        Assert.Contains(systems, s => s.Id == "dnd5e-2024");
    }

    [Fact]
    public void The_pack_is_a_separate_registry_entry_from_dnd5e_2014_should_it_also_be_installed()
    {
        // #85's own acceptance criteria: a separate profile, not a variant sharing an id with 2014.
        var system = LoadSystem();

        Assert.Equal("dnd5e-2024", system.Id);
        Assert.NotEqual("dnd5e-2014", system.Id);
    }

    [Fact]
    public void FromEmbeddedSystems_includes_dnd5e_2024_alongside_the_generic_system()
    {
        var registry = CharacterSystemRegistry.FromEmbeddedSystems();

        Assert.True(registry.TryGetById("dnd5e-2024", out var system));
        Assert.Equal("D&D 5e (2024)", system!.Name);
    }

    // ---- Terminology: 2024 revision, not a re-skinned 2014 profile ----

    [Fact]
    public void PcFields_use_species_not_race()
    {
        var system = LoadSystem();

        Assert.Contains(system.PcFields, f => f.Key == "species");
        Assert.DoesNotContain(system.PcFields, f => f.Key == "race");
    }

    [Fact]
    public void PcFields_include_an_origin_feat_field_granted_by_background()
    {
        var system = LoadSystem();

        var background = Assert.Single(system.PcFields, f => f.Key == "background");
        Assert.Contains("origin feat", background.HelpText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ability score increase", background.HelpText, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(system.PcFields, f => f.Key == "originFeat");
    }

    [Fact]
    public void PcFields_include_a_weapons_repeating_group_with_the_eight_2024_mastery_properties()
    {
        var system = LoadSystem();

        var weapons = Assert.Single(system.PcFields, f => f.Key == "weapons");
        Assert.Equal(StatFieldTypes.RepeatingGroup, weapons.Type);

        var masteryField = Assert.Single(weapons.ItemFields!, f => f.Key == "masteryProperty");
        Assert.Equal(StatFieldTypes.Enum, masteryField.Type);
        Assert.Equal(
            new[] { "Cleave", "Graze", "Nick", "Push", "Sap", "Slow", "Topple", "Vex" },
            masteryField.Options);
    }

    [Fact]
    public void PcFields_gate_initiative_on_a_proficiency_bonus_not_a_bare_dex_modifier()
    {
        // 2024 rule change: Initiative is its own proficiency-gated stat, unlike the 2014 rules'
        // bare Dexterity modifier (see SYSTEMS.md's own dnd5e-2014 sketch, which has no such field).
        var system = LoadSystem();

        Assert.Contains(system.PcFields, f => f.Key == "initiativeProficiencyBonus");
        var initiative = Assert.Single(system.PcFields, f => f.Key == "initiative");
        Assert.Equal("dexMod + initiativeProficiencyBonus", initiative.Formula);
    }

    [Fact]
    public void NpcFields_include_2024_Monster_Manual_terminology_additions()
    {
        var system = LoadSystem();

        // Explicit Proficiency Bonus and Initiative lines are new to the 2024 Monster Manual's
        // stat block layout; the 2014 sketch in SYSTEMS.md has neither.
        Assert.Contains(system.NpcFields, f => f.Key == "proficiencyBonus");
        Assert.Contains(system.NpcFields, f => f.Key == "initiative");
        Assert.Contains(system.NpcFields, f => f.Key == "gear");
    }

    [Fact]
    public void NpcFields_still_cover_the_shared_monster_block_shape_from_84()
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
        // SYSTEMS.md's grammar has no function-call syntax at all ("No function calls -- no
        // floor(), min(), max(), abs(), nothing"); rounding is field metadata (the `rounding`
        // property), never a formula-level call. Guards against literally writing "floor(...)" into
        // a formula string, which parses as a field reference named "floor" followed by unparsed
        // trailing input and fails CharacterSystemLoader.Validate.
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