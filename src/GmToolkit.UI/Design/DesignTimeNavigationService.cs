using System.ComponentModel;

using GmToolkit.UI.Services;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Design;

/// <summary>
/// No-op <see cref="INavigationService"/> used only to construct <see cref="GeneratorViewModel"/>
/// for the XAML previewer's <c>Design.DataContext</c> (see <see cref="GeneratorViewModel"/>'s own
/// parameterless constructor) -- mirrors <see cref="DesignTimeNpcRepository"/>/
/// <see cref="DesignTimeCampaignRepository"/>. <see cref="NavigateTo"/> is intentionally a no-op:
/// the previewer never actually invokes <see cref="GeneratorViewModel"/>'s save/navigate command,
/// and this type exists purely to satisfy the constructor parameter. Never used at runtime; both
/// real heads resolve <see cref="INavigationService"/> from the DI container instead (see
/// <c>ServiceCollectionExtensions.AddGmToolkitUi</c>), and <see cref="Services.NavigationService"/>
/// itself supplies the real instance to <see cref="GeneratorViewModel"/> by passing <c>this</c> into
/// its own factory lambda.
/// </summary>
internal sealed class DesignTimeNavigationService : INavigationService
{
    public NavigationDestination CurrentDestination { get; private set; }

    public ViewModelBase CurrentViewModel { get; } = new SettingsViewModel();

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NavigateTo(NavigationDestination destination)
    {
        CurrentDestination = destination;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentDestination)));
    }
}