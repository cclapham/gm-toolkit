using GmToolkit.UI.Services;

namespace GmToolkit.UI.Tests.Services;

/// <summary>
/// Tests <see cref="NotificationService"/>'s actual logic (issue #32) -- adding/removing from
/// <see cref="NotificationService.Toasts"/>. Auto-dismiss's <see cref="Avalonia.Threading.DispatcherTimer"/>
/// isn't exercised here (no pumped dispatcher loop in a plain xUnit test); see that class's remarks
/// for why that's consistent with this app's existing convention of not unit-testing pure visual/
/// timing behavior.
/// </summary>
public class NotificationServiceTests
{
    [Fact]
    public void Show_adds_a_toast_with_the_given_message_and_severity()
    {
        var service = new NotificationService();

        service.Show("The database file is missing.", ToastSeverity.Error);

        var toast = Assert.Single(service.Toasts);
        Assert.Equal("The database file is missing.", toast.Message);
        Assert.Equal(ToastSeverity.Error, toast.Severity);
        Assert.True(toast.IsError);
        Assert.False(toast.IsInfo);
        Assert.False(toast.IsWarning);
    }

    [Fact]
    public void Show_defaults_to_Info_severity_when_none_is_given()
    {
        var service = new NotificationService();

        service.Show("Saved.");

        Assert.Equal(ToastSeverity.Info, Assert.Single(service.Toasts).Severity);
    }

    [Fact]
    public void Multiple_Show_calls_add_multiple_independent_toasts()
    {
        var service = new NotificationService();

        service.Show("First");
        service.Show("Second");

        Assert.Equal(2, service.Toasts.Count);
        Assert.Equal("First", service.Toasts[0].Message);
        Assert.Equal("Second", service.Toasts[1].Message);
    }

    [Fact]
    public void DismissCommand_on_a_toast_removes_only_that_toast()
    {
        var service = new NotificationService();
        service.Show("First");
        service.Show("Second");
        var first = service.Toasts[0];

        first.DismissCommand.Execute(null);

        var remaining = Assert.Single(service.Toasts);
        Assert.Equal("Second", remaining.Message);
    }

    [Fact]
    public void Show_throws_for_a_null_or_empty_message()
    {
        var service = new NotificationService();

        Assert.Throws<ArgumentException>(() => service.Show(string.Empty));
    }
}