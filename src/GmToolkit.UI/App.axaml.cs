using System;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using GmToolkit.Core.Services;
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
        // Services is only unset here if a composition root forgot to set it before starting the
        // Avalonia lifetime -- there's no legitimate runtime path where it's null (the XAML
        // previewer never reaches this method at all; it renders views directly via their
        // Design.DataContext, see e.g. ShellView.axaml). Fail loudly rather than silently
        // constructing a broken shell.
        var services = Services ?? throw new InvalidOperationException(
            $"{nameof(App)}.{nameof(Services)} must be set by the composition root " +
            "(GmToolkit.Desktop/Program.cs or GmToolkit.Android/Application.cs) before the Avalonia lifetime starts.");

        // Global exception handling (issue #32): Dispatcher.UIThread is only guaranteed to exist
        // once Avalonia's platform backend is set up, which has already happened by the time this
        // method runs -- see GlobalExceptionHandler's remarks for why this specific hook (as
        // opposed to the AppDomain/TaskScheduler ones each head installs at the very top of its own
        // Main/OnCreate, before any of this) has to wait until here.
        GlobalExceptionHandler.InstallDispatcherHandler();

        // Apply the persisted theme preference before constructing any window/view below -- see
        // InitialThemePreference's remarks.
        ThemeApplier.Apply(this, InitialThemePreference);

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