using GmToolkit.Core.Systems;
using GmToolkit.Core.Systems.Formula;

namespace GmToolkit.Core.Tests.Systems;

/// <summary>
/// Covers issue #87's acceptance criteria against the real embedded
/// <c>Resources/CharacterSystems/gurps.json</c> pack: it loads and validates cleanly under the real
/// <see cref="CharacterSystemLoader"/>, and -- as the schema's genericity "acid test" -- it can
/// represent a GURPS character without forcing any D&D-shaped field onto it: no <c>class</c>, no
/// <c>level</c> (roster-list level notwithstanding, this pack's own field set has neither), and no
/// fixed skill enum (skills and traits are both open-ended <c>repeating-group</c>s). It also proves
/// point-buy attribute cost can be <c>derived</c> directly from the bought score, and exercises the
/// formula evaluator with real GURPS 4e math: multi-hop dependency chains, a field multiplied by
/// itself (Basic Lift), and the pack's only use of <c>rounding: ceiling</c>.
/// </summary>
public class GurpsCharacterSystemTests
{
    private const string ResourceName = "GmToolkit.Core.CharacterSystems.gurps.json";

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

        Assert.Contains(systems, s => s.Id == "gurps-4e");
    }

    [Fact]
    public void FromEmbeddedSystems_includes_gurps_alongside_the_generic_system()
    {
        var registry = CharacterSystemRegistry.FromEmbeddedSystems();

        Assert.True(registry.TryGetById("gurps-4e", out var system));
        Assert.Equal("GURPS Fourth Edition", system!.Name);
    }

    // ---- The acid test: no class, no level, no fixed skill list ----

    [Fact]
    public void Neither_pcFields_nor_npcFields_declare_a_class_or_level_field()
    {
        var system = LoadSystem();

        foreach (var fields in new[] { system.PcFields, system.NpcFields })
        {
            Assert.DoesNotContain(fields, f => f.Key == "class");
            Assert.DoesNotContain(fields, f => f.Key == "characterClass");
            Assert.DoesNotContain(fields, f => f.Key == "level");
        }
    }

    [Fact]
    public void Skills_repeating_group_has_no_enum_of_valid_skill_names()
    {
        // GURPS has 100+ published skills, many with specialties -- Name must be free text, not a
        // closed enum the way D&D's/Pathfinder's skill lists are.
        var system = LoadSystem();

        var skills = Assert.Single(system.PcFields, f => f.Key == "skills");
        Assert.Equal(StatFieldTypes.RepeatingGroup, skills.Type);

        var name = Assert.Single(skills.ItemFields!, f => f.Key == "name");
        Assert.Equal(StatFieldTypes.Text, name.Type);
        Assert.Null(name.Options);
    }

    [Fact]
    public void Traits_repeating_group_has_no_enum_of_valid_advantage_or_disadvantage_names()
    {
        // GURPS has hundreds of published advantages/disadvantages plus homebrew ones -- only
        // Category (the four kinds of trait GURPS itself recognizes) is a closed enum; Name is not.
        var system = LoadSystem();

        var traits = Assert.Single(system.PcFields, f => f.Key == "traits");
        Assert.Equal(StatFieldTypes.RepeatingGroup, traits.Type);

        var name = Assert.Single(traits.ItemFields!, f => f.Key == "name");
        Assert.Equal(StatFieldTypes.Text, name.Type);
        Assert.Null(name.Options);

        var category = Assert.Single(traits.ItemFields!, f => f.Key == "category");
        Assert.Equal(StatFieldTypes.Enum, category.Type);
        Assert.Equal(new[] { "Advantage", "Disadvantage", "Quirk", "Perk" }, category.Options);
    }

    [Fact]
    public void Traits_and_skills_item_fields_cover_the_shapes_issue_87_asks_for()
    {
        var system = LoadSystem();

        var traits = Assert.Single(system.PcFields, f => f.Key == "traits");
        Assert.Contains(traits.ItemFields!, f => f.Key == "name" && f.Type == StatFieldTypes.Text);
        Assert.Contains(traits.ItemFields!, f => f.Key == "pointCost" && f.Type == StatFieldTypes.Number);
        Assert.Contains(traits.ItemFields!, f => f.Key == "description" && f.Type == StatFieldTypes.FreeTextBlock);

        var skills = Assert.Single(system.PcFields, f => f.Key == "skills");
        Assert.Contains(skills.ItemFields!, f => f.Key == "name" && f.Type == StatFieldTypes.Text);
        Assert.Contains(skills.ItemFields!, f => f.Key == "relativeLevel" && f.Type == StatFieldTypes.Number);
        Assert.Contains(skills.ItemFields!, f => f.Key == "pointCost" && f.Type == StatFieldTypes.Number);
    }

    [Fact]
    public void Repeating_groups_carry_no_derived_or_nested_repeating_group_item_fields()
    {
        // SYSTEMS.md: `derived` is top-level-only, and a repeating-group can never nest another one
        // -- GURPS's skills group is exactly the case that established this rule (skill level can't
        // be derived because the controlling attribute varies per row).
        var system = LoadSystem();

        foreach (var group in system.PcFields.Concat(system.NpcFields).Where(f => f.Type == StatFieldTypes.RepeatingGroup))
        {
            Assert.All(group.ItemFields!, itemField =>
            {
                Assert.NotEqual(StatFieldTypes.Derived, itemField.Type);
                Assert.NotEqual(StatFieldTypes.RepeatingGroup, itemField.Type);
            });
        }
    }

    // ---- NPC fields mirror PC fields (GURPS has no separate monster stat block) ----

    [Fact]
    public void NpcFields_cover_the_same_core_gurps_concepts_as_pcFields()
    {
        var system = LoadSystem();

        foreach (var key in new[] { "st", "dx", "iq", "ht", "hp", "will", "perception", "fp", "basicSpeed", "basicMove", "dodge", "basicLift", "traits", "skills" })
        {
            Assert.Contains(system.PcFields, f => f.Key == key);
            Assert.Contains(system.NpcFields, f => f.Key == key);
        }
    }

    [Fact]
    public void NpcFields_add_gm_facing_reaction_and_notes_fields_absent_from_pcFields()
    {
        var system = LoadSystem();

        var reaction = Assert.Single(system.NpcFields, f => f.Key == "reactionToPCs");
        Assert.Equal(StatFieldTypes.Enum, reaction.Type);
        Assert.Contains("Neutral", reaction.Options!);

        Assert.Contains(system.NpcFields, f => f.Key == "gmNotes" && f.Type == StatFieldTypes.FreeTextBlock);

        Assert.DoesNotContain(system.PcFields, f => f.Key == "reactionToPCs");
        Assert.DoesNotContain(system.PcFields, f => f.Key == "gmNotes");
    }

    // ---- Point-buy: cost derived from the attribute value, not tracked separately ----

    [Theory]
    [InlineData("st", "stCost")]
    [InlineData("dx", "dxCost")]
    [InlineData("iq", "iqCost")]
    [InlineData("ht", "htCost")]
    public void Primary_attributes_are_point_buy_number_fields_with_a_derived_point_cost(string attributeKey, string costKey)
    {
        var system = LoadSystem();

        var attribute = Assert.Single(system.PcFields, f => f.Key == attributeKey);
        Assert.Equal(StatFieldTypes.Number, attribute.Type);
        Assert.Equal(10m, attribute.Default);

        var cost = Assert.Single(system.PcFields, f => f.Key == costKey);
        Assert.Equal(StatFieldTypes.Derived, cost.Type);
        Assert.Contains(attributeKey, cost.Formula, StringComparison.Ordinal);
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
                Assert.DoesNotContain("ceil(", field.Formula, StringComparison.Ordinal);
                Assert.DoesNotContain("min(", field.Formula, StringComparison.Ordinal);
                Assert.DoesNotContain("max(", field.Formula, StringComparison.Ordinal);
            }
        }
    }

    // ---- Derived-field math: real GURPS 4e values, end to end ----

    [Fact]
    public void Pc_derived_fields_compute_correct_gurps_values_end_to_end()
    {
        var system = LoadSystem();
        var graph = DerivedFieldGraph.Build(system.PcFields);

        var rawValues = new Dictionary<string, string>
        {
            ["st"] = "13",
            ["dx"] = "12",
            ["iq"] = "14",
            ["ht"] = "11",
            ["hpAdjustment"] = "2",
            ["willAdjustment"] = "0",
            ["perAdjustment"] = "1",
            ["fpAdjustment"] = "0",
            ["speedAdjustment"] = "0",
            ["moveAdjustment"] = "0",
            ["dodgeAdjustment"] = "0",
        };

        var results = DerivedFieldEvaluator.EvaluateAll(system.PcFields, graph.EvaluationOrder, rawValues);

        // Point costs: 10/level (ST, HT), 20/level (DX, IQ) above/below 10.
        Assert.Equal(30m, results["stCost"]); // (13-10)*10
        Assert.Equal(40m, results["dxCost"]); // (12-10)*20
        Assert.Equal(80m, results["iqCost"]); // (14-10)*20
        Assert.Equal(10m, results["htCost"]); // (11-10)*10
        Assert.Equal(160m, results["attributePointsSpent"]); // 30+40+80+10 -- 2-hop chain

        Assert.Equal(15m, results["hp"]); // st(13) + hpAdjustment(2)
        Assert.Equal(4m, results["hpCost"]); // 2 * hpAdjustment(2)
        Assert.Equal(14m, results["will"]); // iq(14) + willAdjustment(0)
        Assert.Equal(15m, results["perception"]); // iq(14) + perAdjustment(1)
        Assert.Equal(5m, results["perceptionCost"]); // 5 * perAdjustment(1)
        Assert.Equal(11m, results["fp"]); // ht(11) + fpAdjustment(0)

        Assert.Equal(5.75m, results["basicSpeed"]); // (11+12)/4 + 0 = 5.75
        Assert.Equal(5m, results["basicMove"]); // floor(5.75) + 0 -- 2-hop chain via basicSpeed
        Assert.Equal(8m, results["dodge"]); // floor(5.75) + 3 + 0 -- also chains off basicSpeed, not basicMove

        Assert.Equal(34m, results["basicLift"]); // ceiling(13*13/5) = ceiling(33.8) = 34
    }

    [Fact]
    public void Dodge_is_unaffected_by_basic_move_purchases_because_it_derives_from_basic_speed_not_basic_move()
    {
        // Real GURPS 4e rule: Dodge = floor(Basic Speed) + 3, independent of any points spent buying
        // Basic Move up separately. Proves the pack didn't take the shortcut of chaining dodge off
        // basicMove (which would incorrectly let moveAdjustment leak into Dodge).
        var system = LoadSystem();
        var graph = DerivedFieldGraph.Build(system.PcFields);

        var rawValues = new Dictionary<string, string>
        {
            ["st"] = "10",
            ["dx"] = "10",
            ["iq"] = "10",
            ["ht"] = "10",
            ["moveAdjustment"] = "3", // buy Basic Move up substantially
        };

        var results = DerivedFieldEvaluator.EvaluateAll(system.PcFields, graph.EvaluationOrder, rawValues);

        Assert.Equal(5m, results["basicSpeed"]); // (10+10)/4 = 5.00
        Assert.Equal(8m, results["basicMove"]); // 5 + moveAdjustment(3)
        Assert.Equal(8m, results["dodge"]); // floor(5) + 3 + dodgeAdjustment default(0) -- unaffected by moveAdjustment
    }

    [Fact]
    public void Pc_derived_fields_compute_correctly_on_a_freshly_created_character_with_unset_adjustment_fields()
    {
        // A brand-new character has only the four primary attributes collected up front -- every
        // adjustment field (hpAdjustment, willAdjustment, perAdjustment, fpAdjustment,
        // speedAdjustment, moveAdjustment, dodgeAdjustment) declares "default": 0 and isn't in
        // rawValues yet, so every derived field referencing one must still resolve using that
        // default rather than fail closed.
        var system = LoadSystem();
        var graph = DerivedFieldGraph.Build(system.PcFields);

        var rawValues = new Dictionary<string, string>
        {
            ["st"] = "10",
            ["dx"] = "10",
            ["iq"] = "10",
            ["ht"] = "10",
        };

        var results = DerivedFieldEvaluator.EvaluateAll(system.PcFields, graph.EvaluationOrder, rawValues);

        Assert.Equal(0m, results["stCost"]);
        Assert.Equal(0m, results["attributePointsSpent"]);
        Assert.Equal(10m, results["hp"]); // st(10) + hpAdjustment default(0)
        Assert.Equal(10m, results["will"]);
        Assert.Equal(10m, results["perception"]);
        Assert.Equal(10m, results["fp"]);
        Assert.Equal(5m, results["basicSpeed"]); // (10+10)/4 = 5.00
        Assert.Equal(5m, results["basicMove"]); // floor(5) + moveAdjustment default(0)
        Assert.Equal(8m, results["dodge"]); // floor(5) + 3 + dodgeAdjustment default(0)
        Assert.Equal(20m, results["basicLift"]); // ceiling(10*10/5) = ceiling(20) = 20
    }

    [Fact]
    public void Npc_derived_fields_compute_correct_gurps_values_end_to_end()
    {
        // GURPS has no separate monster stat block -- the NPC side uses the identical formula set as
        // the PC side, unlike every other profile's npcFields (which diverge from pcFields).
        var system = LoadSystem();
        var graph = DerivedFieldGraph.Build(system.NpcFields);

        var rawValues = new Dictionary<string, string>
        {
            ["st"] = "16",
            ["dx"] = "13",
            ["iq"] = "9",
            ["ht"] = "12",
        };

        var results = DerivedFieldEvaluator.EvaluateAll(system.NpcFields, graph.EvaluationOrder, rawValues);

        Assert.Equal(60m, results["stCost"]); // (16-10)*10
        Assert.Equal(16m, results["hp"]); // st(16) + hpAdjustment default(0)
        Assert.Equal(6.25m, results["basicSpeed"]); // (12+13)/4 = 6.25
        Assert.Equal(6m, results["basicMove"]); // floor(6.25)
        Assert.Equal(9m, results["dodge"]); // floor(6.25) + 3
        Assert.Equal(52m, results["basicLift"]); // ceiling(16*16/5) = ceiling(51.2) = 52
    }
}