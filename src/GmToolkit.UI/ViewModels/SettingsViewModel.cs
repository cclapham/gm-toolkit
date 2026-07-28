using Avalonia;

using CommunityToolkit.Mvvm.ComponentModel;

using GmToolkit.Core.Services;
using GmToolkit.UI.Design;
using GmToolkit.UI.Services;

namespace GmToolkit.UI.ViewModels;

/// <summary>
/// The Settings screen (issue #31): today, just the theme preference (System/Light/Dark) -- the
/// manual override that makes "everywhere else" in that issue's task list actually work, since it
/// doesn't depend on Avalonia's platform theme detection at all. Always reachable, regardless of
/// whether a campaign is active (see <c>ShellViewModel</c>'s nav gating, which never gates
/// Settings).
/// </summary>
/// <remarks>
/// <para>
/// <b>Loads asynchronously from the constructor</b> (<see cref="LoadAsync"/>, fire-and-forget),
/// same idiom as <see cref="CampaignsViewModel"/>/every other list-backed view model in this app --
/// there's no synchronous way to read <see cref="IAppSettingsService.GetThemePreferenceAsync"/>'s
/// result before the constructor returns, and blocking on it would violate CONTRIBUTING.md's
/// "no blocking waits on async calls" rule for no good reason (unlike Android's <c>OnCreate</c>,
/// this constructor has no platform-lifecycle excuse to do otherwise).
/// </para>
/// <para>
/// <b><see cref="_isRestoringPersistedPreference"/> guards against re-persisting the value that was
/// just loaded.</b> Setting <see cref="SelectedTheme"/> from <see cref="LoadAsync"/> would
/// otherwise re-trigger <see cref="OnSelectedThemeChanged"/> and immediately write back the exact
/// value that was just read -- harmless, but a pointless extra disk write every single launch.
/// </para>
/// <para>
/// <b>Applying the theme live goes through <see cref="ThemeApplier"/>, guarded by an
/// <see cref="Application.Current"/> null-check.</b> <see cref="Application.Current"/> is only
/// non-null once a real Avalonia lifetime has started (never true in this project's xunit tests,
/// which construct this view model directly with a fake <see cref="IAppSettingsService"/> and no
/// running <see cref="Application"/>) -- guarding it keeps this class runtime-safe without needing
/// a design-/test-time seam just for that one line, while every real head (which does have a
/// running <see cref="Application"/>) gets the actual live theme switch.
/// </para>
/// </remarks>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly IAppSettingsService _appSettingsService;
    private bool _isRestoringPersistedPreference;

    public SettingsViewModel(IAppSettingsService appSettingsService)
    {
        _appSettingsService = appSettingsService;
        _ = LoadAsync();
    }

    /// <summary>Design-time-only constructor for the XAML previewer's <c>Design.DataContext</c>
    /// -- mirrors the pattern used throughout <c>GmToolkit.UI.ViewModels</c>. Never used at
    /// runtime; both heads resolve the constructor above via <c>Services.NavigationService</c>.</summary>
    public SettingsViewModel()
        : this(new DesignTimeAppSettingsService())
    {
    }

    /// <summary>The full set of selectable theme preferences, in display order, for the
    /// Settings screen's selector.</summary>
    public IReadOnlyList<ThemePreference> ThemeOptions { get; } = Enum.GetValues<ThemePreference>();

    [ObservableProperty]
    public partial ThemePreference SelectedTheme { get; set; } = ThemePreference.System;

    partial void OnSelectedThemeChanged(ThemePreference value)
    {
        if (_isRestoringPersistedPreference)
        {
            return;
        }

        if (Application.Current is not null)
        {
            ThemeApplier.Apply(Application.Current, value);
        }

        _ = _appSettingsService.SetThemePreferenceAsync(value);
    }

    private async Task LoadAsync()
    {
        var preference = await _appSettingsService.GetThemePreferenceAsync();

        _isRestoringPersistedPreference = true;
        SelectedTheme = preference;
        _isRestoringPersistedPreference = false;
    }
}