using System;
using System.Threading.Tasks;

using Avalonia;

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
        await using var serviceProvider = services.BuildServiceProvider();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}