using System;
using System.Collections.Generic;
using System.ComponentModel;

using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Services;

/// <inheritdoc cref="INavigationService" />
/// <remarks>
/// Deliberately knows nothing about which destinations require an active campaign, or about
/// <see cref="GmToolkit.Core.Services.ActiveCampaignContext"/> at all — that gating policy lives
/// in <see cref="ShellViewModel"/>, which owns both this service and the active-campaign
/// subscription. Keeping this service policy-free keeps it small and independently testable.
/// </remarks>
public sealed class NavigationService : INavigationService
{
    private static readonly IReadOnlyDictionary<NavigationDestination, Func<ViewModelBase>> Factories =
        new Dictionary<NavigationDestination, Func<ViewModelBase>>
        {
            [NavigationDestination.Campaigns] = () => new CampaignsViewModel(),
            [NavigationDestination.Characters] = () => new CharactersViewModel(),
            [NavigationDestination.Npcs] = () => new NpcsViewModel(),
            [NavigationDestination.Generator] = () => new GeneratorViewModel(),
            [NavigationDestination.Settings] = () => new SettingsViewModel(),
        };

    // Screen view models are created once and cached, so navigating away and back doesn't lose
    // any in-progress state on a screen (relevant once later issues add real forms/filters) --
    // cheap to do now, and avoids having to retrofit it later.
    private readonly Dictionary<NavigationDestination, ViewModelBase> _cache = [];

    public NavigationService()
    {
        CurrentDestination = NavigationDestination.Campaigns;
        CurrentViewModel = GetOrCreate(CurrentDestination);
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
            viewModel = Factories[destination]();
            _cache[destination] = viewModel;
        }

        return viewModel;
    }
}