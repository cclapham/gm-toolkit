using GmToolkit.Core.Services;

namespace GmToolkit.UI.Tests.Fakes;

/// <summary>In-memory <see cref="IAppSettingsService"/> for testing <see cref="ViewModels.SettingsViewModel"/>
/// without touching the filesystem. Completes every call synchronously (via <see cref="Task.FromResult{TResult}"/>/
/// <see cref="Task.CompletedTask"/>), same as <see cref="FakeCampaignRepository"/>, so a
/// constructor's fire-and-forget initial load has always finished by the time the constructor
/// returns.</summary>
internal sealed class FakeAppSettingsService(ThemePreference initialPreference = ThemePreference.System) : IAppSettingsService
{
    private ThemePreference _preference = initialPreference;

    /// <summary>Every preference passed to <see cref="SetThemePreferenceAsync"/>, in call order --
    /// lets tests assert both that a save happened and, separately, that one didn't (e.g. loading
    /// the initial preference shouldn't immediately re-save it).</summary>
    public List<ThemePreference> SavedPreferences { get; } = [];

    public Task<ThemePreference> GetThemePreferenceAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_preference);

    public Task SetThemePreferenceAsync(ThemePreference preference, CancellationToken cancellationToken = default)
    {
        _preference = preference;
        SavedPreferences.Add(preference);
        return Task.CompletedTask;
    }
}