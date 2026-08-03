using GmToolkit.Core.Systems;

namespace GmToolkit.Core.Tests.Systems;

/// <summary>Covers <see cref="StatFieldValidator"/>'s per-type rules (issues #89/#90's "client-side
/// validation against each field's min/max/regex before save") -- one happy path plus one rejection
/// per rule.</summary>
public class StatFieldValidatorTests
{
    // ---- number ----

    [Fact]
    public void Number_field_accepts_a_value_within_range()
    {
        var field = new StatFieldDefinition { Key = "str", Label = "Strength", Type = StatFieldTypes.Number, Min = 1, Max = 30 };

        Assert.Null(StatFieldValidator.ValidateValue(field, "16"));
    }

    [Fact]
    public void Number_field_rejects_a_non_numeric_value()
    {
        var field = new StatFieldDefinition { Key = "str", Label = "Strength", Type = StatFieldTypes.Number };

        var error = StatFieldValidator.ValidateValue(field, "sixteen");

        Assert.NotNull(error);
        Assert.Contains("number", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Number_field_rejects_a_value_below_minimum()
    {
        var field = new StatFieldDefinition { Key = "str", Label = "Strength", Type = StatFieldTypes.Number, Min = 1, Max = 30 };

        var error = StatFieldValidator.ValidateValue(field, "0");

        Assert.NotNull(error);
        Assert.Contains("at least", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Number_field_rejects_a_value_above_maximum()
    {
        var field = new StatFieldDefinition { Key = "str", Label = "Strength", Type = StatFieldTypes.Number, Min = 1, Max = 30 };

        var error = StatFieldValidator.ValidateValue(field, "31");

        Assert.NotNull(error);
        Assert.Contains("at most", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Number_field_treats_an_empty_value_as_valid()
    {
        // The schema format has no "required field" concept -- see this class's remarks.
        var field = new StatFieldDefinition { Key = "str", Label = "Strength", Type = StatFieldTypes.Number, Min = 1, Max = 30 };

        Assert.Null(StatFieldValidator.ValidateValue(field, string.Empty));
        Assert.Null(StatFieldValidator.ValidateValue(field, null));
    }

    // ---- text ----

    [Fact]
    public void Text_field_accepts_a_value_within_maxLength()
    {
        var field = new StatFieldDefinition { Key = "class", Label = "Class", Type = StatFieldTypes.Text, MaxLength = 10 };

        Assert.Null(StatFieldValidator.ValidateValue(field, "Ranger"));
    }

    [Fact]
    public void Text_field_rejects_a_value_over_maxLength()
    {
        var field = new StatFieldDefinition { Key = "class", Label = "Class", Type = StatFieldTypes.Text, MaxLength = 5 };

        var error = StatFieldValidator.ValidateValue(field, "Ranger");

        Assert.NotNull(error);
        Assert.Contains("5 characters", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Text_field_falls_back_to_the_default_maxLength_when_unset()
    {
        var field = new StatFieldDefinition { Key = "class", Label = "Class", Type = StatFieldTypes.Text };

        var error = StatFieldValidator.ValidateValue(field, new string('a', CharacterSystemLoader.DefaultTextMaxLength + 1));

        Assert.NotNull(error);
    }

    [Fact]
    public void Text_field_accepts_a_value_matching_its_pattern()
    {
        var field = new StatFieldDefinition { Key = "code", Label = "Code", Type = StatFieldTypes.Text, MaxLength = 10, Pattern = "^[A-Z]{3}$" };

        Assert.Null(StatFieldValidator.ValidateValue(field, "ABC"));
    }

    [Fact]
    public void Text_field_rejects_a_value_not_matching_its_pattern()
    {
        var field = new StatFieldDefinition { Key = "code", Label = "Code", Type = StatFieldTypes.Text, MaxLength = 10, Pattern = "^[A-Z]{3}$" };

        var error = StatFieldValidator.ValidateValue(field, "abc");

        Assert.NotNull(error);
        Assert.Contains("format", error, StringComparison.OrdinalIgnoreCase);
    }

    // ---- free-text-block ----

    [Fact]
    public void FreeTextBlock_field_falls_back_to_its_own_default_maxLength_when_unset()
    {
        var field = new StatFieldDefinition { Key = "notes", Label = "Notes", Type = StatFieldTypes.FreeTextBlock };

        var error = StatFieldValidator.ValidateValue(field, new string('a', CharacterSystemLoader.DefaultFreeTextBlockMaxLength + 1));

        Assert.NotNull(error);
    }

    [Fact]
    public void FreeTextBlock_field_within_maxLength_is_valid()
    {
        var field = new StatFieldDefinition { Key = "notes", Label = "Notes", Type = StatFieldTypes.FreeTextBlock, MaxLength = 4000 };

        Assert.Null(StatFieldValidator.ValidateValue(field, "A trait description."));
    }

    // ---- boolean ----

    [Fact]
    public void Boolean_field_accepts_true_and_false()
    {
        var field = new StatFieldDefinition { Key = "legendary", Label = "Legendary", Type = StatFieldTypes.Boolean };

        Assert.Null(StatFieldValidator.ValidateValue(field, bool.TrueString));
        Assert.Null(StatFieldValidator.ValidateValue(field, bool.FalseString));
        Assert.Null(StatFieldValidator.ValidateValue(field, string.Empty));
    }

    [Fact]
    public void Boolean_field_rejects_a_non_boolean_value()
    {
        var field = new StatFieldDefinition { Key = "legendary", Label = "Legendary", Type = StatFieldTypes.Boolean };

        Assert.NotNull(StatFieldValidator.ValidateValue(field, "maybe"));
    }

    // ---- enum ----

    [Fact]
    public void Enum_field_accepts_one_of_its_options()
    {
        var field = new StatFieldDefinition { Key = "alignment", Label = "Alignment", Type = StatFieldTypes.Enum, Options = ["Lawful Good", "Chaotic Evil"] };

        Assert.Null(StatFieldValidator.ValidateValue(field, "Lawful Good"));
    }

    [Fact]
    public void Enum_field_rejects_a_value_outside_its_options()
    {
        var field = new StatFieldDefinition { Key = "alignment", Label = "Alignment", Type = StatFieldTypes.Enum, Options = ["Lawful Good", "Chaotic Evil"] };

        var error = StatFieldValidator.ValidateValue(field, "True Neutral");

        Assert.NotNull(error);
        Assert.Contains("Lawful Good", error);
    }

    // ---- derived / repeating-group have nothing to validate via ValidateValue ----

    [Fact]
    public void Derived_field_has_nothing_to_validate_via_ValidateValue()
    {
        var field = new StatFieldDefinition { Key = "mod", Label = "Modifier", Type = StatFieldTypes.Derived, Formula = "str" };

        Assert.Null(StatFieldValidator.ValidateValue(field, "anything"));
    }

    [Fact]
    public void RepeatingGroup_field_has_nothing_to_validate_via_ValidateValue()
    {
        var field = new StatFieldDefinition
        {
            Key = "skills",
            Label = "Skills",
            Type = StatFieldTypes.RepeatingGroup,
            ItemFields = [new StatFieldDefinition { Key = "name", Label = "Name", Type = StatFieldTypes.Text }],
        };

        Assert.Null(StatFieldValidator.ValidateValue(field, "[{\"name\":\"Stealth\"}]"));
    }

    // ---- repeating-group row count ----

    [Fact]
    public void ValidateRepeatingGroupRowCount_accepts_a_count_within_bounds()
    {
        var field = new StatFieldDefinition { Key = "skills", Label = "Skills", Type = StatFieldTypes.RepeatingGroup, ItemFields = [], MinItems = 1, MaxItems = 5 };

        Assert.Null(StatFieldValidator.ValidateRepeatingGroupRowCount(field, 3));
    }

    [Fact]
    public void ValidateRepeatingGroupRowCount_rejects_too_few_rows()
    {
        var field = new StatFieldDefinition { Key = "skills", Label = "Skills", Type = StatFieldTypes.RepeatingGroup, ItemFields = [], MinItems = 2 };

        var error = StatFieldValidator.ValidateRepeatingGroupRowCount(field, 1);

        Assert.NotNull(error);
        Assert.Contains("at least", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRepeatingGroupRowCount_rejects_too_many_rows()
    {
        var field = new StatFieldDefinition { Key = "skills", Label = "Skills", Type = StatFieldTypes.RepeatingGroup, ItemFields = [], MaxItems = 3 };

        var error = StatFieldValidator.ValidateRepeatingGroupRowCount(field, 4);

        Assert.NotNull(error);
        Assert.Contains("at most", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRepeatingGroupRowCount_falls_back_to_the_engine_default_maxItems_when_unset()
    {
        var field = new StatFieldDefinition { Key = "skills", Label = "Skills", Type = StatFieldTypes.RepeatingGroup, ItemFields = [] };

        Assert.NotNull(StatFieldValidator.ValidateRepeatingGroupRowCount(field, CharacterSystemLoader.DefaultMaxItems + 1));
        Assert.Null(StatFieldValidator.ValidateRepeatingGroupRowCount(field, CharacterSystemLoader.DefaultMaxItems));
    }
}