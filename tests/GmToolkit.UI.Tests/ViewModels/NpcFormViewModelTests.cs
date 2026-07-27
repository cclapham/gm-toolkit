using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;
using GmToolkit.UI.Tests.Fakes;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Tests.ViewModels;

/// <remarks>
/// <see cref="FakeNpcRepository"/> completes every call synchronously, so
/// <see cref="NpcFormViewModel.BeginCreate"/>/<see cref="NpcFormViewModel.BeginEdit"/>'s
/// fire-and-forget suggestion load has always finished by the time those methods return -- mirrors
/// <see cref="NpcsViewModelTests"/>' identical remark on <see cref="NpcsViewModel"/>'s own
/// constructor-time load.
/// </remarks>
public class NpcFormViewModelTests
{
    [Fact]
    public void BeginCreate_resets_to_empty_fields_and_is_initially_invalid()
    {
        var form = new NpcFormViewModel(new FakeNpcRepository());

        form.BeginCreate(Guid.NewGuid());

        Assert.False(form.IsEditMode);
        Assert.Equal(string.Empty, form.Name);
        Assert.Equal(string.Empty, form.Role);
        Assert.Equal(string.Empty, form.Faction);
        Assert.Equal(string.Empty, form.Location);
        Assert.Equal(string.Empty, form.Appearance);
        Assert.Equal(string.Empty, form.Mannerism);
        Assert.Equal(string.Empty, form.Motivation);
        Assert.Equal(string.Empty, form.Secret);
        Assert.Equal(string.Empty, form.Notes);
        Assert.False(form.KnownToPlayers);
        Assert.False(form.CanSave);
        Assert.NotNull(form.NameError);
        Assert.False(form.IsDeleteAvailable);
    }

    [Fact]
    public void Setting_a_valid_name_clears_the_error_and_allows_saving()
    {
        var form = new NpcFormViewModel(new FakeNpcRepository());
        form.BeginCreate(Guid.NewGuid());

        form.Name = "Baelor the Butcher";

        Assert.Null(form.NameError);
        Assert.True(form.CanSave);
    }

    [Fact]
    public void Setting_a_name_over_the_max_length_sets_a_visible_error_and_blocks_saving()
    {
        var form = new NpcFormViewModel(new FakeNpcRepository());
        form.BeginCreate(Guid.NewGuid());

        form.Name = new string('a', Npc.NameMaxLength + 1);

        Assert.NotNull(form.NameError);
        Assert.False(form.CanSave);
    }

    [Fact]
    public void Setting_a_whitespace_only_name_sets_a_visible_error_and_blocks_saving()
    {
        // Regression guard: DataAnnotations' [Required] alone does not reject whitespace-only
        // strings, but Npc.Name's own setter does -- mirrors CharacterFormViewModelTests'/
        // CampaignFormViewModelTests' identical regression guard.
        var form = new NpcFormViewModel(new FakeNpcRepository());
        form.BeginCreate(Guid.NewGuid());

        form.Name = "   ";

        Assert.NotNull(form.NameError);
        Assert.False(form.CanSave);
    }

    [Fact]
    public async Task SaveAsync_with_an_invalid_name_does_not_persist_anything()
    {
        var campaignId = Guid.NewGuid();
        var repository = new FakeNpcRepository();
        var form = new NpcFormViewModel(repository);
        form.BeginCreate(campaignId);

        await form.SaveCommand.ExecuteAsync(null);

        Assert.Empty(await repository.GetByCampaignAsync(campaignId));
    }

    [Fact]
    public async Task SaveAsync_with_all_fields_populated_adds_a_new_npc_and_it_reads_back_identically()
    {
        var campaignId = Guid.NewGuid();
        var repository = new FakeNpcRepository();
        var form = new NpcFormViewModel(repository);
        form.BeginCreate(campaignId);
        form.Name = "Baelor the Butcher";
        form.Role = "Blacksmith";
        form.Faction = "The Iron Concord";
        form.Location = "Blackmoor Docks";
        form.Appearance = "Broad-shouldered, soot-stained apron.";
        form.Mannerism = "Taps his hammer twice before speaking.";
        form.Motivation = "Wants to buy back his family's forge.";
        form.Secret = "Owes a blood debt to the Concord.";
        form.Notes = "Good source of rumors about the docks.";
        form.KnownToPlayers = true;
        Npc? saved = null;
        form.Saved += npc =>
        {
            saved = npc;
            return Task.CompletedTask;
        };

        await form.SaveCommand.ExecuteAsync(null);

        var all = await repository.GetByCampaignAsync(campaignId);
        var persisted = Assert.Single(all);
        Assert.Equal("Baelor the Butcher", persisted.Name);
        Assert.Equal("Blacksmith", persisted.Role);
        Assert.Equal("The Iron Concord", persisted.Faction);
        Assert.Equal("Blackmoor Docks", persisted.Location);
        Assert.Equal("Broad-shouldered, soot-stained apron.", persisted.Appearance);
        Assert.Equal("Taps his hammer twice before speaking.", persisted.Mannerism);
        Assert.Equal("Wants to buy back his family's forge.", persisted.Motivation);
        Assert.Equal("Owes a blood debt to the Concord.", persisted.Secret);
        Assert.Equal("Good source of rumors about the docks.", persisted.Notes);
        Assert.True(persisted.KnownToPlayers);
        Assert.Equal(campaignId, persisted.CampaignId);
        Assert.NotNull(saved);
        Assert.Same(persisted, saved);
    }

    [Fact]
    public async Task A_hand_created_npc_leaves_WasGenerated_false()
    {
        // Issue #25's own acceptance criterion: a hand-created NPC must be indistinguishable from a
        // generated one afterwards, other than WasGenerated, which this form never sets and which
        // stays at Npc's own default of false.
        var campaignId = Guid.NewGuid();
        var repository = new FakeNpcRepository();
        var form = new NpcFormViewModel(repository);
        form.BeginCreate(campaignId);
        form.Name = "Baelor the Butcher";

        await form.SaveCommand.ExecuteAsync(null);

        var persisted = Assert.Single(await repository.GetByCampaignAsync(campaignId));
        Assert.False(persisted.WasGenerated);
    }

    [Fact]
    public void BeginEdit_populates_every_field_from_the_existing_npc()
    {
        var npc = new Npc
        {
            CampaignId = Guid.NewGuid(),
            Name = "Baelor",
            Role = "Blacksmith",
            Faction = "The Iron Concord",
            Location = "Blackmoor Docks",
            Appearance = "Broad-shouldered.",
            Mannerism = "Taps his hammer.",
            Motivation = "Wants his forge back.",
            Secret = "Owes a debt.",
            Notes = "Good rumors.",
            KnownToPlayers = true,
        };
        var form = new NpcFormViewModel(new FakeNpcRepository(npc));

        form.BeginEdit(npc);

        Assert.True(form.IsEditMode);
        Assert.True(form.IsDeleteAvailable);
        Assert.Equal("Baelor", form.Name);
        Assert.Equal("Blacksmith", form.Role);
        Assert.Equal("The Iron Concord", form.Faction);
        Assert.Equal("Blackmoor Docks", form.Location);
        Assert.Equal("Broad-shouldered.", form.Appearance);
        Assert.Equal("Taps his hammer.", form.Mannerism);
        Assert.Equal("Wants his forge back.", form.Motivation);
        Assert.Equal("Owes a debt.", form.Secret);
        Assert.Equal("Good rumors.", form.Notes);
        Assert.True(form.KnownToPlayers);
        Assert.True(form.CanSave);
        Assert.Null(form.NameError);
    }

    [Fact]
    public async Task SaveAsync_in_edit_mode_updates_the_existing_npc_via_the_repository()
    {
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "Baelor" };
        var repository = new FakeNpcRepository(npc);
        var form = new NpcFormViewModel(repository);
        form.BeginEdit(npc);
        form.Name = "Baelor the Butcher";
        form.Role = "Blacksmith";

        await form.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Baelor the Butcher", npc.Name);
        Assert.Equal("Blacksmith", npc.Role);
        var reloaded = await repository.GetAsync(npc.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("Baelor the Butcher", reloaded.Name);
    }

    [Fact]
    public async Task Editing_a_generated_npc_leaves_WasGenerated_untouched()
    {
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "Baelor", WasGenerated = true };
        var repository = new FakeNpcRepository(npc);
        var form = new NpcFormViewModel(repository);
        form.BeginEdit(npc);
        form.Role = "Blacksmith";

        await form.SaveCommand.ExecuteAsync(null);

        Assert.True(npc.WasGenerated);
    }

    [Fact]
    public void FactionSuggestions_and_LocationSuggestions_are_built_from_the_campaigns_existing_distinct_values()
    {
        var campaignId = Guid.NewGuid();
        var otherCampaignId = Guid.NewGuid();
        var one = new Npc { CampaignId = campaignId, Name = "Baelor", Faction = "The Iron Concord", Location = "Blackmoor Docks" };
        var two = new Npc { CampaignId = campaignId, Name = "Zoric", Faction = "The Whispering Cult", Location = "Blackmoor Market" };
        var duplicate = new Npc { CampaignId = campaignId, Name = "Third", Faction = "the iron concord", Location = string.Empty };
        var inOtherCampaign = new Npc { CampaignId = otherCampaignId, Name = "Fourth", Faction = "Unrelated Faction", Location = "Unrelated Place" };
        var repository = new FakeNpcRepository(one, two, duplicate, inOtherCampaign);
        var form = new NpcFormViewModel(repository);

        form.BeginCreate(campaignId);

        Assert.Equal(["The Iron Concord", "The Whispering Cult"], form.FactionSuggestions);
        Assert.Equal(["Blackmoor Docks", "Blackmoor Market"], form.LocationSuggestions);
    }

    [Fact]
    public void BeginEdit_builds_suggestions_scoped_to_the_npcs_own_campaign()
    {
        var campaignId = Guid.NewGuid();
        var otherCampaignId = Guid.NewGuid();
        var editing = new Npc { CampaignId = campaignId, Name = "Baelor", Faction = "The Iron Concord", Location = "Blackmoor Docks" };
        var inOtherCampaign = new Npc { CampaignId = otherCampaignId, Name = "Fourth", Faction = "Unrelated Faction", Location = "Unrelated Place" };
        var repository = new FakeNpcRepository(editing, inOtherCampaign);
        var form = new NpcFormViewModel(repository);

        form.BeginEdit(editing);

        Assert.Equal(["The Iron Concord"], form.FactionSuggestions);
        Assert.Equal(["Blackmoor Docks"], form.LocationSuggestions);
    }

    [Fact]
    public void Cancel_with_no_unsaved_changes_raises_Cancelled_immediately()
    {
        var form = new NpcFormViewModel(new FakeNpcRepository());
        form.BeginCreate(Guid.NewGuid());
        var cancelledRaised = false;
        form.Cancelled += () => cancelledRaised = true;

        form.CancelCommand.Execute(null);

        Assert.True(cancelledRaised);
        Assert.False(form.IsShowingDiscardConfirmation);
    }

    [Fact]
    public void Cancel_with_unsaved_changes_shows_the_inline_discard_guard_instead_of_raising_Cancelled()
    {
        var form = new NpcFormViewModel(new FakeNpcRepository());
        form.BeginCreate(Guid.NewGuid());
        form.Name = "Baelor";
        var cancelledRaised = false;
        form.Cancelled += () => cancelledRaised = true;

        form.CancelCommand.Execute(null);

        Assert.False(cancelledRaised);
        Assert.True(form.IsShowingDiscardConfirmation);
        Assert.False(form.IsFieldsVisible);
    }

    [Fact]
    public void Changing_a_non_name_field_also_counts_as_an_unsaved_change()
    {
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "Baelor" };
        var form = new NpcFormViewModel(new FakeNpcRepository(npc));
        form.BeginEdit(npc);
        var cancelledRaised = false;
        form.Cancelled += () => cancelledRaised = true;

        form.KnownToPlayers = true;
        form.CancelCommand.Execute(null);

        Assert.False(cancelledRaised);
        Assert.True(form.IsShowingDiscardConfirmation);
    }

    [Fact]
    public void ConfirmDiscard_raises_Cancelled_and_hides_the_guard()
    {
        var form = new NpcFormViewModel(new FakeNpcRepository());
        form.BeginCreate(Guid.NewGuid());
        form.Name = "Baelor";
        form.CancelCommand.Execute(null);
        var cancelledRaised = false;
        form.Cancelled += () => cancelledRaised = true;

        form.ConfirmDiscardCommand.Execute(null);

        Assert.True(cancelledRaised);
        Assert.False(form.IsShowingDiscardConfirmation);
    }

    [Fact]
    public void CancelDiscard_hides_the_guard_without_raising_Cancelled_and_keeps_the_edits()
    {
        var form = new NpcFormViewModel(new FakeNpcRepository());
        form.BeginCreate(Guid.NewGuid());
        form.Name = "Baelor";
        form.CancelCommand.Execute(null);
        var cancelledRaised = false;
        form.Cancelled += () => cancelledRaised = true;

        form.CancelDiscardCommand.Execute(null);

        Assert.False(cancelledRaised);
        Assert.False(form.IsShowingDiscardConfirmation);
        Assert.Equal("Baelor", form.Name);
    }

    [Fact]
    public void RequestDeleteCommand_is_a_no_op_in_create_mode()
    {
        var form = new NpcFormViewModel(new FakeNpcRepository());
        form.BeginCreate(Guid.NewGuid());

        form.RequestDeleteCommand.Execute(null);

        Assert.False(form.IsShowingDeleteConfirmation);
    }

    [Fact]
    public void RequestDeleteCommand_in_edit_mode_shows_the_confirmation_panel_and_ConfirmDeleteCommand_is_disabled()
    {
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "Baelor" };
        var form = new NpcFormViewModel(new FakeNpcRepository(npc));
        form.BeginEdit(npc);

        form.RequestDeleteCommand.Execute(null);

        Assert.True(form.IsShowingDeleteConfirmation);
        Assert.False(form.CanConfirmDelete);
        Assert.False(form.ConfirmDeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task Typing_the_wrong_name_does_not_enable_ConfirmDeleteCommand_and_does_not_delete()
    {
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "Baelor" };
        var repository = new FakeNpcRepository(npc);
        var form = new NpcFormViewModel(repository);
        form.BeginEdit(npc);
        form.RequestDeleteCommand.Execute(null);

        form.DeleteConfirmationInput = "baelor"; // wrong case -- must not match

        Assert.False(form.CanConfirmDelete);
        Assert.False(form.ConfirmDeleteCommand.CanExecute(null));

        // Even if somehow invoked directly (bypassing the disabled button), it must be a no-op --
        // mirrors CharacterFormViewModelTests'/CampaignsViewModelTests' identical bypass-resistance
        // test, since IAsyncRelayCommand.ExecuteAsync does not itself call CanExecute.
        await form.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.NotEmpty(await repository.GetByCampaignAsync(npc.CampaignId));
        Assert.True(form.IsShowingDeleteConfirmation);
    }

    [Fact]
    public void Typing_the_exact_npc_name_enables_ConfirmDeleteCommand()
    {
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "Baelor" };
        var form = new NpcFormViewModel(new FakeNpcRepository(npc));
        form.BeginEdit(npc);
        form.RequestDeleteCommand.Execute(null);

        form.DeleteConfirmationInput = "Baelor";

        Assert.True(form.CanConfirmDelete);
        Assert.True(form.ConfirmDeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task Confirming_delete_removes_the_npc_and_raises_Deleted()
    {
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "Baelor" };
        var repository = new FakeNpcRepository(npc);
        var form = new NpcFormViewModel(repository);
        form.BeginEdit(npc);
        form.RequestDeleteCommand.Execute(null);
        form.DeleteConfirmationInput = "Baelor";
        var deletedRaised = false;
        form.Deleted += () =>
        {
            deletedRaised = true;
            return Task.CompletedTask;
        };

        await form.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.True(deletedRaised);
        Assert.False(form.IsShowingDeleteConfirmation);
        Assert.Null(await repository.GetAsync(npc.Id));
    }

    [Fact]
    public void CancelDeleteCommand_hides_the_confirmation_panel_without_deleting_anything()
    {
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "Baelor" };
        var repository = new FakeNpcRepository(npc);
        var form = new NpcFormViewModel(repository);
        form.BeginEdit(npc);
        form.RequestDeleteCommand.Execute(null);
        form.DeleteConfirmationInput = "Baelor";

        form.CancelDeleteCommand.Execute(null);

        Assert.False(form.IsShowingDeleteConfirmation);
        Assert.False(form.ConfirmDeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_slow_stale_suggestion_load_never_overwrites_a_newer_campaigns_suggestions()
    {
        // Regression guard from the skeptical review of PR #58: BeginCreate/BeginEdit kick off
        // LoadSuggestionsAsync fire-and-forget with no ordering guarantee between overlapping
        // calls. Simulates campaign A's load still being in flight when the form is reopened for
        // campaign B (whose own, faster load already completed) -- A's late completion must not
        // clobber B's already-current suggestions.
        var campaignA = Guid.NewGuid();
        var campaignB = Guid.NewGuid();
        var npcInA = new Npc { CampaignId = campaignA, Name = "Alpha", Faction = "Faction A", Location = "Location A" };
        var npcInB = new Npc { CampaignId = campaignB, Name = "Beta", Faction = "Faction B", Location = "Location B" };
        var repository = new SlowFirstCallNpcRepository(campaignA, npcInA, npcInB);
        var form = new NpcFormViewModel(repository);

        form.BeginCreate(campaignA); // Kicks off a load for A that repository.Release() below controls.
        form.BeginCreate(campaignB); // A's own load for B completes synchronously (not the slow one).

        Assert.Equal(["Faction B"], form.FactionSuggestions);
        Assert.Equal(["Location B"], form.LocationSuggestions);

        repository.ReleaseSlowCall();
        await Task.Yield(); // Let A's now-completing continuation run.

        Assert.Equal(["Faction B"], form.FactionSuggestions);
        Assert.Equal(["Location B"], form.LocationSuggestions);
    }

    /// <summary>Test-only <see cref="INpcRepository"/> whose <see cref="GetByCampaignAsync"/> never
    /// completes for one designated campaign until <see cref="ReleaseSlowCall"/> is called -- every
    /// other campaign's call completes synchronously, same as <see cref="FakeNpcRepository"/>.</summary>
    private sealed class SlowFirstCallNpcRepository(Guid slowCampaignId, params Npc[] npcs) : INpcRepository
    {
        private readonly TaskCompletionSource _release = new();

        public async Task<IReadOnlyList<Npc>> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
        {
            if (campaignId == slowCampaignId)
            {
                await _release.Task;
            }

            return [.. npcs.Where(npc => npc.CampaignId == campaignId)];
        }

        public void ReleaseSlowCall() => _release.TrySetResult();

        public Task<Npc?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(npcs.FirstOrDefault(npc => npc.Id == id));

        public Task AddAsync(Npc npc, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(Npc npc, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}