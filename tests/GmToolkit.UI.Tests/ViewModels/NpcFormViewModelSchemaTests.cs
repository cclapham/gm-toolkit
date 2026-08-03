using GmToolkit.Core.Models;
using GmToolkit.Core.Systems;
using GmToolkit.UI.Tests.Fakes;
using GmToolkit.UI.ViewModels;
using GmToolkit.UI.ViewModels.Stats;

namespace GmToolkit.UI.Tests.ViewModels;

/// <summary>
/// Covers issue #90's schema-driven stats form on <see cref="NpcFormViewModel"/> -- mirrors
/// <see cref="CharacterFormViewModelSchemaTests"/>'s shape, but against
/// <c>dnd5e-2014</c>'s <c>npcFields</c> (which, unlike its <c>pcFields</c>, includes a
/// <c>repeating-group</c> for <c>skills</c> -- issue #90's own "variable-length actions list" acid
/// test) and with "no schema" meaning no stats section at all rather than a freeform fallback -- see
/// <see cref="NpcFormViewModel"/>'s remarks.
/// </summary>
public class NpcFormViewModelSchemaTests
{
    private const string Dnd5e2014Id = "dnd5e-2014";

    private static ICharacterSystemRegistry Registry { get; } = CharacterSystemRegistry.FromEmbeddedSystems();

    [Fact]
    public void BeginCreate_with_a_dnd5e_system_shows_a_full_npc_stat_block()
    {
        var form = new NpcFormViewModel(new FakeNpcRepository(), Registry);

        form.BeginCreate(Guid.NewGuid(), Dnd5e2014Id);

        Assert.True(form.HasSchema);
        Assert.NotNull(form.SchemaForm);
        Assert.IsType<NumberStatFieldViewModel>(form.SchemaForm!.Fields.Single(f => f.Key == "armorClass"));
        Assert.IsType<NumberStatFieldViewModel>(form.SchemaForm.Fields.Single(f => f.Key == "hitPoints"));
        Assert.IsType<RepeatingGroupStatFieldViewModel>(form.SchemaForm.Fields.Single(f => f.Key == "skills"));
        Assert.IsType<RepeatingGroupStatFieldViewModel>(form.SchemaForm.Fields.Single(f => f.Key == "actions"));
        Assert.IsType<RepeatingGroupStatFieldViewModel>(form.SchemaForm.Fields.Single(f => f.Key == "legendaryActions"));
    }

    [Fact]
    public void BeginCreate_with_no_system_id_shows_no_stats_section_at_all()
    {
        var form = new NpcFormViewModel(new FakeNpcRepository(), Registry);

        form.BeginCreate(Guid.NewGuid(), characterSystemId: null);

        Assert.False(form.HasSchema);
        Assert.Null(form.SchemaForm);
        Assert.Null(form.SystemMissingWarning);
    }

    [Fact]
    public void BeginCreate_with_an_uninstalled_system_id_shows_a_warning_and_no_stats_section()
    {
        var form = new NpcFormViewModel(new FakeNpcRepository(), Registry);

        form.BeginCreate(Guid.NewGuid(), "some-uninstalled-system");

        Assert.False(form.HasSchema);
        Assert.Null(form.SchemaForm);
        Assert.NotNull(form.SystemMissingWarning);
    }

    [Fact]
    public void Adding_a_skill_row_to_the_repeating_group_and_editing_it_works()
    {
        var form = new NpcFormViewModel(new FakeNpcRepository(), Registry);
        form.BeginCreate(Guid.NewGuid(), Dnd5e2014Id);
        var skills = Assert.IsType<RepeatingGroupStatFieldViewModel>(form.SchemaForm!.Fields.Single(f => f.Key == "skills"));

        skills.AddRowCommand.Execute(null);
        var row = Assert.Single(skills.Rows);
        var skillBonus = Assert.IsType<NumberStatFieldViewModel>(row.Fields.Single(f => f.Key == "skillBonus"));
        skillBonus.Value = 7;

        Assert.Equal("7", skillBonus.RawValue);
        Assert.True(skills.Validate());
    }

    [Fact]
    public async Task Saving_with_a_schema_persists_the_npc_stat_block_including_repeating_group_rows()
    {
        var campaignId = Guid.NewGuid();
        var repository = new FakeNpcRepository();
        var form = new NpcFormViewModel(repository, Registry);
        form.BeginCreate(campaignId, Dnd5e2014Id);
        form.Name = "Ancient Red Dragon";
        var armorClass = Assert.IsType<NumberStatFieldViewModel>(form.SchemaForm!.Fields.Single(f => f.Key == "armorClass"));
        armorClass.Value = 22;
        var skills = Assert.IsType<RepeatingGroupStatFieldViewModel>(form.SchemaForm.Fields.Single(f => f.Key == "skills"));
        skills.AddRowCommand.Execute(null);
        var skillName = Assert.IsType<EnumStatFieldViewModel>(skills.Rows[0].Fields.Single(f => f.Key == "skillName"));
        skillName.SelectedValue = "Perception";
        var skillBonus = Assert.IsType<NumberStatFieldViewModel>(skills.Rows[0].Fields.Single(f => f.Key == "skillBonus"));
        skillBonus.Value = 13;

        await form.SaveCommand.ExecuteAsync(null);

        var persisted = Assert.Single(await repository.GetByCampaignAsync(campaignId));
        Assert.Equal("22", persisted.Stats["armorClass"]);
        var rows = RepeatingGroupCodec.Deserialize(persisted.Stats["skills"]);
        var savedRow = Assert.Single(rows);
        Assert.Equal("Perception", savedRow["skillName"]);
        Assert.Equal("13", savedRow["skillBonus"]);

        // Reopen: the persisted repeating-group row must show back up.
        var reopened = new NpcFormViewModel(repository, Registry);
        reopened.BeginEdit(persisted, Dnd5e2014Id);
        var reopenedSkills = Assert.IsType<RepeatingGroupStatFieldViewModel>(reopened.SchemaForm!.Fields.Single(f => f.Key == "skills"));
        var reopenedRow = Assert.Single(reopenedSkills.Rows);
        var reopenedSkillName = Assert.IsType<EnumStatFieldViewModel>(reopenedRow.Fields.Single(f => f.Key == "skillName"));
        Assert.Equal("Perception", reopenedSkillName.SelectedValue);
    }

    [Fact]
    public async Task Saving_an_NPC_with_no_schema_never_touches_its_existing_Stats()
    {
        // This form has no general-purpose stats editor at all when HasSchema is false (unlike
        // CharacterFormViewModel's freeform fallback) -- see NpcFormViewModel's remarks.
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "Baelor" };
        npc.Stats["handEditedKey"] = "untouched";
        var repository = new FakeNpcRepository(npc);
        var form = new NpcFormViewModel(repository, Registry);
        form.BeginEdit(npc, characterSystemId: null);

        await form.SaveCommand.ExecuteAsync(null);

        Assert.Equal("untouched", npc.Stats["handEditedKey"]);
    }

    [Fact]
    public void Switching_from_freeform_to_a_schema_system_between_BeginEdit_calls_shows_the_stat_block()
    {
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "Baelor" };
        var form = new NpcFormViewModel(new FakeNpcRepository(npc), Registry);
        form.BeginEdit(npc, characterSystemId: null);
        Assert.False(form.HasSchema);

        form.BeginEdit(npc, Dnd5e2014Id);

        Assert.True(form.HasSchema);
        Assert.NotNull(form.SchemaForm);
    }
}