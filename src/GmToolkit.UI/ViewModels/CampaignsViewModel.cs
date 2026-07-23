using System.Collections.ObjectModel;
using System.Linq;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;
using GmToolkit.Core.Services;
using GmToolkit.UI.Design;

namespace GmToolkit.UI.ViewModels;

/// <summary>
/// The Campaigns screen (issue #17): a sorted list of every campaign, an empty state for a
/// first-time user with nothing yet, and an in-place create/edit form (issue #18).
/// </summary>
/// <remarks>
/// <para>
/// <b>Create/edit is an in-place mode switch, not a separate <see cref="Avalonia.Controls.Window"/>
/// or a second <see cref="Services.NavigationDestination"/>.</b> This app runs on Android's
/// single-view lifetime (see <c>App.axaml.cs</c>), which has no popup-<c>Window</c> equivalent,
/// and <see cref="Services.NavigationService"/> caches exactly one view model instance per
/// destination for the app's lifetime, so a second "create campaign" destination would need real
/// changes there for no real benefit. Instead, <see cref="IsFormVisible"/> toggles between the
/// list/empty-state UI and <see cref="Form"/> (a <see cref="CampaignFormViewModel"/>) within this
/// one screen -- see <c>CampaignsView.axaml</c>.
/// </para>
/// <para>
/// <b>Counts come from <c>ICampaignRepository.GetAllAsync</c> directly</b>, not from separate
/// <c>IPlayerCharacterRepository</c>/<c>INpcRepository</c> calls per campaign:
/// <see cref="Data.Repositories.CampaignRepository"/>'s <c>GetAllAsync</c> already populates each
/// <see cref="Campaign.PlayerCharacters"/>/<see cref="Campaign.Npcs"/> list (unlike a hypothetical
/// lazy-loading repository), so <see cref="CampaignListItemViewModel"/> just reads
/// <c>.Count</c> off what's already loaded -- no extra queries, no extra constructor dependencies.
/// </para>
/// <para>
/// <b>"Click to select and navigate" doesn't navigate away from this screen.</b> Clicking a row
/// calls <see cref="ActiveCampaignContext.SelectCampaignAsync"/> (which unlocks the
/// Characters/NPCs/Generator nav items -- <c>ShellViewModel</c> already reacts to
/// <see cref="ActiveCampaignContext.ActiveCampaignChanged"/>) and marks the row as active; it does
/// not jump to another destination. Characters/NPCs/Generator are still placeholders with no real
/// content behind them (that lands in later issues), so there's nowhere meaningful to auto-jump
/// to yet -- doing so now would just swap one empty screen for another and hide the very list the
/// user might want to pick a *different* campaign from a moment later.
/// </para>
/// </remarks>
public sealed partial class CampaignsViewModel : ViewModelBase
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly ActiveCampaignContext _activeCampaignContext;

    public CampaignsViewModel(ICampaignRepository campaignRepository, ActiveCampaignContext activeCampaignContext)
    {
        _campaignRepository = campaignRepository;
        _activeCampaignContext = activeCampaignContext;

        Form = new CampaignFormViewModel(campaignRepository);
        Form.Saved += OnFormSavedAsync;
        Form.Cancelled += OnFormCancelled;

        _activeCampaignContext.ActiveCampaignChanged += OnActiveCampaignChanged;

        _ = LoadAsync();
    }

    /// <summary>Design-time-only constructor for the XAML previewer's <c>Design.DataContext</c>
    /// (see <c>CampaignsView.axaml</c>) -- mirrors <see cref="ShellViewModel"/>'s parameterless
    /// constructor. Never used at runtime; both heads resolve the constructor above via
    /// <c>Services.NavigationService</c>.</summary>
    public CampaignsViewModel()
        : this(new DesignTimeCampaignRepository(), new ActiveCampaignContext(new DesignTimeCampaignRepository()))
    {
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    public partial bool IsFormVisible { get; set; }

    /// <summary>Set if loading the campaign list threw (e.g. the database file is locked or
    /// unreadable); <c>null</c> otherwise. Surfaced instead of leaving <see cref="IsLoading"/>
    /// stuck true forever with nothing shown, on the one screen a first-time user sees before
    /// anything else exists.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    public partial string? LoadError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    public partial ObservableCollection<CampaignListItemViewModel> Campaigns { get; set; } = [];

    public bool HasLoadError => LoadError is not null;

    /// <summary>True once loading has finished without error and there are no campaigns and the
    /// form isn't showing -- drives the empty-state UI (explain what a campaign is, offer
    /// Create).</summary>
    public bool IsEmpty => !IsLoading && !HasLoadError && !IsFormVisible && Campaigns.Count == 0;

    /// <summary>True once loading has finished without error, there's at least one campaign, and
    /// the form isn't showing -- drives the populated list UI.</summary>
    public bool IsListVisible => !IsLoading && !HasLoadError && !IsFormVisible && Campaigns.Count > 0;

    /// <summary>The shared create/edit form (issue #18) -- see this class's remarks for why it's
    /// composed in-place rather than a separate destination/window.</summary>
    public CampaignFormViewModel Form { get; }

    [RelayCommand]
    private void ShowCreateForm()
    {
        Form.BeginCreate();
        IsFormVisible = true;
    }

    [RelayCommand]
    private async Task SelectAsync(CampaignListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        await _activeCampaignContext.SelectCampaignAsync(item.Campaign);
        RefreshActiveSelection();
    }

    /// <summary>Retries a failed load -- the only way forward from <see cref="HasLoadError"/>'s
    /// error state, since there's nothing else to interact with on this screen when it's showing.</summary>
    [RelayCommand]
    private Task RetryLoadAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        IsLoading = true;
        LoadError = null;

        try
        {
            var campaigns = await _campaignRepository.GetAllAsync();
            var sorted = campaigns.OrderByDescending(campaign => campaign.LastOpenedUtc);

            Campaigns = new ObservableCollection<CampaignListItemViewModel>(
                sorted.Select(campaign => new CampaignListItemViewModel(campaign)));

            RefreshActiveSelection();
        }
        catch (Exception ex)
        {
            // Constructor-time load is fire-and-forget (nothing to await it), so an unhandled
            // exception here would otherwise be silently lost and IsLoading would stay true
            // forever -- an infinite spinner with nothing shown, on the one screen a first-time
            // user sees before anything else exists. Surface it and let RetryLoadAsync retry.
            LoadError = $"Couldn't load your campaigns: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshActiveSelection()
    {
        var activeId = _activeCampaignContext.ActiveCampaign?.Id;
        foreach (var item in Campaigns)
        {
            item.IsActive = activeId is not null && item.Campaign.Id == activeId;
        }
    }

    private async Task OnFormSavedAsync(Campaign campaign)
    {
        IsFormVisible = false;
        await LoadAsync();
    }

    private void OnFormCancelled()
    {
        IsFormVisible = false;
    }

    private void OnActiveCampaignChanged()
    {
        // ActiveCampaignContext.ActiveCampaignChanged can fire from a non-UI thread (Android
        // restores the active campaign inside a background Task.Run at startup) -- always marshal
        // back to the UI thread before touching bound state. Mirrors ShellViewModel's identical
        // handling of the same event.
        Dispatcher.UIThread.Post(RefreshActiveSelection);
    }
}