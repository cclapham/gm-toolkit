using Avalonia.Controls;

namespace GmToolkit.UI.Views;

/// <summary>
/// Shown for a fixed 3 seconds on desktop startup before <see cref="MainWindow"/> replaces it —
/// see <c>App.axaml.cs</c>'s classic-desktop-lifetime branch for the sequencing.
/// </summary>
/// <remarks>
/// The progress bar's fill (an <c>Animation</c> in <c>SplashWindow.axaml</c>) is deliberately fake:
/// by the time this window is shown, the composition root (<c>GmToolkit.Desktop/Program.cs</c>)
/// has already finished DB initialization, settings load, and restoring the last-opened campaign —
/// the real cold-start work is done in ~200ms (see issue #33's profiling), so there is nothing left
/// to report real progress on. This is branding, not a loading screen.
/// Desktop-only: Android already has its own OS-level splash (<c>Resources/drawable/splash_screen.xml</c>
/// plus the AndroidX SplashScreen library, shown before Avalonia even starts), so this window is
/// only ever constructed from the <c>IClassicDesktopStyleApplicationLifetime</c> branch.
/// </remarks>
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }
}