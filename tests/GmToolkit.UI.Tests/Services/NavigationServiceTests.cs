using System.ComponentModel;

using GmToolkit.Core.Generator;
using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;
using GmToolkit.Core.Services;
using GmToolkit.UI.Services;
using GmToolkit.UI.Tests.Fakes;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Tests.Services;

public class NavigationServiceTests
{
    /// <summary>Builds a real <see cref="NavigationService"/> (not the design-time parameterless
    /// constructor) over in-memory fakes, with <paramref name="campaign"/> already selected active
    /// -- lets issue #68's tests below drive <see cref="NavigationService.NavigateTo"/> against
    /// repositories they can mutate directly between navigations, the same way
    /// <see cref="GmToolkit.UI.ViewModels.GeneratorViewModel"/>'s real save flow does.</summary>
    private static (NavigationService NavigationService, INpcRepository NpcRepository) CreateWithActiveCampaign(Campaign campaign)
    {
        var campaignRepository = new FakeCampaignRepository(campaign);
        var playerCharacterRepository = new FakePlayerCharacterRepository();
        var npcRepository = new FakeNpcRepository();
        var registry = GeneratorRegistry.FromEmbeddedTables();
        var npcGenerator = new NpcGenerator(registry);
        var activeCampaignContext = new ActiveCampaignContext(campaignRepository);
        activeCampaignContext.SelectCampaignAsync(campaign).GetAwaiter().GetResult();

        var navigationService = new NavigationService(
            campaignRepository,
            playerCharacterRepository,
            npcRepository,
            registry,
            npcGenerator,
            activeCampaignContext,
            new FakeAppSettingsService());

        return (navigationService, npcRepository);
    }


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

    [Fact]
    public async Task NavigateTo_refreshes_an_already_cached_screen_that_implements_IRefreshable()
    {
        // Issue #68: navigating away from Npcs and back must not go on showing whatever it showed
        // the first time it was cached -- proves NavigationService itself (not just the view model
        // in isolation) actually triggers the refresh.
        var campaign = new Campaign { Name = "Wandering Souls" };
        var (navigationService, npcRepository) = CreateWithActiveCampaign(campaign);

        navigationService.NavigateTo(NavigationDestination.Npcs);
        var npcsViewModel = Assert.IsType<NpcsViewModel>(navigationService.CurrentViewModel);
        Assert.True(npcsViewModel.IsEmpty);

        navigationService.NavigateTo(NavigationDestination.Settings);
        await npcRepository.AddAsync(new Npc { CampaignId = campaign.Id, Name = "Generated Ghoul" });
        navigationService.NavigateTo(NavigationDestination.Npcs);

        // NpcsViewModel.RefreshAsync (invoked fire-and-forget from NavigateTo) races the assertions
        // below against FakeNpcRepository, which completes synchronously -- so by the time
        // NavigateTo returns, the refresh has already finished (mirrors every other test in this
        // suite's identical "fire-and-forget over a synchronous fake" reasoning).
        Assert.Same(npcsViewModel, navigationService.CurrentViewModel);
        Assert.False(npcsViewModel.IsEmpty);
        Assert.Equal("Generated Ghoul", npcsViewModel.Npcs.Single().Name);
    }

    [Fact]
    public async Task Acceptance_generating_and_saving_an_npc_then_navigating_to_Npcs_shows_it_without_reselecting_the_campaign()
    {
        // The exact scenario from issue #68's Acceptance section: generate an NPC on the Generator
        // screen, save it, navigate directly to NPCs (no active-campaign reselect) -- the new NPC
        // must appear.
        var campaign = new Campaign { Name = "Wandering Souls" };
        var (navigationService, _) = CreateWithActiveCampaign(campaign);

        navigationService.NavigateTo(NavigationDestination.Npcs);
        var npcsViewModel = Assert.IsType<NpcsViewModel>(navigationService.CurrentViewModel);
        Assert.True(npcsViewModel.IsEmpty);

        navigationService.NavigateTo(NavigationDestination.Generator);
        var generatorViewModel = Assert.IsType<GeneratorViewModel>(navigationService.CurrentViewModel);
        generatorViewModel.GenerateCommand.Execute(null);
        Assert.True(generatorViewModel.HasGenerated);
        var generatedName = generatorViewModel.Name;

        await generatorViewModel.SaveCommand.ExecuteAsync(null);

        navigationService.NavigateTo(NavigationDestination.Npcs);

        Assert.Same(npcsViewModel, navigationService.CurrentViewModel);
        Assert.False(npcsViewModel.IsEmpty);
        Assert.Contains(npcsViewModel.Npcs, npc => npc.Name == generatedName);
    }
}