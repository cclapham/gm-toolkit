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
        await using var database = await GmToolkitDatabase.CreateAndInitializeAsync(databasePath);

        var services = new ServiceCollection();
        services.AddGmToolkitData(database);
        services.AddGmToolkitUi();
        await using var serviceProvider = services.BuildServiceProvider();

        // Make the container reachable from GmToolkit.UI (see App.Services' doc comment), and
        // restore whichever campaign was last opened before any UI is shown.
        App.Services = serviceProvider;
        await serviceProvider.GetRequiredService<ActiveCampaignContext>().RestoreLastOpenedAsync();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}