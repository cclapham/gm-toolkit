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
/// The Characters screen (issue #20): a roster of every <see cref="PlayerCharacter"/> in the
/// active campaign, an empty state for a campaign with none yet, and an in-place create/edit form
/// (issue #21). Only reachable when a campaign is active -- see <c>ShellViewModel</c>'s gating on
/// <see cref="NavItemViewModel.RequiresActiveCampaign"/> -- but see <see cref="OnActiveCampaignChanged"/>
/// for why this view model still has to cope with the active campaign changing (or disappearing)
/// out from under it while it's showing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Create/edit/delete is an in-place mode switch, not a separate <see cref="Avalonia.Controls.Window"/>
/// or a second <see cref="Services.NavigationDestination"/>.</b> Mirrors
/// <see cref="CampaignsViewModel"/>'s identical reasoning one-for-one (Android's single-view
/// lifetime has no popup-<c>Window</c> equivalent, and <see cref="Services.NavigationService"/>
/// caches one view model per destination) -- see that class's remarks for the full explanation.
/// <see cref="IsFormVisible"/> toggles between the list/empty-state UI and <see cref="Form"/> (a
/// <see cref="CharacterFormViewModel"/>) within this one screen.
/// </para>
/// <para>
/// <b>"Pinned passive values" (issue #20's own task) are simply every entry in
/// <see cref="PlayerCharacter.Stats"/>, rendered compactly</b> -- not a real pin/unpin data-model
/// feature. <see cref="PlayerCharacter.Stats"/> is a system-agnostic, open-ended
/// <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/> (D&amp;D's STR/DEX/HP, Call of
/// Cthulhu's SAN/Idea/Luck, homebrew's whatever-the-GM-wants); adding real per-key "pinned" metadata
/// would mean either bolting extra structure onto a plain string-keyed dictionary or a parallel
/// "pinned key names" list on <see cref="PlayerCharacter"/>, both of which are more invasive than
/// this MVP needs for what's fundamentally a display-density concern. Instead,
/// <see cref="CharacterListItemViewModel.StatsSummary"/> shows the whole bag as a single-line,
/// ellipsis-truncated string (same "compact inline summary" idiom as
/// <see cref="CampaignListItemViewModel.CountsLabel"/>), which keeps every roster row a small, fixed
/// height regardless of how many stats a PC has -- what actually makes "four PCs readable on one
/// screen without scrolling" (this issue's acceptance criterion) hold at typical laptop resolution.
/// </para>
/// </remarks>
public sealed partial class CharactersViewModel : ViewModelBase, IRefreshable
{
    private readonly IPlayerCharacterRepository _playerCharacterRepository;
    private readonly ActiveCampaignContext _activeCampaignContext;

    public CharactersViewModel(IPlayerCharacterRepository playerCharacterRepository, ActiveCampaignContext activeCampaignContext)
    {
        _playerCharacterRepository = playerCharacterRepository;
        _activeCampaignContext = activeCampaignContext;

        Form = new CharacterFormViewModel(playerCharacterRepository);
        Form.Saved += OnFormSavedAsync;
        Form.Cancelled += OnFormCancelled;
        Form.Deleted += OnFormDeletedAsync;

        _activeCampaignContext.ActiveCampaignChanged += OnActiveCampaignChanged;

        _ = LoadAsync();
    }

    /// <summary>Design-time-only constructor for the XAML previewer's <c>Design.DataContext</c>
    /// (see <c>CharactersView.axaml</c>) -- mirrors <see cref="CampaignsViewModel"/>'s own
    /// parameterless constructor. Never used at runtime; both heads resolve the constructor above
    /// via <c>Services.NavigationService</c>.</summary>
    public CharactersViewModel()
        : this(new DesignTimePlayerCharacterRepository(), new ActiveCampaignContext(new DesignTimeCampaignRepository()))
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

    /// <summary>Set if loading this campaign's characters threw; <c>null</c> otherwise -- mirrors
    /// <see cref="CampaignsViewModel.LoadError"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    public partial string? LoadError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    public partial ObservableCollection<CharacterListItemViewModel> Characters { get; set; } = [];

    public bool HasLoadError => LoadError is not null;

    /// <summary>True once loading has finished without error and there are no characters yet in
    /// the active campaign and the form isn't showing -- drives the empty-state UI.</summary>
    public bool IsEmpty => !IsLoading && !HasLoadError && !IsFormVisible && Characters.Count == 0;

    /// <summary>True once loading has finished without error, there's at least one character, and
    /// the form isn't showing -- drives the populated roster UI.</summary>
    public bool IsListVisible => !IsLoading && !HasLoadError && !IsFormVisible && Characters.Count > 0;

    /// <summary>The shared create/edit form (issue #21) -- see this class's remarks for why it's
    /// composed in-place rather than a separate destination/window.</summary>
    public CharacterFormViewModel Form { get; }

    [RelayCommand]
    private void ShowCreateForm()
    {
        var campaignId = _activeCampaignContext.ActiveCampaign?.Id;
        if (campaignId is null)
        {
            // This screen is only reachable via a nav item gated on an active campaign existing
            // (see this class's remarks) -- defense-in-depth only, not expected in practice.
            return;
        }

        Form.BeginCreate(campaignId.Value);
        IsFormVisible = true;
    }

    /// <summary>Click-through to edit (issue #20's "click through to detail/edit" task) -- opens
    /// <see cref="Form"/> pre-populated with <paramref name="item"/>'s character, in place.</summary>
    [RelayCommand]
    private void Select(CharacterListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        Form.BeginEdit(item.PlayerCharacter);
        IsFormVisible = true;
    }

    /// <summary>Retries a failed load -- mirrors <see cref="CampaignsViewModel.RetryLoadAsync"/>.</summary>
    [RelayCommand]
    private Task RetryLoadAsync() => LoadAsync();

    /// <inheritdoc/>
    /// <remarks>Delegates straight to <see cref="LoadAsync"/> -- the same method the constructor's
    /// own fire-and-forget initial load already calls -- mirrors
    /// <see cref="NpcsViewModel.RefreshAsync"/>'s identical remark on why this deliberately does not
    /// also close <see cref="Form"/>. Passes <c>showLoadingIndicator: false</c> -- see
    /// <see cref="NpcsViewModel.RefreshAsync"/>'s remarks on why toggling <see cref="IsLoading"/> on
    /// every single navigation back to an already-populated screen would be a UX regression.</remarks>
    public Task RefreshAsync() => LoadAsync(showLoadingIndicator: false);

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

        var campaignId = _activeCampaignContext.ActiveCampaign?.Id;
        if (campaignId is null)
        {
            // No active campaign -- either this screen hasn't finished being navigated away from
            // yet (see OnActiveCampaignChanged) or it's being constructed before one is selected.
            // Not an error state: just show as empty.
            Characters = [];

            if (showLoadingIndicator)
            {
                IsLoading = false;
            }

            return;
        }

        try
        {
            var characters = await _playerCharacterRepository.GetByCampaignAsync(campaignId.Value);
            var sorted = characters.OrderBy(character => character.CharacterName, StringComparer.OrdinalIgnoreCase);

            Characters = new ObservableCollection<CharacterListItemViewModel>(
                sorted.Select(character => new CharacterListItemViewModel(character)));
        }
        catch (Exception ex)
        {
            // Constructor-time load is fire-and-forget -- see CampaignsViewModel.LoadAsync's
            // identical remark on why this must be surfaced rather than left to throw silently.
            LoadError = $"Couldn't load this campaign's characters: {ex.Message}";
        }
        finally
        {
            if (showLoadingIndicator)
            {
                IsLoading = false;
            }
        }
    }

    private async Task OnFormSavedAsync(PlayerCharacter character)
    {
        IsFormVisible = false;
        await LoadAsync();
    }

    private void OnFormCancelled()
    {
        IsFormVisible = false;
    }

    private async Task OnFormDeletedAsync()
    {
        IsFormVisible = false;
        await LoadAsync();
    }

    private void OnActiveCampaignChanged()
    {
        // ActiveCampaignContext.ActiveCampaignChanged can fire from a non-UI thread (Android
        // restores the active campaign inside a background Task.Run at startup) -- always marshal
        // back to the UI thread before touching bound state. Mirrors CampaignsViewModel/
        // ShellViewModel's identical handling of the same event.
        Dispatcher.UIThread.Post(HandleActiveCampaignChanged);
    }

    /// <summary>
    /// The actual work done in response to <see cref="ActiveCampaignContext.ActiveCampaignChanged"/>,
    /// factored out of <see cref="OnActiveCampaignChanged"/> so it's callable directly (bypassing
    /// <see cref="Dispatcher.UIThread"/>) from tests that don't run inside an Avalonia dispatcher
    /// loop -- mirrors <c>ShellViewModel.HandleActiveCampaignChanged</c> (see <c>InternalsVisibleTo</c>
    /// in <c>AssemblyInfo.cs</c>). Not called directly by application code.
    /// </summary>
    /// <remarks>
    /// Reacts to the active campaign changing (including becoming <c>null</c>, e.g. issue #19's
    /// delete-active-campaign flow) while this screen is showing -- it isn't necessarily navigated
    /// away from immediately (<c>ShellViewModel</c> only redirects away once the active campaign
    /// becomes <c>null</c>, not when it merely changes to a *different* campaign), so this view
    /// model has to reload for whichever campaign (if any) is now active, and close the form since
    /// it may be mid-edit of a character that belongs to a campaign that's no longer active.
    /// </remarks>
    internal void HandleActiveCampaignChanged()
    {
        IsFormVisible = false;
        _ = LoadAsync();
    }
}