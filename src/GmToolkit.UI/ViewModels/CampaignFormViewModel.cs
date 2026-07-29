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
/// Shared create/edit form for a <see cref="Campaign"/> (issue #18). A single instance is reused
/// by <see cref="CampaignsViewModel"/> for both modes -- call <see cref="BeginCreate"/> or
/// <see cref="BeginEdit"/> to (re)initialize it before showing it.
/// </summary>
/// <remarks>
/// <para>
/// Inherits <see cref="ObservableValidator"/> directly (rather than through
/// <see cref="ViewModelBase"/>, which adds nothing beyond <see cref="ObservableObject"/> that
/// <see cref="ObservableValidator"/> doesn't already provide) to get
/// <see cref="System.ComponentModel.INotifyDataErrorInfo"/> for inline validation essentially for
/// free, per CommunityToolkit.Mvvm's usual pattern.
/// </para>
/// <para>
/// <b>Validation reuses <see cref="Campaign"/>'s own rules instead of duplicating them</b> (per
/// CONTRIBUTING.md): <see cref="ValidateName"/>, wired up via <see cref="CustomValidationAttribute"/>
/// on <see cref="Name"/>, validates by actually constructing a throwaway <see cref="Campaign"/>
/// with the candidate name and surfacing the <see cref="ArgumentException"/>'s own message if the
/// domain model's setter rejects it -- so there is exactly one place (<c>Campaign.ValidateName</c>)
/// that knows what makes a campaign name valid, and this form can never drift out of sync with it.
/// <see cref="SaveAsync"/> then applies the (already-validated) values to a real
/// <see cref="Campaign"/> via its own setters as the final, authoritative gate before persisting,
/// with a defensive catch as a last resort in case some future rule differs subtly from what
/// <see cref="ValidateName"/> checks.
/// </para>
/// </remarks>
public sealed partial class CampaignFormViewModel : ObservableValidator
{
    private readonly ICampaignRepository _campaignRepository;

    private Campaign? _editingCampaign;
    private string _originalName = string.Empty;
    private string _originalGameSystem = string.Empty;
    private string _originalDescription = string.Empty;
    private bool _canSave;

    public CampaignFormViewModel(ICampaignRepository campaignRepository)
    {
        _campaignRepository = campaignRepository;
        ErrorsChanged += OnErrorsChanged;
    }

    /// <summary>Design-time-only constructor for the XAML previewer's <c>Design.DataContext</c>
    /// (see <c>CampaignFormView.axaml</c>) -- mirrors the pattern used by
    /// <see cref="ShellViewModel"/> and <see cref="CampaignsViewModel"/>'s own design-time
    /// constructors. Never used at runtime.</summary>
    public CampaignFormViewModel()
        : this(new DesignTimeCampaignRepository())
    {
    }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(CampaignFormViewModel), nameof(ValidateName))]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GameSystem { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    [NotifyPropertyChangedFor(nameof(SaveButtonLabel))]
    public partial bool IsEditMode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFieldsVisible))]
    public partial bool IsShowingDiscardConfirmation { get; set; }

    /// <summary>First validation message for <see cref="Name"/>, or <c>null</c> if it's currently
    /// valid -- kept as its own bindable property since <see cref="ObservableValidator.GetErrors"/>
    /// isn't itself bindable from AXAML.</summary>
    [ObservableProperty]
    public partial string? NameError { get; set; }

    /// <summary>Set if <see cref="SaveAsync"/>'s defensive catch is ever actually hit -- see this
    /// class's remarks. Not expected to be reachable in normal use.</summary>
    [ObservableProperty]
    public partial string? SaveError { get; set; }

    public string FormTitle => IsEditMode ? "Edit Campaign" : "Create Campaign";

    public string SaveButtonLabel => IsEditMode ? "Save" : "Create";

    /// <summary>Whether the editable fields (as opposed to the discard-confirmation prompt) should
    /// be shown -- see <see cref="IsShowingDiscardConfirmation"/>.</summary>
    public bool IsFieldsVisible => !IsShowingDiscardConfirmation;

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

    /// <summary>Raised after a successful save, with the persisted campaign.</summary>
    public event Func<Campaign, Task>? Saved;

    /// <summary>Raised when the user discards their changes (via <see cref="CancelCommand"/> with
    /// nothing unsaved, or via <see cref="ConfirmDiscardCommand"/>).</summary>
    public event Action? Cancelled;

    /// <summary>Resets the form to create a brand-new campaign.</summary>
    public void BeginCreate()
    {
        _editingCampaign = null;
        IsEditMode = false;
        SetFields(string.Empty, string.Empty, string.Empty);
    }

    /// <summary>Resets the form to edit <paramref name="campaign"/> in place. Wired to
    /// <see cref="CampaignsViewModel"/>'s per-row "Edit" trigger (issue #71); built per #18's
    /// explicit "shared view/view model for create and edit" requirement.</summary>
    public void BeginEdit(Campaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        _editingCampaign = campaign;
        IsEditMode = true;
        SetFields(campaign.Name, campaign.GameSystem, campaign.Description);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }

        SaveError = null;

        try
        {
            if (_editingCampaign is null)
            {
                var campaign = new Campaign { Name = Name, GameSystem = GameSystem, Description = Description };
                await _campaignRepository.AddAsync(campaign);
                _editingCampaign = campaign;
            }
            else
            {
                _editingCampaign.Name = Name;
                _editingCampaign.GameSystem = GameSystem;
                _editingCampaign.Description = Description;
                await _campaignRepository.UpdateAsync(_editingCampaign);
            }

            if (Saved is not null)
            {
                await Saved.Invoke(_editingCampaign);
            }
        }
        catch (ArgumentException ex)
        {
            // Defensive only -- ValidateName above already exercises this same domain setter, so
            // this should be unreachable in practice. See this class's remarks.
            SaveError = ex.Message;
        }
        catch (Exception ex)
        {
            // A real repository failure (issue #32) -- e.g. a caught DataAccessException from the
            // database file disappearing mid-session. Without this, the exception would otherwise
            // propagate out of this [RelayCommand]-generated async command uncaught; see
            // GlobalExceptionHandler's remarks for what happens then. Mirrors
            // GeneratorViewModel.SaveAsync's identical catch.
            SaveError = $"Couldn't save this campaign: {ex.Message}";
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

    /// <summary>
    /// Reused by <see cref="Name"/>'s <see cref="CustomValidationAttribute"/> -- validates by
    /// actually going through <see cref="Campaign.Name"/>'s own setter and surfacing whatever
    /// message its <see cref="ArgumentException"/> carries, rather than reimplementing "required,
    /// max 200 chars" as separate rules that could drift from <c>Campaign.ValidateName</c>. See
    /// this class's remarks.
    /// </summary>
    public static ValidationResult? ValidateName(string name, ValidationContext context)
    {
        try
        {
            _ = new Campaign { Name = name };
            return ValidationResult.Success;
        }
        catch (ArgumentException ex)
        {
            return new ValidationResult(StripParameterSuffix(ex));
        }
    }

    /// <summary><see cref="ArgumentException.Message"/> appends " (Parameter 'name')" to whatever
    /// message <see cref="Campaign"/>'s setter actually wrote -- fine for a developer-facing
    /// exception, not something a user should see in an inline form error. Strips it back off
    /// using <see cref="ArgumentException.ParamName"/> so <see cref="ValidateName"/> surfaces
    /// exactly the message <c>Campaign.ValidateName</c> wrote, no more.</summary>
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

    private bool IsDirty() =>
        Name != _originalName || GameSystem != _originalGameSystem || Description != _originalDescription;

    private void SetFields(string name, string gameSystem, string description)
    {
        Name = name;
        GameSystem = gameSystem;
        Description = description;
        _originalName = name;
        _originalGameSystem = gameSystem;
        _originalDescription = description;

        IsShowingDiscardConfirmation = false;
        SaveError = null;

        ClearErrors();
        ValidateAllProperties();

        // ValidateAllProperties() above only raises ErrorsChanged for a property whose error
        // *list* actually changed -- e.g. BeginEdit-ing an already-valid campaign goes from "no
        // errors recorded yet" to "no errors", which is not a change, so OnErrorsChanged below
        // would never run and CanSave/NameError would be stuck at their previous (possibly
        // stale, e.g. default-false) values. Recompute explicitly here so the very first
        // validation pass after a fresh BeginCreate/BeginEdit is always reflected.
        RefreshValidationState();
    }

    private void OnErrorsChanged(object? sender, DataErrorsChangedEventArgs e) => RefreshValidationState();

    private void RefreshValidationState()
    {
        NameError = GetErrors(nameof(Name))
            .OfType<ValidationResult>()
            .Select(result => result.ErrorMessage)
            .FirstOrDefault();

        CanSave = !HasErrors;
    }
}