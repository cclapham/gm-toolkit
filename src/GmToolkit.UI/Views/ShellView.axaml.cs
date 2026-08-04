using Avalonia.Controls;

using GmToolkit.UI.Services;

using Microsoft.Extensions.DependencyInjection;

namespace GmToolkit.UI.Views;

public partial class ShellView : UserControl
{
    public ShellView()
    {
        InitializeComponent();

        // Captures this app's single live TopLevel once this shell (the one root visual every
        // application lifetime mounts -- see App.axaml.cs) attaches to the visual tree, so
        // IFileDialogService (issues #130-#132) has somewhere to resolve StorageProvider from --
        // see ITopLevelProvider's remarks for why this can't instead be resolved lazily from
        // Avalonia.Application.Current.ApplicationLifetime at call time. App.Services is null in the
        // XAML previewer (which never runs a real Avalonia lifetime at all -- see App.Services'
        // own doc comment), so this is a no-op there rather than a NullReferenceException.
        AttachedToVisualTree += (_, _) =>
        {
            if (App.Services?.GetService<ITopLevelProvider>() is { } topLevelProvider)
            {
                topLevelProvider.Current = TopLevel.GetTopLevel(this);
            }
        };
    }
}