using System;

namespace Mdv;

public static class TextHelpers
{
    /// Filename without its trailing extension. "foo.md" → "foo", "no-ext" → "no-ext".
    public static string Stem(string filename)
    {
        if (string.IsNullOrEmpty(filename)) return "";
        var dot = filename.LastIndexOf('.');
        return dot > 0 ? filename[..dot] : filename;
    }

    /// Trailing extension including the dot. "foo.md" → ".md", "no-ext" → "".
    public static string Ext(string filename)
    {
        if (string.IsNullOrEmpty(filename)) return "";
        var dot = filename.LastIndexOf('.');
        return dot > 0 ? filename[dot..] : "";
    }

    /// True middle-ellipsis on a string: keeps roughly equal chars from both ends.
    public static string MiddleEllipsis(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
        var keep = max - 1;
        var head = (keep + 1) / 2;
        var tail = keep / 2;
        return s[..head] + "…" + s[^tail..];
    }

    /// Middle-ellipsis on a path, snapped to path-separator boundaries so
    /// directory segments aren't sliced mid-name. `~` substitution applied.
    /// `C:\Users\joshv\git\mdv-win\test-docs\code.md` → `~\git\…\test-docs\code.md`.
    public static string MiddlePathEllipsis(string path, int max)
    {
        var pretty = PrettyPath(path);
        if (pretty.Length <= max) return pretty;

        var sepChars = new[] { '/', '\\' };
        var keep = max - 1;
        var headLen = (keep + 1) / 2;
        var tailLen = keep / 2;

        // Snap head: keep characters up to and including the last separator
        // that fits in headLen.
        var headSlice = pretty[..headLen];
        int lastHeadSep = headSlice.LastIndexOfAny(sepChars);
        var head = lastHeadSep > 0 ? pretty[..(lastHeadSep + 1)] : headSlice;

        // Snap tail: take from the first separator that fits in tailLen.
        var tailSlice = pretty[^tailLen..];
        int firstTailSep = tailSlice.IndexOfAny(sepChars);
        var tail = firstTailSep >= 0 ? tailSlice[firstTailSep..] : tailSlice;

        return head + "…" + tail;
    }

    public static string PrettyPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home)
            && path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
            return "~" + path[home.Length..];
        return path;
    }
}
