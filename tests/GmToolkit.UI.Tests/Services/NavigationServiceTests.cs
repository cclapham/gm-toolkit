using System.ComponentModel;

using GmToolkit.UI.Services;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Tests.Services;

public class NavigationServiceTests
{
    [Fact]
    public void Starts_on_Campaigns_with_a_matching_CurrentViewModel()
    {
        var navigationService = new NavigationService();

        Assert.Equal(NavigationDestination.Campaigns, navigationService.CurrentDestination);
        Assert.IsType<CampaignsViewModel>(navigationService.CurrentViewModel);
    }

    [Theory]
    [InlineData(NavigationDestination.Campaigns, typeof(CampaignsViewModel))]
    [InlineData(NavigationDestination.Characters, typeof(CharactersViewModel))]
    [InlineData(NavigationDestination.Npcs, typeof(NpcsViewModel))]
    [InlineData(NavigationDestination.Generator, typeof(GeneratorViewModel))]
    [InlineData(NavigationDestination.Settings, typeof(SettingsViewModel))]
    public void NavigateTo_sets_CurrentDestination_and_a_matching_CurrentViewModel(NavigationDestination destination, Type expectedViewModelType)
    {
        var navigationService = new NavigationService();

        navigationService.NavigateTo(destination);

        Assert.Equal(destination, navigationService.CurrentDestination);
        Assert.IsType(expectedViewModelType, navigationService.CurrentViewModel);
    }

    [Fact]
    public void NavigateTo_a_new_destination_raises_PropertyChanged_for_both_properties()
    {
        var navigationService = new NavigationService();
        var raisedProperties = new List<string?>();
        navigationService.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName);

        navigationService.NavigateTo(NavigationDestination.Settings);

        Assert.Contains(nameof(INavigationService.CurrentDestination), raisedProperties);
        Assert.Contains(nameof(INavigationService.CurrentViewModel), raisedProperties);
    }

    [Fact]
    public void NavigateTo_the_current_destination_is_a_no_op_and_does_not_raise_PropertyChanged()
    {
        var navigationService = new NavigationService();
        var raiseCount = 0;
        navigationService.PropertyChanged += (_, _) => raiseCount++;

        navigationService.NavigateTo(NavigationDestination.Campaigns);

        Assert.Equal(0, raiseCount);
    }

    [Fact]
    public void NavigateTo_the_same_destination_twice_returns_the_same_cached_view_model_instance()
    {
        var navigationService = new NavigationService();

        navigationService.NavigateTo(NavigationDestination.Characters);
        var firstInstance = navigationService.CurrentViewModel;
        navigationService.NavigateTo(NavigationDestination.Settings);
        navigationService.NavigateTo(NavigationDestination.Characters);
        var secondInstance = navigationService.CurrentViewModel;

        Assert.Same(firstInstance, secondInstance);
    }
}