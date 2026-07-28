using Avalonia;
using Avalonia.Styling;

using GmToolkit.Core.Services;

namespace GmToolkit.UI.Services;

/// <summary>
/// Maps the platform-agnostic <see cref="ThemePreference"/> (persisted via
/// <see cref="Core.Services.IAppSettingsService"/>) to Avalonia's own <see cref="ThemeVariant"/>,
/// and applies it to a running <see cref="Application"/> (issue #31).
/// </summary>
/// <remarks>
/// This is the one place that knows the <see cref="ThemePreference"/> -&gt; <see cref="ThemeVariant"/>
/// mapping, used by both the startup path (<c>App.axaml.cs</c>'s <c>OnFrameworkInitializationCompleted</c>,
/// applying whichever preference the composition root resolved before the Avalonia lifetime
/// started) and the live-switch path (<c>SettingsViewModel</c>, applying immediately when the user
/// changes the selection). <see cref="Application.RequestedThemeVariant"/> is a real
/// <see cref="Avalonia.AvaloniaProperty"/>, not fixed at XAML-parse time -- setting it at runtime
/// re-resolves every <c>DynamicResource</c>-bound brush across the whole app (Colors.axaml's
/// <c>ResourceDictionary.ThemeDictionaries</c> only offers <c>Light</c>/<c>Dark</c> keys via that
/// mechanism), which is exactly what makes a live theme switch from Settings work at all.
/// </remarks>
public static class ThemeApplier
{
    /// <summary>Applies <paramref name="preference"/> to <paramref name="application"/>'s
    /// <see cref="Application.RequestedThemeVariant"/>.</summary>
    public static void Apply(Application application, ThemePreference preference)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.RequestedThemeVariant = ToThemeVariant(preference);
    }

    /// <summary>Maps <see cref="ThemePreference.System"/> to <see cref="ThemeVariant.Default"/>
    /// (follow the OS/platform, same as <c>App.axaml</c>'s original <c>RequestedThemeVariant="Default"</c>),
    /// and <see cref="ThemePreference.Light"/>/<see cref="ThemePreference.Dark"/> to their
    /// like-named <see cref="ThemeVariant"/> values.</summary>
    public static ThemeVariant ToThemeVariant(ThemePreference preference) => preference switch
    {
        ThemePreference.Light => ThemeVariant.Light,
        ThemePreference.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };
}