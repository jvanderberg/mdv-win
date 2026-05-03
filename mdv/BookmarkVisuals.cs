using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Mdv;

public static class BookmarkVisuals
{
    /// Bookmark glyph — same for present and missing files. Missing files use
    /// a warning triangle instead.
    public const string BookmarkGlyph = ""; // Bookmark
    public const string WarningGlyph = ""; // Warning triangle

    public static string Icon(bool fileExists) => fileExists ? BookmarkGlyph : WarningGlyph;

    public static double Opacity(bool fileExists) => fileExists ? 1.0 : 0.55;

    public static string SlotBadge(int? slot) => slot.HasValue ? $"Ctrl+{slot}" : "";

    public static Visibility SlotVis(int? slot) =>
        slot.HasValue ? Visibility.Visible : Visibility.Collapsed;

    public static string FilenameOf(string path) =>
        string.IsNullOrEmpty(path) ? "" : Path.GetFileName(path);

    public static SolidColorBrush IconBrush(bool fileExists) =>
        new SolidColorBrush(fileExists
            ? Color.FromArgb(255, 0xC9, 0x84, 0x6A)
            : Color.FromArgb(255, 0xE0, 0x8E, 0x44));

    /// Classic slim ribbon-bookmark path. Drawn in a 16-wide × 22-tall
    /// implicit viewport so PathIcon's Stretch=Uniform preserves the ribbon
    /// aspect: flat top edge, parallel vertical sides, deep V-notch at the
    /// bottom (~36% of height). The shape you'd recognise as a real bookmark
    /// hanging out of a book.
    public const string BookmarkPath = "M4,1 L12,1 L12,21 L8,15 L4,21 Z";

    public static Visibility VisIfExists(bool fileExists) =>
        fileExists ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility VisIfMissing(bool fileExists) =>
        fileExists ? Visibility.Collapsed : Visibility.Visible;
}
