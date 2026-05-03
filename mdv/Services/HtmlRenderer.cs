using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Windows.UI;

namespace Mdv.Services;

/// Markdig → HTML, wrapped in a themed shell.
/// Code-fence syntax highlighting is pre-rendered via TextMateSharp into
/// colored &lt;span&gt; runs (no JS dependency, no network).
public sealed class HtmlRenderer
{
    private readonly MdvTheme _theme;
    private readonly double _scale;
    private readonly bool _smartTypography;
    private readonly bool _loadRemoteImages;
    private static readonly CodeHighlighter _highlighter = new();

    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseTaskLists()
        .UseAutoLinks()
        .UseGenericAttributes()
        .Build();

    public HtmlRenderer(MdvTheme theme, double scale, bool smartTypography, bool loadRemoteImages)
    {
        _theme = theme;
        _scale = scale;
        _smartTypography = smartTypography && theme.SmartTypographyAllowed;
        _loadRemoteImages = loadRemoteImages;
    }

    /// Render a full HTML document. Each top-level markdown block is wrapped
    /// in `<div class="block" id="block-N">…</div>` so the host can scroll to
    /// a specific block (TOC click, bookmark jump) and tint matched blocks
    /// during find.
    public string Render(string source) => Render(source, null);

    public string Render(string source, int? initialScrollBlock)
    {
        // Split first, then smarten per-block — skipping fenced code blocks
        // and pipe-table blocks where `--` and `|` are syntactic.
        var blocks = DocumentBlocks.Split(source);

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\">");
        sb.Append("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">");
        sb.Append("<base href=\"https://mdv-doc.local/\">");
        sb.Append("<style>").Append(BuildCss()).Append("</style>");
        sb.Append("</head><body><div class=\"article\">");

        for (int i = 0; i < blocks.Count; i++)
        {
            var blockSrc = ShouldSmarten(blocks[i]) ? SmartTypography.Smarten(blocks[i]) : blocks[i];
            var html = Markdown.ToHtml(blockSrc, _pipeline);
            html = HighlightCodeBlocks(html);
            if (!_loadRemoteImages) html = StripRemoteImages(html);
            sb.Append($"<div class=\"block\" id=\"block-{i}\">").Append(html).Append("</div>");
        }

        sb.Append("</div><script>").Append(BuildJs(initialScrollBlock)).Append("</script></body></html>");
        return sb.ToString();
    }

    private bool ShouldSmarten(string blockSource)
    {
        if (!_smartTypography) return false;
        var trimmed = blockSource.TrimStart();
        if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~")) return false;
        // Pipe table: any line starts with `|` (after optional leading space).
        // Smartening would mangle the `---|---` separator into em-dashes and
        // break Markdig's table parser.
        foreach (var line in blockSource.Split('\n'))
            if (line.TrimStart().StartsWith("|")) return false;
        return true;
    }

    // MARK: - CSS

    private string BuildCss()
    {
        var t = _theme;
        var p = t.ResolvedCodePalette;
        var bodyFamily = ResolveFamilyCss(t.BodyFontFamily);
        var bodySize = t.BaseFontSize * _scale;
        var lineHeight = 1 + t.ParagraphLineSpacingEm;
        var maxWidth = t.ArticleMaxWidth.HasValue ? $"{t.ArticleMaxWidth}px" : "none";
        var hPad = t.ArticleHorizontalPadding;
        var headWeight = WeightCss(t.HeadingFontWeight);
        var strongWeight = WeightCss(t.StrongFontWeight);

        return $@"
:root {{
  --bg: {Css(t.Background)};
  --secondary-bg: {Css(t.SecondaryBackground)};
  --text: {Css(t.Text)};
  --secondary-text: {Css(t.SecondaryText)};
  --tertiary-text: {Css(t.TertiaryText)};
  --heading: {Css(t.Heading)};
  --link: {Css(t.Link)};
  --strong: {Css(t.Strong)};
  --border: {Css(t.Border)};
  --divider: {Css(t.Divider)};
  --blockquote-bar: {Css(t.BlockquoteBar)};
  --accent: {Css(t.AccentColor)};
  --code-bg: {Css(p.Background ?? t.SecondaryBackground)};
  --code-plain: {Css(p.Plain)};
}}
* {{ box-sizing: border-box; }}
html, body {{ margin: 0; padding: 0; background: var(--bg); }}
body {{
  color: var(--text);
  font-family: {bodyFamily};
  font-size: {Fmt(bodySize)}px;
  line-height: {Fmt(lineHeight)};
  padding: 28px {hPad}px;
}}
::selection {{ background: var(--accent); color: var(--bg); }}
.article {{ max-width: {maxWidth}; margin: 0 auto; }}
.block {{ padding: 2px 6px; border-radius: 4px; transition: background 120ms; }}
.block.find-match {{ background: rgba(255,224,64,0.30); }}
.block.find-current {{ background: rgba(255,192,32,0.55); }}

h1, h2, h3, h4, h5, h6 {{ color: var(--heading); font-weight: {headWeight}; line-height: 1.25; }}
h1 {{ font-size: {Fmt(t.H1SizeEm)}em; margin: {t.H1TopSpacing}px 0 {t.H1BottomSpacing}px; {(t.ShowH1Rule ? "border-bottom: 1px solid var(--divider); padding-bottom: 0.3em;" : "")} }}
h2 {{ font-size: {Fmt(t.H2SizeEm)}em; margin: {t.H2TopSpacing}px 0 {t.H2BottomSpacing}px; {(t.ShowH2Rule ? "border-bottom: 1px solid var(--divider); padding-bottom: 0.3em;" : "")} }}
h3 {{ font-size: {Fmt(t.H3SizeEm)}em; margin: {t.H3TopSpacing}px 0 {t.H3BottomSpacing}px; }}
h4, h5, h6 {{ margin: 24px 0 16px; }}
h6 {{ color: var(--tertiary-text); font-size: 0.85em; }}

p {{ margin: 0 0 {t.ParagraphBottomSpacing}px; }}
strong, b {{ color: var(--strong); font-weight: {strongWeight}; }}
em, i {{ font-style: italic; }}
a {{ color: var(--link); text-decoration: none; }}
a:hover {{ text-decoration: underline; }}

blockquote {{
  margin: 0 0 16px; padding: 0 0 0 16px;
  border-left: 4px solid var(--blockquote-bar);
  color: var(--secondary-text);
}}

ul, ol {{ padding-left: 1.6em; margin: 0 0 12px; }}
li {{ margin: 4px 0; }}
li > p {{ margin: 0 0 6px; }}

hr {{ border: none; border-top: 1px solid var(--border); margin: 24px 0; }}

img {{ max-width: 100%; height: auto; border-radius: 4px; }}

code {{
  font-family: 'Cascadia Mono', Consolas, 'Courier New', monospace;
  font-size: 0.90em;
  background: var(--secondary-bg);
  padding: 0.1em 0.35em;
  border-radius: 3px;
}}
pre {{
  background: var(--code-bg);
  padding: 14px 16px;
  border-radius: 6px;
  overflow-x: auto;
  margin: 0 0 16px;
  position: relative;
}}
pre code {{
  background: transparent; padding: 0; border-radius: 0;
  color: var(--code-plain);
  font-size: {Fmt(t.BaseFontSize * 0.90)}px;
  line-height: 1.45;
  display: block;
}}
.code-lang {{
  position: absolute; top: 6px; left: 14px;
  font-size: 10.5px; color: var(--tertiary-text); opacity: 0.85;
  font-family: 'Cascadia Mono', Consolas, monospace;
  text-transform: lowercase; pointer-events: none;
}}
pre.has-lang {{ padding-top: 28px; }}

table {{
  border-collapse: collapse;
  border: 1px solid var(--border);
  border-radius: 4px;
  margin: 0 0 16px;
  display: block;
  width: max-content;
  max-width: 100%;
  overflow-x: auto;
}}
th, td {{
  border-bottom: 1px solid var(--border);
  border-right: 1px solid var(--border);
  padding: 8px 13px;
  text-align: left;
  vertical-align: top;
}}
tr:last-child td {{ border-bottom: none; }}
th:last-child, td:last-child {{ border-right: none; }}
thead th {{ background: var(--secondary-bg); font-weight: 600; }}
tbody tr:nth-child(odd) {{ background: var(--secondary-bg); }}

input[type=checkbox] {{ accent-color: var(--accent); margin-right: 6px; }}

::-webkit-scrollbar {{ width: 12px; height: 12px; }}
::-webkit-scrollbar-track {{ background: transparent; }}
::-webkit-scrollbar-thumb {{ background: var(--border); border-radius: 6px; border: 3px solid var(--bg); }}
::-webkit-scrollbar-thumb:hover {{ background: var(--tertiary-text); }}
";
    }

    private string BuildJs(int? initialScrollBlock) => $@"
window.mdvInitialScrollBlock = {(initialScrollBlock.HasValue ? initialScrollBlock.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null")};
document.addEventListener('DOMContentLoaded', function() {{
  if (window.mdvInitialScrollBlock !== null && window.mdvInitialScrollBlock >= 0) {{
    requestAnimationFrame(function() {{
      var el = document.getElementById('block-' + window.mdvInitialScrollBlock);
      if (el) el.scrollIntoView({{ behavior: 'smooth', block: 'start' }});
    }});
  }}
}});
" + @"
window.mdvScrollToBlock = function(idx) {
  var el = document.getElementById('block-' + idx);
  if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
};
document.addEventListener('keydown', function(e) {
  // Zoom: handle =/+ /- and numpad +/- regardless of shift state, via physical key codes.
  if (e.ctrlKey && !e.altKey) {
    if (e.code === 'Equal' || e.code === 'NumpadAdd' || e.key === '=' || e.key === '+') {
      e.preventDefault();
      window.chrome.webview.postMessage({ type: 'shortcut', action: 'zoomIn' });
      return;
    }
    if (e.code === 'Minus' || e.code === 'NumpadSubtract' || e.key === '-') {
      e.preventDefault();
      window.chrome.webview.postMessage({ type: 'shortcut', action: 'zoomOut' });
      return;
    }
  }
  var mod = (e.ctrlKey ? 'C' : '') + (e.shiftKey ? 'S' : '') + (e.altKey ? 'A' : '');
  var k = e.key.toLowerCase();
  var combo = mod + ':' + k;
  var map = {
    'C:f': 'find', 'CS:f': 'searchHistory',
    'C:d': 'bookmarkHere', 'C:b': 'toggleSidebar',
    'CA:0': 'toggleInspector',
    'C:0': 'jumpPlaceholder', 'CS:0': 'setPlaceholder',
    'C:1': 'slot1', 'C:2': 'slot2', 'C:3': 'slot3', 'C:4': 'slot4', 'C:5': 'slot5',
    'A:arrowleft': 'back', 'A:arrowright': 'forward',
    'C:o': 'openFile', 'CS:o': 'openInNewWindow',
    'C:e': 'editCurrent'
  };
  var action = map[combo];
  if (action) {
    e.preventDefault();
    window.chrome.webview.postMessage({ type: 'shortcut', action: action });
  }
});
window.mdvFind = function(query) {
  var blocks = document.querySelectorAll('.block');
  var matches = [];
  blocks.forEach(function(b) { b.classList.remove('find-match','find-current'); });
  if (!query) return { count: 0, matches: [] };
  var q = query.toLowerCase();
  blocks.forEach(function(b, i) {
    if (b.textContent.toLowerCase().indexOf(q) !== -1) {
      b.classList.add('find-match');
      matches.push(i);
    }
  });
  return { count: matches.length, matches: matches };
};
window.mdvFindCurrent = function(blockIdx) {
  document.querySelectorAll('.block.find-current').forEach(function(b) { b.classList.remove('find-current'); });
  var el = document.getElementById('block-' + blockIdx);
  if (el) { el.classList.add('find-current'); el.scrollIntoView({ behavior: 'smooth', block: 'center' }); }
};
window.mdvFindClear = function() {
  document.querySelectorAll('.find-match,.find-current').forEach(function(b) {
    b.classList.remove('find-match','find-current');
  });
};
document.addEventListener('click', function(e) {
  var a = e.target.closest('a');
  if (a && a.href) {
    e.preventDefault();
    window.chrome.webview.postMessage({ type: 'link', href: a.href });
  }
});
";

    // MARK: - Code highlighting via TextMateSharp pre-render

    private static readonly Regex _codeBlockRx = new(
        @"<pre><code(?: class=""language-(?<lang>[^""]+)"")?>(?<body>.*?)</code></pre>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private string HighlightCodeBlocks(string html)
    {
        return _codeBlockRx.Replace(html, m =>
        {
            var lang = m.Groups["lang"].Success ? m.Groups["lang"].Value : null;
            var body = HtmlDecode(m.Groups["body"].Value);
            var palette = _theme.ResolvedCodePalette;
            var sb = new StringBuilder();
            foreach (var token in _highlighter.Highlight(body, lang, palette))
            {
                var hex = HexRgb(token.Color);
                sb.Append("<span style=\"color:").Append(hex).Append("\">")
                  .Append(HtmlEscape(token.Text))
                  .Append("</span>");
            }
            var langClass = string.IsNullOrEmpty(lang) ? "" : " has-lang";
            var langLabel = string.IsNullOrEmpty(lang) ? "" : $"<div class=\"code-lang\">{HtmlEscape(lang)}</div>";
            return $"<pre class=\"{langClass.TrimStart()}\">{langLabel}<code>{sb}</code></pre>";
        });
    }

    private static readonly Regex _imgRx = new(
        @"<img\s+[^>]*src=""(?<src>[^""]+)""[^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private string StripRemoteImages(string html)
    {
        return _imgRx.Replace(html, m =>
        {
            var src = m.Groups["src"].Value;
            if (src.StartsWith("http://") || src.StartsWith("https://"))
                return $"<span style=\"color:var(--tertiary-text);font-style:italic\">[remote image blocked: {HtmlEscape(src)}]</span>";
            return m.Value;
        });
    }

    // MARK: - Helpers

    private static string Css(Color c) => $"rgba({c.R},{c.G},{c.B},{Fmt(c.A / 255.0)})";
    private static string HexRgb(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    private static string Fmt(double d) => d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    private static string WeightCss(Windows.UI.Text.FontWeight w) => w.Weight.ToString();

    private static string ResolveFamilyCss(string? family) => family switch
    {
        null or "" => "'Segoe UI Variable', 'Segoe UI', system-ui, sans-serif",
        "Alegreya" => "'Alegreya', Georgia, serif",
        "Besley" => "'Besley', Georgia, serif",
        "OpenDyslexic" => "'OpenDyslexic', sans-serif",
        _ => $"'{family}', system-ui, sans-serif",
    };

    private static string HtmlEscape(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");

    private static string HtmlDecode(string s) => System.Net.WebUtility.HtmlDecode(s);
}
