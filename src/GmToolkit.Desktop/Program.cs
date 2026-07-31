using System;
using System.Threading.Tasks;

using Avalonia;

using GmToolkit.Core.Repositories;
using GmToolkit.Core.Services;
using GmToolkit.Data;
using GmToolkit.UI;
using GmToolkit.UI.Services;

using Microsoft.Extensions.DependencyInjection;

namespace GmToolkit.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static async Task Main(string[] args)
    {
        // Global exception handling (issue #32) -- installed before anything else (including DB
        // bootstrap below, which is itself capable of throwing) so nothing in this method runs
        // without at least the AppDomain/TaskScheduler safety net in place. Only touches
        // AppDomain/TaskScheduler, neither of which is Avalonia/SynchronizationContext-reliant, so
        // this is safe this early -- see GlobalExceptionHandler's remarks for the full design and
        // why the Dispatcher-based hook (App.axaml.cs's job) has to wait until later instead.
        GlobalExceptionHandler.InstallProcessWideHandlers();

        var databasePath = AppDataPaths.GetDesktopDatabasePath();

        GmToolkitDatabase database;
        try
        {
            database = await GmToolkitDatabase.CreateAndInitializeAsync(databasePath);
        }
        catch (DataAccessException ex)
        {
            // Startup DB bootstrap failed with a friendly, actionable message (see
            // GmToolkitDatabase.CreateAndInitializeAsync's remarks -- e.g. disk full, the file
            // temporarily read-only, or another copy of GM Toolkit holding a lock). There's no
            // database to hand the rest of this composition root, so skip straight to
            // App.StartupError's screen (shared with GmToolkit.Android/Application.cs, see that
            // property's remarks) instead of letting this propagate as an unhandled exception
            // (silent crash, no window ever drawn) or exiting with no explanation at all. The user
            // fixes the underlying condition and relaunches.
            App.StartupError = ex.Message;
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            Environment.Exit(1);
            return;
        }

        var settingsPath = AppDataPaths.GetDesktopSettingsPath();
        var appSettingsService = new AppSettingsService(settingsPath);

        var services = new ServiceCollection();
        services.AddGmToolkitData(database, appSettingsService);
        services.AddGmToolkitUi();
        var serviceProvider = services.BuildServiceProvider();

        // Make the container reachable from GmToolkit.UI (see App.Services' doc comment), resolve
        // the persisted theme preference (issue #31) so it can be applied before any UI is shown
        // (see App.InitialThemePreference's doc comment), and restore whichever campaign was last
        // opened before any UI is shown.
        App.Services = serviceProvider;
        App.InitialThemePreference = await appSettingsService.GetThemePreferenceAsync();
        await serviceProvider.GetRequiredService<ActiveCampaignContext>().RestoreLastOpenedAsync();

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            // ConfigureAwait(false): StartWithClassicDesktopLifetime installs Avalonia's own
            // SynchronizationContext on this thread for the message loop's duration. Once that
            // loop has stopped pumping (window closed / shutdown fired), a captured-context
            // continuation never runs -- these awaits would hang forever without this.
            await serviceProvider.DisposeAsync().ConfigureAwait(false);
            await database.DisposeAsync().ConfigureAwait(false);
        }

        // StartWithClassicDesktopLifetime returning means Avalonia's own shutdown sequence already
        // ran (the last window closed, the default ShutdownMode.OnLastWindowClose fired) -- but on
        // at least this environment's Linux/X11 setup, the OS process doesn't actually terminate
        // afterward; something (the X11 platform backend's own thread, a native SQLite thread, or
        // similar) outlives a normal return from Main, leaving the process running with no window
        // and no way for the user to know short of Ctrl-C in the terminal that launched it. Force
        // the process to actually exit rather than leave it silently running.
        Environment.Exit(0);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}