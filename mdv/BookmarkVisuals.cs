using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Mdv;

public static class BookmarkVisuals
{
    public static string Icon(bool fileExists) =>
        fileExists ? "" : ""; // Bookmark / Warning

    public static double Opacity(bool fileExists) => fileExists ? 1.0 : 0.55;

    public static string SlotBadge(int? slot) => slot.HasValue ? $"Ctrl+{slot}" : "";

    public static Visibility SlotVis(int? slot) =>
        slot.HasValue ? Visibility.Visible : Visibility.Collapsed;

    public static string FilenameOf(string path) =>
        string.IsNullOrEmpty(path) ? "" : Path.GetFileName(path);

    public static SolidColorBrush IconBrush(bool fileExists) =>
        new SolidColorBrush(fileExists
            ? Color.FromArgb(255, 0xC9, 0x84, 0x6A)   // accent-ish amber
            : Color.FromArgb(255, 0xE0, 0x8E, 0x44)); // warning amber
}
