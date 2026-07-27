using System.ComponentModel;

using GmToolkit.UI.Services;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Tests.Fakes;

/// <summary>In-memory <see cref="INavigationService"/> for testing
/// <see cref="GmToolkit.UI.ViewModels.GeneratorViewModel"/>'s <c>ViewSavedNpcCommand</c> (issue #29)
/// without a real app shell -- records every destination passed to <see cref="NavigateTo"/> so tests
/// can assert on the call itself, not just a property that happened to change.</summary>
internal sealed class FakeNavigationService : INavigationService
{
    public NavigationDestination CurrentDestination { get; private set; }

    public ViewModelBase CurrentViewModel { get; } = new SettingsViewModel();

    public List<NavigationDestination> NavigatedTo { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NavigateTo(NavigationDestination destination)
    {
        NavigatedTo.Add(destination);
        CurrentDestination = destination;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentDestination)));
    }
}