using Avalonia.Controls;

namespace GmToolkit.UI.Services;

/// <summary>
/// Holds the app's single live <see cref="TopLevel"/> (the desktop <c>MainWindow</c>, or the
/// Android/single-view lifetime's root view once it's attached to the visual tree), so
/// <see cref="IFileDialogService"/> has somewhere to resolve <c>StorageProvider</c> from without
/// every view model needing its own reference to an Avalonia visual -- which would break the MVVM
/// boundary this app otherwise keeps (view models are plain, directly constructible classes with no
/// Avalonia control references; see e.g. <c>CampaignFormViewModel</c>'s design throughout).
/// </summary>
/// <remarks>
/// <see cref="Views.ShellView"/> -- the one root visual every application lifetime this app
/// supports mounts (see <c>App.axaml.cs</c>'s three <c>ApplicationLifetime</c> branches) -- sets
/// <see cref="Current"/> from its own code-behind once it attaches to the visual tree, mirroring
/// how <c>Controls/ToastHost.axaml</c> is hosted at the shell level for the same "works identically
/// regardless of which lifetime shape is active" reason (see
/// <see cref="INotificationService.Toasts"/>'s remarks). Avalonia's
/// <see cref="Avalonia.Controls.ApplicationLifetimes.IActivityApplicationLifetime"/> (one of
/// Android's two possible lifetime shapes -- see <c>App.axaml.cs</c>) exposes no getter for its
/// current view at all, only a <c>MainViewFactory</c> the framework itself calls, so resolving a
/// <see cref="TopLevel"/> from <see cref="Avalonia.Application.Current"/>'s
/// <see cref="Avalonia.Application.ApplicationLifetime"/> at call time (the way
/// <c>ShellViewModel.Exit</c> resolves <see cref="Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime"/>)
/// isn't reliable across every lifetime shape this app runs under; capturing it once from the
/// visual tree itself (the same trick <c>Markdown/MarkdownRenderer.OpenLink</c> already uses for a
/// single control, just hoisted to the shell so every screen can share one instance) works
/// everywhere.
/// </remarks>
public interface ITopLevelProvider
{
    /// <summary>The current <see cref="TopLevel"/>, or <c>null</c> before <see cref="Views.ShellView"/>
    /// has attached to the visual tree (never actually observed at runtime -- by the time any
    /// screen's view model can invoke <see cref="IFileDialogService"/>, the shell that hosts it has
    /// already attached) or in a plain xUnit test/the XAML previewer, neither of which runs a real
    /// Avalonia application lifetime.</summary>
    TopLevel? Current { get; set; }
}