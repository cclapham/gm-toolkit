using GmToolkit.Core.Systems;
using GmToolkit.UI.ViewModels.Stats;

namespace GmToolkit.UI.Tests.ViewModels.Stats;

/// <summary>Covers each leaf <see cref="StatFieldViewModel"/> subtype's load/edit/raw-value/validate
/// round trip, plus <see cref="StatFieldViewModelFactory"/>'s type dispatch -- one field type per
/// group of tests, mirroring <see cref="StatFieldValidatorTests"/>'s own per-type organization one
/// layer up.</summary>
public class StatFieldViewModelTests
{
    [Fact]
    public void NumberStatFieldViewModel_round_trips_a_stored_value()
    {
        var field = new NumberStatFieldViewModel(new StatFieldDefinition { Key = "str", Label = "Strength", Type = StatFieldTypes.Number, Min = 1, Max = 30 });

        field.LoadRawValue("16");

        Assert.Equal(16m, field.Value);
        Assert.Equal("16", field.RawValue);
        Assert.True(field.Validate());
    }

    [Fact]
    public void NumberStatFieldViewModel_falls_back_to_its_schema_default_when_no_value_is_stored_yet()
    {
        var field = new NumberStatFieldViewModel(new StatFieldDefinition { Key = "str", Label = "Strength", Type = StatFieldTypes.Number, Default = 10 });

        field.LoadRawValue(null);

        Assert.Equal(10m, field.Value);
    }

    [Fact]
    public void NumberStatFieldViewModel_out_of_range_value_fails_validation()
    {
        var field = new NumberStatFieldViewModel(new StatFieldDefinition { Key = "str", Label = "Strength", Type = StatFieldTypes.Number, Min = 1, Max = 30 });
        field.LoadRawValue("16");

        field.Value = 99;

        Assert.False(field.Validate());
        Assert.NotNull(field.ErrorMessage);
    }

    [Fact]
    public void NumberStatFieldViewModel_raises_Changed_when_Value_is_edited()
    {
        var field = new NumberStatFieldViewModel(new StatFieldDefinition { Key = "str", Label = "Strength", Type = StatFieldTypes.Number });
        var raised = false;
        field.Changed += () => raised = true;

        field.Value = 12;

        Assert.True(raised);
    }

    [Fact]
    public void TextStatFieldViewModel_round_trips_and_enforces_maxLength()
    {
        var field = new TextStatFieldViewModel(new StatFieldDefinition { Key = "class", Label = "Class", Type = StatFieldTypes.Text, MaxLength = 5 });

        field.LoadRawValue("Ranger");
        Assert.Equal("Ranger", field.RawValue);

        field.Value = "Ranger";
        Assert.False(field.Validate());
    }

    [Fact]
    public void FreeTextBlockStatFieldViewModel_round_trips_a_stored_value()
    {
        var field = new FreeTextBlockStatFieldViewModel(new StatFieldDefinition { Key = "trait", Label = "Trait", Type = StatFieldTypes.FreeTextBlock });

        field.LoadRawValue("Keen senses.");

        Assert.Equal("Keen senses.", field.Value);
        Assert.Equal("Keen senses.", field.RawValue);
        Assert.True(field.Validate());
    }

    [Fact]
    public void BooleanStatFieldViewModel_round_trips_true_and_false()
    {
        var field = new BooleanStatFieldViewModel(new StatFieldDefinition { Key = "legendary", Label = "Legendary", Type = StatFieldTypes.Boolean });

        field.LoadRawValue(bool.TrueString);
        Assert.True(field.Value);
        Assert.Equal(bool.TrueString, field.RawValue);

        field.LoadRawValue(bool.FalseString);
        Assert.False(field.Value);
    }

    [Fact]
    public void BooleanStatFieldViewModel_defaults_to_false_for_an_absent_value()
    {
        var field = new BooleanStatFieldViewModel(new StatFieldDefinition { Key = "legendary", Label = "Legendary", Type = StatFieldTypes.Boolean });

        field.LoadRawValue(null);

        Assert.False(field.Value);
    }

    [Fact]
    public void EnumStatFieldViewModel_round_trips_a_valid_option()
    {
        var field = new EnumStatFieldViewModel(new StatFieldDefinition { Key = "alignment", Label = "Alignment", Type = StatFieldTypes.Enum, Options = ["Lawful Good", "Chaotic Evil"] });

        field.LoadRawValue("Chaotic Evil");

        Assert.Equal("Chaotic Evil", field.SelectedValue);
        Assert.Equal("Chaotic Evil", field.RawValue);
        Assert.True(field.Validate());
    }

    [Fact]
    public void EnumStatFieldViewModel_ignores_a_stored_value_outside_its_options()
    {
        var field = new EnumStatFieldViewModel(new StatFieldDefinition { Key = "alignment", Label = "Alignment", Type = StatFieldTypes.Enum, Options = ["Lawful Good"] });

        field.LoadRawValue("True Neutral");

        Assert.Null(field.SelectedValue);
        Assert.Equal(string.Empty, field.RawValue);
    }

    [Fact]
    public void DerivedStatFieldViewModel_ignores_LoadRawValue_and_is_never_persisted()
    {
        var field = new DerivedStatFieldViewModel(new StatFieldDefinition { Key = "strMod", Label = "STR Modifier", Type = StatFieldTypes.Derived, Formula = "str" });

        field.LoadRawValue("99");

        Assert.Equal(string.Empty, field.RawValue);
        Assert.True(field.Validate());
    }

    [Fact]
    public void DerivedStatFieldViewModel_SetComputedValue_formats_using_its_precision()
    {
        var field = new DerivedStatFieldViewModel(new StatFieldDefinition { Key = "strMod", Label = "STR Modifier", Type = StatFieldTypes.Derived, Formula = "str", Precision = 0 });

        field.SetComputedValue(3m);
        Assert.Equal("3", field.DisplayValue);

        field.SetComputedValue(null);
        Assert.Equal("—", field.DisplayValue);
    }

    [Theory]
    [InlineData(StatFieldTypes.Number, typeof(NumberStatFieldViewModel))]
    [InlineData(StatFieldTypes.Text, typeof(TextStatFieldViewModel))]
    [InlineData(StatFieldTypes.Boolean, typeof(BooleanStatFieldViewModel))]
    [InlineData(StatFieldTypes.Derived, typeof(DerivedStatFieldViewModel))]
    [InlineData(StatFieldTypes.RepeatingGroup, typeof(RepeatingGroupStatFieldViewModel))]
    [InlineData(StatFieldTypes.FreeTextBlock, typeof(FreeTextBlockStatFieldViewModel))]
    public void StatFieldViewModelFactory_creates_the_right_type_for_every_non_enum_field_type(string type, Type expected)
    {
        var definition = new StatFieldDefinition
        {
            Key = "field",
            Label = "Field",
            Type = type,
            Formula = type == StatFieldTypes.Derived ? "0" : null,
            ItemFields = type == StatFieldTypes.RepeatingGroup ? [] : null,
        };

        var field = StatFieldViewModelFactory.Create(definition);

        Assert.IsType(expected, field);
    }

    [Fact]
    public void StatFieldViewModelFactory_creates_an_EnumStatFieldViewModel_for_enum_fields()
    {
        var definition = new StatFieldDefinition { Key = "field", Label = "Field", Type = StatFieldTypes.Enum, Options = ["A"] };

        var field = StatFieldViewModelFactory.Create(definition);

        Assert.IsType<EnumStatFieldViewModel>(field);
    }

    [Fact]
    public void StatFieldViewModelFactory_throws_for_an_unrecognized_type()
    {
        var definition = new StatFieldDefinition { Key = "field", Label = "Field", Type = "not-a-real-type" };

        Assert.Throws<NotSupportedException>(() => StatFieldViewModelFactory.Create(definition));
    }
}