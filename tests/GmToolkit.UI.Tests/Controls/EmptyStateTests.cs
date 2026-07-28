using CommunityToolkit.Mvvm.Input;

using GmToolkit.UI.Controls;

namespace GmToolkit.UI.Tests.Controls;

/// <summary>
/// Tests <see cref="EmptyState.HasAction"/> -- the one piece of actual logic on this control (issue
/// #23), everything else is plain XAML layout with nothing to assert on beyond what
/// <c>CampaignsViewModel</c>/<c>CharactersViewModel</c>/<c>NpcsViewModel</c>'s own
/// <c>IsEmpty</c>/<c>IsLoading</c>/<c>IsNoSearchResults</c> tests already cover -- see this app's
/// established convention (issues #15-#29) of not writing direct tests for plain View/code-behind
/// files.
/// </summary>
public class EmptyStateTests
{
    [Fact]
    public void HasAction_is_false_when_neither_ActionText_nor_ActionCommand_is_set()
    {
        var emptyState = new EmptyState();

        Assert.False(emptyState.HasAction);
    }

    [Fact]
    public void HasAction_is_false_when_only_ActionText_is_set()
    {
        var emptyState = new EmptyState
        {
            ActionText = "Do the thing",
        };

        Assert.False(emptyState.HasAction);
    }

    [Fact]
    public void HasAction_is_false_when_only_ActionCommand_is_set()
    {
        var emptyState = new EmptyState
        {
            ActionCommand = new RelayCommand(() => { }),
        };

        Assert.False(emptyState.HasAction);
    }

    [Fact]
    public void HasAction_is_false_when_ActionText_is_empty_even_if_ActionCommand_is_set()
    {
        var emptyState = new EmptyState
        {
            ActionText = string.Empty,
            ActionCommand = new RelayCommand(() => { }),
        };

        Assert.False(emptyState.HasAction);
    }

    [Fact]
    public void HasAction_is_true_when_both_ActionText_and_ActionCommand_are_set()
    {
        var emptyState = new EmptyState
        {
            ActionText = "Do the thing",
            ActionCommand = new RelayCommand(() => { }),
        };

        Assert.True(emptyState.HasAction);
    }

    [Fact]
    public void HasAction_becomes_false_again_after_ActionCommand_is_cleared()
    {
        var emptyState = new EmptyState
        {
            ActionText = "Do the thing",
            ActionCommand = new RelayCommand(() => { }),
        };
        Assert.True(emptyState.HasAction);

        emptyState.ActionCommand = null;

        Assert.False(emptyState.HasAction);
    }
}