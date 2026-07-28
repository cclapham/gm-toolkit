using Avalonia.Styling;

using GmToolkit.Core.Services;
using GmToolkit.UI.Services;

namespace GmToolkit.UI.Tests.Services;

public class ThemeApplierTests
{
    [Fact]
    public void ToThemeVariant_maps_Light_to_ThemeVariant_Light()
    {
        Assert.Equal(ThemeVariant.Light, ThemeApplier.ToThemeVariant(ThemePreference.Light));
    }

    [Fact]
    public void ToThemeVariant_maps_Dark_to_ThemeVariant_Dark()
    {
        Assert.Equal(ThemeVariant.Dark, ThemeApplier.ToThemeVariant(ThemePreference.Dark));
    }

    [Fact]
    public void ToThemeVariant_maps_System_to_ThemeVariant_Default()
    {
        Assert.Equal(ThemeVariant.Default, ThemeApplier.ToThemeVariant(ThemePreference.System));
    }
}