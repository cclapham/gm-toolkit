namespace GmToolkit.Core.Services;

/// <summary>
/// The user's theme preference (issue #31).
/// </summary>
/// <remarks>
/// <see cref="System"/> follows whatever the OS/platform reports (Avalonia's own
/// <c>ThemeVariant.Default</c>), which works out of the box on Windows and on Linux desktops that
/// expose a theme preference via the desktop portal. <see cref="Light"/>/<see cref="Dark"/> are an
/// explicit manual override that works everywhere else, including platforms where OS theme
/// detection doesn't apply at all -- Raspberry Pi/embedded Linux may not report a theme preference,
/// per issue #31's own caveat. This enum is the persisted, platform-agnostic representation of that
/// choice; <c>GmToolkit.UI.Services.ThemeApplier</c> maps it to Avalonia's <c>ThemeVariant</c> type
/// so this type itself can stay free of any Avalonia reference (CONTRIBUTING.md's "keep Core
/// clean" rule).
/// </remarks>
public enum ThemePreference
{
    /// <summary>Follow the OS/platform theme wherever Avalonia's platform detection supports it.
    /// This is the app's default and matches <c>App.axaml</c>'s <c>RequestedThemeVariant="Default"</c>.</summary>
    System,

    /// <summary>Always use the light (sepia/parchment) palette, regardless of the OS theme.</summary>
    Light,

    /// <summary>Always use the dark palette, regardless of the OS theme.</summary>
    Dark,
}