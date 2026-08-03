using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using GmToolkit.Core.Systems;

namespace GmToolkit.UI.ViewModels.Stats;

/// <summary>A <see cref="StatFieldTypes.Number"/> field -- bound to a <c>NumericUpDown</c> in
/// <c>SchemaAwareStatsForm.axaml</c>. <see cref="Value"/> is a nullable <see cref="decimal"/> so an
/// empty field (no value entered at all, distinct from entering <c>0</c>) round-trips as an empty
/// <see cref="RawValue"/> rather than being forced to some numeric placeholder.</summary>
public sealed partial class NumberStatFieldViewModel(StatFieldDefinition definition) : StatFieldViewModel(definition)
{
    [ObservableProperty]
    public partial decimal? Value { get; set; }

    /// <summary><c>NumericUpDown.Minimum</c> binding -- <see cref="decimal.MinValue"/> when
    /// <see cref="StatFieldDefinition.Min"/> is unset, since <c>NumericUpDown</c> has no "no minimum"
    /// sentinel of its own.</summary>
    public decimal Minimum => Definition.Min ?? decimal.MinValue;

    /// <summary>See <see cref="Minimum"/>.</summary>
    public decimal Maximum => Definition.Max ?? decimal.MaxValue;

    public decimal Increment => Definition.Step ?? 1m;

    /// <summary><c>NumericUpDown.FormatString</c> binding, e.g. <c>"F0"</c> for a whole-number field.</summary>
    public string FormatString => "F" + (Definition.Precision is > 0 ? Definition.Precision.Value : 0);

    public override void LoadRawValue(string? rawValue)
    {
        if (decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            Value = parsed;
        }
        else if (Definition.Default.HasValue)
        {
            // A field with no stored value yet (e.g. a freshly-created character) starts at its
            // schema-declared default rather than blank -- mirrors DerivedFieldEvaluator's identical
            // "adjustment field idiom" fallback for the same situation.
            Value = Definition.Default.Value;
        }
        else
        {
            Value = null;
        }

        ErrorMessage = null;
    }

    public override string RawValue => Value.HasValue ? Value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;

    public override bool Validate()
    {
        ErrorMessage = StatFieldValidator.ValidateValue(Definition, RawValue);
        return ErrorMessage is null;
    }

    partial void OnValueChanged(decimal? value)
    {
        Validate();
        RaiseChanged();
    }
}