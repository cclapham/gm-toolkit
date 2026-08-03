using GmToolkit.Core.Models;
using GmToolkit.Core.Systems;
using GmToolkit.UI.Tests.Fakes;
using GmToolkit.UI.ViewModels;
using GmToolkit.UI.ViewModels.Stats;

namespace GmToolkit.UI.Tests.ViewModels;

/// <summary>
/// Covers issue #89's schema-driven stats form on <see cref="CharacterFormViewModel"/> -- separate
/// from <see cref="CharacterFormViewModelTests"/> (which covers everything that predates #89 and
/// still applies unchanged) so this file reads as "everything #89 added," using the real embedded
/// <c>dnd5e-2014</c> system pack (via <see cref="CharacterSystemRegistry.FromEmbeddedSystems"/>) as
/// the acceptance criteria's own worked example -- "STR/DEX/etc. as proper typed fields with working
/// validation and live-updating modifiers."
/// </summary>
public class CharacterFormViewModelSchemaTests
{
    private const string Dnd5e2014Id = "dnd5e-2014";

    private static ICharacterSystemRegistry Registry { get; } = CharacterSystemRegistry.FromEmbeddedSystems();

    [Fact]
    public void BeginCreate_with_a_dnd5e_system_shows_typed_ability_score_fields()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository(), Registry);

        form.BeginCreate(Guid.NewGuid(), Dnd5e2014Id);

        Assert.True(form.HasSchema);
        Assert.NotNull(form.SchemaForm);
        Assert.Empty(form.StatRows);

        var strength = Assert.IsType<NumberStatFieldViewModel>(form.SchemaForm!.Fields.Single(f => f.Key == "strength"));
        Assert.Equal(10m, strength.Value); // schema default for a freshly-created character
        Assert.IsType<NumberStatFieldViewModel>(form.SchemaForm.Fields.Single(f => f.Key == "dexterity"));
        Assert.IsType<NumberStatFieldViewModel>(form.SchemaForm.Fields.Single(f => f.Key == "constitution"));
        Assert.IsType<NumberStatFieldViewModel>(form.SchemaForm.Fields.Single(f => f.Key == "intelligence"));
        Assert.IsType<NumberStatFieldViewModel>(form.SchemaForm.Fields.Single(f => f.Key == "wisdom"));
        Assert.IsType<NumberStatFieldViewModel>(form.SchemaForm.Fields.Single(f => f.Key == "charisma"));
    }

    [Fact]
    public void BeginCreate_with_no_system_id_falls_back_to_the_freeform_editor()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository(), Registry);

        form.BeginCreate(Guid.NewGuid(), characterSystemId: null);

        Assert.False(form.HasSchema);
        Assert.Null(form.SchemaForm);
        Assert.Null(form.SystemMissingWarning);
    }

    [Fact]
    public void BeginCreate_with_the_generic_system_id_falls_back_to_the_freeform_editor()
    {
        // "generic" (GenericCharacterSystem.Instance) resolves fine but declares zero pcFields --
        // pixel-for-pixel the same freeform behavior as no system attached at all (issue #89's own
        // acceptance criterion).
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository(), Registry);

        form.BeginCreate(Guid.NewGuid(), GenericCharacterSystem.Id);

        Assert.False(form.HasSchema);
        Assert.Null(form.SchemaForm);
    }

    [Fact]
    public void BeginCreate_with_an_uninstalled_system_id_shows_a_warning_and_falls_back_to_freeform()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository(), Registry);

        form.BeginCreate(Guid.NewGuid(), "some-uninstalled-system");

        Assert.False(form.HasSchema);
        Assert.Null(form.SchemaForm);
        Assert.NotNull(form.SystemMissingWarning);
        Assert.Contains("some-uninstalled-system", form.SystemMissingWarning);
    }

    [Fact]
    public void Editing_an_ability_score_live_recomputes_its_modifier()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository(), Registry);
        form.BeginCreate(Guid.NewGuid(), Dnd5e2014Id);
        var strength = Assert.IsType<NumberStatFieldViewModel>(form.SchemaForm!.Fields.Single(f => f.Key == "strength"));
        var strMod = Assert.IsType<DerivedStatFieldViewModel>(form.SchemaForm.Fields.Single(f => f.Key == "strMod"));

        strength.Value = 18;

        Assert.Equal("4", strMod.DisplayValue); // floor((18 - 10) / 2)
    }

    [Fact]
    public void An_out_of_range_ability_score_blocks_saving()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository(), Registry);
        form.BeginCreate(Guid.NewGuid(), Dnd5e2014Id);
        form.CharacterName = "Arannis";
        Assert.True(form.CanSave);

        var strength = Assert.IsType<NumberStatFieldViewModel>(form.SchemaForm!.Fields.Single(f => f.Key == "strength"));
        strength.Value = 999; // dnd5e-2014's own max is 30

        Assert.False(form.CanSave);
    }

    [Fact]
    public async Task Saving_with_a_schema_persists_typed_stats_and_reopening_shows_them()
    {
        var campaignId = Guid.NewGuid();
        var repository = new FakePlayerCharacterRepository();
        var form = new CharacterFormViewModel(repository, Registry);
        form.BeginCreate(campaignId, Dnd5e2014Id);
        form.CharacterName = "Arannis Windrunner";
        var strength = Assert.IsType<NumberStatFieldViewModel>(form.SchemaForm!.Fields.Single(f => f.Key == "strength"));
        strength.Value = 17;

        await form.SaveCommand.ExecuteAsync(null);

        var persisted = Assert.Single(await repository.GetByCampaignAsync(campaignId));
        Assert.Equal("17", persisted.Stats["strength"]);
        Assert.False(persisted.Stats.ContainsKey("strMod")); // derived fields are never persisted

        // Reopen: BeginEdit with the same system id must show the persisted value back.
        var reopened = new CharacterFormViewModel(repository, Registry);
        reopened.BeginEdit(persisted, Dnd5e2014Id);
        var reopenedStrength = Assert.IsType<NumberStatFieldViewModel>(reopened.SchemaForm!.Fields.Single(f => f.Key == "strength"));
        Assert.Equal(17m, reopenedStrength.Value);
    }

    [Fact]
    public async Task Saving_with_a_schema_preserves_stats_from_a_previously_attached_different_system()
    {
        var campaignId = Guid.NewGuid();
        var character = new PlayerCharacter { CampaignId = campaignId, CharacterName = "Arannis" };
        character.Stats["someOtherSystemsField"] = "keep-me";
        var repository = new FakePlayerCharacterRepository(character);
        var form = new CharacterFormViewModel(repository, Registry);
        form.BeginEdit(character, Dnd5e2014Id);
        var strength = Assert.IsType<NumberStatFieldViewModel>(form.SchemaForm!.Fields.Single(f => f.Key == "strength"));
        strength.Value = 12;

        await form.SaveCommand.ExecuteAsync(null);

        Assert.Equal("keep-me", character.Stats["someOtherSystemsField"]);
        Assert.Equal("12", character.Stats["strength"]);
    }

    [Fact]
    public void BeginEdit_with_a_schema_reuses_the_same_form_instance_across_multiple_calls_without_leaking_subscriptions()
    {
        // Regression guard: InitializeSchema must tear down the previous SchemaForm's Changed
        // subscription before building a new one, or a stale SchemaForm from a prior BeginEdit call
        // could still drive this form's validation state after being discarded.
        var characterA = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Arannis" };
        var characterB = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Baelor" };
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository(characterA, characterB), Registry);

        form.BeginEdit(characterA, Dnd5e2014Id);
        var staleSchemaForm = form.SchemaForm!;

        form.BeginEdit(characterB, Dnd5e2014Id);
        Assert.NotSame(staleSchemaForm, form.SchemaForm);

        // Editing the stale, discarded form must not affect the current form's CanSave.
        var staleField = Assert.IsType<NumberStatFieldViewModel>(staleSchemaForm.Fields.Single(f => f.Key == "strength"));
        staleField.Value = 999; // invalid, would fail validation if it were still wired up

        Assert.True(form.CanSave);
    }

    [Fact]
    public void Switching_from_a_schema_system_to_freeform_between_BeginEdit_calls_re_renders_the_freeform_editor()
    {
        var character = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Arannis" };
        character.Stats["STR"] = "16";
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository(character), Registry);
        form.BeginEdit(character, Dnd5e2014Id);
        Assert.True(form.HasSchema);

        form.BeginEdit(character, characterSystemId: null);

        Assert.False(form.HasSchema);
        Assert.Null(form.SchemaForm);
        Assert.Contains(form.StatRows, row => row.Key == "STR" && row.Value == "16");
    }
}