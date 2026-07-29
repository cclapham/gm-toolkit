using System;
using System.Collections.Generic;
using System.ComponentModel;

using GmToolkit.Core.Generator;
using GmToolkit.Core.Repositories;
using GmToolkit.Core.Services;
using GmToolkit.UI.Design;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Services;

/// <inheritdoc cref="INavigationService" />
/// <remarks>
/// Deliberately knows nothing about which destinations require an active campaign, or about
/// <see cref="GmToolkit.Core.Services.ActiveCampaignContext"/>'s gating policy — that lives in
/// <see cref="ShellViewModel"/>, which owns both this service and the active-campaign
/// subscription. It does, however, need <see cref="ICampaignRepository"/>,
/// <see cref="IPlayerCharacterRepository"/> and <see cref="ActiveCampaignContext"/> themselves
/// (issue #17/#18, extended by #20/#21 for <see cref="IPlayerCharacterRepository"/> and by #24 for
/// <see cref="INpcRepository"/>) purely to
/// construct <see cref="CampaignsViewModel"/>/<see cref="CharactersViewModel"/>/<see cref="NpcsViewModel"/> — the alternative
/// (resolving those view models from <see cref="IServiceProvider"/> instead) would mean this
/// service depends on the DI container itself rather than its actual dependencies, which is worse
/// for testability. Issue #28 extends this with <see cref="IGeneratorRegistry"/>/
/// <see cref="INpcGenerator"/>, threaded through the same way to construct
/// <see cref="GeneratorViewModel"/> -- both are registered as singletons by
/// <c>ServiceCollectionExtensions.AddGmToolkitUi</c>, same as every dependency above.
/// </remarks>
/// <remarks>
/// <b>Issue #29's <see cref="GeneratorViewModel"/> factory passes <c>this</c> as its
/// <see cref="INavigationService"/>.</b> Safe specifically because <see cref="_factories"/>' lambdas
/// are stored, not invoked, at the point they're constructed below -- this constructor only ever
/// invokes the <see cref="NavigationDestination.Campaigns"/> factory itself (via
/// <see cref="GetOrCreate"/> for <see cref="CurrentViewModel"/>'s initial value), so by the time the
/// Generator lambda actually runs (the first time a caller navigates there), construction of this
/// <see cref="NavigationService"/> instance has already fully completed and <c>this</c> is a
/// perfectly usable, fully-initialized <see cref="INavigationService"/> -- there is no partially
/// constructed <c>this</c> escaping anywhere.
/// </remarks>
public sealed class NavigationService : INavigationService
{
    private readonly IReadOnlyDictionary<NavigationDestination, Func<ViewModelBase>> _factories;

    // Screen view models are created once and cached, so navigating away and back doesn't lose
    // any in-progress state on a screen (relevant once later issues add real forms/filters) --
    // cheap to do now, and avoids having to retrofit it later.
    private readonly Dictionary<NavigationDestination, ViewModelBase> _cache = [];

    public NavigationService(ICampaignRepository campaignRepository, IPlayerCharacterRepository playerCharacterRepository, INpcRepository npcRepository, IGeneratorRegistry generatorRegistry, INpcGenerator npcGenerator, ActiveCampaignContext activeCampaignContext, IAppSettingsService appSettingsService)
    {
        _factories = new Dictionary<NavigationDestination, Func<ViewModelBase>>
        {
            [NavigationDestination.Campaigns] = () => new CampaignsViewModel(campaignRepository, activeCampaignContext),
            [NavigationDestination.Characters] = () => new CharactersViewModel(playerCharacterRepository, activeCampaignContext),
            [NavigationDestination.Npcs] = () => new NpcsViewModel(npcRepository, activeCampaignContext),
            [NavigationDestination.Generator] = () => new GeneratorViewModel(generatorRegistry, npcGenerator, npcRepository, activeCampaignContext, this),
            [NavigationDestination.Settings] = () => new SettingsViewModel(appSettingsService),
        };

        CurrentDestination = NavigationDestination.Campaigns;
        CurrentViewModel = GetOrCreate(CurrentDestination);
    }

    /// <summary>Design-time/test-only constructor -- used by the XAML previewer's
    /// <c>Design.DataContext</c> (via <see cref="ShellViewModel"/>'s own parameterless
    /// constructor) and by tests that don't need a real database. Backed by the same
    /// always-empty, no-op <see cref="DesignTimeCampaignRepository"/>/<see cref="DesignTimePlayerCharacterRepository"/>/
    /// <see cref="DesignTimeNpcRepository"/>/<see cref="DesignTimeAppSettingsService"/> used
    /// elsewhere for this purpose, plus a real <see cref="GeneratorRegistry"/>/<see cref="NpcGenerator"/>
    /// over the embedded tables (see <see cref="GeneratorViewModel"/>'s own design-time constructor
    /// for why those two don't need a design-time fake). Never used at
    /// runtime; both heads resolve the constructor above via DI (see
    /// <c>ServiceCollectionExtensions.AddGmToolkitUi</c>).</summary>
    public NavigationService()
        : this(new DesignTimeCampaignRepository(), new DesignTimePlayerCharacterRepository(), new DesignTimeNpcRepository(), GeneratorRegistry.FromEmbeddedTables(), CreateDesignTimeNpcGenerator(), new ActiveCampaignContext(new DesignTimeCampaignRepository()), new DesignTimeAppSettingsService())
    {
    }

    private static INpcGenerator CreateDesignTimeNpcGenerator() => new NpcGenerator(GeneratorRegistry.FromEmbeddedTables());

    public NavigationDestination CurrentDestination { get; private set; }

    public ViewModelBase CurrentViewModel { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NavigateTo(NavigationDestination destination)
    {
        if (destination == CurrentDestination)
        {
            return;
        }

        CurrentDestination = destination;
        CurrentViewModel = GetOrCreate(destination);

        // Issue #68: refresh whatever's showing every time it's navigated to -- including the very
        // first time, when GetOrCreate above just constructed it -- so a screen that was left
        // cached (see _cache's own remark) never goes on showing stale data after some other
        // screen's action changed the same underlying data out from under it (e.g. GeneratorViewModel
        // saving a generated NPC directly via INpcRepository.AddAsync, bypassing NpcsViewModel's own
        // Form entirely -- see IRefreshable's remarks). Fire-and-forget, same idiom as these view
        // models' own constructor-time initial load; errors surface via each one's own LoadError.
        if (CurrentViewModel is IRefreshable refreshable)
        {
            _ = refreshable.RefreshAsync();
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentDestination)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentViewModel)));
    }

    private ViewModelBase GetOrCreate(NavigationDestination destination)
    {
        if (!_cache.TryGetValue(destination, out var viewModel))
        {
            viewModel = _factories[destination]();
            _cache[destination] = viewModel;
        }

        return viewModel;
    }
}