using System.Collections;

using Avalonia;
using Avalonia.Controls;

namespace GmToolkit.UI.Controls;

/// <summary>
/// Reusable toast/notification host (issue #32): a stack of dismissible, auto-expiring toasts
/// anchored to one corner. <c>ShellView.axaml</c> is the one place this is instantiated, bound to
/// <c>ShellViewModel.Toasts</c> (itself <c>INotificationService.Toasts</c>) -- see that view's
/// remarks for why the shell, specifically, is the right host, mirroring <c>EmptyState</c>/
/// <c>LoadingIndicator</c>'s "reusable layout primitive in <c>Controls/</c>" convention (issue #23).
/// </summary>
/// <remarks>
/// <b><see cref="ItemsSource"/> is a plain <see cref="IEnumerable"/> StyledProperty</b>, not typed
/// to <c>ObservableCollection&lt;ToastViewModel&gt;</c> specifically -- matches
/// <see cref="ItemsControl.ItemsSource"/>'s own type, and keeps this control from needing to know
/// about <see cref="Services.INotificationService"/> at all, only about the
/// <c>GmToolkit.UI.ViewModels.ToastViewModel</c> shape its <c>DataTemplate</c> (in the .axaml)
/// binds against.
/// </remarks>
public partial class ToastHost : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<ToastHost, IEnumerable?>(nameof(ItemsSource));

    public ToastHost()
    {
        InitializeComponent();
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
}