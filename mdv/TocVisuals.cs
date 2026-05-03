using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using FontWeight = Windows.UI.Text.FontWeight;

namespace Mdv;

public static class TocVisuals
{
    public static Thickness Indent(int level) =>
        new((level - 1) * 12, 0, 0, 0);

    public static FontWeight Weight(int level) =>
        level == 1 ? FontWeights.SemiBold : FontWeights.Normal;

    public static double LevelOpacity(int level) =>
        level == 1 ? 1.0 : (level == 2 ? 0.92 : 0.78);

    public static double LevelFontSize(int level) =>
        level switch { 1 => 12.5, 2 => 12, 3 => 11.5, _ => 11 };
}
