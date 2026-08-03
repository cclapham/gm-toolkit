using GmToolkit.Core.Systems;
using GmToolkit.UI.ViewModels.Stats;

namespace GmToolkit.UI.Tests.ViewModels.Stats;

/// <summary>
/// Covers <see cref="SchemaStatsFormViewModel"/> -- the top-level schema-driven stats form (issues
/// #89/#90): live derived-field recompute, validation aggregation, stats-preservation on
/// <see cref="SchemaStatsFormViewModel.BuildStats"/>, the dirty-check snapshot, and repeating-group
/// add/remove/reorder.
/// </summary>
public class SchemaStatsFormViewModelTests
{
    private static IReadOnlyList<StatFieldDefinition> DndAbilityFields() =>
    [
        new StatFieldDefinition { Key = "strength", Label = "Strength", Type = StatFieldTypes.Number, Min = 1, Max = 30, Default = 10 },
        new StatFieldDefinition { Key = "dexterity", Label = "Dexterity", Type = StatFieldTypes.Number, Min = 1, Max = 30, Default = 10 },
        new StatFieldDefinition
        {
            Key = "strMod",
            Label = "Strength Modifier",
            Type = StatFieldTypes.Derived,
            Formula = "(strength - 10) / 2",
            Precision = 0,
            Rounding = RoundingModes.Floor,
        },
    ];

    [Fact]
    public void Constructor_loads_stored_values_and_computes_derived_fields()
    {
        var stats = new Dictionary<string, string> { ["strength"] = "16", ["dexterity"] = "14" };

        var form = new SchemaStatsFormViewModel(DndAbilityFields(), stats);

        var strength = Assert.IsType<NumberStatFieldViewModel>(form.Fields.Single(f => f.Key == "strength"));
        Assert.Equal(16m, strength.Value);

        var strMod = Assert.IsType<DerivedStatFieldViewModel>(form.Fields.Single(f => f.Key == "strMod"));
        Assert.Equal("3", strMod.DisplayValue); // floor((16 - 10) / 2) = 3
    }

    [Fact]
    public void Editing_an_input_field_live_recomputes_a_dependent_derived_field()
    {
        var form = new SchemaStatsFormViewModel(DndAbilityFields(), new Dictionary<string, string> { ["strength"] = "10" });
        var strength = Assert.IsType<NumberStatFieldViewModel>(form.Fields.Single(f => f.Key == "strength"));
        var strMod = Assert.IsType<DerivedStatFieldViewModel>(form.Fields.Single(f => f.Key == "strMod"));
        Assert.Equal("0", strMod.DisplayValue);

        strength.Value = 18;

        Assert.Equal("4", strMod.DisplayValue); // floor((18 - 10) / 2) = 4
    }

    [Fact]
    public void Changed_is_raised_when_any_field_edits()
    {
        var form = new SchemaStatsFormViewModel(DndAbilityFields(), new Dictionary<string, string>());
        var strength = Assert.IsType<NumberStatFieldViewModel>(form.Fields.Single(f => f.Key == "strength"));
        var raised = false;
        form.Changed += () => raised = true;

        strength.Value = 12;

        Assert.True(raised);
    }

    [Fact]
    public void Validate_aggregates_HasErrors_from_out_of_range_fields()
    {
        var form = new SchemaStatsFormViewModel(DndAbilityFields(), new Dictionary<string, string>());
        var strength = Assert.IsType<NumberStatFieldViewModel>(form.Fields.Single(f => f.Key == "strength"));

        strength.Value = 999;
        var valid = form.Validate();

        Assert.False(valid);
        Assert.True(form.HasErrors);
    }

    [Fact]
    public void BuildStats_preserves_keys_outside_the_schema_and_never_writes_derived_keys()
    {
        var existing = new Dictionary<string, string> { ["strength"] = "10", ["legacyHomebrewStat"] = "keep-me" };
        var form = new SchemaStatsFormViewModel(DndAbilityFields(), existing);
        var strength = Assert.IsType<NumberStatFieldViewModel>(form.Fields.Single(f => f.Key == "strength"));
        strength.Value = 18;

        var result = form.BuildStats(existing);

        Assert.Equal("18", result["strength"]);
        Assert.Equal("keep-me", result["legacyHomebrewStat"]);
        Assert.False(result.ContainsKey("strMod"));
    }

    [Fact]
    public void ComputeSnapshot_changes_when_a_field_is_edited_and_is_stable_otherwise()
    {
        var form = new SchemaStatsFormViewModel(DndAbilityFields(), new Dictionary<string, string> { ["strength"] = "10" });
        var before = form.ComputeSnapshot();

        Assert.Equal(before, form.ComputeSnapshot());

        var strength = Assert.IsType<NumberStatFieldViewModel>(form.Fields.Single(f => f.Key == "strength"));
        strength.Value = 12;

        Assert.NotEqual(before, form.ComputeSnapshot());
    }

    // ---- repeating-group ----

    private static IReadOnlyList<StatFieldDefinition> SkillsGroupFields() =>
    [
        new StatFieldDefinition
        {
            Key = "skills",
            Label = "Skills",
            Type = StatFieldTypes.RepeatingGroup,
            MinItems = 0,
            MaxItems = 2,
            ItemFields =
            [
                new StatFieldDefinition { Key = "skillName", Label = "Skill", Type = StatFieldTypes.Enum, Options = ["Stealth", "Perception"] },
                new StatFieldDefinition { Key = "skillBonus", Label = "Bonus", Type = StatFieldTypes.Number, Min = -20, Max = 20 },
            ],
        },
    ];

    [Fact]
    public void RepeatingGroup_round_trips_stored_rows()
    {
        var stored = new Dictionary<string, string>
        {
            ["skills"] = RepeatingGroupCodec.Serialize(
            [
                new Dictionary<string, string> { ["skillName"] = "Stealth", ["skillBonus"] = "5" },
            ]),
        };

        var form = new SchemaStatsFormViewModel(SkillsGroupFields(), stored);
        var group = Assert.IsType<RepeatingGroupStatFieldViewModel>(form.Fields.Single(f => f.Key == "skills"));

        Assert.Single(group.Rows);
        var skillNameField = Assert.IsType<EnumStatFieldViewModel>(group.Rows[0].Fields.Single(f => f.Key == "skillName"));
        Assert.Equal("Stealth", skillNameField.SelectedValue);
    }

    [Fact]
    public void AddRow_appends_a_row_and_RemoveRow_removes_it()
    {
        var form = new SchemaStatsFormViewModel(SkillsGroupFields(), new Dictionary<string, string>());
        var group = Assert.IsType<RepeatingGroupStatFieldViewModel>(form.Fields.Single(f => f.Key == "skills"));

        group.AddRowCommand.Execute(null);
        Assert.Single(group.Rows);

        var row = group.Rows[0];
        group.RemoveRowCommand.Execute(row);
        Assert.Empty(group.Rows);
    }

    [Fact]
    public void AddRow_is_disabled_once_maxItems_rows_exist()
    {
        var form = new SchemaStatsFormViewModel(SkillsGroupFields(), new Dictionary<string, string>());
        var group = Assert.IsType<RepeatingGroupStatFieldViewModel>(form.Fields.Single(f => f.Key == "skills"));

        group.AddRowCommand.Execute(null);
        group.AddRowCommand.Execute(null); // MaxItems: 2

        Assert.False(group.AddRowCommand.CanExecute(null));
        Assert.Equal(2, group.Rows.Count);
    }

    [Fact]
    public void MoveRowUp_and_MoveRowDown_reorder_rows()
    {
        var form = new SchemaStatsFormViewModel(SkillsGroupFields(), new Dictionary<string, string>());
        var group = Assert.IsType<RepeatingGroupStatFieldViewModel>(form.Fields.Single(f => f.Key == "skills"));
        group.AddRowCommand.Execute(null);
        group.AddRowCommand.Execute(null);
        var first = group.Rows[0];
        var second = group.Rows[1];

        group.MoveRowDownCommand.Execute(first);
        Assert.Same(second, group.Rows[0]);
        Assert.Same(first, group.Rows[1]);

        group.MoveRowUpCommand.Execute(first);
        Assert.Same(first, group.Rows[0]);
        Assert.Same(second, group.Rows[1]);
    }

    [Fact]
    public void Validate_reports_an_error_when_row_count_is_below_minItems()
    {
        var minItemsFields = new[]
        {
            new StatFieldDefinition
            {
                Key = "skills",
                Label = "Skills",
                Type = StatFieldTypes.RepeatingGroup,
                MinItems = 1,
                ItemFields = [new StatFieldDefinition { Key = "skillName", Label = "Skill", Type = StatFieldTypes.Text }],
            },
        };
        var form = new SchemaStatsFormViewModel(minItemsFields, new Dictionary<string, string>());
        var group = Assert.IsType<RepeatingGroupStatFieldViewModel>(form.Fields.Single(f => f.Key == "skills"));

        var valid = form.Validate();

        Assert.False(valid);
        Assert.NotNull(group.ErrorMessage);
    }

    [Fact]
    public void RepeatingGroup_BuildStats_serializes_current_rows_as_JSON()
    {
        var form = new SchemaStatsFormViewModel(SkillsGroupFields(), new Dictionary<string, string>());
        var group = Assert.IsType<RepeatingGroupStatFieldViewModel>(form.Fields.Single(f => f.Key == "skills"));
        group.AddRowCommand.Execute(null);
        var enumField = Assert.IsType<EnumStatFieldViewModel>(group.Rows[0].Fields.Single(f => f.Key == "skillName"));
        enumField.SelectedValue = "Perception";

        var result = form.BuildStats(new Dictionary<string, string>());

        var rows = RepeatingGroupCodec.Deserialize(result["skills"]);
        var row = Assert.Single(rows);
        Assert.Equal("Perception", row["skillName"]);
    }

    [Fact]
    public void A_change_inside_a_repeating_group_row_bubbles_up_as_the_form_s_Changed_event()
    {
        var form = new SchemaStatsFormViewModel(SkillsGroupFields(), new Dictionary<string, string>());
        var group = Assert.IsType<RepeatingGroupStatFieldViewModel>(form.Fields.Single(f => f.Key == "skills"));
        group.AddRowCommand.Execute(null);
        var raised = false;
        form.Changed += () => raised = true;

        var bonusField = Assert.IsType<NumberStatFieldViewModel>(group.Rows[0].Fields.Single(f => f.Key == "skillBonus"));
        bonusField.Value = 4;

        Assert.True(raised);
    }
}