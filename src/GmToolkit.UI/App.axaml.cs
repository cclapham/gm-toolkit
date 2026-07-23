using System;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using GmToolkit.UI.ViewModels;
using GmToolkit.UI.Views;

namespace GmToolkit.UI;

public partial class App : Application
{
    /// <summary>
    /// The composition root's DI container. Avalonia constructs <see cref="App"/> itself (via
    /// <c>AppBuilder.Configure&lt;App&gt;()</c>), so the provider can't be threaded through a
    /// constructor parameter — each head (<c>GmToolkit.Desktop</c>/<c>GmToolkit.Android</c>) sets
    /// this static property after building its <c>ServiceProvider</c> and before starting the
    /// Avalonia lifetime, so it's reachable from here once view models need it (#15 builds the
    /// real navigation/view-model wiring on top of this).
    /// </summary>
    /// <remarks>
    /// Typed as <see cref="IServiceProvider"/> (framework type, no extra package reference)
    /// rather than the concrete <c>Microsoft.Extensions.DependencyInjection.ServiceProvider</c>,
    /// so <c>GmToolkit.UI</c> doesn't need a package reference just to hold this.
    /// </remarks>
    public static IServiceProvider? Services { get; internal set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView { DataContext = new MainViewModel() };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}