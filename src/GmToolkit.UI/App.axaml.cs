using System;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = services.GetRequiredService<ShellViewModel>()
            };
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
}