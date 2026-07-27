using GmToolkit.Core.Generator;
using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;
using GmToolkit.Core.Services;
using GmToolkit.UI.Services;
using GmToolkit.UI.Tests.Fakes;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Tests.ViewModels;

/// <remarks>
/// Every test here runs against the real embedded generator tables (via
/// <see cref="GeneratorRegistry.FromEmbeddedTables"/>/<see cref="NpcGenerator"/>), same as
/// <c>GmToolkit.Core.Tests.Generator.NpcGeneratorTests</c>, but always through the internal,
/// test-only constructor overload that accepts a seeded <see cref="SystemRandomSource"/> -- see
/// <see cref="GeneratorViewModel"/>'s remarks on why the public constructor's randomness is
/// deliberately non-deterministic and therefore not appropriate for assertions here.
/// </remarks>
public class GeneratorViewModelTests
{
    private static GeneratorViewModel CreateViewModel(int seed, INpcRepository? npcRepository = null, ActiveCampaignContext? activeCampaignContext = null, INavigationService? navigationService = null)
    {
        var registry = GeneratorRegistry.FromEmbeddedTables();
        var npcGenerator = new NpcGenerator(registry);
        return new GeneratorViewModel(
            registry,
            npcGenerator,
            npcRepository ?? new FakeNpcRepository(),
            activeCampaignContext ?? CreateActiveCampaignContext(),
            navigationService ?? new FakeNavigationService(),
            new SystemRandomSource(seed));
    }

    /// <summary>A real <see cref="ActiveCampaignContext"/> (over an in-memory
    /// <see cref="FakeCampaignRepository"/>) with a campaign already selected as active -- issue
    /// #29's <c>SaveCommand</c> needs one to resolve <see cref="Npc.CampaignId"/> from.</summary>
    private static ActiveCampaignContext CreateActiveCampaignContext(Guid? campaignId = null)
    {
        var context = new ActiveCampaignContext(new FakeCampaignRepository());
        var campaign = new Campaign { Id = campaignId ?? Guid.NewGuid(), Name = "Test Campaign" };
        context.SelectCampaignAsync(campaign).GetAwaiter().GetResult();
        return context;
    }

    [Fact]
    public void Generate_populates_all_six_fields_non_empty()
    {
        var viewModel = CreateViewModel(1);

        viewModel.GenerateCommand.Execute(null);

        Assert.True(viewModel.HasGenerated);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Name));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Role));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Appearance));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Mannerism));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Motivation));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Secret));
    }

    [Fact]
    public void Regenerating_with_the_name_locked_leaves_the_name_unchanged_while_every_other_field_changes()
    {
        // Issue #28's own acceptance criterion, asserted on the actual values (not just the lock
        // flag): Generate -> lock the name -> regenerate -> the name is unchanged, everything else
        // is new.
        var viewModel = CreateViewModel(2026);
        viewModel.GenerateCommand.Execute(null);

        var originalName = viewModel.Name;
        var originalRole = viewModel.Role;
        var originalAppearance = viewModel.Appearance;
        var originalMannerism = viewModel.Mannerism;
        var originalMotivation = viewModel.Motivation;
        var originalSecret = viewModel.Secret;

        viewModel.IsNameLocked = true;
        viewModel.GenerateCommand.Execute(null);

        Assert.Equal(originalName, viewModel.Name);
        Assert.NotEqual(originalRole, viewModel.Role);
        Assert.NotEqual(originalAppearance, viewModel.Appearance);
        Assert.NotEqual(originalMannerism, viewModel.Mannerism);
        Assert.NotEqual(originalMotivation, viewModel.Motivation);
        Assert.NotEqual(originalSecret, viewModel.Secret);
    }

    [Fact]
    public void Rerolling_a_single_field_changes_only_that_field()
    {
        var viewModel = CreateViewModel(7);
        viewModel.GenerateCommand.Execute(null);

        var originalName = viewModel.Name;
        var originalAppearance = viewModel.Appearance;
        var originalMannerism = viewModel.Mannerism;
        var originalMotivation = viewModel.Motivation;
        var originalSecret = viewModel.Secret;

        viewModel.RerollRoleCommand.Execute(null);

        Assert.Equal(originalName, viewModel.Name);
        Assert.Equal(originalAppearance, viewModel.Appearance);
        Assert.Equal(originalMannerism, viewModel.Mannerism);
        Assert.Equal(originalMotivation, viewModel.Motivation);
        Assert.Equal(originalSecret, viewModel.Secret);
    }

    [Fact]
    public void Rerolling_the_name_changes_only_the_name()
    {
        var viewModel = CreateViewModel(8);
        viewModel.GenerateCommand.Execute(null);

        var originalRole = viewModel.Role;
        var originalAppearance = viewModel.Appearance;
        var originalMannerism = viewModel.Mannerism;
        var originalMotivation = viewModel.Motivation;
        var originalSecret = viewModel.Secret;

        viewModel.RerollNameCommand.Execute(null);

        Assert.Equal(originalRole, viewModel.Role);
        Assert.Equal(originalAppearance, viewModel.Appearance);
        Assert.Equal(originalMannerism, viewModel.Mannerism);
        Assert.Equal(originalMotivation, viewModel.Motivation);
        Assert.Equal(originalSecret, viewModel.Secret);
    }

    [Fact]
    public void Locking_a_field_blocks_its_own_reroll_button_too()
    {
        // The design decision this class's remarks document: locking means "untouched, full stop",
        // so a field's own reroll command is a no-op while that field is locked, not just
        // GenerateCommand.
        var viewModel = CreateViewModel(3);
        viewModel.GenerateCommand.Execute(null);
        var originalName = viewModel.Name;

        viewModel.IsNameLocked = true;
        viewModel.RerollNameCommand.Execute(null);

        Assert.Equal(originalName, viewModel.Name);
    }

    [Fact]
    public void Inline_edits_to_a_fields_value_are_preserved_until_that_field_is_rerolled_or_regenerated()
    {
        var viewModel = CreateViewModel(4);
        viewModel.GenerateCommand.Execute(null);

        viewModel.Appearance = "A hand-typed description the GM just wrote.";
        viewModel.RerollRoleCommand.Execute(null);

        Assert.Equal("A hand-typed description the GM just wrote.", viewModel.Appearance);
    }

    [Fact]
    public void RerollName_with_a_matching_culture_constraint_does_not_show_a_fallback_notice()
    {
        var viewModel = CreateViewModel(11);
        Assert.Contains("highland", viewModel.NameCultureOptions, StringComparer.OrdinalIgnoreCase);
        viewModel.SelectedNameCulture = "highland";

        viewModel.RerollNameCommand.Execute(null);

        Assert.False(string.IsNullOrWhiteSpace(viewModel.Name));
        Assert.Null(viewModel.NameFallbackNotice);
    }

    [Fact]
    public void RerollName_with_an_unrecognized_culture_shows_a_fallback_notice_and_still_produces_a_value()
    {
        var viewModel = CreateViewModel(12);
        viewModel.SelectedNameCulture = "atlantean";

        viewModel.RerollNameCommand.Execute(null);

        Assert.False(string.IsNullOrWhiteSpace(viewModel.Name));
        Assert.NotNull(viewModel.NameFallbackNotice);
    }

    [Fact]
    public void RerollRole_with_a_matching_occupation_category_does_not_show_a_fallback_notice()
    {
        var viewModel = CreateViewModel(13);
        Assert.Contains("criminal", viewModel.OccupationCategoryOptions, StringComparer.OrdinalIgnoreCase);
        viewModel.SelectedOccupationCategory = "criminal";

        viewModel.RerollRoleCommand.Execute(null);

        Assert.False(string.IsNullOrWhiteSpace(viewModel.Role));
        Assert.Null(viewModel.RoleFallbackNotice);
    }

    [Fact]
    public void RerollRole_with_an_unrecognized_occupation_category_shows_a_fallback_notice_and_still_produces_a_value()
    {
        var viewModel = CreateViewModel(14);
        viewModel.SelectedOccupationCategory = "does-not-exist";

        viewModel.RerollRoleCommand.Execute(null);

        Assert.False(string.IsNullOrWhiteSpace(viewModel.Role));
        Assert.NotNull(viewModel.RoleFallbackNotice);
    }

    [Fact]
    public void A_later_successful_reroll_clears_a_previously_shown_fallback_notice()
    {
        var viewModel = CreateViewModel(15);
        viewModel.SelectedNameCulture = "atlantean";
        viewModel.RerollNameCommand.Execute(null);
        Assert.NotNull(viewModel.NameFallbackNotice);

        viewModel.SelectedNameCulture = GeneratorViewModel.AnyOption;
        viewModel.RerollNameCommand.Execute(null);

        Assert.Null(viewModel.NameFallbackNotice);
    }

    [Fact]
    public void NameCultureOptions_always_starts_with_the_Any_sentinel()
    {
        var viewModel = CreateViewModel(1);

        Assert.Equal(GeneratorViewModel.AnyOption, viewModel.NameCultureOptions[0]);
    }

    [Fact]
    public void OccupationCategoryOptions_always_starts_with_the_Any_sentinel_and_contains_real_tags()
    {
        var viewModel = CreateViewModel(1);

        Assert.Equal(GeneratorViewModel.AnyOption, viewModel.OccupationCategoryOptions[0]);
        Assert.True(viewModel.OccupationCategoryOptions.Count > 1, "Expected at least one real occupation category tag besides the Any sentinel.");
    }

    // --- Issue #29: saving a generated NPC into the active campaign ---

    [Fact]
    public async Task Full_round_trip_generate_reroll_the_name_save_and_the_npc_appears_in_the_campaigns_list()
    {
        // The MVP.md "ten-second test" this issue's own acceptance criterion is phrased around:
        // open the generator, generate, reroll the name, save, and the NPC shows up in the list.
        var campaignId = Guid.NewGuid();
        var repository = new FakeNpcRepository();
        var activeCampaignContext = CreateActiveCampaignContext(campaignId);
        var viewModel = CreateViewModel(21, repository, activeCampaignContext);
        viewModel.GenerateCommand.Execute(null);
        viewModel.RerollNameCommand.Execute(null);
        var expectedName = viewModel.Name;

        await viewModel.SaveCommand.ExecuteAsync(null);

        var npcs = await repository.GetByCampaignAsync(campaignId);
        var saved = Assert.Single(npcs);
        Assert.Equal(expectedName, saved.Name);
        Assert.True(saved.WasGenerated);
    }

    [Fact]
    public async Task SaveAsync_persists_an_npc_with_WasGenerated_true_and_the_active_campaigns_id()
    {
        var campaignId = Guid.NewGuid();
        var repository = new FakeNpcRepository();
        var activeCampaignContext = CreateActiveCampaignContext(campaignId);
        var viewModel = CreateViewModel(31, repository, activeCampaignContext);
        viewModel.GenerateCommand.Execute(null);

        await viewModel.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(await repository.GetByCampaignAsync(campaignId));
        Assert.True(saved.WasGenerated);
        Assert.Equal(campaignId, saved.CampaignId);
    }

    [Fact]
    public async Task SaveAsync_round_trips_every_generated_field_onto_the_persisted_npc()
    {
        var campaignId = Guid.NewGuid();
        var repository = new FakeNpcRepository();
        var activeCampaignContext = CreateActiveCampaignContext(campaignId);
        var viewModel = CreateViewModel(32, repository, activeCampaignContext);
        viewModel.GenerateCommand.Execute(null);
        var expectedName = viewModel.Name;
        var expectedRole = viewModel.Role;
        var expectedAppearance = viewModel.Appearance;
        var expectedMannerism = viewModel.Mannerism;
        var expectedMotivation = viewModel.Motivation;
        var expectedSecret = viewModel.Secret;

        await viewModel.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(await repository.GetByCampaignAsync(campaignId));
        Assert.Equal(expectedName, saved.Name);
        Assert.Equal(expectedRole, saved.Role);
        Assert.Equal(expectedAppearance, saved.Appearance);
        Assert.Equal(expectedMannerism, saved.Mannerism);
        Assert.Equal(expectedMotivation, saved.Motivation);
        Assert.Equal(expectedSecret, saved.Secret);
    }

    [Fact]
    public async Task SaveAsync_persists_Faction_and_Location_entered_at_save_time()
    {
        var campaignId = Guid.NewGuid();
        var repository = new FakeNpcRepository();
        var activeCampaignContext = CreateActiveCampaignContext(campaignId);
        var viewModel = CreateViewModel(33, repository, activeCampaignContext);
        viewModel.GenerateCommand.Execute(null);
        viewModel.Faction = "The Iron Concord";
        viewModel.Location = "Blackmoor Docks";

        await viewModel.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(await repository.GetByCampaignAsync(campaignId));
        Assert.Equal("The Iron Concord", saved.Faction);
        Assert.Equal("Blackmoor Docks", saved.Location);
    }

    [Fact]
    public async Task SaveAsync_resets_the_generator_to_its_empty_state()
    {
        var viewModel = CreateViewModel(34);
        viewModel.GenerateCommand.Execute(null);
        viewModel.IsRoleLocked = true;
        viewModel.Faction = "The Iron Concord";
        viewModel.Location = "Blackmoor Docks";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasGenerated);
        Assert.Equal(string.Empty, viewModel.Name);
        Assert.Equal(string.Empty, viewModel.Role);
        Assert.Equal(string.Empty, viewModel.Appearance);
        Assert.Equal(string.Empty, viewModel.Mannerism);
        Assert.Equal(string.Empty, viewModel.Motivation);
        Assert.Equal(string.Empty, viewModel.Secret);
        Assert.Equal(string.Empty, viewModel.Faction);
        Assert.Equal(string.Empty, viewModel.Location);
        Assert.False(viewModel.IsNameLocked);
        Assert.False(viewModel.IsRoleLocked);
        Assert.False(viewModel.IsAppearanceLocked);
        Assert.False(viewModel.IsMannerismLocked);
        Assert.False(viewModel.IsMotivationLocked);
        Assert.False(viewModel.IsSecretLocked);
    }

    [Fact]
    public async Task SaveAsync_shows_a_confirmation_message_naming_the_saved_npc()
    {
        var viewModel = CreateViewModel(35);
        viewModel.GenerateCommand.Execute(null);
        var expectedName = viewModel.Name;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(viewModel.SaveConfirmationMessage);
        Assert.Contains(expectedName, viewModel.SaveConfirmationMessage);
    }

    [Fact]
    public async Task ViewSavedNpcCommand_navigates_to_the_npcs_list()
    {
        var navigationService = new FakeNavigationService();
        var viewModel = CreateViewModel(36, navigationService: navigationService);
        viewModel.GenerateCommand.Execute(null);
        await viewModel.SaveCommand.ExecuteAsync(null);

        viewModel.ViewSavedNpcCommand.Execute(null);

        Assert.Contains(NavigationDestination.Npcs, navigationService.NavigatedTo);
    }

    [Fact]
    public async Task Generate_clears_a_previous_save_confirmation_message()
    {
        var viewModel = CreateViewModel(37);
        viewModel.GenerateCommand.Execute(null);
        await viewModel.SaveCommand.ExecuteAsync(null);
        Assert.NotNull(viewModel.SaveConfirmationMessage);

        viewModel.GenerateCommand.Execute(null);

        Assert.Null(viewModel.SaveConfirmationMessage);
    }

    [Fact]
    public async Task Clearing_the_generated_name_to_blank_disables_saving_and_does_not_persist_anything()
    {
        var campaignId = Guid.NewGuid();
        var repository = new FakeNpcRepository();
        var activeCampaignContext = CreateActiveCampaignContext(campaignId);
        var viewModel = CreateViewModel(38, repository, activeCampaignContext);
        viewModel.GenerateCommand.Execute(null);

        viewModel.Name = "   ";

        Assert.NotNull(viewModel.NameError);
        Assert.False(viewModel.SaveCommand.CanExecute(null));

        // Even if somehow invoked directly (bypassing the disabled button), it must be a no-op --
        // mirrors NpcFormViewModelTests'/CharacterFormViewModelTests' identical bypass-resistance
        // test, since IAsyncRelayCommand.ExecuteAsync does not itself call CanExecute.
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Empty(await repository.GetByCampaignAsync(campaignId));
        Assert.True(viewModel.HasGenerated);
    }

    [Fact]
    public void Setting_a_valid_name_after_a_blank_one_re_enables_saving()
    {
        var viewModel = CreateViewModel(39);
        viewModel.GenerateCommand.Execute(null);
        viewModel.Name = string.Empty;
        Assert.False(viewModel.SaveCommand.CanExecute(null));

        viewModel.Name = "Baelor the Butcher";

        Assert.Null(viewModel.NameError);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void FactionSuggestions_and_LocationSuggestions_are_built_from_the_active_campaigns_existing_npcs()
    {
        var campaignId = Guid.NewGuid();
        var otherCampaignId = Guid.NewGuid();
        var one = new Npc { CampaignId = campaignId, Name = "Baelor", Faction = "The Iron Concord", Location = "Blackmoor Docks" };
        var two = new Npc { CampaignId = campaignId, Name = "Zoric", Faction = "The Whispering Cult", Location = "Blackmoor Market" };
        var inOtherCampaign = new Npc { CampaignId = otherCampaignId, Name = "Fourth", Faction = "Unrelated Faction", Location = "Unrelated Place" };
        var repository = new FakeNpcRepository(one, two, inOtherCampaign);
        var activeCampaignContext = CreateActiveCampaignContext(campaignId);

        var viewModel = CreateViewModel(40, repository, activeCampaignContext);

        Assert.Equal(["The Iron Concord", "The Whispering Cult"], viewModel.FactionSuggestions);
        Assert.Equal(["Blackmoor Docks", "Blackmoor Market"], viewModel.LocationSuggestions);
    }

    [Fact]
    public async Task HandleActiveCampaignChanged_rebuilds_suggestions_for_the_newly_active_campaign()
    {
        var firstCampaignId = Guid.NewGuid();
        var secondCampaignId = Guid.NewGuid();
        var inFirst = new Npc { CampaignId = firstCampaignId, Name = "Baelor", Faction = "The Iron Concord", Location = "Blackmoor Docks" };
        var inSecond = new Npc { CampaignId = secondCampaignId, Name = "Zoric", Faction = "The Whispering Cult", Location = "Blackmoor Market" };
        var repository = new FakeNpcRepository(inFirst, inSecond);
        var campaignRepository = new FakeCampaignRepository();
        var activeCampaignContext = new ActiveCampaignContext(campaignRepository);
        var firstCampaign = new Campaign { Id = firstCampaignId, Name = "First" };
        await activeCampaignContext.SelectCampaignAsync(firstCampaign);
        var viewModel = CreateViewModel(41, repository, activeCampaignContext);
        Assert.Equal(["The Iron Concord"], viewModel.FactionSuggestions);

        var secondCampaign = new Campaign { Id = secondCampaignId, Name = "Second" };
        await activeCampaignContext.SelectCampaignAsync(secondCampaign);
        viewModel.HandleActiveCampaignChanged();

        Assert.Equal(["The Whispering Cult"], viewModel.FactionSuggestions);
        Assert.Equal(["Blackmoor Market"], viewModel.LocationSuggestions);
    }

    [Fact]
    public async Task Saving_after_switching_campaigns_saves_to_the_campaign_the_npc_was_generated_for()
    {
        // Regression guard from the skeptical review of PR #63: this screen is cached for the app's
        // whole lifetime and deliberately does not reset an in-progress NPC on a campaign switch (see
        // HandleActiveCampaignChanged's remarks) -- so without capturing which campaign the NPC was
        // actually generated for, Save would silently attribute it to whichever campaign happened to
        // be active at the moment Save was clicked, not the one the GM generated and reviewed it in.
        var firstCampaignId = Guid.NewGuid();
        var secondCampaignId = Guid.NewGuid();
        var repository = new FakeNpcRepository();
        var campaignRepository = new FakeCampaignRepository();
        var activeCampaignContext = new ActiveCampaignContext(campaignRepository);
        var firstCampaign = new Campaign { Id = firstCampaignId, Name = "First" };
        await activeCampaignContext.SelectCampaignAsync(firstCampaign);
        var viewModel = CreateViewModel(42, repository, activeCampaignContext);

        viewModel.GenerateCommand.Execute(null);
        Assert.Equal("First", viewModel.GeneratingCampaignName);

        var secondCampaign = new Campaign { Id = secondCampaignId, Name = "Second" };
        await activeCampaignContext.SelectCampaignAsync(secondCampaign);
        viewModel.HandleActiveCampaignChanged();
        Assert.Equal("First", viewModel.GeneratingCampaignName); // unaffected by the switch

        await viewModel.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(await repository.GetByCampaignAsync(firstCampaignId));
        Assert.Empty(await repository.GetByCampaignAsync(secondCampaignId));
        Assert.Equal(firstCampaignId, saved.CampaignId);
    }

    [Fact]
    public void GeneratingCampaignName_is_null_before_the_first_generate_and_after_a_reset()
    {
        var viewModel = CreateViewModel(43);
        Assert.Null(viewModel.GeneratingCampaignName);

        viewModel.GenerateCommand.Execute(null);
        Assert.Equal("Test Campaign", viewModel.GeneratingCampaignName);
    }

    [Fact]
    public async Task SaveAsync_names_the_generating_campaign_in_the_confirmation_message()
    {
        var viewModel = CreateViewModel(44);
        viewModel.GenerateCommand.Execute(null);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(viewModel.SaveConfirmationMessage);
        Assert.Contains("Test Campaign", viewModel.SaveConfirmationMessage);
        Assert.Null(viewModel.GeneratingCampaignName); // cleared by the reset
    }
}