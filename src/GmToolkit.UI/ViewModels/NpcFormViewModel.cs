using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;
using GmToolkit.Core.Systems;
using GmToolkit.UI.Design;
using GmToolkit.UI.ViewModels.Stats;

namespace GmToolkit.UI.ViewModels;

/// <summary>
/// Shared create/edit form for an <see cref="Npc"/> (issue #25). A single instance is reused by
/// <see cref="NpcsViewModel"/> for both modes -- call <see cref="BeginCreate"/> or
/// <see cref="BeginEdit"/> to (re)initialize it before showing it. Mirrors
/// <see cref="CharacterFormViewModel"/>'s shape and idioms throughout (validation via the domain
/// model's own setter, the unsaved-changes discard guard, and delete via type-the-name-to-confirm
/// with the defense-in-depth re-check in <see cref="ConfirmDeleteAsync"/>); see that class's remarks
/// for background this class doesn't repeat below.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Npc.WasGenerated"/> is never surfaced here, and always stays at its domain default
/// of <c>false</c> for anything created by this form.</b> This form only ever produces hand-created
/// NPCs (a not-yet-built generator, issue #29, is the only other place an <see cref="Npc"/> gets
/// created) and every other field this class edits is otherwise identical in shape whether an NPC
/// was hand-typed or generated -- that's what makes issue #25's "indistinguishable afterwards"
/// acceptance criterion hold trivially. <see cref="SaveAsync"/> never assigns
/// <see cref="Npc.WasGenerated"/> in either create or edit mode, so editing a generated NPC through
/// this form (once the generator exists) leaves its flag untouched rather than resetting it.
/// </para>
/// <para>
/// <b>Faction/Location suggestions (<see cref="FactionSuggestions"/>/<see cref="LocationSuggestions"/>)
/// are free-text autocomplete hints, not a constrained set of options.</b> Unlike
/// <see cref="NpcsViewModel.FactionOptions"/>/<see cref="NpcsViewModel.LocationOptions"/> (dropdown
/// filters that must only ever contain values that already exist, plus "All"), these are suggestions
/// for Avalonia's <c>AutoCompleteBox</c> (see <c>NpcFormView.axaml</c>) -- a GM can always type a
/// brand-new faction or location that isn't in the list yet; nothing here validates or constrains
/// <see cref="Faction"/>/<see cref="Location"/> beyond what <see cref="Npc"/> itself already allows
/// (plain optional strings, no domain-level validation). Built once per <see cref="BeginCreate"/>/
/// <see cref="BeginEdit"/> call from every NPC currently in the target campaign (via
/// <see cref="INpcRepository.GetByCampaignAsync"/>), same "distinct, non-blank, alphabetical" spirit
/// as <see cref="NpcsViewModel.RebuildFilterOptions"/>'s <c>DistinctNonBlank</c>, but with no
/// "All"-equivalent sentinel since there's no such thing as "no suggestion" to prepend.
/// </para>
/// <para>
/// <b><see cref="HasSchema"/>/<see cref="SchemaForm"/> add a schema-driven stat block (issue #90),
/// shown only when the active campaign's system resolves to a non-empty
/// <see cref="CharacterSystem.NpcFields"/> set.</b> Unlike <see cref="CharacterFormViewModel"/>'s
/// freeform <c>StatRows</c> fallback, this form has never had *any* general-purpose stats editor for
/// <see cref="Npc.Stats"/> -- issue #90's own acceptance criterion is "falls back to today's behavior
/// (no stats section at all)," so <see cref="HasSchema"/> <c>false</c> simply means no stats section
/// renders at all, not a freeform one. Mirrors <see cref="CharacterFormViewModel"/>'s
/// <see cref="ICharacterSystemRegistry.TryGetById"/> resolution and <see cref="SystemMissingWarning"/>
/// handling one-for-one otherwise.
/// </para>
/// </remarks>
public sealed partial class NpcFormViewModel : ObservableValidator
{
    private readonly INpcRepository _npcRepository;
    private readonly ICharacterSystemRegistry _characterSystemRegistry;

    private Npc? _editingNpc;
    private Guid _campaignId;

    private string _originalName = string.Empty;
    private string _originalRole = string.Empty;
    private string _originalFaction = string.Empty;
    private string _originalLocation = string.Empty;
    private string _originalAppearance = string.Empty;
    private string _originalMannerism = string.Empty;
    private string _originalMotivation = string.Empty;
    private string _originalSecret = string.Empty;
    private string _originalNotes = string.Empty;
    private bool _originalKnownToPlayers;
    private string _originalSchemaStatsSnapshot = string.Empty;

    private bool _canSave;
    private bool _canConfirmDelete;

    /// <param name="characterSystemRegistry">See <see cref="CharacterFormViewModel"/>'s identical
    /// parameter -- same optional-with-embedded-default reasoning, for the same backward-compatibility
    /// reason.</param>
    public NpcFormViewModel(INpcRepository npcRepository, ICharacterSystemRegistry? characterSystemRegistry = null)
    {
        _npcRepository = npcRepository;
        _characterSystemRegistry = characterSystemRegistry ?? CharacterSystemRegistry.FromEmbeddedSystems();
        ErrorsChanged += OnErrorsChanged;
    }

    /// <summary>Design-time-only constructor for the XAML previewer's <c>Design.DataContext</c>
    /// (see <c>NpcFormView.axaml</c>) -- mirrors <see cref="CharacterFormViewModel"/>'s own
    /// design-time constructor. Never used at runtime.</summary>
    public NpcFormViewModel()
        : this(new DesignTimeNpcRepository())
    {
    }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(NpcFormViewModel), nameof(ValidateName))]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Role { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Faction { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Location { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Appearance { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Mannerism { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Motivation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Secret { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool KnownToPlayers { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    [NotifyPropertyChangedFor(nameof(SaveButtonLabel))]
    [NotifyPropertyChangedFor(nameof(IsDeleteAvailable))]
    public partial bool IsEditMode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFieldsVisible))]
    public partial bool IsShowingDiscardConfirmation { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFieldsVisible))]
    public partial bool IsShowingDeleteConfirmation { get; set; }

    /// <summary>Distinct, non-blank <see cref="Npc.Faction"/> values already present across the
    /// target campaign's NPCs -- see this class's remarks. Rebuilt by <see cref="BeginCreate"/>/
    /// <see cref="BeginEdit"/> via <see cref="LoadSuggestionsAsync"/>.</summary>
    [ObservableProperty]
    public partial ObservableCollection<string> FactionSuggestions { get; set; } = [];

    /// <summary>Distinct, non-blank <see cref="Npc.Location"/> values already present across the
    /// target campaign's NPCs -- see this class's remarks. Rebuilt by <see cref="BeginCreate"/>/
    /// <see cref="BeginEdit"/> via <see cref="LoadSuggestionsAsync"/>.</summary>
    [ObservableProperty]
    public partial ObservableCollection<string> LocationSuggestions { get; set; } = [];

    /// <summary>Whether the active campaign's system (issue #90) supplies a non-empty NPC field set --
    /// see this class's remarks. When <c>true</c>, <c>NpcFormView.axaml</c> renders
    /// <see cref="SchemaForm"/>'s stat block; when <c>false</c>, no stats section shows at all.</summary>
    [ObservableProperty]
    public partial bool HasSchema { get; set; }

    /// <summary>The schema-driven stats form (issue #90) built from the resolved
    /// <see cref="CharacterSystem.NpcFields"/> plus this NPC's current <c>Stats</c>, or <c>null</c>
    /// when <see cref="HasSchema"/> is <c>false</c>.</summary>
    [ObservableProperty]
    public partial SchemaStatsFormViewModel? SchemaForm { get; set; }

    /// <summary>Mirrors <see cref="CharacterFormViewModel.SystemMissingWarning"/> -- set when the
    /// active campaign's <see cref="Campaign.CharacterSystemId"/> no longer resolves via
    /// <see cref="ICharacterSystemRegistry.TryGetById"/>.</summary>
    [ObservableProperty]
    public partial string? SystemMissingWarning { get; set; }

    /// <summary>First validation message for <see cref="Name"/>, or <c>null</c> if it's currently
    /// valid -- mirrors <see cref="CharacterFormViewModel.NameError"/>.</summary>
    [ObservableProperty]
    public partial string? NameError { get; set; }

    /// <summary>Set if <see cref="SaveAsync"/>'s defensive catch is ever actually hit; <c>null</c>
    /// otherwise. Mirrors <see cref="CharacterFormViewModel.SaveError"/>.</summary>
    [ObservableProperty]
    public partial string? SaveError { get; set; }

    /// <summary>What the user has typed into the delete-confirmation panel, compared against
    /// <see cref="Name"/> in <see cref="UpdateCanConfirmDelete"/>. Mirrors
    /// <see cref="CharacterFormViewModel.DeleteConfirmationInput"/>.</summary>
    [ObservableProperty]
    public partial string DeleteConfirmationInput { get; set; } = string.Empty;

    /// <summary>Set if <see cref="ConfirmDeleteAsync"/>'s repository call throws; <c>null</c>
    /// otherwise. Mirrors <see cref="CharacterFormViewModel.DeleteError"/>.</summary>
    [ObservableProperty]
    public partial string? DeleteError { get; set; }

    public string FormTitle => IsEditMode ? "Edit NPC" : "Create NPC";

    public string SaveButtonLabel => IsEditMode ? "Save" : "Create";

    /// <summary>Whether the editable fields (as opposed to the discard- or delete-confirmation
    /// prompts) should be shown -- mirrors <see cref="CharacterFormViewModel.IsFieldsVisible"/>.</summary>
    public bool IsFieldsVisible => !IsShowingDiscardConfirmation && !IsShowingDeleteConfirmation;

    /// <summary>Whether the "Delete NPC" trigger should be shown at all -- only in edit mode, since
    /// a not-yet-saved NPC has nothing to delete.</summary>
    public bool IsDeleteAvailable => IsEditMode;

    public bool CanSave
    {
        get => _canSave;
        private set
        {
            if (SetProperty(ref _canSave, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Type-to-confirm gate for <see cref="ConfirmDeleteCommand"/> -- mirrors
    /// <see cref="CharacterFormViewModel.CanConfirmDelete"/>'s remarks.</summary>
    public bool CanConfirmDelete
    {
        get => _canConfirmDelete;
        private set
        {
            if (SetProperty(ref _canConfirmDelete, value))
            {
                ConfirmDeleteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Raised after a successful save, with the persisted NPC.</summary>
    public event Func<Npc, Task>? Saved;

    /// <summary>Raised when the user discards their changes (via <see cref="CancelCommand"/> with
    /// nothing unsaved, or via <see cref="ConfirmDiscardCommand"/>).</summary>
    public event Action? Cancelled;

    /// <summary>Raised after a successful delete (via <see cref="ConfirmDeleteCommand"/>).</summary>
    public event Func<Task>? Deleted;

    /// <summary>Resets the form to create a brand-new NPC in <paramref name="campaignId"/>.</summary>
    /// <param name="characterSystemId">The active campaign's <see cref="Campaign.CharacterSystemId"/>
    /// (issue #90), or <c>null</c> for a freeform campaign -- defaults to <c>null</c> so every caller
    /// that predates issue #90 keeps compiling unchanged and gets exactly today's no-stats-section
    /// behavior.</param>
    public void BeginCreate(Guid campaignId, string? characterSystemId = null)
    {
        _editingNpc = null;
        _campaignId = campaignId;
        IsEditMode = false;
        SetFields(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, knownToPlayers: false, new Dictionary<string, string>(), characterSystemId);
        _ = LoadSuggestionsAsync(campaignId);
    }

    /// <summary>Resets the form to edit <paramref name="npc"/> in place.</summary>
    /// <param name="characterSystemId">See <see cref="BeginCreate"/>.</param>
    public void BeginEdit(Npc npc, string? characterSystemId = null)
    {
        ArgumentNullException.ThrowIfNull(npc);

        _editingNpc = npc;
        _campaignId = npc.CampaignId;
        IsEditMode = true;
        SetFields(npc.Name, npc.Role, npc.Faction, npc.Location, npc.Appearance, npc.Mannerism, npc.Motivation, npc.Secret, npc.Notes, npc.KnownToPlayers, npc.Stats, characterSystemId);
        _ = LoadSuggestionsAsync(npc.CampaignId);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        ValidateAllProperties();
        if (HasSchema)
        {
            SchemaForm?.Validate();
        }

        if (HasErrors || (HasSchema && SchemaForm?.HasErrors == true))
        {
            return;
        }

        SaveError = null;

        // Only ever touches Npc.Stats at all when a schema is attached (issue #90) -- with no
        // schema, this form still has no general-purpose stats editor of any kind (see this class's
        // remarks), so an existing NPC's Stats must round-trip through a save completely untouched,
        // exactly as it always has. SchemaForm.BuildStats always returns a fresh Dictionary instance
        // (never _editingNpc.Stats itself), so there's no risk of clearing-while-aliased below.
        var stats = HasSchema && SchemaForm is not null
            ? SchemaForm.BuildStats(_editingNpc?.Stats ?? new Dictionary<string, string>())
            : null;

        try
        {
            if (_editingNpc is null)
            {
                var npc = new Npc
                {
                    CampaignId = _campaignId,
                    Name = Name,
                    Role = Role,
                    Faction = Faction,
                    Location = Location,
                    Appearance = Appearance,
                    Mannerism = Mannerism,
                    Motivation = Motivation,
                    Secret = Secret,
                    Notes = Notes,
                    KnownToPlayers = KnownToPlayers,
                };
                if (stats is not null)
                {
                    ApplyStats(npc, stats);
                }

                await _npcRepository.AddAsync(npc);
                _editingNpc = npc;
            }
            else
            {
                _editingNpc.Name = Name;
                _editingNpc.Role = Role;
                _editingNpc.Faction = Faction;
                _editingNpc.Location = Location;
                _editingNpc.Appearance = Appearance;
                _editingNpc.Mannerism = Mannerism;
                _editingNpc.Motivation = Motivation;
                _editingNpc.Secret = Secret;
                _editingNpc.Notes = Notes;
                _editingNpc.KnownToPlayers = KnownToPlayers;
                if (stats is not null)
                {
                    ApplyStats(_editingNpc, stats);
                }

                await _npcRepository.UpdateAsync(_editingNpc);
            }

            SetBaseline();

            if (Saved is not null)
            {
                await Saved.Invoke(_editingNpc);
            }
        }
        catch (ArgumentException ex)
        {
            // Defensive only -- ValidateName above already exercises this same domain setter, so
            // this should be unreachable in practice. See CharacterFormViewModel's identical remark.
            SaveError = ex.Message;
        }
        catch (Exception ex)
        {
            // A real repository failure (issue #32) -- see CampaignFormViewModel.SaveAsync's
            // identical catch and GlobalExceptionHandler's remarks.
            SaveError = $"Couldn't save this NPC: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsDirty())
        {
            IsShowingDiscardConfirmation = true;
            return;
        }

        Cancelled?.Invoke();
    }

    [RelayCommand]
    private void ConfirmDiscard()
    {
        IsShowingDiscardConfirmation = false;
        Cancelled?.Invoke();
    }

    [RelayCommand]
    private void CancelDiscard()
    {
        IsShowingDiscardConfirmation = false;
    }

    [RelayCommand]
    private void RequestDelete()
    {
        if (!IsEditMode)
        {
            return;
        }

        IsShowingDeleteConfirmation = true;
        DeleteConfirmationInput = string.Empty;
        DeleteError = null;
        UpdateCanConfirmDelete();
    }

    [RelayCommand]
    private void CancelDelete()
    {
        IsShowingDeleteConfirmation = false;
        DeleteConfirmationInput = string.Empty;
        DeleteError = null;
        UpdateCanConfirmDelete();
    }

    /// <remarks>
    /// Re-checks <see cref="CanConfirmDelete"/> itself as the first thing in the body -- mirrors
    /// <see cref="CharacterFormViewModel.ConfirmDeleteAsync"/>'s identical defense-in-depth, since
    /// <see cref="CommunityToolkit.Mvvm.Input.IAsyncRelayCommand.ExecuteAsync"/> does not itself call
    /// <see cref="System.Windows.Input.ICommand.CanExecute"/> first.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanConfirmDelete))]
    private async Task ConfirmDeleteAsync()
    {
        if (!CanConfirmDelete || _editingNpc is null)
        {
            return;
        }

        DeleteError = null;

        try
        {
            await _npcRepository.DeleteAsync(_editingNpc.Id);

            IsShowingDeleteConfirmation = false;
            DeleteConfirmationInput = string.Empty;
            UpdateCanConfirmDelete();

            if (Deleted is not null)
            {
                await Deleted.Invoke();
            }
        }
        catch (Exception ex)
        {
            // Leave the confirmation panel showing (with whatever the user typed still intact) so
            // they don't have to retype the NPC's name to try again.
            DeleteError = $"Couldn't delete this NPC: {ex.Message}";
        }
    }

    /// <summary>
    /// Reused by <see cref="Name"/>'s <see cref="CustomValidationAttribute"/> -- validates by
    /// actually going through <see cref="Npc.Name"/>'s own setter and surfacing whatever message its
    /// <see cref="ArgumentException"/> carries, exactly mirroring
    /// <see cref="CharacterFormViewModel.ValidateCharacterName"/> for the same reason (one place --
    /// the domain model's setter -- owns what makes a name valid).
    /// </summary>
    public static ValidationResult? ValidateName(string name, ValidationContext context)
    {
        try
        {
            _ = new Npc { Name = name };
            return ValidationResult.Success;
        }
        catch (ArgumentException ex)
        {
            return new ValidationResult(StripParameterSuffix(ex));
        }
    }

    private static string StripParameterSuffix(ArgumentException ex)
    {
        if (ex.ParamName is null)
        {
            return ex.Message;
        }

        var suffix = $" (Parameter '{ex.ParamName}')";
        return ex.Message.EndsWith(suffix, StringComparison.Ordinal)
            ? ex.Message[..^suffix.Length]
            : ex.Message;
    }

    /// <summary>
    /// Rebuilds <see cref="FactionSuggestions"/>/<see cref="LocationSuggestions"/> from every NPC
    /// currently in <paramref name="campaignId"/> -- see this class's remarks. Fire-and-forget from
    /// <see cref="BeginCreate"/>/<see cref="BeginEdit"/>; a failure here is not form-blocking (it
    /// just leaves the suggestion list empty), since autocomplete hints are a convenience, not a
    /// requirement for saving.
    /// </summary>
    /// <remarks>
    /// Guards its own result against <see cref="_campaignId"/> having moved on by the time the
    /// repository call completes (e.g. the form was reopened for a different campaign before this
    /// load finished) -- <see cref="INpcRepository"/> makes no ordering guarantee between two
    /// overlapping calls, so without this check a slow load for an old campaign could land its
    /// results after a newer, faster load for a different one, showing that campaign's faction/
    /// location names as suggestions. Caught during the skeptical review of PR #58.
    /// </remarks>
    private async Task LoadSuggestionsAsync(Guid campaignId)
    {
        try
        {
            var npcs = await _npcRepository.GetByCampaignAsync(campaignId);
            if (campaignId != _campaignId)
            {
                return;
            }

            FactionSuggestions = new ObservableCollection<string>(DistinctNonBlank(npcs.Select(npc => npc.Faction)));
            LocationSuggestions = new ObservableCollection<string>(DistinctNonBlank(npcs.Select(npc => npc.Location)));
        }
        catch
        {
            if (campaignId != _campaignId)
            {
                return;
            }

            FactionSuggestions = [];
            LocationSuggestions = [];
        }
    }

    private static IEnumerable<string> DistinctNonBlank(IEnumerable<string> values) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);

    private static void ApplyStats(Npc npc, Dictionary<string, string> stats)
    {
        npc.Stats.Clear();
        foreach (var stat in stats)
        {
            npc.Stats[stat.Key] = stat.Value;
        }
    }

    private void OnErrorsChanged(object? sender, DataErrorsChangedEventArgs e) => RefreshValidationState();

    private bool IsDirty() =>
        Name != _originalName ||
        Role != _originalRole ||
        Faction != _originalFaction ||
        Location != _originalLocation ||
        Appearance != _originalAppearance ||
        Mannerism != _originalMannerism ||
        Motivation != _originalMotivation ||
        Secret != _originalSecret ||
        Notes != _originalNotes ||
        KnownToPlayers != _originalKnownToPlayers ||
        (HasSchema && SchemaForm?.ComputeSnapshot() != _originalSchemaStatsSnapshot);

    private void SetFields(string name, string role, string faction, string location, string appearance, string mannerism, string motivation, string secret, string notes, bool knownToPlayers, IReadOnlyDictionary<string, string> stats, string? characterSystemId)
    {
        Name = name;
        Role = role;
        Faction = faction;
        Location = location;
        Appearance = appearance;
        Mannerism = mannerism;
        Motivation = motivation;
        Secret = secret;
        Notes = notes;
        KnownToPlayers = knownToPlayers;

        InitializeSchema(characterSystemId, stats);

        IsShowingDiscardConfirmation = false;
        IsShowingDeleteConfirmation = false;
        DeleteConfirmationInput = string.Empty;
        SaveError = null;
        DeleteError = null;

        ClearErrors();
        ValidateAllProperties();

        // ValidateAllProperties() above only raises ErrorsChanged for a property whose error
        // *list* actually changed -- e.g. BeginEdit-ing an already-valid NPC goes from "no errors
        // recorded yet" to "no errors", which is not a change, so OnErrorsChanged below would never
        // run and CanSave/NameError would be stuck at their previous (possibly stale) values.
        // Recompute explicitly here, mirroring CampaignFormViewModel.SetFields' identical comment.
        RefreshValidationState();

        SetBaseline();
    }

    /// <summary>Resolves <paramref name="characterSystemId"/> (issue #90) and sets
    /// <see cref="HasSchema"/>/<see cref="SchemaForm"/>/<see cref="SystemMissingWarning"/> -- mirrors
    /// <see cref="CharacterFormViewModel.InitializeSchema"/> one-for-one except that "no schema" here
    /// means no stats section at all (see this class's remarks), not a freeform fallback.</summary>
    private void InitializeSchema(string? characterSystemId, IReadOnlyDictionary<string, string> stats)
    {
        if (SchemaForm is not null)
        {
            SchemaForm.Changed -= OnSchemaFormChanged;
        }

        SchemaForm = null;
        SystemMissingWarning = null;

        if (characterSystemId is null)
        {
            HasSchema = false;
            return;
        }

        if (!_characterSystemRegistry.TryGetById(characterSystemId, out var system))
        {
            HasSchema = false;
            SystemMissingWarning =
                $"This campaign's system (\"{characterSystemId}\") isn't currently installed, so its stat block " +
                "can't be shown. No stats section will be available until a different system is attached.";
            return;
        }

        if (system.NpcFields.Count == 0)
        {
            HasSchema = false;
            return;
        }

        HasSchema = true;
        var form = new SchemaStatsFormViewModel(system.NpcFields, stats);
        form.Changed += OnSchemaFormChanged;
        SchemaForm = form;
    }

    private void OnSchemaFormChanged() => RefreshValidationState();

    private void SetBaseline()
    {
        _originalName = Name;
        _originalRole = Role;
        _originalFaction = Faction;
        _originalLocation = Location;
        _originalAppearance = Appearance;
        _originalMannerism = Mannerism;
        _originalMotivation = Motivation;
        _originalSecret = Secret;
        _originalNotes = Notes;
        _originalKnownToPlayers = KnownToPlayers;
        _originalSchemaStatsSnapshot = SchemaForm?.ComputeSnapshot() ?? string.Empty;
    }

    private void RefreshValidationState()
    {
        NameError = GetErrors(nameof(Name))
            .OfType<ValidationResult>()
            .Select(result => result.ErrorMessage)
            .FirstOrDefault();

        CanSave = !HasErrors && (!HasSchema || SchemaForm?.HasErrors != true);
    }

    private void UpdateCanConfirmDelete() =>
        CanConfirmDelete = IsEditMode && string.Equals(DeleteConfirmationInput, Name, StringComparison.Ordinal);

    partial void OnDeleteConfirmationInputChanged(string value) => UpdateCanConfirmDelete();
}