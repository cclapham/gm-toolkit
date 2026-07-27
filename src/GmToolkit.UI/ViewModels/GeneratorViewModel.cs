using System.Collections.ObjectModel;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using GmToolkit.Core.Generator;

namespace GmToolkit.UI.ViewModels;

/// <summary>
/// The NPC Generator screen (issue #28) -- "the feature the app exists for". Generates a full
/// <see cref="GeneratedNpc"/> in one action, lets the GM reroll or lock any one of its six fields
/// independently, edit any field's current value inline, and constrain <see cref="Role"/>/
/// <see cref="Name"/> generation by occupation category / name culture. Only reachable when a
/// campaign is active -- see <c>ShellViewModel</c>'s gating logic -- but this class itself has no
/// dependency on which campaign is active: it only produces a <see cref="GeneratedNpc"/> in memory,
/// never persists anything. Saving a generated NPC into the active campaign is #29's job, not built
/// here -- see this class's remarks.
/// </summary>
/// <remarks>
/// <para>
/// <b>No save step, on purpose.</b> Issue #28's own scope note: persisting a generated NPC into the
/// active campaign is issue #29, not yet built. This class only ever holds its six fields in memory;
/// there is deliberately no command here that reaches an <c>INpcRepository</c>.
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

    // Constructed once and reused for this instance's entire lifetime -- see this class's remarks.
    private readonly IRandomSource _random;

    public GeneratorViewModel(IGeneratorRegistry registry, INpcGenerator npcGenerator)
        : this(registry, npcGenerator, new SystemRandomSource())
    {
    }

    /// <summary>Test-only seam letting <c>GmToolkit.UI.Tests</c> supply a seeded
    /// <see cref="IRandomSource"/> for deterministic assertions (see <c>AssemblyInfo.cs</c>'s
    /// <c>InternalsVisibleTo</c>) -- the public constructor above always uses real,
    /// non-deterministic randomness (see this class's remarks on why) and is what both heads
    /// actually resolve via DI.</summary>
    internal GeneratorViewModel(IGeneratorRegistry registry, INpcGenerator npcGenerator, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(npcGenerator);
        ArgumentNullException.ThrowIfNull(random);

        _npcGenerator = npcGenerator;
        _random = random;

        NameCultureOptions = new ObservableCollection<string>(
            new[] { AnyOption }.Concat(registry.GetNameGenerator().Cultures.Where(c => !string.IsNullOrWhiteSpace(c))));
        OccupationCategoryOptions = new ObservableCollection<string>(
            new[] { AnyOption }.Concat(LoadOccupationCategoryOptions()));
    }

    /// <summary>Design-time-only constructor for the XAML previewer's <c>Design.DataContext</c> (see
    /// <c>GeneratorView.axaml</c>) -- builds a real <see cref="GeneratorRegistry"/>/<see cref="NpcGenerator"/>
    /// over the embedded tables rather than a design-time fake, since (unlike the repository-backed
    /// view models elsewhere in this namespace) neither type touches <c>GmToolkit.Data</c> or anything
    /// else unavailable at design time. Never used at runtime; both heads resolve the constructor
    /// above via DI (see <c>ServiceCollectionExtensions.AddGmToolkitUi</c> and
    /// <c>Services.NavigationService</c>).</summary>
    public GeneratorViewModel()
        : this(GeneratorRegistry.FromEmbeddedTables())
    {
    }

    private GeneratorViewModel(IGeneratorRegistry registry)
        : this(registry, new NpcGenerator(registry))
    {
    }

    /// <summary>Whether a full NPC has been generated at least once this session -- gates the
    /// "nothing generated yet" empty state versus the six-field editing view (issue #28's own "two
    /// screen states" design). Never reset back to <c>false</c> once set.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GenerateButtonLabel))]
    public partial bool HasGenerated { get; set; }

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
    /// </summary>
    [RelayCommand]
    private void Generate()
    {
        HasGenerated = true;

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