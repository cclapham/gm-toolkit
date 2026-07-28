using System;
using System.Threading.Tasks;

using Avalonia;

using GmToolkit.Core.Services;
using GmToolkit.Data;
using GmToolkit.UI;

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
        var databasePath = AppDataPaths.GetDesktopDatabasePath();
        var database = await GmToolkitDatabase.CreateAndInitializeAsync(databasePath);

        var services = new ServiceCollection();
        services.AddGmToolkitData(database);
        services.AddGmToolkitUi();
        var serviceProvider = services.BuildServiceProvider();

        // Make the container reachable from GmToolkit.UI (see App.Services' doc comment), and
        // restore whichever campaign was last opened before any UI is shown.
        App.Services = serviceProvider;
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