using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Mdv.Services;

public sealed record TocHeading(int Level, string Text, int BlockIndex);

/// Fence-aware paragraph/block splitter and TOC extractor.
/// Direct port of ContentView.swift `blocks` + `tocHeadings`.
public static class DocumentBlocks
{
    /// Split markdown source into blocks. Blank lines separate paragraphs,
    /// but blank lines inside a fenced code block (``` or ~~~) belong to the
    /// code and must not split. Without this, multi-paragraph code samples
    /// get shredded and syntax highlighting breaks.
    public static List<string> Split(string source)
    {
        var result = new List<string>();
        var current = new List<string>();
        string? fenceMarker = null;

        void Flush()
        {
            var joined = string.Join('\n', current).Trim('\n', '\r');
            if (!string.IsNullOrEmpty(joined)) result.Add(joined);
            current.Clear();
        }

        foreach (var line in source.Split('\n'))
        {
            var trimmedStart = line.TrimStart(' ');
            if (fenceMarker != null)
            {
                current.Add(line);
                if (trimmedStart.StartsWith(fenceMarker)) fenceMarker = null;
                continue;
            }
            if (trimmedStart.StartsWith("```"))
            {
                if (current.Count > 0) Flush();
                current.Add(line);
                fenceMarker = "```";
                continue;
            }
            if (trimmedStart.StartsWith("~~~"))
            {
                if (current.Count > 0) Flush();
                current.Add(line);
                fenceMarker = "~~~";
                continue;
            }
            if (string.IsNullOrWhiteSpace(line)) Flush();
            else current.Add(line);
        }
        Flush();
        return result;
    }

    public static List<TocHeading> ExtractToc(IReadOnlyList<string> blocks)
    {
        var result = new List<TocHeading>();
        for (int i = 0; i < blocks.Count; i++)
        {
            var trimmed = blocks[i].Trim();
            if (trimmed.StartsWith("```")) continue;

            int level;
            string prefix;
            if (trimmed.StartsWith("###### ")) { level = 6; prefix = "###### "; }
            else if (trimmed.StartsWith("##### ")) { level = 5; prefix = "##### "; }
            else if (trimmed.StartsWith("#### ")) { level = 4; prefix = "#### "; }
            else if (trimmed.StartsWith("### ")) { level = 3; prefix = "### "; }
            else if (trimmed.StartsWith("## ")) { level = 2; prefix = "## "; }
            else if (trimmed.StartsWith("# ")) { level = 1; prefix = "# "; }
            else continue;

            var firstLine = trimmed.Split('\n', 2)[0];
            var raw = firstLine[prefix.Length..];
            result.Add(new TocHeading(level, StripInlineMarkdown(raw), i));
        }
        return result;
    }

    private static readonly Regex _trailingHashes = new(@"\s+#+\s*$", RegexOptions.Compiled);
    private static readonly Regex _bareStar = new(@"(?<!\\)\*", RegexOptions.Compiled);
    private static readonly Regex _bareUnderscore = new(@"(?<![A-Za-z0-9])_(?=[^_]+_)", RegexOptions.Compiled);
    private static readonly Regex _mdLink = new(@"\[([^\]]+)\]\([^)]+\)", RegexOptions.Compiled);

    public static string StripInlineMarkdown(string s)
    {
        var t = _trailingHashes.Replace(s, "");
        t = t.Replace("**", "").Replace("__", "").Replace("`", "");
        t = _bareStar.Replace(t, "");
        t = _bareUnderscore.Replace(t, "");
        t = _mdLink.Replace(t, "$1");
        return t.Trim();
    }
}
