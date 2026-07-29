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
public sealed partial class CampaignsViewModel : ViewModelBase, IRefreshable
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly ActiveCampaignContext _activeCampaignContext;

    /// <summary>The row currently showing its delete-confirmation panel (issue #19), or
    /// <c>null</c> if none is. Only one row can be mid-delete at a time -- requesting delete on a
    /// different row while one is already pending closes the earlier one first (see
    /// <see cref="RequestDelete"/>).</summary>
    private CampaignListItemViewModel? _deleteTarget;

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

    /// <summary>What the user has typed into the delete-confirmation row's text field, compared
    /// against <see cref="_deleteTarget"/>'s name in <see cref="CanConfirmDelete"/>. Empty
    /// whenever <see cref="_deleteTarget"/> is <c>null</c>.</summary>
    [ObservableProperty]
    public partial string DeleteConfirmationInput { get; set; } = string.Empty;

    /// <summary>Set if <see cref="ConfirmDeleteAsync"/>'s repository call throws; <c>null</c>
    /// otherwise. Mirrors <see cref="CampaignFormViewModel.SaveError"/>.</summary>
    [ObservableProperty]
    public partial string? DeleteError { get; set; }

    private bool _canConfirmDelete;

    /// <summary>
    /// Type-to-confirm gate for <see cref="ConfirmDeleteCommand"/>: deleting a campaign also
    /// destroys every one of its PlayerCharacters and Npcs (see
    /// <see cref="ICampaignRepository.DeleteAsync"/>'s cascade), and there is no undo, so a single
    /// misclick must not be enough. True only once <see cref="DeleteConfirmationInput"/> is an
    /// exact (ordinal, case-sensitive) match of <see cref="_deleteTarget"/>'s name -- the same
    /// "type the name to confirm" pattern GitHub uses for repository deletion, and a more robust
    /// guard against an accidental double-click than a second confirmation button would be.
    /// Recomputed by <see cref="UpdateCanConfirmDelete"/> (mirrors
    /// <see cref="CampaignFormViewModel.CanSave"/>'s explicitly-notified backing-field pattern,
    /// since it depends on <see cref="_deleteTarget"/>, a private field with no change
    /// notification of its own) rather than being a plain computed getter, so both
    /// <see cref="ConfirmDeleteCommand"/>'s enabled state and any other binding to this property
    /// stay in sync with every state change, not just the ones that happen to coincide with an
    /// <see cref="DeleteConfirmationInput"/> edit.
    /// </summary>
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

    /// <inheritdoc/>
    /// <remarks>Delegates straight to <see cref="LoadAsync"/> -- the same method the constructor's
    /// own fire-and-forget initial load already calls, and the only method that ever repopulates
    /// <see cref="Campaigns"/> -- mirrors <see cref="NpcsViewModel.RefreshAsync"/>'s identical
    /// remark. This screen has no search/filter state to preserve, but a fresh
    /// <see cref="Campaign.LastOpenedUtc"/>-based sort and active-row marker (see
    /// <see cref="RefreshActiveSelection"/>) every time it's navigated to is exactly what makes this
    /// class immune to the same staleness class of bug issue #68 fixed for
    /// <see cref="NpcsViewModel"/>/<see cref="CharactersViewModel"/>. Passes
    /// <c>showLoadingIndicator: false</c> -- see <see cref="NpcsViewModel.RefreshAsync"/>'s remarks on
    /// why toggling <see cref="IsLoading"/> on every single navigation back to an already-populated
    /// screen would be a UX regression.</remarks>
    public Task RefreshAsync() => LoadAsync(showLoadingIndicator: false);

    /// <summary>
    /// Opens <paramref name="item"/> in the shared create/edit form's edit mode (issue #71), via
    /// the same <see cref="CampaignFormViewModel.BeginEdit"/> that issue #18 already built but left
    /// unwired. A sibling of <see cref="RequestDelete"/>'s trigger button, not a reuse of
    /// <see cref="SelectAsync"/>'s row click: that click's job stays "activate this campaign" (see
    /// this class's remarks), so making a campaign the active one is never a side effect of just
    /// wanting to fix a typo in its name.
    /// </summary>
    [RelayCommand]
    private void RequestEdit(CampaignListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        Form.BeginEdit(item.Campaign);
        IsFormVisible = true;
    }

    /// <summary>
    /// Toggles <paramref name="item"/>'s expanded overview panel (issue #72: description + created
    /// date) open or closed. A third sibling of <see cref="RequestEdit"/>/<see cref="RequestDelete"/>'s
    /// row-level triggers, not a reuse of <see cref="SelectAsync"/>'s row click -- same reasoning as
    /// those two commands' doc comments: looking at more detail about a campaign must never make it
    /// the active one as a side effect. Unlike <see cref="RequestDelete"/>, expanding one row does
    /// not collapse any other -- each row's <see cref="CampaignListItemViewModel.IsExpanded"/> is
    /// independent, since there's no correctness reason (unlike delete confirmation) to limit this
    /// to one row at a time.
    /// </summary>
    [RelayCommand]
    private void ToggleExpanded(CampaignListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        item.IsExpanded = !item.IsExpanded;
    }

    /// <summary>
    /// Shows <paramref name="item"/>'s inline delete-confirmation panel (issue #19) -- the delete
    /// trigger's own click never deletes anything by itself, only this. If another row already
    /// has its confirmation showing, it's closed first: only one row can be mid-delete at a time,
    /// so a stray typed name from a previous attempt can never silently satisfy
    /// <see cref="CanConfirmDelete"/> for a different campaign.
    /// </summary>
    [RelayCommand]
    private void RequestDelete(CampaignListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (_deleteTarget is not null)
        {
            _deleteTarget.IsShowingDeleteConfirmation = false;
        }

        _deleteTarget = item;
        item.IsShowingDeleteConfirmation = true;
        DeleteConfirmationInput = string.Empty;
        DeleteError = null;
        UpdateCanConfirmDelete();
    }

    [RelayCommand]
    private void CancelDelete()
    {
        if (_deleteTarget is not null)
        {
            _deleteTarget.IsShowingDeleteConfirmation = false;
        }

        _deleteTarget = null;
        DeleteConfirmationInput = string.Empty;
        DeleteError = null;
        UpdateCanConfirmDelete();
    }

    /// <summary>
    /// Deletes <see cref="_deleteTarget"/> via <see cref="ICampaignRepository.DeleteAsync"/> (which
    /// already cascades to its PlayerCharacters/Npcs rows -- see that method's doc comment), clears
    /// <see cref="ActiveCampaignContext.ActiveCampaign"/> if it was the campaign just deleted, and
    /// reloads the list. Gated by <see cref="CanConfirmDelete"/>, so a bound button only ever
    /// invokes this once the user has typed the campaign's exact name.
    /// </summary>
    /// <remarks>
    /// Re-checks <see cref="CanConfirmDelete"/> itself as the first thing in the body (mirrors
    /// <see cref="CampaignFormViewModel.SaveAsync"/>'s identical defense-in-depth), rather than
    /// trusting the <c>[RelayCommand(CanExecute = ...)]</c> gate alone: unlike a normal
    /// <see cref="System.Windows.Input.ICommand.Execute"/> call from a bound, auto-disabling
    /// button, <see cref="IAsyncRelayCommand.ExecuteAsync"/> does not itself call
    /// <see cref="System.Windows.Input.ICommand.CanExecute"/> first, so anything invoking it
    /// directly (including a test) would otherwise bypass the confirmation entirely.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanConfirmDelete))]
    private async Task ConfirmDeleteAsync()
    {
        if (!CanConfirmDelete || _deleteTarget is null)
        {
            return;
        }

        var deletedId = _deleteTarget.Campaign.Id;
        DeleteError = null;

        try
        {
            await _campaignRepository.DeleteAsync(deletedId);

            if (_activeCampaignContext.ActiveCampaign?.Id == deletedId)
            {
                _activeCampaignContext.Clear();
            }

            _deleteTarget = null;
            DeleteConfirmationInput = string.Empty;
            UpdateCanConfirmDelete();

            await LoadAsync();
        }
        catch (Exception ex)
        {
            // Leave the confirmation panel showing (with whatever the user typed still intact) so
            // they don't have to retype the campaign name to try again.
            DeleteError = $"Couldn't delete this campaign: {ex.Message}";
        }
    }

    partial void OnDeleteConfirmationInputChanged(string value) => UpdateCanConfirmDelete();

    private void UpdateCanConfirmDelete() =>
        CanConfirmDelete = _deleteTarget is not null && string.Equals(DeleteConfirmationInput, _deleteTarget.Name, StringComparison.Ordinal);

    /// <param name="showLoadingIndicator">Whether to toggle <see cref="IsLoading"/> true for the
    /// duration of this load -- mirrors <see cref="NpcsViewModel.LoadAsync(bool)"/>'s identical
    /// parameter one-for-one.</param>
    private async Task LoadAsync(bool showLoadingIndicator = true)
    {
        if (showLoadingIndicator)
        {
            IsLoading = true;
        }

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
            if (showLoadingIndicator)
            {
                IsLoading = false;
            }
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