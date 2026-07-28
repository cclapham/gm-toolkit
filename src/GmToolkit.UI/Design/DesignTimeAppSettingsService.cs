using GmToolkit.Core.Services;

namespace GmToolkit.UI.Design;

/// <summary>
/// No-op, always-<see cref="ThemePreference.System"/> <see cref="IAppSettingsService"/> used only
/// to construct <see cref="ViewModels.SettingsViewModel"/> for the XAML previewer's
/// <c>Design.DataContext</c> and its own parameterless constructor -- mirrors
/// <see cref="DesignTimeNavigationService"/>/<see cref="DesignTimeCampaignRepository"/>. Never used
/// at runtime; both real heads resolve <see cref="IAppSettingsService"/> from the DI container
/// instead (see <c>GmToolkit.Data.ServiceCollectionExtensions.AddGmToolkitData</c>).
/// </summary>
internal sealed class DesignTimeAppSettingsService : IAppSettingsService
{
    public Task<ThemePreference> GetThemePreferenceAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ThemePreference.System);

    public Task SetThemePreferenceAsync(ThemePreference preference, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}