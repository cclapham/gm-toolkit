using CommunityToolkit.Mvvm.ComponentModel;

using GmToolkit.Core.Systems;

namespace GmToolkit.UI.ViewModels.Stats;

/// <summary>An <see cref="StatFieldTypes.Enum"/> field -- bound to a <c>ComboBox</c> in
/// <c>SchemaAwareStatsForm.axaml</c>. <see cref="SelectedValue"/> is <c>null</c> when nothing's been
/// picked yet (there's no domain rule requiring an enum field to have a selection -- see
/// <see cref="StatFieldValidator"/>'s remarks on the schema format having no "required field"
/// concept), which the <c>ComboBox</c> simply renders as no selection rather than defaulting to
/// <see cref="Options"/>[0].</summary>
public sealed partial class EnumStatFieldViewModel : StatFieldViewModel
{
    public EnumStatFieldViewModel(StatFieldDefinition definition)
        : base(definition)
    {
        Options = definition.Options ?? [];
    }

    public IReadOnlyList<string> Options { get; }

    [ObservableProperty]
    public partial string? SelectedValue { get; set; }

    public override void LoadRawValue(string? rawValue)
    {
        SelectedValue = rawValue is not null && Options.Contains(rawValue, StringComparer.Ordinal) ? rawValue : null;
        ErrorMessage = null;
    }

    public override string RawValue => SelectedValue ?? string.Empty;

    public override bool Validate()
    {
        ErrorMessage = StatFieldValidator.ValidateValue(Definition, RawValue);
        return ErrorMessage is null;
    }

    partial void OnSelectedValueChanged(string? value)
    {
        Validate();
        RaiseChanged();
    }
}