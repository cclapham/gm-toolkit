using System.Collections.ObjectModel;
using System.Linq;

using CommunityToolkit.Mvvm.Input;

using GmToolkit.Core.Systems;

namespace GmToolkit.UI.ViewModels.Stats;

/// <summary>
/// A <see cref="StatFieldTypes.RepeatingGroup"/> field -- a variable-length list of
/// <see cref="RepeatingGroupRowViewModel"/> rows, add/remove/reorder all supported (issue #90's own
/// task: "not just a fixed set of inputs"). Bound in <c>SchemaAwareStatsForm.axaml</c> to a nested
/// <c>ItemsControl</c> over <see cref="Rows"/>, each row itself rendering its own
/// <see cref="RepeatingGroupRowViewModel.Fields"/> via the same implicit per-field-type templates the
/// top-level form uses -- safe to do with the same (unkeyed) templates because a row's item fields
/// can never themselves be another <see cref="StatFieldTypes.RepeatingGroup"/> or
/// <see cref="StatFieldTypes.Derived"/> (<see cref="CharacterSystemLoader"/> rejects that at
/// pack-load time), so there's no risk of infinite recursion in the visual tree.
/// </summary>
public sealed partial class RepeatingGroupStatFieldViewModel(StatFieldDefinition definition) : StatFieldViewModel(definition)
{
    private int MaxItems => Definition.MaxItems ?? CharacterSystemLoader.DefaultMaxItems;

    public IReadOnlyList<StatFieldDefinition> ItemFields { get; } = definition.ItemFields ?? [];

    public ObservableCollection<RepeatingGroupRowViewModel> Rows { get; } = [];

    public bool CanAddRow => Rows.Count < MaxItems;

    public override void LoadRawValue(string? rawValue)
    {
        foreach (var row in Rows)
        {
            row.Changed -= OnRowChanged;
        }

        Rows.Clear();
        foreach (var rowValues in RepeatingGroupCodec.Deserialize(rawValue))
        {
            Rows.Add(CreateRow(rowValues));
        }

        ErrorMessage = null;
        AddRowCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Serializes every row's current cell values as a JSON array -- see
    /// <see cref="RepeatingGroupCodec"/> and SYSTEMS.md's storage section.</summary>
    public override string RawValue =>
        RepeatingGroupCodec.Serialize(Rows.Select(row => (IReadOnlyDictionary<string, string>)row.BuildRowValues()).ToList());

    /// <summary>Validates this group's row count against <see cref="StatFieldDefinition.MinItems"/>/
    /// <see cref="StatFieldDefinition.MaxItems"/>, plus every row's own cells -- see
    /// <see cref="StatFieldValidator.ValidateRepeatingGroupRowCount"/> and
    /// <see cref="RepeatingGroupRowViewModel.Validate"/>.</summary>
    public override bool Validate()
    {
        var rowCountError = StatFieldValidator.ValidateRepeatingGroupRowCount(Definition, Rows.Count);
        var allRowsValid = true;
        foreach (var row in Rows)
        {
            if (!row.Validate())
            {
                allRowsValid = false;
            }
        }

        ErrorMessage = rowCountError;
        return rowCountError is null && allRowsValid;
    }

    [RelayCommand(CanExecute = nameof(CanAddRow))]
    private void AddRow()
    {
        Rows.Add(CreateRow(initialValues: null));
        AddRowCommand.NotifyCanExecuteChanged();
        Validate();
        RaiseChanged();
    }

    [RelayCommand]
    private void RemoveRow(RepeatingGroupRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        row.Changed -= OnRowChanged;
        Rows.Remove(row);
        AddRowCommand.NotifyCanExecuteChanged();
        Validate();
        RaiseChanged();
    }

    [RelayCommand]
    private void MoveRowUp(RepeatingGroupRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var index = Rows.IndexOf(row);
        if (index <= 0)
        {
            return;
        }

        Rows.Move(index, index - 1);
        RaiseChanged();
    }

    [RelayCommand]
    private void MoveRowDown(RepeatingGroupRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var index = Rows.IndexOf(row);
        if (index < 0 || index >= Rows.Count - 1)
        {
            return;
        }

        Rows.Move(index, index + 1);
        RaiseChanged();
    }

    private RepeatingGroupRowViewModel CreateRow(IReadOnlyDictionary<string, string>? initialValues)
    {
        var row = new RepeatingGroupRowViewModel(ItemFields, initialValues);
        row.Changed += OnRowChanged;
        return row;
    }

    private void OnRowChanged()
    {
        Validate();
        RaiseChanged();
    }
}