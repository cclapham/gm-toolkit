using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;
using GmToolkit.UI.Design;

namespace GmToolkit.UI.ViewModels;

/// <summary>
/// Shared create/edit form for a <see cref="PlayerCharacter"/> (issue #21). A single instance is
/// reused by <see cref="CharactersViewModel"/> for both modes -- call <see cref="BeginCreate"/> or
/// <see cref="BeginEdit"/> to (re)initialize it before showing it. Mirrors
/// <see cref="CampaignFormViewModel"/>'s shape and idioms throughout; see that class's remarks for
/// background on why validation reuses the domain model's own setter and why create/edit is a
/// shared in-place instance rather than two separate view models.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stats are edited as a dynamic list of <see cref="StatRowViewModel"/> rows
/// (<see cref="StatRows"/>), not directly as a <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>.</b>
/// <see cref="PlayerCharacter.Stats"/> is a system-agnostic, open-ended key/value bag (D&amp;D's
/// STR/DEX/HP, Call of Cthulhu's SAN/Idea/Luck, or whatever a homebrew system needs -- see that
/// property's own doc comment), and Avalonia has nothing that edits a
/// <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>'s entries directly with
/// two-way binding, so an <see cref="ObservableCollection{T}"/> of small, independently-bindable
/// row view models is the natural fit. <see cref="AddStatRowCommand"/>/<see cref="RemoveStatRowCommand"/>
/// mutate <see cref="StatRows"/> in place (append/remove) rather than ever replacing the collection
/// instance itself, which keeps the per-row <see cref="INotifyPropertyChanged.PropertyChanged"/>
/// subscriptions <see cref="UpdateStatsValidation"/> depends on simple to wire up and tear down.
/// </para>
/// <para>
/// <b>Empty/duplicate stat keys are validated, not silently allowed to overwrite each other.</b>
/// <see cref="UpdateStatsValidation"/> runs after every row edit/add/remove and rejects (a) a row
/// with a value but no key, and (b) two rows sharing the same trimmed key (ordinal comparison,
/// matching <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>'s own default key
/// comparer) -- either would otherwise mean one of the GM's stats silently vanishes the moment
/// <see cref="BuildStats"/> assembles the final dictionary on save. A row that's entirely blank
/// (both key and value empty -- the state a freshly-added row starts in) is not an error; it's
/// simply skipped by <see cref="BuildStats"/>, so clicking "+ Add stat" and then not filling it in
/// doesn't block saving.
/// </para>
/// <para>
/// <b>Delete lives on this form, not on <see cref="CharactersViewModel"/>'s roster rows</b> --
/// unlike <see cref="CampaignsViewModel"/>'s roster-row delete (issue #19), issue #21 scopes
/// "Delete with confirmation" to the create/edit form itself, so <see cref="RequestDeleteCommand"/>/
/// <see cref="CancelDeleteCommand"/>/<see cref="ConfirmDeleteCommand"/>/<see cref="CanConfirmDelete"/>
/// live here and mirror <see cref="CampaignsViewModel"/>'s identically-named state machine
/// one-for-one (including the type-the-name-to-confirm gate and the defense-in-depth re-check at
/// the top of <see cref="ConfirmDeleteAsync"/>'s body, since <see cref="IAsyncRelayCommand.ExecuteAsync"/>
/// does not itself call <see cref="System.Windows.Input.ICommand.CanExecute"/>). Only ever
/// reachable in edit mode (<see cref="IsEditMode"/>) -- there is nothing to delete yet in create
/// mode.
/// </para>
/// </remarks>
public sealed partial class CharacterFormViewModel : ObservableValidator
{
    private readonly IPlayerCharacterRepository _playerCharacterRepository;

    private PlayerCharacter? _editingCharacter;
    private Guid _campaignId;

    private string _originalCharacterName = string.Empty;
    private string _originalPlayerName = string.Empty;
    private string _originalAncestry = string.Empty;
    private string _originalClass = string.Empty;
    private decimal? _originalLevel = 1;
    private string _originalNotes = string.Empty;
    private string _originalStatsSnapshot = string.Empty;

    private bool _canSave;
    private bool _canConfirmDelete;

    public CharacterFormViewModel(IPlayerCharacterRepository playerCharacterRepository)
    {
        _playerCharacterRepository = playerCharacterRepository;
        ErrorsChanged += OnErrorsChanged;
    }

    /// <summary>Design-time-only constructor for the XAML previewer's <c>Design.DataContext</c>
    /// (see <c>CharacterFormView.axaml</c>) -- mirrors <see cref="CampaignFormViewModel"/>'s own
    /// design-time constructor. Never used at runtime.</summary>
    public CharacterFormViewModel()
        : this(new DesignTimePlayerCharacterRepository())
    {
    }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(CharacterFormViewModel), nameof(ValidateCharacterName))]
    public partial string CharacterName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PlayerName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Ancestry { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Class { get; set; } = string.Empty;

    /// <summary>
    /// Nullable <see cref="decimal"/> (rather than the domain model's plain <see cref="int"/>) to
    /// bind directly to Avalonia's <c>NumericUpDown.Value</c> without a converter -- see
    /// <see cref="BuildCharacterLevel"/> for the fallback applied when saving.
    /// </summary>
    [ObservableProperty]
    public partial decimal? Level { get; set; } = 1;

    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

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

    /// <summary>Dynamic key/value stat rows (issue #21) -- see this class's remarks. Mutated in
    /// place by <see cref="AddStatRowCommand"/>/<see cref="RemoveStatRowCommand"/> and by
    /// <see cref="BeginCreate"/>/<see cref="BeginEdit"/>; never reassigned to a new instance.</summary>
    public ObservableCollection<StatRowViewModel> StatRows { get; } = [];

    /// <summary>First validation message for <see cref="CharacterName"/>, or <c>null</c> if it's
    /// currently valid -- mirrors <see cref="CampaignFormViewModel.NameError"/>.</summary>
    [ObservableProperty]
    public partial string? NameError { get; set; }

    /// <summary>Aggregate validation message covering every row in <see cref="StatRows"/> (empty
    /// key with a value, or a duplicate key), or <c>null</c> if they're all currently valid -- see
    /// this class's remarks. Kept as a single message (not per-row) to match this form's overall
    /// level of validation granularity elsewhere (<see cref="NameError"/>, <see cref="SaveError"/>).</summary>
    [ObservableProperty]
    public partial string? StatsError { get; set; }

    /// <summary>Set if <see cref="SaveAsync"/>'s defensive catch is ever actually hit; <c>null</c>
    /// otherwise. Mirrors <see cref="CampaignFormViewModel.SaveError"/>.</summary>
    [ObservableProperty]
    public partial string? SaveError { get; set; }

    /// <summary>What the user has typed into the delete-confirmation panel, compared against
    /// <see cref="CharacterName"/> in <see cref="UpdateCanConfirmDelete"/>. Mirrors
    /// <see cref="CampaignsViewModel.DeleteConfirmationInput"/>.</summary>
    [ObservableProperty]
    public partial string DeleteConfirmationInput { get; set; } = string.Empty;

    /// <summary>Set if <see cref="ConfirmDeleteAsync"/>'s repository call throws; <c>null</c>
    /// otherwise. Mirrors <see cref="CampaignsViewModel.DeleteError"/>.</summary>
    [ObservableProperty]
    public partial string? DeleteError { get; set; }

    public string FormTitle => IsEditMode ? "Edit Character" : "Create Character";

    public string SaveButtonLabel => IsEditMode ? "Save" : "Create";

    /// <summary>Whether the editable fields (as opposed to the discard- or delete-confirmation
    /// prompts) should be shown -- mirrors <see cref="CampaignFormViewModel.IsFieldsVisible"/>,
    /// extended to also hide behind <see cref="IsShowingDeleteConfirmation"/>.</summary>
    public bool IsFieldsVisible => !IsShowingDiscardConfirmation && !IsShowingDeleteConfirmation;

    /// <summary>Whether the "Delete character" trigger should be shown at all -- only in edit mode,
    /// since a not-yet-saved character has nothing to delete.</summary>
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
    /// <see cref="CampaignsViewModel.CanConfirmDelete"/>'s remarks on why type-to-confirm was
    /// chosen and why this is a recomputed property rather than a plain getter.</summary>
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

    /// <summary>Raised after a successful save, with the persisted character.</summary>
    public event Func<PlayerCharacter, Task>? Saved;

    /// <summary>Raised when the user discards their changes (via <see cref="CancelCommand"/> with
    /// nothing unsaved, or via <see cref="ConfirmDiscardCommand"/>).</summary>
    public event Action? Cancelled;

    /// <summary>Raised after a successful delete (via <see cref="ConfirmDeleteCommand"/>).</summary>
    public event Func<Task>? Deleted;

    /// <summary>Resets the form to create a brand-new character in <paramref name="campaignId"/>.</summary>
    public void BeginCreate(Guid campaignId)
    {
        _editingCharacter = null;
        _campaignId = campaignId;
        IsEditMode = false;
        SetFields(string.Empty, string.Empty, string.Empty, string.Empty, 1, string.Empty, new Dictionary<string, string>());
    }

    /// <summary>Resets the form to edit <paramref name="character"/> in place.</summary>
    public void BeginEdit(PlayerCharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);

        _editingCharacter = character;
        _campaignId = character.CampaignId;
        IsEditMode = true;
        SetFields(character.CharacterName, character.PlayerName, character.Ancestry, character.Class, character.Level, character.Notes, character.Stats);
    }

    [RelayCommand]
    private void AddStatRow()
    {
        var row = new StatRowViewModel(string.Empty, string.Empty);
        row.PropertyChanged += OnStatRowChanged;
        StatRows.Add(row);
        UpdateStatsValidation();
    }

    [RelayCommand]
    private void RemoveStatRow(StatRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        row.PropertyChanged -= OnStatRowChanged;
        StatRows.Remove(row);
        UpdateStatsValidation();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        ValidateAllProperties();
        UpdateStatsValidation();
        if (HasErrors || StatsError is not null)
        {
            return;
        }

        SaveError = null;
        var stats = BuildStats();

        try
        {
            if (_editingCharacter is null)
            {
                var character = new PlayerCharacter
                {
                    CampaignId = _campaignId,
                    CharacterName = CharacterName,
                    PlayerName = PlayerName,
                    Ancestry = Ancestry,
                    Class = Class,
                    Level = BuildCharacterLevel(),
                    Notes = Notes,
                };
                ApplyStats(character, stats);
                await _playerCharacterRepository.AddAsync(character);
                _editingCharacter = character;
            }
            else
            {
                _editingCharacter.CharacterName = CharacterName;
                _editingCharacter.PlayerName = PlayerName;
                _editingCharacter.Ancestry = Ancestry;
                _editingCharacter.Class = Class;
                _editingCharacter.Level = BuildCharacterLevel();
                _editingCharacter.Notes = Notes;
                ApplyStats(_editingCharacter, stats);
                await _playerCharacterRepository.UpdateAsync(_editingCharacter);
            }

            SetBaseline();

            if (Saved is not null)
            {
                await Saved.Invoke(_editingCharacter);
            }
        }
        catch (ArgumentException ex)
        {
            // Defensive only -- ValidateCharacterName above already exercises this same domain
            // setter, so this should be unreachable in practice. See CampaignFormViewModel's
            // identical remark.
            SaveError = ex.Message;
        }
        catch (Exception ex)
        {
            // A real repository failure (issue #32) -- see CampaignFormViewModel.SaveAsync's
            // identical catch and GlobalExceptionHandler's remarks.
            SaveError = $"Couldn't save this character: {ex.Message}";
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
    /// <see cref="CampaignsViewModel.ConfirmDeleteAsync"/>'s identical defense-in-depth, since
    /// <see cref="IAsyncRelayCommand.ExecuteAsync"/> does not itself call
    /// <see cref="System.Windows.Input.ICommand.CanExecute"/> first.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanConfirmDelete))]
    private async Task ConfirmDeleteAsync()
    {
        if (!CanConfirmDelete || _editingCharacter is null)
        {
            return;
        }

        DeleteError = null;

        try
        {
            await _playerCharacterRepository.DeleteAsync(_editingCharacter.Id);

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
            // they don't have to retype the character name to try again.
            DeleteError = $"Couldn't delete this character: {ex.Message}";
        }
    }

    /// <summary>
    /// Reused by <see cref="CharacterName"/>'s <see cref="CustomValidationAttribute"/> -- validates
    /// by actually going through <see cref="PlayerCharacter.CharacterName"/>'s own setter and
    /// surfacing whatever message its <see cref="ArgumentException"/> carries, exactly mirroring
    /// <see cref="CampaignFormViewModel.ValidateName"/> for the same reason (one place -- the
    /// domain model's setter -- owns what makes a name valid).
    /// </summary>
    public static ValidationResult? ValidateCharacterName(string characterName, ValidationContext context)
    {
        try
        {
            _ = new PlayerCharacter { CharacterName = characterName };
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

    private void OnStatRowChanged(object? sender, PropertyChangedEventArgs e) => UpdateStatsValidation();

    private void OnErrorsChanged(object? sender, DataErrorsChangedEventArgs e) => RefreshValidationState();

    /// <summary>
    /// Recomputes <see cref="StatsError"/> from the current contents of <see cref="StatRows"/> --
    /// see this class's remarks for the exact rules (blank rows ignored, a value without a key is
    /// an error, duplicate trimmed keys are an error).
    /// </summary>
    private void UpdateStatsValidation()
    {
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        string? error = null;

        foreach (var row in StatRows)
        {
            var key = row.Key.Trim();
            var value = row.Value.Trim();

            if (key.Length == 0 && value.Length == 0)
            {
                continue;
            }

            if (key.Length == 0)
            {
                error ??= "Every stat needs a key.";
                continue;
            }

            if (!seenKeys.Add(key))
            {
                error ??= $"Stat keys must be unique (duplicate: \"{key}\").";
            }
        }

        StatsError = error;
        RefreshValidationState();
    }

    /// <summary>Assembles the final key/value pairs to persist from <see cref="StatRows"/>,
    /// trimming both key and value and skipping entirely-blank rows. Only ever called once
    /// <see cref="StatsError"/> is confirmed <c>null</c> (see <see cref="SaveAsync"/>), so it does
    /// not need to re-guard against empty/duplicate keys itself.</summary>
    private Dictionary<string, string> BuildStats()
    {
        var stats = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var row in StatRows)
        {
            var key = row.Key.Trim();
            var value = row.Value.Trim();

            if (key.Length == 0 && value.Length == 0)
            {
                continue;
            }

            stats[key] = value;
        }

        return stats;
    }

    private static void ApplyStats(PlayerCharacter character, Dictionary<string, string> stats)
    {
        character.Stats.Clear();
        foreach (var stat in stats)
        {
            character.Stats[stat.Key] = stat.Value;
        }
    }

    /// <summary><see cref="Level"/> is a nullable <see cref="decimal"/> for direct
    /// <c>NumericUpDown</c> binding (see that property's doc comment); a cleared/empty input
    /// falls back to <see cref="PlayerCharacter"/>'s own default of 1 rather than blocking save --
    /// there is no domain rule requiring a specific level (unlike <see cref="CharacterName"/>),
    /// so treating an empty level field as "unspecified, use the default" is friendlier than a
    /// hard validation error. Also floors at 1 regardless of the raw value -- the bound
    /// <c>NumericUpDown</c>'s <c>Minimum="1"</c> should already prevent entering anything lower,
    /// but this keeps a cleared field (which falls back to 1 above) and an explicitly-entered 0
    /// or negative value from producing two different, both-nonsensical results.</summary>
    private int BuildCharacterLevel() => Math.Max(1, (int)(Level ?? 1));

    private bool IsDirty() =>
        CharacterName != _originalCharacterName ||
        PlayerName != _originalPlayerName ||
        Ancestry != _originalAncestry ||
        Class != _originalClass ||
        Level != _originalLevel ||
        Notes != _originalNotes ||
        ComputeStatsSnapshot() != _originalStatsSnapshot;

    /// <summary>Opaque, order-sensitive fingerprint of <see cref="StatRows"/>'s current (trimmed)
    /// contents, used only to detect whether stats have changed since the last
    /// <see cref="SetBaseline"/> -- not meant to be human-readable, just collision-resistant enough
    /// for this form's own key/value text. The separators are Unicode "record/group separator"
    /// control pictures, chosen so an ordinary stat key or value could never accidentally produce
    /// the same fingerprint as a different set of rows. Skips entirely-blank rows (both key and
    /// value empty) so clicking "+ Add stat" and leaving it empty doesn't mark the form dirty --
    /// matches <see cref="BuildStats"/>'s identical treatment of the same rows on save, so what
    /// counts as "a change" agrees between the discard guard and the actual persisted data.</summary>
    private string ComputeStatsSnapshot() =>
        string.Join('␟', StatRows
            .Where(row => row.Key.Trim().Length > 0 || row.Value.Trim().Length > 0)
            .Select(row => $"{row.Key.Trim()}␞{row.Value.Trim()}"));

    private void SetFields(string characterName, string playerName, string ancestry, string @class, int level, string notes, IReadOnlyDictionary<string, string> stats)
    {
        CharacterName = characterName;
        PlayerName = playerName;
        Ancestry = ancestry;
        Class = @class;
        Level = level;
        Notes = notes;

        foreach (var row in StatRows)
        {
            row.PropertyChanged -= OnStatRowChanged;
        }

        StatRows.Clear();
        foreach (var stat in stats)
        {
            var row = new StatRowViewModel(stat.Key, stat.Value);
            row.PropertyChanged += OnStatRowChanged;
            StatRows.Add(row);
        }

        IsShowingDiscardConfirmation = false;
        IsShowingDeleteConfirmation = false;
        DeleteConfirmationInput = string.Empty;
        SaveError = null;
        DeleteError = null;

        ClearErrors();
        ValidateAllProperties();

        // ValidateAllProperties() above only raises ErrorsChanged for a property whose error
        // *list* actually changed (see CampaignFormViewModel.SetFields' identical comment).
        // UpdateStatsValidation also calls RefreshValidationState, so this covers both.
        UpdateStatsValidation();

        SetBaseline();
    }

    private void SetBaseline()
    {
        _originalCharacterName = CharacterName;
        _originalPlayerName = PlayerName;
        _originalAncestry = Ancestry;
        _originalClass = Class;
        _originalLevel = Level;
        _originalNotes = Notes;
        _originalStatsSnapshot = ComputeStatsSnapshot();
    }

    private void RefreshValidationState()
    {
        NameError = GetErrors(nameof(CharacterName))
            .OfType<ValidationResult>()
            .Select(result => result.ErrorMessage)
            .FirstOrDefault();

        CanSave = !HasErrors && StatsError is null;
    }

    private void UpdateCanConfirmDelete() =>
        CanConfirmDelete = IsEditMode && string.Equals(DeleteConfirmationInput, CharacterName, StringComparison.Ordinal);

    partial void OnDeleteConfirmationInputChanged(string value) => UpdateCanConfirmDelete();
}