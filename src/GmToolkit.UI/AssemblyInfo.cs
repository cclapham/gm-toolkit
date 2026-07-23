using System.Runtime.CompilerServices;

// Lets the two composition roots (Desktop, Android) set App.Services' internal setter at
// startup, without making that setter public to every consumer of GmToolkit.UI.
[assembly: InternalsVisibleTo("GmToolkit.Desktop")]
[assembly: InternalsVisibleTo("GmToolkit.Android")]