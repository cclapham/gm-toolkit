using System.ComponentModel;

using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Services;

/// <summary>
/// Swaps which screen view model the app shell displays. This is a presentation concern tied to
/// <c>GmToolkit.UI</c>'s own view model types — not domain logic — so per CONTRIBUTING.md's
/// "business logic belongs in Core" rule, it lives here rather than in
/// <c>GmToolkit.Core</c>. Avalonia has no built-in router, hence this small hand-rolled one.
/// </summary>
public interface INavigationService : INotifyPropertyChanged
{
    /// <summary>Which of the 5 destinations is currently showing.</summary>
    NavigationDestination CurrentDestination { get; }

    /// <summary>
    /// The view model for <see cref="CurrentDestination"/>. The shell binds this to a
    /// <c>ContentControl</c>; the app-wide <see cref="GmToolkit.UI.ViewLocator"/> resolves it to
    /// the matching view by the usual <c>FooViewModel</c> -&gt; <c>FooView</c> naming convention.
    /// </summary>
    ViewModelBase CurrentViewModel { get; }

    /// <summary>
    /// Navigates to <paramref name="destination"/>, raising <see cref="INotifyPropertyChanged.PropertyChanged"/>
    /// for both <see cref="CurrentDestination"/> and <see cref="CurrentViewModel"/>. A no-op if
    /// already there.
    /// </summary>
    void NavigateTo(NavigationDestination destination);
}