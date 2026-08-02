using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;

using CommunityToolkit.Mvvm.Input;

namespace GmToolkit.UI.Views;

/// <summary>
/// Desktop's "the database couldn't be opened at startup" screen -- shown by
/// <c>App.axaml.cs</c>'s <c>OnFrameworkInitializationCompleted</c> in place of the normal
/// splash/shell sequence when <see cref="App.StartupError"/> is set (see that property's remarks).
/// </summary>
/// <remarks>
/// No <c>ViewModel</c>: this window can be shown before the DI container even exists (that's
/// exactly the failure case it exists for), so it exposes its own tiny bit of state directly as
/// Avalonia properties rather than following this codebase's usual MVVM-with-a-resolved-view-model
/// convention.
/// </remarks>
public partial class StartupErrorWindow : Window
{
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<StartupErrorWindow, string?>(nameof(Message));

    public StartupErrorWindow()
    {
        // Closing the window is enough to end the process: this is always the classic desktop
        // lifetime's only/MainWindow, so closing it triggers the default
        // ShutdownMode.OnLastWindowClose, which unwinds StartWithClassicDesktopLifetime back in
        // GmToolkit.Desktop/Program.cs exactly like a normal shutdown -- no need to reach for
        // Environment.Exit directly from here.
        CloseCommand = new RelayCommand(Close);
        InitializeComponent();
    }

    /// <summary>The friendly <see cref="Core.Repositories.DataAccessException.Message"/> to show.</summary>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public ICommand CloseCommand { get; }
}