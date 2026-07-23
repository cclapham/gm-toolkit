using System;
using System.Collections.Generic;
using System.ComponentModel;

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
/// (issue #17/#18, extended by #20/#21 for <see cref="IPlayerCharacterRepository"/>) purely to
/// construct <see cref="CampaignsViewModel"/>/<see cref="CharactersViewModel"/> — the alternative
/// (resolving those view models from <see cref="IServiceProvider"/> instead) would mean this
/// service depends on the DI container itself rather than its actual dependencies, which is worse
/// for testability.
/// </remarks>
public sealed class NavigationService : INavigationService
{
    private readonly IReadOnlyDictionary<NavigationDestination, Func<ViewModelBase>> _factories;

    // Screen view models are created once and cached, so navigating away and back doesn't lose
    // any in-progress state on a screen (relevant once later issues add real forms/filters) --
    // cheap to do now, and avoids having to retrofit it later.
    private readonly Dictionary<NavigationDestination, ViewModelBase> _cache = [];

    public NavigationService(ICampaignRepository campaignRepository, IPlayerCharacterRepository playerCharacterRepository, ActiveCampaignContext activeCampaignContext)
    {
        _factories = new Dictionary<NavigationDestination, Func<ViewModelBase>>
        {
            [NavigationDestination.Campaigns] = () => new CampaignsViewModel(campaignRepository, activeCampaignContext),
            [NavigationDestination.Characters] = () => new CharactersViewModel(playerCharacterRepository, activeCampaignContext),
            [NavigationDestination.Npcs] = () => new NpcsViewModel(),
            [NavigationDestination.Generator] = () => new GeneratorViewModel(),
            [NavigationDestination.Settings] = () => new SettingsViewModel(),
        };

        CurrentDestination = NavigationDestination.Campaigns;
        CurrentViewModel = GetOrCreate(CurrentDestination);
    }

    /// <summary>Design-time/test-only constructor -- used by the XAML previewer's
    /// <c>Design.DataContext</c> (via <see cref="ShellViewModel"/>'s own parameterless
    /// constructor) and by tests that don't need a real database. Backed by the same
    /// always-empty, no-op <see cref="DesignTimeCampaignRepository"/>/<see cref="DesignTimePlayerCharacterRepository"/>
    /// used elsewhere for this purpose. Never used at runtime; both heads resolve the constructor
    /// above via DI (see <c>ServiceCollectionExtensions.AddGmToolkitUi</c>).</summary>
    public NavigationService()
        : this(new DesignTimeCampaignRepository(), new DesignTimePlayerCharacterRepository(), new ActiveCampaignContext(new DesignTimeCampaignRepository()))
    {
    }

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