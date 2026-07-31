using System;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using GmToolkit.Core.Services;
using GmToolkit.UI.Controls;
using GmToolkit.UI.Services;
using GmToolkit.UI.ViewModels;
using GmToolkit.UI.Views;

using Microsoft.Extensions.DependencyInjection;

namespace GmToolkit.UI;

public partial class App : Application
{
    /// <summary>
    /// The composition root's DI container. Avalonia constructs <see cref="App"/> itself (via
    /// <c>AppBuilder.Configure&lt;App&gt;()</c>), so the provider can't be threaded through a
    /// constructor parameter — each head (<c>GmToolkit.Desktop</c>/<c>GmToolkit.Android</c>) sets
    /// this static property after building its <c>ServiceProvider</c> and before starting the
    /// Avalonia lifetime, so it's reachable from here once view models need it (used below to
    /// resolve <see cref="ShellViewModel"/>).
    /// </summary>
    /// <remarks>
    /// Typed as <see cref="IServiceProvider"/> (framework type, no extra package reference)
    /// rather than the concrete <c>Microsoft.Extensions.DependencyInjection.ServiceProvider</c>,
    /// so <c>GmToolkit.UI</c> doesn't need a package reference just to hold this.
    /// </remarks>
    public static IServiceProvider? Services { get; internal set; }

    /// <summary>
    /// The theme preference (issue #31) resolved from <see cref="IAppSettingsService"/> by the
    /// composition root before the Avalonia lifetime starts -- set the same way and for the same
    /// reason as <see cref="Services"/> (Avalonia constructs <see cref="App"/> itself, so this
    /// can't be threaded through a constructor parameter). Applied at the very top of
    /// <see cref="OnFrameworkInitializationCompleted"/>, before any window/view is constructed, so
    /// there is never a flash of the wrong theme on startup -- mirrors how
    /// <c>ActiveCampaignContext.RestoreLastOpenedAsync</c> is already awaited before either head
    /// starts the Avalonia lifetime at all.
    /// </summary>
    public static ThemePreference InitialThemePreference { get; internal set; } = ThemePreference.System;

    /// <summary>
    /// Set by a composition root (<c>GmToolkit.Desktop/Program.cs</c>/<c>GmToolkit.Android/Application.cs</c>)
    /// when <c>GmToolkit.Data.GmToolkitDatabase.CreateAndInitializeAsync</c> throws a
    /// <see cref="GmToolkit.Core.Repositories.DataAccessException"/> during startup -- a transient
    /// failure (disk full, file briefly read-only/locked, etc.) that leaves no database, and
    /// therefore no <see cref="Services"/>, to build the normal shell against. When this is set,
    /// <see cref="OnFrameworkInitializationCompleted"/> shows a friendly error screen carrying
    /// <see cref="GmToolkit.Core.Repositories.DataAccessException.Message"/> instead of the normal
    /// splash/shell sequence, rather than letting the composition root either crash with no
    /// explanation or silently exit -- see <c>GmToolkitDatabase.CreateAndInitializeAsync</c>'s
    /// remarks for what the caller (this property's two setters) is expected to do with a failure.
    /// There's nothing to retry in-process (the transient condition is external -- e.g. free up
    /// disk space, restore write access, close another copy holding the lock); the error screen's
    /// only action is to close, and the user relaunches once the condition is cleared.
    /// </summary>
    public static string? StartupError { get; internal set; }

    /// <summary>
    /// How long <see cref="SplashWindow"/> stays up before <see cref="MainWindow"/> replaces it —
    /// see that window's remarks for why this is a fixed cosmetic delay, not tied to real work.
    /// </summary>
    private static readonly TimeSpan SplashDuration = TimeSpan.FromSeconds(3);

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Global exception handling (issue #32): Dispatcher.UIThread is only guaranteed to exist
        // once Avalonia's platform backend is set up, which has already happened by the time this
        // method runs -- see GlobalExceptionHandler's remarks for why this specific hook (as
        // opposed to the AppDomain/TaskScheduler ones each head installs at the very top of its own
        // Main/OnCreate, before any of this) has to wait until here. Installed unconditionally,
        // even on the StartupError branch below, so a failure while merely showing the error
        // screen still gets the same net rather than none at all.
        GlobalExceptionHandler.InstallDispatcherHandler();

        // Apply the persisted theme preference before constructing any window/view below -- see
        // InitialThemePreference's remarks. Left at its ThemePreference.System default on the
        // StartupError branch below (the composition root never got as far as loading the real
        // preference), which is a fine fallback for a screen that only has to be legible once.
        ThemeApplier.Apply(this, InitialThemePreference);

        // Startup DB bootstrap failed -- see StartupError's remarks. There is no DI container to
        // resolve a real shell against in this case, so show the friendly error screen instead
        // and skip everything below that assumes Services is set.
        if (StartupError is { } startupError)
        {
            ShowStartupError(startupError);
            base.OnFrameworkInitializationCompleted();
            return;
        }

        // Services is only unset here if a composition root forgot to set it before starting the
        // Avalonia lifetime -- there's no legitimate runtime path where it's null (the XAML
        // previewer never reaches this method at all; it renders views directly via their
        // Design.DataContext, see e.g. ShellView.axaml). Fail loudly rather than silently
        // constructing a broken shell.
        var services = Services ?? throw new InvalidOperationException(
            $"{nameof(App)}.{nameof(Services)} must be set by the composition root " +
            "(GmToolkit.Desktop/Program.cs or GmToolkit.Android/Application.cs) before the Avalonia lifetime starts.");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var splash = new SplashWindow();
            splash.Show();

            var mainWindow = new MainWindow
            {
                DataContext = services.GetRequiredService<ShellViewModel>()
            };

            // Fire-and-forget: exceptions surface via GlobalExceptionHandler's Dispatcher hook
            // (installed above) once the awaited delay resumes back on this UI thread.
            _ = ShowMainWindowAfterSplashAsync(desktop, splash, mainWindow);
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory =
                () => new ShellView { DataContext = services.GetRequiredService<ShellViewModel>() };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new ShellView
            {
                DataContext = services.GetRequiredService<ShellViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Shows <paramref name="message"/> in place of the normal splash/shell sequence -- see
    /// <see cref="StartupError"/>'s remarks for when/why this runs instead of the usual path.
    /// </summary>
    /// <remarks>
    /// Covers the same three <see cref="ApplicationLifetime"/> shapes as the normal path below it
    /// (classic desktop, Android's <see cref="IActivityApplicationLifetime"/>, and the generic
    /// <see cref="ISingleViewApplicationLifetime"/>), but skips <see cref="SplashWindow"/> entirely
    /// on the classic-desktop branch -- there is nothing worth branding a wait for when startup has
    /// already failed, and the whole point is to get the explanation in front of the user as fast
    /// as possible instead of behind a further 3-second delay.
    /// </remarks>
    private void ShowStartupError(string message)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var errorWindow = new StartupErrorWindow { Message = message };
            desktop.MainWindow = errorWindow;
            errorWindow.Show();
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory =
                () => new EmptyState { Icon = "⚠️", Title = "GM Toolkit couldn't start", Message = message };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView =
                new EmptyState { Icon = "⚠️", Title = "GM Toolkit couldn't start", Message = message };
        }
    }

    /// <summary>
    /// Waits out <see cref="SplashDuration"/>, then swaps <paramref name="splash"/> for
    /// <paramref name="mainWindow"/> as the classic desktop lifetime's <c>MainWindow</c>.
    /// </summary>
    /// <remarks>
    /// <paramref name="mainWindow"/> is shown (and <c>desktop.MainWindow</c> reassigned to it)
    /// <i>before</i> <paramref name="splash"/> closes, not after — with the default
    /// <c>ShutdownMode.OnLastWindowClose</c>, closing the splash while it's still the only open
    /// window would tear down the whole app before the real window ever appeared.
    /// </remarks>
    private static async Task ShowMainWindowAfterSplashAsync(
        IClassicDesktopStyleApplicationLifetime desktop, Window splash, Window mainWindow)
    {
        await Task.Delay(SplashDuration);

        desktop.MainWindow = mainWindow;
        mainWindow.Show();
        splash.Close();
    }
}