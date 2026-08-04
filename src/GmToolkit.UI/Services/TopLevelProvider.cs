using Avalonia.Controls;

namespace GmToolkit.UI.Services;

/// <inheritdoc cref="ITopLevelProvider" />
public sealed class TopLevelProvider : ITopLevelProvider
{
    public TopLevel? Current { get; set; }
}