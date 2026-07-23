using System.Runtime.CompilerServices;

// Lets the two composition roots (Desktop, Android) set App.Services' internal setter at
// startup, without making that setter public to every consumer of GmToolkit.UI.
[assembly: InternalsVisibleTo("GmToolkit.Desktop")]
[assembly: InternalsVisibleTo("GmToolkit.Android")]

// Lets tests call ShellViewModel.HandleActiveCampaignChanged directly, bypassing
// Avalonia.Threading.Dispatcher.UIThread (which has no running message loop in plain xUnit
// tests), without making that method a public part of the view model's API surface.
[assembly: InternalsVisibleTo("GmToolkit.UI.Tests")]