using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using GmToolkit.UI.Services;

namespace GmToolkit.UI.ViewModels;

/// <summary>
/// One toast/notification (issue #32) shown by <c>Controls/ToastHost.axaml</c>, bound from
/// <c>ShellView.axaml</c> to <see cref="NotificationService.Toasts"/>. Instances are only ever
/// constructed by <see cref="NotificationService"/> itself, which owns both auto-dismiss (a timer)
/// and manual dismiss (<see cref="DismissCommand"/>, wired to the same removal path) -- see that
/// class's remarks.
/// </summary>
public sealed partial class ToastViewModel : ObservableObject
{
    private readonly Action<ToastViewModel> _onDismiss;

    public ToastViewModel(string message, ToastSeverity severity, Action<ToastViewModel> onDismiss)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(onDismiss);

        Message = message;
        Severity = severity;
        _onDismiss = onDismiss;
    }

    public string Message { get; }

    public ToastSeverity Severity { get; }

    /// <summary>Convenience bool for <c>Controls/ToastHost.axaml</c>'s <c>Classes.info</c> binding --
    /// <c>Classes</c> is a collection, not a plain CLR property, so it can't bind directly to
    /// <see cref="Severity"/> (an enum); mirrors this app's existing idiom of adding a small bool
    /// property for exactly this purpose (e.g. <c>CampaignsViewModel.HasLoadError</c>).</summary>
    public bool IsInfo => Severity == ToastSeverity.Info;

    /// <summary>See <see cref="IsInfo"/>'s remarks.</summary>
    public bool IsWarning => Severity == ToastSeverity.Warning;

    /// <summary>See <see cref="IsInfo"/>'s remarks.</summary>
    public bool IsError => Severity == ToastSeverity.Error;

    [RelayCommand]
    private void Dismiss() => _onDismiss(this);
}