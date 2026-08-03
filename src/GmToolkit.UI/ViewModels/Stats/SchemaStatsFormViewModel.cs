using System.Collections.ObjectModel;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;

using GmToolkit.Core.Systems;
using GmToolkit.Core.Systems.Formula;

namespace GmToolkit.UI.ViewModels.Stats;

/// <summary>
/// The schema-driven replacement for a form's freeform stat-row editor (issues #89/#90) -- built from
/// one <see cref="CharacterSystem"/> scope's field set (<see cref="CharacterSystem.PcFields"/> or
/// <see cref="CharacterSystem.NpcFields"/>) plus a character/NPC's current Stats bag.
/// <see cref="CharacterFormViewModel"/>/<see cref="NpcFormViewModel"/> construct one of these only
/// when the active campaign has a <see cref="Campaign.CharacterSystemId"/> that resolves to a system
/// with a non-empty field set for that scope; otherwise they keep today's freeform
/// <see cref="StatRowViewModel"/> editor entirely unchanged (see those classes' own remarks).
/// </summary>
/// <remarks>
/// <para>
/// <b>Relies on every <see cref="CharacterSystem"/> reachable through
/// <see cref="ICharacterSystemRegistry"/> already having passed <see cref="CharacterSystemLoader.Validate"/>'s
/// dependency-graph check</b> (either at embedded-pack load time, or, for
/// <see cref="GenericCharacterSystem.Instance"/>, explicitly in <see cref="CharacterSystemRegistry.FromEmbeddedSystems"/>).
/// That's what makes calling <see cref="DerivedFieldGraph.Build"/> again here safe to do
/// unconditionally in the constructor rather than needing its own defensive catch: a cycle or
/// over-deep chain would already have kept that system out of the registry in the first place.
/// </para>
/// <para>
/// <b>Stats outside this schema are preserved, not dropped.</b> <see cref="BuildStats"/> starts from
/// the caller's existing Stats bag and only overwrites the keys this schema actually defines --
/// leftover keys from a since-switched-away-from system, or hand-added ones, round-trip through a
/// save unchanged. This is deliberately different from the freeform editor's own save path (which
/// rebuilds the whole bag from its row list), since a schema-attached campaign's stats aren't wholly
/// owned by this form the way a freeform campaign's are.
/// </para>
/// </remarks>
public sealed partial class SchemaStatsFormViewModel : ObservableObject
{
    private readonly IReadOnlyList<StatFieldDefinition> _fields;
    private readonly IReadOnlyList<string> _derivedEvaluationOrder;
    private readonly Dictionary<string, DerivedStatFieldViewModel> _derivedFieldsByKey = [];

    public SchemaStatsFormViewModel(IReadOnlyList<StatFieldDefinition> fields, IReadOnlyDictionary<string, string> initialStats)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(initialStats);

        _fields = fields;
        _derivedEvaluationOrder = DerivedFieldGraph.Build(fields).EvaluationOrder;

        var built = new ObservableCollection<StatFieldViewModel>();
        foreach (var definition in fields)
        {
            var field = StatFieldViewModelFactory.Create(definition);
            field.LoadRawValue(initialStats.GetValueOrDefault(definition.Key));

            if (field is DerivedStatFieldViewModel derived)
            {
                _derivedFieldsByKey[definition.Key] = derived;
            }
            else
            {
                field.Changed += OnFieldChanged;
            }

            built.Add(field);
        }

        Fields = built;

        RecomputeDerivedFields();
        Validate();
    }

    /// <summary>Every top-level field in this scope, in schema order -- what
    /// <c>SchemaAwareStatsForm.axaml</c>'s <c>ItemsControl</c> renders one-for-one via implicit
    /// per-<see cref="StatFieldViewModel"/>-subtype <c>DataTemplate</c>s.</summary>
    public ObservableCollection<StatFieldViewModel> Fields { get; }

    /// <summary>Whether any field currently fails <see cref="StatFieldValidator"/>'s rules -- gates
    /// <c>CharacterFormViewModel.CanSave</c>/<c>NpcFormViewModel.CanSave</c> the same way
    /// <c>StatsError</c> gates the freeform editor's own save button.</summary>
    [ObservableProperty]
    public partial bool HasErrors { get; set; }

    /// <summary>Raised whenever any field anywhere in this form changes (including inside a
    /// <see cref="RepeatingGroupStatFieldViewModel"/>'s rows) -- the owning form view model
    /// subscribes to refresh its own dirty-check/<c>CanSave</c> state, mirroring
    /// <c>CharacterFormViewModel.OnStatRowChanged</c>'s identical role for the freeform editor.</summary>
    public event Action? Changed;

    /// <summary>
    /// Re-validates every field (leaf rules via <see cref="StatFieldValidator"/>, repeating-group row
    /// counts and per-row cells recursively) and updates <see cref="HasErrors"/>. Called once at
    /// construction and again after every <see cref="Changed"/>-raising edit; also safe to call
    /// directly before save, mirroring <c>CharacterFormViewModel.SaveAsync</c>'s own
    /// belt-and-suspenders re-validation immediately before persisting.
    /// </summary>
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

        HasErrors = !allValid;
        return allValid;
    }

    /// <summary>
    /// Merges this form's current field values into <paramref name="existingStats"/>: every
    /// schema-defined, non-derived top-level field's <see cref="StatFieldViewModel.RawValue"/>
    /// overwrites (or adds) its own key; every other key already present in
    /// <paramref name="existingStats"/> is copied through untouched -- see this class's remarks.
    /// <see cref="DerivedStatFieldViewModel"/> fields are always skipped (SYSTEMS.md: never
    /// persisted).
    /// </summary>
    public Dictionary<string, string> BuildStats(IReadOnlyDictionary<string, string> existingStats)
    {
        ArgumentNullException.ThrowIfNull(existingStats);

        var result = new Dictionary<string, string>(existingStats, StringComparer.Ordinal);
        foreach (var field in Fields)
        {
            if (field is DerivedStatFieldViewModel)
            {
                continue;
            }

            result[field.Key] = field.RawValue;
        }

        return result;
    }

    /// <summary>
    /// An opaque, order-stable fingerprint of every non-derived field's current
    /// <see cref="StatFieldViewModel.RawValue"/> -- used only to detect whether this form's edits
    /// differ from a saved baseline, exactly mirroring
    /// <c>CharacterFormViewModel.ComputeStatsSnapshot</c>'s identical role for the freeform editor
    /// (same separators, same "not meant to be human-readable" caveat). Sorted by key (unlike the
    /// freeform snapshot, which preserves row order) since <see cref="Fields"/>' order is fixed by
    /// the schema and never reordered by the GM the way freeform rows can be.
    /// </summary>
    public string ComputeSnapshot() =>
        string.Join(
            '␟',
            Fields.Where(field => field is not DerivedStatFieldViewModel)
                .OrderBy(field => field.Key, StringComparer.Ordinal)
                .Select(field => $"{field.Key}␞{field.RawValue}"));

    private void OnFieldChanged()
    {
        RecomputeDerivedFields();
        Validate();
        Changed?.Invoke();
    }

    /// <summary>
    /// Recomputes every <c>derived</c> field's <see cref="DerivedStatFieldViewModel.DisplayValue"/>
    /// live from the form's current input values -- issue #89's "derived fields recompute live as
    /// their inputs change ... rather than only on save." Uses the same memoized, single-pass
    /// <see cref="DerivedFieldEvaluator"/> the app's other derived-field consumers use, fed this
    /// form's own in-progress (not-yet-saved) values rather than whatever's currently persisted.
    /// </summary>
    private void RecomputeDerivedFields()
    {
        if (_derivedFieldsByKey.Count == 0)
        {
            return;
        }

        var rawValues = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in Fields)
        {
            if (field is DerivedStatFieldViewModel)
            {
                continue;
            }

            rawValues[field.Key] = field.RawValue;
        }

        var results = DerivedFieldEvaluator.EvaluateAll(_fields, _derivedEvaluationOrder, rawValues);
        foreach (var (key, value) in results)
        {
            if (_derivedFieldsByKey.TryGetValue(key, out var derivedField))
            {
                derivedField.SetComputedValue(value);
            }
        }
    }
}