using System.Collections.ObjectModel;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;

using GmToolkit.Core.Systems;

namespace GmToolkit.UI.ViewModels.Stats;

/// <summary>
/// One row of a <see cref="RepeatingGroupStatFieldViewModel"/> -- a small, independently-bindable
/// bundle of <see cref="StatFieldViewModel"/>s, one per the group's <c>itemFields</c> (built via
/// <see cref="StatFieldViewModelFactory"/>, same as top-level fields, since an item field is drawn
/// from the same <see cref="StatFieldTypes.Number"/>/<c>text</c>/<c>boolean</c>/<c>enum</c>/
/// <c>free-text-block</c> set -- never <c>derived</c> or another <c>repeating-group</c>, which
/// <c>CharacterSystemLoader</c> already rejects at pack-load time). Mirrors
/// <see cref="StatRowViewModel"/>'s "small mutable row, not a <c>KeyValuePair</c>" reasoning one level
/// up: an <c>ObservableCollection&lt;RepeatingGroupRowViewModel&gt;</c> is what
/// <see cref="RepeatingGroupStatFieldViewModel.Rows"/> actually is.
/// </summary>
public sealed class RepeatingGroupRowViewModel : ObservableObject
{
    public RepeatingGroupRowViewModel(IReadOnlyList<StatFieldDefinition> itemFields, IReadOnlyDictionary<string, string>? initialValues)
    {
        ArgumentNullException.ThrowIfNull(itemFields);

        Fields = new ObservableCollection<StatFieldViewModel>(itemFields.Select(StatFieldViewModelFactory.Create));
        foreach (var field in Fields)
        {
            field.LoadRawValue(initialValues?.GetValueOrDefault(field.Key));
            field.Changed += OnFieldChanged;
        }
    }

    /// <summary>This row's cells, one per the owning group's <c>itemFields</c>, in schema order.</summary>
    public ObservableCollection<StatFieldViewModel> Fields { get; }

    /// <summary>Raised whenever any cell in this row changes -- <see cref="RepeatingGroupStatFieldViewModel"/>
    /// re-raises this as its own <see cref="StatFieldViewModel.Changed"/> so a single subscription at
    /// the top-level <see cref="SchemaStatsFormViewModel"/> catches every edit anywhere in the form,
    /// however deeply nested.</summary>
    public event Action? Changed;

    /// <summary>This row's current cell values, keyed by item field <see cref="StatFieldDefinition.Key"/>
    /// -- what <see cref="RepeatingGroupStatFieldViewModel.RawValue"/> serializes one row of via
    /// <see cref="RepeatingGroupCodec"/>.</summary>
    public Dictionary<string, string> BuildRowValues() =>
        Fields.ToDictionary(field => field.Key, field => field.RawValue, StringComparer.Ordinal);

    /// <summary>Validates every cell in this row, returning whether they're all currently valid.</summary>
    public bool Validate()
    {
        var allValid = true;
        foreach (var field in Fields)
        {
            if (!field.Validate())
            {
                allValid = false;
            }
        }

        return allValid;
    }

    private void OnFieldChanged() => Changed?.Invoke();
}