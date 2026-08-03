using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using GmToolkit.Core.Systems;

namespace GmToolkit.UI.ViewModels.Stats;

/// <summary>
/// A <see cref="StatFieldTypes.Derived"/> field -- rendered read-only in
/// <c>SchemaAwareStatsForm.axaml</c> (never a bindable editable control). Its value is never
/// user-entered and never persisted (see <see cref="RawValue"/>); <see cref="SchemaStatsFormViewModel"/>
/// recomputes <see cref="DisplayValue"/> live via <see cref="SetComputedValue"/> every time any input
/// field elsewhere in the same form changes, using
/// <c>GmToolkit.Core.Systems.Formula.DerivedFieldEvaluator</c> -- exactly SYSTEMS.md's "a `derived`
/// value is always recomputed at load/display time by the memoized evaluator," never a value this
/// class computes for itself.
/// </summary>
public sealed partial class DerivedStatFieldViewModel(StatFieldDefinition definition) : StatFieldViewModel(definition)
{
    /// <summary>The formatted, currently-computed value, or an em dash while unresolved (an
    /// unreferenceable input, a formula runtime failure -- see
    /// <c>DerivedFieldEvaluator.EvaluateAll</c>'s fail-closed-per-field semantics).</summary>
    [ObservableProperty]
    public partial string DisplayValue { get; set; } = "—";

    /// <summary>No-op: a <c>derived</c> field's value is never stored in a Stats bag in the first
    /// place (see SYSTEMS.md's storage section), so there is nothing to load here -- its
    /// <see cref="DisplayValue"/> is set by <see cref="SetComputedValue"/> instead, once the rest of
    /// the form has finished loading and the evaluator has run.</summary>
    public override void LoadRawValue(string? rawValue)
    {
    }

    /// <summary>Never persisted -- see this class's remarks.
    /// <see cref="SchemaStatsFormViewModel.BuildStats"/> skips every <see cref="DerivedStatFieldViewModel"/>
    /// entirely rather than relying on this being meaningful, but it still needs to return something
    /// rather than throw.</summary>
    public override string RawValue => string.Empty;

    /// <summary>Never user-input, so always valid -- a formula runtime failure shows as
    /// <see cref="DisplayValue"/> being an em dash, not a validation error.</summary>
    public override bool Validate()
    {
        ErrorMessage = null;
        return true;
    }

    /// <summary>Called by <see cref="SchemaStatsFormViewModel"/> after every recompute pass with this
    /// field's freshly-evaluated result (<c>null</c> when unresolved -- see this class's remarks).</summary>
    internal void SetComputedValue(decimal? value)
    {
        if (!value.HasValue)
        {
            DisplayValue = "—";
            return;
        }

        var precision = Definition.Precision is > 0 ? Definition.Precision.Value : 0;
        DisplayValue = value.Value.ToString("F" + precision, CultureInfo.InvariantCulture);
    }
}