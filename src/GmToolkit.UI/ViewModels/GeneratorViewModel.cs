using System.Collections.ObjectModel;
using System.Linq;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using GmToolkit.Core.Generator;
using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;
using GmToolkit.Core.Services;
using GmToolkit.UI.Design;
using GmToolkit.UI.Services;

namespace GmToolkit.UI.ViewModels;

/// <summary>
/// The NPC Generator screen (issues #28+#29) -- "the feature the app exists for". Generates a full
/// <see cref="GeneratedNpc"/> in one action, lets the GM reroll or lock any one of its six fields
/// independently, edit any field's current value inline, constrain <see cref="Role"/>/
/// <see cref="Name"/> generation by occupation category / name culture, and (issue #29) save the
/// result into the active campaign as a real <see cref="Npc"/> with <see cref="Npc.WasGenerated"/>
/// set to <c>true</c>. Only reachable when a campaign is active -- see <c>ShellViewModel</c>'s
/// gating logic -- but see this class's remarks for how it still defends against a <c>null</c>
/// active campaign at save time, same as every other repository-backed view model in this
/// namespace.
/// </summary>
/// <remarks>
/// <para>
/// <b>Saving builds a brand-new <see cref="Npc"/> straight from this screen's six generated fields
/// plus two save-time-only inputs, then resets back to the empty state.</b> <see cref="Name"/>/
/// <see cref="Role"/>/<see cref="Appearance"/>/<see cref="Mannerism"/>/<see cref="Motivation"/>/
/// <see cref="Secret"/> map 1:1 onto <see cref="Npc"/>'s same-named properties; <see cref="Faction"/>/
/// <see cref="Location"/> exist only here at save time (there are no faction/location generator
/// tables, unlike every other field) and are never touched by <see cref="GenerateCommand"/> or any
/// reroll command. <see cref="SaveCommand"/> persists via <see cref="INpcRepository.AddAsync"/>,
/// sets <see cref="SaveConfirmationMessage"/>, then calls <see cref="ResetGenerator"/> to clear every
/// field/lock back to <see cref="GeneratorViewModel"/>'s own "nothing generated yet" state (issue
/// #29's own "generator resets ready for the next one" task) -- <see cref="SaveConfirmationMessage"/>
/// itself is deliberately *not* cleared by the reset, so the confirmation (and its
/// <see cref="ViewSavedNpcCommand"/> link) stays visible above the now-empty screen until the GM
/// either clicks that link (navigating away) or starts generating the next NPC, at which point
/// <see cref="Generate"/> clears it -- see that method's own remarks.
/// </para>
/// <para>
/// <b>Saves to <see cref="GeneratingCampaign"/> (the campaign captured once when the NPC was first
/// generated), not necessarily whatever <see cref="ActiveCampaignContext"/> currently reports.</b>
/// This screen is cached for the app's whole lifetime, so a GM could generate an NPC, switch to a
/// different campaign elsewhere in the app, and come back here with the same generated-but-unsaved
/// NPC still on screen -- without this capture, <see cref="SaveAsync"/> would silently attribute the
/// NPC to whichever campaign happened to be active at the moment Save was clicked, not the one the GM
/// actually generated and reviewed it under. <see cref="GeneratingCampaignName"/> shows the GM which
/// campaign it's actually going to (see <see cref="Generate"/>'s remarks for the full reasoning, added
/// during the skeptical review of PR #63) -- mirrors <see cref="NpcFormViewModel"/>'s identical
/// capture-once-at-<c>BeginCreate</c> behavior.
/// </para>
/// <para>
/// <b>No toast/notification infrastructure -- an inline caption plus a real navigation button,
/// same as issue #28's fallback notices.</b> Issue #32 (a real toast component) doesn't exist yet
/// and is out of this issue's scope, same situation #28 was already in for
/// <see cref="NameFallbackNotice"/>/<see cref="RoleFallbackNotice"/>. <see cref="ViewSavedNpcCommand"/>
/// calls <see cref="INavigationService.NavigateTo"/> with <see cref="NavigationDestination.Npcs"/>
/// directly -- it doesn't attempt to deep-link to the specific saved row, since
/// <see cref="NpcsViewModel"/> has no such concept and this issue's acceptance criterion only
/// requires the NPC to "appear in the list", which simply landing on that screen already satisfies.
/// </para>
/// <para>
/// <b>An invalid (blank) <see cref="Name"/> disables <see cref="SaveCommand"/> rather than crashing
/// or silently no-op-ing.</b> <see cref="Name"/> is editable inline (issue #28), so a GM could clear
/// it to blank before saving even though it was always non-blank coming out of the generator itself.
/// <see cref="NameError"/> revalidates on every <see cref="Name"/> change by going through
/// <see cref="Npc.Name"/>'s own setter -- exactly mirroring <see cref="NpcFormViewModel.ValidateName"/>
/// -- and <see cref="CanSave"/> (bound to <see cref="SaveCommand"/>'s <c>CanExecute</c>) is
/// <c>false</c> whenever <see cref="NameError"/> is non-null, so the disabled Save button itself is
/// the feedback; <see cref="SaveAsync"/> also re-validates as its first act (defense in depth,
/// mirroring <see cref="NpcFormViewModel.ConfirmDeleteAsync"/>'s identical re-check) since
/// <see cref="CommunityToolkit.Mvvm.Input.IAsyncRelayCommand.ExecuteAsync"/> does not itself call
/// <c>CanExecute</c> first.
/// </para>
/// <para>
/// <b><see cref="FactionSuggestions"/>/<see cref="LocationSuggestions"/> mirror
/// <see cref="NpcFormViewModel"/>'s free-text autocomplete pattern, not <see cref="NpcsViewModel"/>'s
/// constrained "All plus existing values" filter dropdowns.</b> Built from every NPC currently in
/// the active campaign (via <see cref="INpcRepository.GetByCampaignAsync"/>), same "distinct,
/// non-blank, alphabetical" idiom as <see cref="NpcFormViewModel.LoadSuggestionsAsync"/>'s
/// <c>DistinctNonBlank</c>. Unlike that class (which only ever needs to (re)build suggestions when
/// its own <c>BeginCreate</c>/<c>BeginEdit</c> is called), this view model is cached for the app's
/// whole lifetime once the Generator screen has been visited once (see this class's remarks below on
/// <see cref="_random"/>'s identical lifetime) -- so suggestions are also rebuilt whenever
/// <see cref="ActiveCampaignContext.ActiveCampaignChanged"/> fires (e.g. the GM switches to a
/// different campaign and later comes back to this screen), not just once at construction time.
/// </para>
/// <para>
/// <b>One shared <see cref="IRandomSource"/> for this view model's entire lifetime, not one per
/// click.</b> <see cref="SystemRandomSource"/>'s default (unseeded) constructor wraps
/// <see cref="System.Random"/>'s own default constructor, which seeds itself from a low-resolution
/// clock tick -- constructing a fresh one on every button click could produce identical sequences
/// for rapid repeated clicks, which would be a real (if subtle) bug for a screen whose entire point
/// is "click Generate/reroll repeatedly and get different results every time". <see cref="_random"/>
/// is constructed once in the constructor and reused by every <see cref="GenerateCommand"/>/reroll
/// call for as long as this instance lives -- which, per <c>NavigationService</c>'s per-destination
/// view model caching, is the app's whole lifetime once the Generator screen has been visited once.
/// </para>
/// <para>
/// <b>Locking a field blocks its own reroll button too, not just the whole-NPC <see cref="GenerateCommand"/>.</b>
/// The alternative -- letting a field's own reroll button ignore its lock, since the GM explicitly
/// clicked it -- was considered and rejected: it would give "locked" two different meanings depending
/// on which button the GM presses (blocks <see cref="GenerateCommand"/> but not its own reroll
/// button), which is a subtler, easier-to-misremember rule than "locked means untouched, full stop,
/// until you unlock it". Concretely, <see cref="RerollNameCommand"/>/<see cref="RerollRoleCommand"/>/
/// <see cref="RerollAppearanceCommand"/>/<see cref="RerollMannerismCommand"/>/
/// <see cref="RerollMotivationCommand"/>/<see cref="RerollSecretCommand"/> are all only enabled
/// (see <c>GeneratorView.axaml</c>'s <c>IsEnabled="{Binding !IsXLocked}"</c> bindings) while their
/// field is unlocked, and each also re-checks its own lock flag before doing anything -- defense in
/// depth mirroring <see cref="NpcFormViewModel.ConfirmDeleteAsync"/>'s identical re-check, since a
/// bound <c>IsEnabled</c> is a UI-layer guard, not a guarantee about how the command could be invoked.
/// </para>
/// <para>
/// <b>Per-field reroll never touches any other field.</b> Each <c>RerollXCommand</c> calls exactly
/// one of <see cref="INpcGenerator.GenerateField(NpcField, IRandomSource)"/>/
/// <see cref="INpcGenerator.GenerateField(NpcField, IRandomSource, GeneratorConstraints)"/> for its
/// own field only and assigns the result to that field's own property -- no other field's property
/// is read or written by any single-field reroll. <see cref="GenerateCommand"/> (the "regenerate
/// everything" button) is the only command that touches more than one field, and even it explicitly
/// skips any field whose lock flag is set rather than rerolling and coincidentally leaving it
/// unchanged -- see its own remarks.
/// </para>
/// <para>
/// <b>Constraints (<see cref="SelectedNameCulture"/>/<see cref="SelectedOccupationCategory"/>) only
/// ever apply to <see cref="Name"/>/<see cref="Role"/></b>, per <see cref="GeneratorConstraints"/>'s
/// own design -- every other field's reroll/generate always uses the plain unconstrained
/// <see cref="INpcGenerator.GenerateField(NpcField, IRandomSource)"/> overload. A successful
/// constrained draw clears the corresponding fallback notice (<see cref="NameFallbackNotice"/>/
/// <see cref="RoleFallbackNotice"/>); a fallback draw (the requested culture/category didn't match
/// anything) sets it, and it stays visible until that same field is generated or rerolled again --
/// this is issue #27's <see cref="GenerationResult.FallbackNotice"/> becoming visible to the GM for
/// the first time, shown here as an inline caption near the relevant field (see
/// <c>GeneratorView.axaml</c>) rather than via any toast/notification infrastructure, which doesn't
/// exist yet (issue #32) and is out of this issue's scope regardless.
/// </para>
/// <para>
/// <b>Occupation category options are derived by loading the embedded tables a second time via
/// <see cref="GeneratorTableLoader.LoadAll"/>, not through <see cref="IGeneratorRegistry"/>.</b>
/// <see cref="IGeneratorRegistry"/> only exposes generators that can each produce one value; there is
/// currently no member that enumerates a category's available tag values (unlike
/// <see cref="IGeneratorRegistry.GetNameGenerator"/>'s <see cref="NameGenerator.Cultures"/>, which
/// already exists for the exact same purpose on the "names" side). Rather than add a new
/// registry/<see cref="TableGenerator"/> member purely to support this one screen,
/// <see cref="LoadOccupationCategoryOptions"/> calls the same public, dependency-free
/// <see cref="GeneratorTableLoader.LoadAll"/> static method <see cref="GeneratorRegistry.FromEmbeddedTables"/>
/// itself is built from, finds the "occupation"-category table, and collects its entries' distinct
/// <see cref="GeneratorTableEntry.Tags"/> -- the same "distinct, non-blank, alphabetical" derivation
/// idiom as <see cref="NpcsViewModel.RebuildFilterOptions"/>'s <c>DistinctNonBlank</c>. This does mean
/// the embedded JSON is parsed twice at startup (once for the shared <see cref="IGeneratorRegistry"/>
/// singleton, once here) -- a one-time, constructor-time cost paid once per app run, not per click,
/// and small enough at this app's data scale not to be worth threading a new registry member through
/// for. The "occupation" category name itself is not a new hardcoded fact introduced here: it already
/// appears in <see cref="NpcGenerator"/>'s own private <c>CategoryFor</c> mapping for
/// <see cref="NpcField.Role"/>; this method just needs the same string a second time to find the same
/// table by a different route.
/// </para>
/// </remarks>
public sealed partial class GeneratorViewModel : ViewModelBase
{
    /// <summary>Sentinel option meaning "no preference" for <see cref="SelectedNameCulture"/>/
    /// <see cref="SelectedOccupationCategory"/> -- always first in <see cref="NameCultureOptions"/>/
    /// <see cref="OccupationCategoryOptions"/>. Named "Any" rather than reusing
    /// <see cref="NpcsViewModel.AllOption"/>'s "All": these are generation preferences ("any culture
    /// is fine"), not list filters ("show every row"), even though the "always-present sentinel
    /// first in the dropdown" idiom is the same.</summary>
    public const string AnyOption = "Any";

    private const string OccupationCategory = "occupation";

    private readonly INpcGenerator _npcGenerator;
    private readonly INpcRepository _npcRepository;
    private readonly ActiveCampaignContext _activeCampaignContext;
    private readonly INavigationService _navigationService;

    // Constructed once and reused for this instance's entire lifetime -- see this class's remarks.
    private readonly IRandomSource _random;

    private bool _canSave;

    public GeneratorViewModel(IGeneratorRegistry registry, INpcGenerator npcGenerator, INpcRepository npcRepository, ActiveCampaignContext activeCampaignContext, INavigationService navigationService)
        : this(registry, npcGenerator, npcRepository, activeCampaignContext, navigationService, new SystemRandomSource())
    {
    }

    /// <summary>Test-only seam letting <c>GmToolkit.UI.Tests</c> supply a seeded
    /// <see cref="IRandomSource"/> for deterministic assertions (see <c>AssemblyInfo.cs</c>'s
    /// <c>InternalsVisibleTo</c>) -- the public constructor above always uses real,
    /// non-deterministic randomness (see this class's remarks on why) and is what both heads
    /// actually resolve via DI.</summary>
    internal GeneratorViewModel(IGeneratorRegistry registry, INpcGenerator npcGenerator, INpcRepository npcRepository, ActiveCampaignContext activeCampaignContext, INavigationService navigationService, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(npcGenerator);
        ArgumentNullException.ThrowIfNull(npcRepository);
        ArgumentNullException.ThrowIfNull(activeCampaignContext);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(random);

        _npcGenerator = npcGenerator;
        _npcRepository = npcRepository;
        _activeCampaignContext = activeCampaignContext;
        _navigationService = navigationService;
        _random = random;

        NameCultureOptions = new ObservableCollection<string>(
            new[] { AnyOption }.Concat(registry.GetNameGenerator().Cultures.Where(c => !string.IsNullOrWhiteSpace(c))));
        OccupationCategoryOptions = new ObservableCollection<string>(
            new[] { AnyOption }.Concat(LoadOccupationCategoryOptions()));

        _activeCampaignContext.ActiveCampaignChanged += OnActiveCampaignChanged;

        if (_activeCampaignContext.ActiveCampaign is { } activeCampaign)
        {
            _ = LoadSuggestionsAsync(activeCampaign.Id);
        }
    }

    /// <summary>Design-time-only constructor for the XAML previewer's <c>Design.DataContext</c> (see
    /// <c>GeneratorView.axaml</c>) -- builds a real <see cref="GeneratorRegistry"/>/<see cref="NpcGenerator"/>
    /// over the embedded tables rather than a design-time fake, since (unlike the repository-backed
    /// view models elsewhere in this namespace) neither type touches <c>GmToolkit.Data</c> or anything
    /// else unavailable at design time; <see cref="INpcRepository"/>/<see cref="ActiveCampaignContext"/>/
    /// <see cref="INavigationService"/> (issue #29's new dependencies) do need design-time fakes, same
    /// as every other repository-backed view model's design-time constructor. Never used at runtime;
    /// both heads resolve the constructor above via DI (see
    /// <c>ServiceCollectionExtensions.AddGmToolkitUi</c> and <c>Services.NavigationService</c>).</summary>
    public GeneratorViewModel()
        : this(GeneratorRegistry.FromEmbeddedTables())
    {
    }

    private GeneratorViewModel(IGeneratorRegistry registry)
        : this(registry, new NpcGenerator(registry))
    {
    }

    private GeneratorViewModel(IGeneratorRegistry registry, INpcGenerator npcGenerator)
        : this(registry, npcGenerator, new DesignTimeNpcRepository(), new ActiveCampaignContext(new DesignTimeCampaignRepository()), new DesignTimeNavigationService())
    {
    }

    /// <summary>Whether a full NPC has been generated at least once this session -- gates the
    /// "nothing generated yet" empty state versus the six-field editing view (issue #28's own "two
    /// screen states" design). Set back to <c>false</c> by <see cref="ResetGenerator"/> after a
    /// successful save (issue #29's "generator resets ready for the next one" task) -- otherwise
    /// stays <c>true</c> once set.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GenerateButtonLabel))]
    public partial bool HasGenerated { get; set; }

    /// <summary>The campaign the current in-progress NPC was generated for -- captured once by
    /// <see cref="Generate"/> the moment a brand-new NPC starts, not re-read from whatever's
    /// currently active. <see cref="SaveAsync"/> saves to this campaign, not necessarily whatever the
    /// rest of the app currently shows as active -- see <see cref="Generate"/>'s remarks for why.
    /// Cleared by <see cref="ResetGenerator"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GeneratingCampaignName))]
    public partial Campaign? GeneratingCampaign { get; set; }

    /// <summary><see cref="GeneratingCampaign"/>'s name, shown near the Save button so the GM always
    /// knows which campaign the in-progress NPC will actually be saved to -- even after switching the
    /// active campaign elsewhere in the app while this NPC sits unsaved.</summary>
    public string? GeneratingCampaignName => GeneratingCampaign?.Name;

    /// <summary>"Generate" before anything exists, "Regenerate" once a full NPC is already showing --
    /// purely cosmetic; <see cref="GenerateCommand"/>'s behavior doesn't otherwise depend on which
    /// label is showing.</summary>
    public string GenerateButtonLabel => HasGenerated ? "Regenerate" : "Generate";

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsNameLocked { get; set; }

    /// <summary>Set by the most recent generate/reroll of <see cref="Name"/> when the requested
    /// <see cref="SelectedNameCulture"/> didn't match any known culture and generation fell back to a
    /// random one instead; <c>null</c> otherwise. Cleared (or re-set) by the next generate/reroll of
    /// <see cref="Name"/> specifically -- see this class's remarks.</summary>
    [ObservableProperty]
    public partial string? NameFallbackNotice { get; set; }

    [ObservableProperty]
    public partial string Role { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsRoleLocked { get; set; }

    /// <summary>Same as <see cref="NameFallbackNotice"/>, for <see cref="Role"/>/
    /// <see cref="SelectedOccupationCategory"/>.</summary>
    [ObservableProperty]
    public partial string? RoleFallbackNotice { get; set; }

    [ObservableProperty]
    public partial string Appearance { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsAppearanceLocked { get; set; }

    [ObservableProperty]
    public partial string Mannerism { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsMannerismLocked { get; set; }

    [ObservableProperty]
    public partial string Motivation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsMotivationLocked { get; set; }

    [ObservableProperty]
    public partial string Secret { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSecretLocked { get; set; }

    /// <summary><see cref="AnyOption"/> or one of <see cref="NameCultureOptions"/>'s real culture
    /// values -- consulted only when generating/rerolling <see cref="Name"/> (see
    /// <see cref="BuildConstraints"/>).</summary>
    [ObservableProperty]
    public partial string SelectedNameCulture { get; set; } = AnyOption;

    /// <summary><see cref="AnyOption"/> or one of <see cref="OccupationCategoryOptions"/>'s real
    /// category tag values -- consulted only when generating/rerolling <see cref="Role"/> (see
    /// <see cref="BuildConstraints"/>).</summary>
    [ObservableProperty]
    public partial string SelectedOccupationCategory { get; set; } = AnyOption;

    /// <summary><see cref="AnyOption"/> plus every name culture the registered <see cref="NameGenerator"/>
    /// knows about (<see cref="NameGenerator.Cultures"/>) -- built once in the constructor, since the
    /// set of embedded name culture tables doesn't change at runtime.</summary>
    [ObservableProperty]
    public partial ObservableCollection<string> NameCultureOptions { get; set; } = [];

    /// <summary><see cref="AnyOption"/> plus every distinct occupation category tag found in the
    /// embedded "occupation" table -- see this class's remarks for how these are derived. Built once
    /// in the constructor for the same reason as <see cref="NameCultureOptions"/>.</summary>
    [ObservableProperty]
    public partial ObservableCollection<string> OccupationCategoryOptions { get; set; } = [];

    /// <summary>Save-time-only input (issue #29) -- never produced or touched by
    /// <see cref="GenerateCommand"/>/any reroll command, since there is no faction generator table.
    /// See this class's remarks.</summary>
    [ObservableProperty]
    public partial string Faction { get; set; } = string.Empty;

    /// <summary>Save-time-only input (issue #29) -- see <see cref="Faction"/>'s remarks; there is no
    /// location generator table either.</summary>
    [ObservableProperty]
    public partial string Location { get; set; } = string.Empty;

    /// <summary>Distinct, non-blank <see cref="Npc.Faction"/> values already present across the
    /// active campaign's NPCs -- see this class's remarks. Rebuilt at construction time and whenever
    /// <see cref="ActiveCampaignContext.ActiveCampaignChanged"/> fires.</summary>
    [ObservableProperty]
    public partial ObservableCollection<string> FactionSuggestions { get; set; } = [];

    /// <summary>Distinct, non-blank <see cref="Npc.Location"/> values already present across the
    /// active campaign's NPCs -- see this class's remarks. Rebuilt at construction time and whenever
    /// <see cref="ActiveCampaignContext.ActiveCampaignChanged"/> fires.</summary>
    [ObservableProperty]
    public partial ObservableCollection<string> LocationSuggestions { get; set; } = [];

    /// <summary>First validation message for <see cref="Name"/> at save time, or <c>null</c> if it's
    /// currently valid -- mirrors <see cref="NpcFormViewModel.NameError"/>. Revalidated on every
    /// <see cref="Name"/> change (see <see cref="OnNameChanged(string)"/>), including ones made by
    /// generate/reroll, not just the GM's own inline edits.</summary>
    [ObservableProperty]
    public partial string? NameError { get; set; }

    /// <summary>Set if <see cref="SaveAsync"/>'s defensive catch is ever actually hit; <c>null</c>
    /// otherwise. Mirrors <see cref="NpcFormViewModel.SaveError"/>.</summary>
    [ObservableProperty]
    public partial string? SaveError { get; set; }

    /// <summary>Set by <see cref="SaveAsync"/> after a successful save (e.g. "Saved 'Baelor the
    /// Butcher' to 'Shadows Over Blackmoor'."); <c>null</c> before the first save this session, or once
    /// <see cref="Generate"/> clears it for the next NPC. Deliberately *not* cleared by
    /// <see cref="ResetGenerator"/> itself -- see this class's remarks on why the confirmation
    /// outlives the reset.</summary>
    [ObservableProperty]
    public partial string? SaveConfirmationMessage { get; set; }

    /// <summary>Whether <see cref="SaveCommand"/> can currently execute -- <c>false</c> whenever
    /// <see cref="NameError"/> is set. Mirrors <see cref="NpcFormViewModel.CanSave"/>'s
    /// <c>SetProperty</c>-plus-<c>NotifyCanExecuteChanged</c> shape.</summary>
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

    /// <summary>
    /// Generates a brand-new NPC, or (once <see cref="HasGenerated"/> is already <c>true</c>)
    /// regenerates every field except whichever are currently locked -- issue #28's own acceptance
    /// criterion: "lock the name, regenerate, the name is unchanged, everything else is new". Each
    /// unlocked field is regenerated by calling this same field's own <c>RerollXField</c> helper (the
    /// same code path its individual reroll button uses), so this command can't drift from "redraw
    /// every unlocked field" into "redraw the whole NPC and then restore the locked fields' old
    /// values" -- the latter would still satisfy a naive reading of the acceptance criterion while
    /// actually re-rolling every field (including locked ones) before discarding those extra rolls,
    /// which is wasteful and, if a field's generator ever gained an observable side effect, wrong.
    /// Also clears <see cref="SaveConfirmationMessage"/> (issue #29) -- a confirmation from a
    /// previously saved NPC would otherwise keep showing above the newly-generating one, which would
    /// misleadingly read as if it referred to whatever is currently on screen.
    /// </summary>
    /// <remarks>
    /// Captures <see cref="GeneratingCampaign"/> here, the first time a brand-new NPC starts (not on
    /// every subsequent regenerate) -- caught during the skeptical review of PR #63: this screen is
    /// cached for the app's whole lifetime and deliberately does not reset an in-progress NPC when
    /// the active campaign changes (see <see cref="HandleActiveCampaignChanged"/>'s remarks), so
    /// without capturing which campaign a generated-but-unsaved NPC actually belongs to,
    /// <see cref="SaveAsync"/> would save it to whatever campaign happened to be active at the moment
    /// Save was clicked -- possibly a different one than the GM was looking at while generating and
    /// reviewing it. Capturing once here and using that captured campaign in <see cref="SaveAsync"/>
    /// mirrors <see cref="NpcFormViewModel"/>'s identical "capture once at <c>BeginCreate</c>, unaffected
    /// by a later campaign switch" behavior.
    /// </remarks>
    [RelayCommand]
    private void Generate()
    {
        if (!HasGenerated)
        {
            GeneratingCampaign = _activeCampaignContext.ActiveCampaign;
        }

        HasGenerated = true;
        SaveConfirmationMessage = null;

        if (!IsNameLocked)
        {
            RerollNameField();
        }

        if (!IsRoleLocked)
        {
            RerollRoleField();
        }

        if (!IsAppearanceLocked)
        {
            RerollAppearanceField();
        }

        if (!IsMannerismLocked)
        {
            RerollMannerismField();
        }

        if (!IsMotivationLocked)
        {
            RerollMotivationField();
        }

        if (!IsSecretLocked)
        {
            RerollSecretField();
        }
    }

    [RelayCommand]
    private void RerollName()
    {
        if (IsNameLocked)
        {
            return;
        }

        RerollNameField();
    }

    [RelayCommand]
    private void RerollRole()
    {
        if (IsRoleLocked)
        {
            return;
        }

        RerollRoleField();
    }

    [RelayCommand]
    private void RerollAppearance()
    {
        if (IsAppearanceLocked)
        {
            return;
        }

        RerollAppearanceField();
    }

    [RelayCommand]
    private void RerollMannerism()
    {
        if (IsMannerismLocked)
        {
            return;
        }

        RerollMannerismField();
    }

    [RelayCommand]
    private void RerollMotivation()
    {
        if (IsMotivationLocked)
        {
            return;
        }

        RerollMotivationField();
    }

    [RelayCommand]
    private void RerollSecret()
    {
        if (IsSecretLocked)
        {
            return;
        }

        RerollSecretField();
    }

    /// <summary>
    /// Persists the currently-generated NPC into the active campaign (issue #29's own reason for
    /// being) and resets the screen for the next one -- see this class's remarks for the full
    /// design. Re-validates <see cref="Name"/> as its first act, mirroring
    /// <see cref="NpcFormViewModel.ConfirmDeleteAsync"/>'s identical defense-in-depth re-check of its
    /// own <c>CanExecute</c> gate, since <see cref="IAsyncRelayCommand.ExecuteAsync"/> does not itself
    /// call <c>CanExecute</c> first.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        RefreshNameValidation();
        if (!CanSave)
        {
            return;
        }

        // Saves to GeneratingCampaign (captured once by Generate -- see its remarks), not whatever
        // ActiveCampaignContext currently reports: the GM could have switched to a different campaign
        // while this NPC sat generated-but-unsaved, and this NPC belongs to the campaign it was
        // actually generated for, not whatever the rest of the app now shows as active. Caught during
        // the skeptical review of PR #63.
        var savingCampaignId = GeneratingCampaign?.Id;
        if (savingCampaignId is null)
        {
            // Only reachable if Save were somehow invoked before Generate ever ran (Generate always
            // sets GeneratingCampaign the first time HasGenerated becomes true, and the Save button
            // is only visible once HasGenerated is true) -- defense-in-depth only, not expected in
            // practice.
            return;
        }

        SaveError = null;

        try
        {
            var npc = new Npc
            {
                CampaignId = savingCampaignId.Value,
                Name = Name,
                Role = Role,
                Faction = Faction,
                Location = Location,
                Appearance = Appearance,
                Mannerism = Mannerism,
                Motivation = Motivation,
                Secret = Secret,
                WasGenerated = true,
            };

            await _npcRepository.AddAsync(npc);

            var savedName = npc.Name;
            var savedCampaignName = GeneratingCampaign?.Name;
            ResetGenerator();
            SaveConfirmationMessage = string.IsNullOrWhiteSpace(savedCampaignName)
                ? $"Saved '{savedName}' to the campaign."
                : $"Saved '{savedName}' to '{savedCampaignName}'.";

            // Refreshes suggestions for whatever campaign is currently active -- which may differ
            // from savingCampaignId above if the GM switched campaigns before saving -- since that's
            // the campaign the next NPC (if any) will actually be generated for.
            var currentCampaignId = _activeCampaignContext.ActiveCampaign?.Id;
            if (currentCampaignId is not null)
            {
                _ = LoadSuggestionsAsync(currentCampaignId.Value);
            }
        }
        catch (ArgumentException ex)
        {
            // Defensive only -- RefreshNameValidation above already exercises this same domain
            // setter, so this should be unreachable in practice. Mirrors
            // NpcFormViewModel.SaveAsync's identical defensive catch.
            SaveError = ex.Message;
        }
        catch (Exception ex)
        {
            SaveError = $"Couldn't save this NPC: {ex.Message}";
        }
    }

    /// <summary>Navigates to the NPCs list so the GM can see the just-saved NPC there -- see this
    /// class's remarks for why this doesn't attempt to deep-link to the specific row.</summary>
    [RelayCommand]
    private void ViewSavedNpc() => _navigationService.NavigateTo(NavigationDestination.Npcs);

    /// <summary>
    /// Clears every generated field, lock flag and fallback notice back to
    /// <see cref="GeneratorViewModel"/>'s own "nothing generated yet" state (issue #29's "generator
    /// resets ready for the next one" task) -- but deliberately leaves
    /// <see cref="SaveConfirmationMessage"/> untouched; see this class's remarks for why.
    /// </summary>
    private void ResetGenerator()
    {
        HasGenerated = false;
        GeneratingCampaign = null;

        Name = string.Empty;
        IsNameLocked = false;
        NameFallbackNotice = null;

        Role = string.Empty;
        IsRoleLocked = false;
        RoleFallbackNotice = null;

        Appearance = string.Empty;
        IsAppearanceLocked = false;

        Mannerism = string.Empty;
        IsMannerismLocked = false;

        Motivation = string.Empty;
        IsMotivationLocked = false;

        Secret = string.Empty;
        IsSecretLocked = false;

        Faction = string.Empty;
        Location = string.Empty;
    }

    /// <summary>Revalidates <see cref="NameError"/>/<see cref="CanSave"/> against <see cref="Name"/>'s
    /// current value -- runs on every change to <see cref="Name"/>, whether from the GM's own typing
    /// or a generate/reroll assigning a new generated value.</summary>
    partial void OnNameChanged(string value) => RefreshNameValidation();

    private void RefreshNameValidation()
    {
        NameError = ValidateName(Name);
        CanSave = NameError is null;
    }

    /// <summary>
    /// Validates <paramref name="name"/> by actually going through <see cref="Npc.Name"/>'s own
    /// setter and surfacing whatever message its <see cref="ArgumentException"/> carries -- exactly
    /// mirroring <see cref="NpcFormViewModel.ValidateName"/> for the same reason (one place -- the
    /// domain model's setter -- owns what makes a name valid).
    /// </summary>
    private static string? ValidateName(string name)
    {
        try
        {
            _ = new Npc { Name = name };
            return null;
        }
        catch (ArgumentException ex)
        {
            return StripParameterSuffix(ex);
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
    /// the constructor/<see cref="HandleActiveCampaignChanged"/>/<see cref="SaveAsync"/>; a failure
    /// here is not blocking (it just leaves the suggestion list as it was), since autocomplete hints
    /// are a convenience, not a requirement for saving.
    /// </summary>
    /// <remarks>
    /// Guards its own result against the active campaign having moved on by the time the repository
    /// call completes (e.g. the GM switched to a different campaign before this load finished) --
    /// mirrors <see cref="NpcFormViewModel.LoadSuggestionsAsync"/>'s identical guard, caught during
    /// the skeptical review of PR #58.
    /// </remarks>
    private async Task LoadSuggestionsAsync(Guid campaignId)
    {
        try
        {
            var npcs = await _npcRepository.GetByCampaignAsync(campaignId);
            if (campaignId != _activeCampaignContext.ActiveCampaign?.Id)
            {
                return;
            }

            FactionSuggestions = new ObservableCollection<string>(DistinctNonBlank(npcs.Select(npc => npc.Faction)));
            LocationSuggestions = new ObservableCollection<string>(DistinctNonBlank(npcs.Select(npc => npc.Location)));
        }
        catch
        {
            if (campaignId != _activeCampaignContext.ActiveCampaign?.Id)
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

    private void OnActiveCampaignChanged() => Dispatcher.UIThread.Post(HandleActiveCampaignChanged);

    /// <summary>
    /// The actual work done in response to <see cref="ActiveCampaignContext.ActiveCampaignChanged"/>,
    /// factored out of <see cref="OnActiveCampaignChanged"/> so it's callable directly (bypassing
    /// <see cref="Dispatcher.UIThread"/>) from tests that don't run inside an Avalonia dispatcher
    /// loop -- mirrors <c>NpcsViewModel.HandleActiveCampaignChanged</c> (see <c>InternalsVisibleTo</c>
    /// in <c>AssemblyInfo.cs</c>). Not called directly by application code.
    /// </summary>
    /// <remarks>
    /// Only rebuilds <see cref="FactionSuggestions"/>/<see cref="LocationSuggestions"/> -- unlike
    /// <c>NpcsViewModel.HandleActiveCampaignChanged</c>, this view model has no list to reload and
    /// deliberately does not reset any in-progress generated NPC or clear
    /// <see cref="SaveConfirmationMessage"/> just because the GM briefly looked at a different
    /// campaign; only <see cref="Generate"/>/<see cref="SaveAsync"/> do that. This does NOT mean a
    /// campaign switch can misattribute an in-progress NPC to the wrong campaign at save time --
    /// <see cref="GeneratingCampaign"/> was already captured once by <see cref="Generate"/> before any
    /// switch could happen, and <see cref="SaveAsync"/> saves to that captured campaign regardless of
    /// what this method (or <see cref="ActiveCampaignContext"/>) reports as active by the time Save is
    /// actually clicked -- see <see cref="Generate"/>'s remarks for the full reasoning (added after the
    /// skeptical review of PR #63 found the original version of this comment only addressed "don't
    /// destroy in-progress work" and not "save to the right campaign").
    /// </remarks>
    internal void HandleActiveCampaignChanged()
    {
        var campaignId = _activeCampaignContext.ActiveCampaign?.Id;
        if (campaignId is null)
        {
            FactionSuggestions = [];
            LocationSuggestions = [];
            return;
        }

        _ = LoadSuggestionsAsync(campaignId.Value);
    }

    private void RerollNameField()
    {
        var result = _npcGenerator.GenerateField(NpcField.Name, _random, BuildConstraints());
        Name = result.Value;
        NameFallbackNotice = result.FallbackNotice;
    }

    private void RerollRoleField()
    {
        var result = _npcGenerator.GenerateField(NpcField.Role, _random, BuildConstraints());
        Role = result.Value;
        RoleFallbackNotice = result.FallbackNotice;
    }

    private void RerollAppearanceField() => Appearance = _npcGenerator.GenerateField(NpcField.Appearance, _random);

    private void RerollMannerismField() => Mannerism = _npcGenerator.GenerateField(NpcField.Mannerism, _random);

    private void RerollMotivationField() => Motivation = _npcGenerator.GenerateField(NpcField.Motivation, _random);

    private void RerollSecretField() => Secret = _npcGenerator.GenerateField(NpcField.Secret, _random);

    private GeneratorConstraints BuildConstraints() => new()
    {
        NameCulture = SelectedNameCulture == AnyOption ? null : SelectedNameCulture,
        OccupationCategory = SelectedOccupationCategory == AnyOption ? null : SelectedOccupationCategory,
    };

    /// <summary>See this class's remarks for why this loads the embedded tables directly rather than
    /// going through <see cref="IGeneratorRegistry"/>.</summary>
    private static IEnumerable<string> LoadOccupationCategoryOptions()
    {
        var tables = GeneratorTableLoader.LoadAll();
        var occupationTable = tables.FirstOrDefault(
            table => string.Equals(table.Category, OccupationCategory, StringComparison.OrdinalIgnoreCase));

        if (occupationTable is null)
        {
            return [];
        }

        return occupationTable.Entries
            .SelectMany(entry => entry.Tags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase);
    }
}