using GmToolkit.Core.Systems;

namespace GmToolkit.Core.Tests.Systems;

/// <summary>
/// Covers SYSTEMS.md's "Load-time validation checklist" item by item (each numbered item gets at
/// least one rejection test), plus a full valid-pack happy path and a hostile-input smoke test.
/// </summary>
public class CharacterSystemLoaderTests
{
    private const string Context = "test-pack";

    // ---- Embedded-resource loading (mirrors GeneratorTableLoader's own smoke tests) ----

    [Fact]
    public void LoadAll_against_the_real_Core_assembly_succeeds()
    {
        // #83 ships the engine itself with no in-box system content -- that's #84-#87's job. Once a
        // content pack (e.g. dnd5e-2024's Resources/CharacterSystems/dnd5e-2024.json) is embedded,
        // LoadAll must load and validate it without throwing -- the same smoke test
        // GeneratorTableLoaderTests.LoadAll_loads_every_embedded_table_without_error runs for the
        // generator tables.
        var systems = CharacterSystemLoader.LoadAll();

        Assert.NotEmpty(systems);
    }

    [Fact]
    public void LoadResource_throws_a_clear_error_for_a_missing_resource()
    {
        var coreAssembly = typeof(CharacterSystemLoader).Assembly;

        var ex = Assert.Throws<CharacterSystemLoadException>(
            () => CharacterSystemLoader.LoadResource(coreAssembly, "GmToolkit.Core.CharacterSystems.does-not-exist.json"));

        Assert.Contains("was not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_accepts_a_fully_valid_pack()
    {
        var system = BuildSystem(
            pcFields:
            [
                NumberField("st", min: 1, max: 20),
                NumberField("dx", min: 1, max: 20),
                NumberField("hpAdjustment", min: -50, max: 50, defaultValue: 0),
                DerivedField("hp", "st + hpAdjustment"),
                RepeatingGroupField(
                    "traits",
                    [
                        TextField("name", maxLength: 100),
                        EnumField("category", ["Advantage", "Disadvantage"]),
                        NumberField("pointCost", min: -50, max: 50),
                        FreeTextBlockField("description", maxLength: 1000),
                    ]),
            ],
            npcFields: [BooleanField("isElite")]);

        var ex = Record.Exception(() => CharacterSystemLoader.Validate(system, Context));

        Assert.Null(ex);
    }

    // ---- Checklist item 1: formatVersion ----

    [Fact]
    public void Validate_rejects_unrecognized_formatVersion()
    {
        var system = BuildSystem(formatVersion: 999);

        var ex = Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
        Assert.Contains("formatVersion", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Checklist item 2: id format ----

    [Theory]
    [InlineData("GURPS")] // uppercase not allowed
    [InlineData("-gurps")] // must start with [a-z0-9]
    [InlineData("gurps/4e")] // slash not allowed
    [InlineData("../../etc/passwd")] // path traversal must not even parse under this charset
    [InlineData("")]
    public void Validate_rejects_invalid_id_format(string id)
    {
        var system = BuildSystem(id: id);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    [Fact]
    public void Validate_rejects_id_over_64_characters()
    {
        var system = BuildSystem(id: new string('a', 65));

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    [Fact]
    public void Validate_accepts_id_at_64_characters()
    {
        var system = BuildSystem(id: new string('a', 64));

        var ex = Record.Exception(() => CharacterSystemLoader.Validate(system, Context));

        Assert.Null(ex);
    }

    // ---- Checklist item 3: field key format / uniqueness ----

    [Theory]
    [InlineData("1abc")] // must start with a letter or underscore
    [InlineData("has-dash")]
    [InlineData("has space")]
    public void Validate_rejects_invalid_field_key_format(string key)
    {
        var system = BuildSystem(pcFields: [NumberField(key)]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    [Fact]
    public void Validate_rejects_duplicate_top_level_field_keys()
    {
        var system = BuildSystem(pcFields: [NumberField("st"), NumberField("st")]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    [Fact]
    public void Validate_rejects_duplicate_itemFields_keys_within_the_same_group()
    {
        var system = BuildSystem(pcFields: [RepeatingGroupField("skills", [TextField("name"), TextField("name")])]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    [Fact]
    public void Validate_allows_the_same_itemFields_key_reused_across_two_different_groups()
    {
        // Two different repeating-groups are independent naming scopes -- both `actions` and
        // `traits` having a `name` field is not a collision.
        var system = BuildSystem(pcFields:
        [
            RepeatingGroupField("actions", [TextField("name")]),
            RepeatingGroupField("traits", [TextField("name")]),
        ]);

        var ex = Record.Exception(() => CharacterSystemLoader.Validate(system, Context));

        Assert.Null(ex);
    }

    // ---- Checklist item 4: enum options ----

    [Fact]
    public void Validate_rejects_enum_field_with_empty_options()
    {
        var system = BuildSystem(pcFields: [EnumField("rank", [])]);

        var ex = Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
        Assert.Contains("options", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Checklist item 5: maxLength / pattern ----

    [Fact]
    public void Validate_rejects_maxLength_over_the_10000_character_hard_ceiling()
    {
        var system = BuildSystem(pcFields: [TextField("notes", maxLength: 10_001)]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    [Fact]
    public void Validate_rejects_free_text_block_maxLength_over_the_10000_character_hard_ceiling()
    {
        var system = BuildSystem(pcFields: [FreeTextBlockField("notes", maxLength: 10_001)]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    [Fact]
    public void Validate_rejects_pattern_set_without_maxLength()
    {
        var system = BuildSystem(
            pcFields: [new StatFieldDefinition { Key = "code", Label = "Code", Type = StatFieldTypes.Text, Pattern = "^[A-Z]+$" }]);

        var ex = Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
        Assert.Contains("maxLength", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_rejects_pattern_over_200_characters()
    {
        var system = BuildSystem(
            pcFields: [TextField("code", maxLength: 500, pattern: "^(" + new string('a', 200) + ")$")]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    [Fact]
    public void Validate_rejects_pattern_that_fails_to_compile_under_NonBacktracking()
    {
        // A backreference is one of the constructs RegexOptions.NonBacktracking rejects
        // structurally (not because it's specifically dangerous, but because the engine doesn't
        // support it at all) -- SYSTEMS.md's own example of the "narrower than everything safe"
        // tradeoff. This must surface as an ordinary CharacterSystemLoadException, proving the
        // NotSupportedException the regex engine throws is caught specifically, not left to escape.
        var system = BuildSystem(pcFields: [TextField("code", maxLength: 100, pattern: @"(\w)\1+")]);

        var ex = Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
        Assert.Contains("NonBacktracking", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_rejects_free_text_block_field_that_sets_pattern()
    {
        var system = BuildSystem(
            pcFields: [new StatFieldDefinition { Key = "notes", Label = "Notes", Type = StatFieldTypes.FreeTextBlock, MaxLength = 100, Pattern = "^a+$" }]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    // ---- Checklist item 6: repeating-group nesting/derived + all ceilings ----

    [Fact]
    public void Validate_rejects_repeating_group_nested_inside_another_repeating_group()
    {
        var system = BuildSystem(pcFields: [RepeatingGroupField("outer", [RepeatingGroupField("inner", [TextField("name")])])]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    [Fact]
    public void Validate_rejects_derived_field_inside_a_repeating_group()
    {
        var system = BuildSystem(pcFields: [RepeatingGroupField("skills", [DerivedField("level", "1")])]);

        var ex = Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
        Assert.Contains("top-level-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_rejects_more_than_50_itemFields_in_one_group()
    {
        var itemFields = Enumerable.Range(0, 51).Select(i => TextField($"f{i}")).ToList();
        var system = BuildSystem(pcFields: [RepeatingGroupField("group", itemFields)]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    [Fact]
    public void Validate_rejects_maxItems_over_the_1000_row_hard_ceiling()
    {
        var system = BuildSystem(pcFields: [RepeatingGroupField("group", [TextField("name")], maxItems: 1001)]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    [Fact]
    public void Validate_rejects_more_than_200_top_level_field_definitions()
    {
        var fields = Enumerable.Range(0, 201).Select(i => NumberField($"f{i}")).ToList();
        var system = BuildSystem(pcFields: fields);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    [Fact]
    public void Validate_rejects_aggregate_field_instance_count_over_10000_combined_across_scopes()
    {
        // 100 groups x 20 itemFields x 100 (default maxItems) = 200,000 instances in pcFields alone,
        // comfortably over the 10,000 combined ceiling, while staying under the 200-definitions-
        // per-scope and 50-itemFields-per-group limits individually.
        var itemFields = Enumerable.Range(0, 20).Select(i => TextField($"f{i}")).ToList();
        var groups = Enumerable.Range(0, 100).Select(i => RepeatingGroupField($"group{i}", itemFields)).ToList();
        var system = BuildSystem(pcFields: groups);

        var ex = Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
        Assert.Contains("aggregate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Checklist item 7: formula length / nesting depth / parse-completeness ----

    [Fact]
    public void Validate_rejects_a_formula_over_500_characters()
    {
        var system = BuildSystem(pcFields: [DerivedField("x", new string('1', 501))]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    [Fact]
    public void Validate_rejects_a_formula_over_32_levels_of_nesting()
    {
        var depth = 33;
        var formula = new string('(', depth) + "1" + new string(')', depth);
        var system = BuildSystem(pcFields: [DerivedField("x", formula)]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    [Fact]
    public void Validate_rejects_a_formula_with_trailing_unparsed_input()
    {
        var system = BuildSystem(pcFields: [DerivedField("x", "1 + 2 garbage")]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    // ---- Checklist item 8: formula scope-reference validity ----

    [Fact]
    public void Validate_rejects_a_formula_referencing_an_unknown_field()
    {
        var system = BuildSystem(pcFields: [DerivedField("hp", "st + 1")]); // `st` never defined

        var ex = Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
        Assert.Contains("unknown field", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_rejects_a_formula_referencing_a_key_that_only_exists_inside_a_repeating_group_row()
    {
        var system = BuildSystem(pcFields:
        [
            RepeatingGroupField("skills", [NumberField("level")]),
            DerivedField("total", "level + 1"), // `level` only exists inside `skills`' rows
        ]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    [Fact]
    public void Validate_rejects_a_formula_referencing_a_key_that_only_exists_in_the_other_scope()
    {
        // pcFields and npcFields are independent scopes -- a pcFields formula can't reach npcFields.
        var system = BuildSystem(
            pcFields: [DerivedField("total", "monsterHp + 1")],
            npcFields: [NumberField("monsterHp")]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    // ---- Checklist item 9: dependency cycle / chain-depth ----

    [Fact]
    public void Validate_rejects_a_self_referencing_derived_field()
    {
        var system = BuildSystem(pcFields: [DerivedField("a", "a + 1")]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    [Fact]
    public void Validate_rejects_a_mutually_referencing_pair_of_derived_fields()
    {
        var system = BuildSystem(pcFields: [DerivedField("a", "b + 1"), DerivedField("b", "a + 1")]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    [Fact]
    public void Validate_rejects_a_dependency_chain_over_64_fields_deep()
    {
        var fields = new List<StatFieldDefinition> { DerivedField("f0", "1") };
        for (var i = 1; i <= 64; i++)
        {
            fields.Add(DerivedField($"f{i}", $"f{i - 1} + 1"));
        }

        var system = BuildSystem(pcFields: fields);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    // ---- Unrecognized field type (basic structural correctness, beyond the numbered checklist) ----

    [Fact]
    public void Validate_rejects_an_unrecognized_field_type()
    {
        var system = BuildSystem(pcFields: [new StatFieldDefinition { Key = "x", Label = "X", Type = "not-a-real-type" }]);

        Assert.Throws<CharacterSystemLoadException>(() => CharacterSystemLoader.Validate(system, Context));
    }

    // ---- Hostile-input smoke test: never an unhandled exception type ----

    [Theory]
    [MemberData(nameof(HostileSystems))]
    public void Validate_never_throws_anything_other_than_CharacterSystemLoadException(CharacterSystem system)
    {
        var ex = Record.Exception(() => CharacterSystemLoader.Validate(system, Context));

        Assert.IsType<CharacterSystemLoadException>(ex);
    }

    public static TheoryData<CharacterSystem> HostileSystems()
    {
        return new TheoryData<CharacterSystem>
        {
            BuildSystem(formatVersion: int.MinValue),
            BuildSystem(id: "\0\0\0"),
            BuildSystem(id: string.Concat(Enumerable.Repeat("a/", 1000))),
            BuildSystem(pcFields: [DerivedField("x", new string('(', 20_000) + "1" + new string(')', 20_000))]),
            BuildSystem(pcFields: [DerivedField("x", new string('9', 10_000))]),
            BuildSystem(pcFields: [TextField("code", maxLength: 100, pattern: new string('(', 199) + "a" + new string(')', 199))]),
            BuildSystem(pcFields: [RepeatingGroupField("g", [])]),
            BuildSystem(pcFields: [new StatFieldDefinition { Key = "g", Label = "G", Type = StatFieldTypes.RepeatingGroup, ItemFields = null }]),
            BuildSystem(pcFields: [new StatFieldDefinition { Key = "e", Label = "E", Type = StatFieldTypes.Enum, Options = null }]),
        };
    }

    private static CharacterSystem BuildSystem(
        int formatVersion = 1,
        string id = "test-system",
        IReadOnlyList<StatFieldDefinition>? pcFields = null,
        IReadOnlyList<StatFieldDefinition>? npcFields = null) => new()
        {
            FormatVersion = formatVersion,
            Id = id,
            Name = "Test System",
            PcFields = pcFields ?? [],
            NpcFields = npcFields ?? [],
        };

    private static StatFieldDefinition NumberField(string key, decimal? min = null, decimal? max = null, decimal? defaultValue = null) => new()
    {
        Key = key,
        Label = key,
        Type = StatFieldTypes.Number,
        Min = min,
        Max = max,
        Default = defaultValue,
    };

    private static StatFieldDefinition TextField(string key, int? maxLength = null, string? pattern = null) => new()
    {
        Key = key,
        Label = key,
        Type = StatFieldTypes.Text,
        MaxLength = maxLength,
        Pattern = pattern,
    };

    private static StatFieldDefinition BooleanField(string key) => new()
    {
        Key = key,
        Label = key,
        Type = StatFieldTypes.Boolean,
    };

    private static StatFieldDefinition EnumField(string key, IReadOnlyList<string> options) => new()
    {
        Key = key,
        Label = key,
        Type = StatFieldTypes.Enum,
        Options = options,
    };

    private static StatFieldDefinition DerivedField(string key, string formula) => new()
    {
        Key = key,
        Label = key,
        Type = StatFieldTypes.Derived,
        Formula = formula,
    };

    private static StatFieldDefinition FreeTextBlockField(string key, int? maxLength = null) => new()
    {
        Key = key,
        Label = key,
        Type = StatFieldTypes.FreeTextBlock,
        MaxLength = maxLength,
    };

    private static StatFieldDefinition RepeatingGroupField(string key, IReadOnlyList<StatFieldDefinition> itemFields, int? maxItems = null) => new()
    {
        Key = key,
        Label = key,
        Type = StatFieldTypes.RepeatingGroup,
        ItemFields = itemFields,
        MaxItems = maxItems,
    };
}