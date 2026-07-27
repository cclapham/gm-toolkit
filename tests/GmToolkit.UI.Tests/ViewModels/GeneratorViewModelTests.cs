using GmToolkit.Core.Generator;
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
    private static GeneratorViewModel CreateViewModel(int seed)
    {
        var registry = GeneratorRegistry.FromEmbeddedTables();
        var npcGenerator = new NpcGenerator(registry);
        return new GeneratorViewModel(registry, npcGenerator, new SystemRandomSource(seed));
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
}