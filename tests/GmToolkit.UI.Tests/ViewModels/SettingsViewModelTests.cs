using GmToolkit.Core.Services;
using GmToolkit.UI.Tests.Fakes;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Tests.ViewModels;

/// <remarks>
/// <see cref="FakeAppSettingsService"/> completes every call synchronously, so
/// <see cref="SettingsViewModel"/>'s fire-and-forget initial load (kicked off from its
/// constructor) has always finished by the time the constructor returns -- no extra waiting
/// needed, same reasoning as <c>CampaignsViewModelTests</c>.
/// </remarks>
public class SettingsViewModelTests
{
    [Fact]
    public void ThemeOptions_lists_exactly_System_Light_and_Dark()
    {
        var vm = new SettingsViewModel(new FakeAppSettingsService());

        Assert.Equal([ThemePreference.System, ThemePreference.Light, ThemePreference.Dark], vm.ThemeOptions);
    }

    [Fact]
    public void With_no_preference_ever_saved_SelectedTheme_defaults_to_System()
    {
        var vm = new SettingsViewModel(new FakeAppSettingsService());

        Assert.Equal(ThemePreference.System, vm.SelectedTheme);
    }

    [Theory]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    [InlineData(ThemePreference.System)]
    public void Constructor_loads_the_persisted_preference_into_SelectedTheme(ThemePreference persisted)
    {
        var vm = new SettingsViewModel(new FakeAppSettingsService(persisted));

        Assert.Equal(persisted, vm.SelectedTheme);
    }

    [Fact]
    public void Loading_the_persisted_preference_does_not_immediately_re_save_it()
    {
        var appSettingsService = new FakeAppSettingsService(ThemePreference.Dark);

        _ = new SettingsViewModel(appSettingsService);

        Assert.Empty(appSettingsService.SavedPreferences);
    }

    [Fact]
    public void Changing_SelectedTheme_persists_the_new_preference()
    {
        var appSettingsService = new FakeAppSettingsService();
        var vm = new SettingsViewModel(appSettingsService);

        vm.SelectedTheme = ThemePreference.Dark;

        Assert.Equal([ThemePreference.Dark], appSettingsService.SavedPreferences);
    }

    [Fact]
    public void Changing_SelectedTheme_multiple_times_persists_each_change_in_order()
    {
        var appSettingsService = new FakeAppSettingsService();
        var vm = new SettingsViewModel(appSettingsService);

        vm.SelectedTheme = ThemePreference.Light;
        vm.SelectedTheme = ThemePreference.Dark;
        vm.SelectedTheme = ThemePreference.System;

        Assert.Equal([ThemePreference.Light, ThemePreference.Dark, ThemePreference.System], appSettingsService.SavedPreferences);
    }

    [Fact]
    public void The_design_time_constructor_defaults_to_System_without_a_real_settings_service()
    {
        var vm = new SettingsViewModel();

        Assert.Equal(ThemePreference.System, vm.SelectedTheme);
    }
}