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
/// The NPCs screen (issues #24+#25): a searchable/filterable/sortable list of every <see cref="Npc"/>
/// in the active campaign, with a visual marker for "known to players", plus an in-place create/edit
/// form (issue #25). Only reachable when a campaign is active -- see <c>ShellViewModel</c>'s gating
/// on <see cref="NavItemViewModel.RequiresActiveCampaign"/> -- but see
/// <see cref="OnActiveCampaignChanged"/> for why this view model still has to cope with the active
/// campaign changing (or disappearing) out from under it while it's showing. Mirrors
/// <see cref="CharactersViewModel"/>'s loading/error/empty-state/form shape one-for-one; see that
/// class's remarks for the parts this class doesn't repeat below.
/// </summary>
/// <remarks>
/// <para>
/// <b>Create/edit/delete is an in-place mode switch (<see cref="IsFormVisible"/>), same as
/// <see cref="CharactersViewModel"/>'s <see cref="CharactersViewModel.IsFormVisible"/> -- but folded
/// into <em>three</em> list states plus a search bar rather than that class's two.</b> Issue #24 (the
/// list) shipped before this class had any form, with <see cref="IsEmpty"/>/<see cref="IsListVisible"/>/
/// <see cref="IsNoSearchResults"/>/<see cref="IsSearchBarVisible"/> each already gated on
/// <see cref="IsLoading"/>/<see cref="HasLoadError"/>/<see cref="AllNpcCount"/>. Issue #25 adds
/// <c>&amp;&amp; !IsFormVisible</c> to all four rather than introducing a separate "form mode" Grid
/// row that could show at the same time as one of them -- so opening <see cref="Form"/> (via
/// <see cref="ShowCreateFormCommand"/> or <see cref="SelectCommand"/>) hides the search bar and
/// whichever of the three list states was showing, exactly like closing a modal, and closing the
/// form (<see cref="OnFormSavedAsync"/>/<see cref="OnFormCancelled"/>/<see cref="OnFormDeletedAsync"/>)
/// re-derives which of the three to show again the same way a fresh load would.
/// </para>
/// <para>
/// <b>NPC rows are now click targets (<see cref="SelectCommand"/>), same "whole row is one Button"
/// idiom as <see cref="CharactersViewModel.SelectCommand"/>/<c>CharactersView.axaml</c>'s
/// <c>characterRow</c> style -- see <c>NpcsView.axaml</c>'s identical remark on why the
/// known-to-players badge inside the row stays a <c>Border</c>, not a <c>Button</c>, so this still
/// avoids Button-in-Button ambiguity.</b>
/// </para>
/// <para>
/// <b>Generator integration is out of scope for this class.</b> Issue #25's own scope note: the
/// generator (issues #26-29) doesn't exist yet, so there is no generator save step to wire into here.
/// Once #29 builds it, the generator will be the one reaching into <see cref="INpcRepository.AddAsync"/>
/// (or reusing <see cref="Form"/>) to persist a generated NPC -- not the other way around -- and
/// nothing about this class needs to change for that to work, since a generated <see cref="Npc"/> is
/// just an <see cref="Npc"/> with <see cref="Npc.WasGenerated"/> set to <c>true</c> (see
/// <see cref="NpcFormViewModel"/>'s remarks on why that flag is never surfaced in this form).
/// </para>
/// <para>
/// <b>Search/filter/sort all run client-side over the one <see cref="INpcRepository.GetByCampaignAsync"/>
/// result, not as repository queries.</b> There's no server-side search/filter/sort on
/// <see cref="INpcRepository"/> and none is needed at this app's scale -- a GM's NPC list for one
/// campaign, not a real database query workload (the issue's own acceptance criterion is phrased in
/// terms of "a list of 100"). <see cref="_allNpcs"/> holds every NPC loaded for the active campaign;
/// <see cref="ApplyFilter"/> re-derives <see cref="Npcs"/> from it in memory on every keystroke/
/// selection change, same spirit as <see cref="CampaignsViewModel"/> sorting by
/// <see cref="Campaign.LastOpenedUtc"/> in memory rather than via a query.
/// </para>
/// <para>
/// <b>Faction/location filters are dropdowns populated from this campaign's own distinct values,
/// plus "All".</b> A free-text second search box for faction/location would double-count with the
/// main search box (which already matches those fields per this issue's acceptance criteria) and
/// invite typos that silently match nothing; a dropdown constrained to values that actually exist
/// guarantees every option returns at least one NPC and needs no autocomplete machinery. Options are
/// computed once per load from <see cref="_allNpcs"/> (the full, unfiltered campaign roster), not
/// re-derived from the currently-filtered subset -- narrowing by search text or the other filter
/// shouldn't also shrink the very control a GM would use to broaden back out.
/// </para>
/// </remarks>
public sealed partial class NpcsViewModel : ViewModelBase
{
    /// <summary>Sentinel option meaning "don't filter by faction/location" -- always first in
    /// <see cref="FactionOptions"/>/<see cref="LocationOptions"/>, regardless of what values are
    /// actually present in the campaign.</summary>
    public const string AllOption = "All";

    private readonly INpcRepository _npcRepository;
    private readonly ActiveCampaignContext _activeCampaignContext;

    /// <summary>Every NPC loaded for the active campaign, unfiltered -- the source
    /// <see cref="ApplyFilter"/> re-derives <see cref="Npcs"/> and <see cref="RebuildFilterOptions"/>
    /// re-derives <see cref="FactionOptions"/>/<see cref="LocationOptions"/> from. See this class's
    /// remarks for why filtering/sorting happens here in memory rather than via the repository.</summary>
    private List<Npc> _allNpcs = [];

    public NpcsViewModel(INpcRepository npcRepository, ActiveCampaignContext activeCampaignContext)
    {
        _npcRepository = npcRepository;
        _activeCampaignContext = activeCampaignContext;

        Form = new NpcFormViewModel(npcRepository);
        Form.Saved += OnFormSavedAsync;
        Form.Cancelled += OnFormCancelled;
        Form.Deleted += OnFormDeletedAsync;

        _activeCampaignContext.ActiveCampaignChanged += OnActiveCampaignChanged;

        _ = LoadAsync();
    }

    /// <summary>Design-time-only constructor for the XAML previewer's <c>Design.DataContext</c>
    /// (see <c>NpcsView.axaml</c>) -- mirrors <see cref="CharactersViewModel"/>'s own parameterless
    /// constructor. Never used at runtime; both heads resolve the constructor above via
    /// <c>Services.NavigationService</c>.</summary>
    public NpcsViewModel()
        : this(new DesignTimeNpcRepository(), new ActiveCampaignContext(new DesignTimeCampaignRepository()))
    {
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    [NotifyPropertyChangedFor(nameof(IsNoSearchResults))]
    [NotifyPropertyChangedFor(nameof(IsSearchBarVisible))]
    public partial bool IsLoading { get; set; } = true;

    /// <summary>Set if loading this campaign's NPCs threw; <c>null</c> otherwise -- mirrors
    /// <see cref="CharactersViewModel.LoadError"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    [NotifyPropertyChangedFor(nameof(IsNoSearchResults))]
    [NotifyPropertyChangedFor(nameof(IsSearchBarVisible))]
    public partial string? LoadError { get; set; }

    /// <summary>Whether the in-place create/edit form (issue #25) is currently showing instead of
    /// the list -- see this class's remarks on folding this into the three list states plus the
    /// search bar, mirroring <see cref="CharactersViewModel.IsFormVisible"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    [NotifyPropertyChangedFor(nameof(IsNoSearchResults))]
    [NotifyPropertyChangedFor(nameof(IsSearchBarVisible))]
    public partial bool IsFormVisible { get; set; }

    /// <summary>How many NPCs the active campaign has in total, before search/filter is applied --
    /// tracked separately from <see cref="Npcs"/>.Count so "zero NPCs in this campaign" (the real
    /// empty state) and "zero NPCs match the current search/filter" (<see cref="IsNoSearchResults"/>)
    /// can be told apart and shown different messaging.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    [NotifyPropertyChangedFor(nameof(IsNoSearchResults))]
    [NotifyPropertyChangedFor(nameof(IsSearchBarVisible))]
    public partial int AllNpcCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    [NotifyPropertyChangedFor(nameof(IsNoSearchResults))]
    public partial ObservableCollection<NpcListItemViewModel> Npcs { get; set; } = [];

    /// <summary>Free-text search across name, role, faction, location and notes (issue #24's own
    /// task) -- see <see cref="ApplyFilter"/> for the exact fields matched.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary><see cref="AllOption"/> or one of this campaign's actual <see cref="Npc.Faction"/>
    /// values -- see this class's remarks for why the filter is a constrained dropdown rather than
    /// a second free-text box.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    public partial string SelectedFaction { get; set; } = AllOption;

    /// <summary><see cref="AllOption"/> or one of this campaign's actual <see cref="Npc.Location"/>
    /// values -- see this class's remarks for why the filter is a constrained dropdown rather than
    /// a second free-text box.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveFilters))]
    public partial string SelectedLocation { get; set; } = AllOption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSortedByName))]
    [NotifyPropertyChangedFor(nameof(IsSortedByRecentlyAdded))]
    public partial NpcSortOrder SortOrder { get; set; } = NpcSortOrder.Name;

    /// <summary><see cref="AllOption"/> plus every distinct, non-blank <see cref="Npc.Faction"/>
    /// value in the active campaign, alphabetical -- rebuilt on every load (see
    /// <see cref="RebuildFilterOptions"/>), not re-derived from the currently-filtered subset.</summary>
    [ObservableProperty]
    public partial ObservableCollection<string> FactionOptions { get; set; } = [AllOption];

    /// <summary><see cref="AllOption"/> plus every distinct, non-blank <see cref="Npc.Location"/>
    /// value in the active campaign, alphabetical -- rebuilt on every load (see
    /// <see cref="RebuildFilterOptions"/>), not re-derived from the currently-filtered subset.</summary>
    [ObservableProperty]
    public partial ObservableCollection<string> LocationOptions { get; set; } = [AllOption];

    public bool HasLoadError => LoadError is not null;

    /// <summary>True once loading has finished without error, the form isn't showing, and the
    /// active campaign has no NPCs at all yet -- distinct from <see cref="IsNoSearchResults"/> (NPCs
    /// exist, but none match the current search/filter). Drives the "no NPCs yet" empty-state UI.</summary>
    public bool IsEmpty => !IsLoading && !HasLoadError && !IsFormVisible && AllNpcCount == 0;

    /// <summary>True once loading has finished without error, the form isn't showing, the campaign
    /// has at least one NPC, and at least one of them matches the current search/filter. Drives the
    /// populated list UI.</summary>
    public bool IsListVisible => !IsLoading && !HasLoadError && !IsFormVisible && AllNpcCount > 0 && Npcs.Count > 0;

    /// <summary>True once loading has finished without error, the form isn't showing, the campaign
    /// has at least one NPC, but none of them match the current search/filter -- distinct from
    /// <see cref="IsEmpty"/> so the messaging can say "no matches" rather than "no NPCs yet".</summary>
    public bool IsNoSearchResults => !IsLoading && !HasLoadError && !IsFormVisible && AllNpcCount > 0 && Npcs.Count == 0;

    /// <summary>Whether the search box and faction/location/sort controls should show at all --
    /// there's nothing to search/filter/sort when the campaign has zero NPCs, and the form (issue
    /// #25) takes over the whole screen while it's showing, same as
    /// <see cref="CharactersViewModel"/>'s form hiding its roster.</summary>
    public bool IsSearchBarVisible => !IsLoading && !HasLoadError && !IsFormVisible && AllNpcCount > 0;

    /// <summary>The shared create/edit form (issue #25) -- see this class's remarks for why it's
    /// composed in-place rather than a separate destination/window.</summary>
    public NpcFormViewModel Form { get; }

    public bool IsSortedByName => SortOrder == NpcSortOrder.Name;

    public bool IsSortedByRecentlyAdded => SortOrder == NpcSortOrder.RecentlyAdded;

    /// <summary>True when <see cref="SearchText"/>, <see cref="SelectedFaction"/> or
    /// <see cref="SelectedLocation"/> is narrowing the list -- drives whether the "Clear filters"
    /// button shows at all. Deliberately doesn't consider <see cref="SortOrder"/>: sorting reorders
    /// the same NPCs rather than narrowing which ones show, so it isn't a "filter" to clear.</summary>
    public bool HasActiveFilters => SearchText.Trim().Length > 0 || SelectedFaction != AllOption || SelectedLocation != AllOption;

    /// <summary>Opens <see cref="Form"/> in create mode -- mirrors
    /// <see cref="CharactersViewModel.ShowCreateFormCommand"/>.</summary>
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

    /// <summary>Click-through to edit -- opens <see cref="Form"/> pre-populated with
    /// <paramref name="item"/>'s NPC, in place. Mirrors <see cref="CharactersViewModel.Select"/>.</summary>
    [RelayCommand]
    private void Select(NpcListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        Form.BeginEdit(item.Npc);
        IsFormVisible = true;
    }

    [RelayCommand]
    private void SortByName() => SortOrder = NpcSortOrder.Name;

    [RelayCommand]
    private void SortByRecentlyAdded() => SortOrder = NpcSortOrder.RecentlyAdded;

    /// <summary>Resets <see cref="SearchText"/>/<see cref="SelectedFaction"/>/<see cref="SelectedLocation"/>
    /// back to "show everyone" in one action, so a GM doesn't have to clear the search box and
    /// reset two dropdowns individually to get back to the full roster.</summary>
    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedFaction = AllOption;
        SelectedLocation = AllOption;
    }

    /// <summary>Retries a failed load -- mirrors <see cref="CharactersViewModel.RetryLoadCommand"/>.</summary>
    [RelayCommand]
    private Task RetryLoadAsync() => LoadAsync();

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedFactionChanged(string value) => ApplyFilter();

    partial void OnSelectedLocationChanged(string value) => ApplyFilter();

    partial void OnSortOrderChanged(NpcSortOrder value) => ApplyFilter();

    private async Task LoadAsync()
    {
        IsLoading = true;
        LoadError = null;

        var campaignId = _activeCampaignContext.ActiveCampaign?.Id;
        if (campaignId is null)
        {
            // No active campaign -- either this screen hasn't finished being navigated away from
            // yet (see OnActiveCampaignChanged) or it's being constructed before one is selected.
            // Not an error state: just show as empty. Mirrors CharactersViewModel.LoadAsync's
            // identical handling.
            _allNpcs = [];
            AllNpcCount = 0;
            RebuildFilterOptions();
            ApplyFilter();
            IsLoading = false;
            return;
        }

        try
        {
            var npcs = await _npcRepository.GetByCampaignAsync(campaignId.Value);
            _allNpcs = [.. npcs];
            AllNpcCount = _allNpcs.Count;
            RebuildFilterOptions();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            // Constructor-time load is fire-and-forget -- see CampaignsViewModel.LoadAsync's
            // identical remark on why this must be surfaced rather than left to throw silently.
            LoadError = $"Couldn't load this campaign's NPCs: {ex.Message}";
            _allNpcs = [];
            AllNpcCount = 0;
            RebuildFilterOptions();
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Re-derives <see cref="FactionOptions"/>/<see cref="LocationOptions"/> from <see cref="_allNpcs"/>
    /// -- called once per load (see this class's remarks for why these lists come from the full
    /// campaign roster rather than the currently-filtered subset). If a previously-selected faction
    /// or location no longer exists in the rebuilt list (e.g. the active campaign just changed to a
    /// different one), the selection resets to <see cref="AllOption"/> rather than pointing at a
    /// value that's no longer selectable.
    /// </summary>
    private void RebuildFilterOptions()
    {
        FactionOptions = new ObservableCollection<string>(DistinctNonBlank(_allNpcs.Select(npc => npc.Faction)).Prepend(AllOption));
        if (!FactionOptions.Contains(SelectedFaction))
        {
            SelectedFaction = AllOption;
        }

        LocationOptions = new ObservableCollection<string>(DistinctNonBlank(_allNpcs.Select(npc => npc.Location)).Prepend(AllOption));
        if (!LocationOptions.Contains(SelectedLocation))
        {
            SelectedLocation = AllOption;
        }
    }

    private static IEnumerable<string> DistinctNonBlank(IEnumerable<string> values) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Re-derives <see cref="Npcs"/> from <see cref="_allNpcs"/> by applying the current
    /// <see cref="SearchText"/>/<see cref="SelectedFaction"/>/<see cref="SelectedLocation"/>/
    /// <see cref="SortOrder"/> -- called on every keystroke/selection change (via the
    /// <c>OnXChanged</c> partial methods above) rather than re-querying the repository; see this
    /// class's remarks for why that's the right tradeoff at this app's scale.
    /// </summary>
    private void ApplyFilter()
    {
        IEnumerable<Npc> query = _allNpcs;

        var term = SearchText.Trim();
        if (term.Length > 0)
        {
            // The five fields issue #24's acceptance criteria call out by name -- deliberately not
            // Motivation/Secret/Appearance/Mannerism, which the issue doesn't mention.
            query = query.Where(npc =>
                Matches(npc.Name, term) ||
                Matches(npc.Role, term) ||
                Matches(npc.Faction, term) ||
                Matches(npc.Location, term) ||
                Matches(npc.Notes, term));
        }

        if (SelectedFaction != AllOption)
        {
            query = query.Where(npc => string.Equals(npc.Faction, SelectedFaction, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedLocation != AllOption)
        {
            query = query.Where(npc => string.Equals(npc.Location, SelectedLocation, StringComparison.OrdinalIgnoreCase));
        }

        query = SortOrder == NpcSortOrder.RecentlyAdded
            ? query.OrderByDescending(npc => npc.CreatedUtc)
            : query.OrderBy(npc => npc.Name, StringComparer.OrdinalIgnoreCase);

        Npcs = new ObservableCollection<NpcListItemViewModel>(query.Select(npc => new NpcListItemViewModel(npc)));
    }

    private static bool Matches(string value, string term) => value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private async Task OnFormSavedAsync(Npc npc)
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
        // back to the UI thread before touching bound state. Mirrors CharactersViewModel/
        // CampaignsViewModel/ShellViewModel's identical handling of the same event.
        Dispatcher.UIThread.Post(HandleActiveCampaignChanged);
    }

    /// <summary>
    /// The actual work done in response to <see cref="ActiveCampaignContext.ActiveCampaignChanged"/>,
    /// factored out of <see cref="OnActiveCampaignChanged"/> so it's callable directly (bypassing
    /// <see cref="Dispatcher.UIThread"/>) from tests that don't run inside an Avalonia dispatcher
    /// loop -- mirrors <c>CharactersViewModel.HandleActiveCampaignChanged</c> (see
    /// <c>InternalsVisibleTo</c> in <c>AssemblyInfo.cs</c>). Not called directly by application code.
    /// </summary>
    /// <remarks>
    /// Reacts to the active campaign changing (including becoming <c>null</c>, e.g. issue #19's
    /// delete-active-campaign flow) while this screen is showing -- it isn't necessarily navigated
    /// away from immediately (<c>ShellViewModel</c> only redirects away once the active campaign
    /// becomes <c>null</c>, not when it merely changes to a *different* campaign), so this view
    /// model has to reload for whichever campaign (if any) is now active. Search text and sort
    /// order deliberately carry over across the switch (a GM's typing shouldn't vanish just because
    /// they tapped a different campaign in the nav), while the faction/location selections reset
    /// only if the new campaign doesn't have that value at all -- see <see cref="RebuildFilterOptions"/>.
    /// Also closes <see cref="Form"/> since it may be mid-edit of an NPC that belongs to a campaign
    /// that's no longer active -- mirrors <c>CharactersViewModel.HandleActiveCampaignChanged</c>'s
    /// identical handling.
    /// </remarks>
    internal void HandleActiveCampaignChanged()
    {
        IsFormVisible = false;
        _ = LoadAsync();
    }
}