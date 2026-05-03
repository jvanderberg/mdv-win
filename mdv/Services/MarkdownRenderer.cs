using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Markdig;
using Markdig.Extensions.Tables;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using Microsoft.UI.Text;
using FontWeight = Windows.UI.Text.FontWeight;
using MdBlock = Markdig.Syntax.Block;
using MdInline = Markdig.Syntax.Inlines.Inline;
using MdHeadingBlock = Markdig.Syntax.HeadingBlock;
using MdParagraphBlock = Markdig.Syntax.ParagraphBlock;
using MdCodeBlock = Markdig.Syntax.CodeBlock;
using MdFencedCodeBlock = Markdig.Syntax.FencedCodeBlock;
using MdQuoteBlock = Markdig.Syntax.QuoteBlock;
using MdListBlock = Markdig.Syntax.ListBlock;
using MdListItemBlock = Markdig.Syntax.ListItemBlock;
using MdLeafBlock = Markdig.Syntax.LeafBlock;
using MdThematicBreakBlock = Markdig.Syntax.ThematicBreakBlock;
using MdHtmlBlock = Markdig.Syntax.HtmlBlock;
using MdContainerInline = Markdig.Syntax.Inlines.ContainerInline;
using MdLiteralInline = Markdig.Syntax.Inlines.LiteralInline;
using MdEmphasisInline = Markdig.Syntax.Inlines.EmphasisInline;
using MdCodeInline = Markdig.Syntax.Inlines.CodeInline;
using MdLinkInline = Markdig.Syntax.Inlines.LinkInline;
using MdAutolinkInline = Markdig.Syntax.Inlines.AutolinkInline;
using MdHtmlInline = Markdig.Syntax.Inlines.HtmlInline;
using MdLineBreakInline = Markdig.Syntax.Inlines.LineBreakInline;

namespace Mdv.Services;

/// Markdig → WinUI element tree, theme-aware. Mirrors the MarkdownUI render
/// shape from the macOS app: per-theme typography (font family, base size,
/// per-element spacing, em-relative heading sizes, heading/strong weight
/// overrides), per-theme palette (background, text tiers, link, blockquote
/// bar, code-block chrome).
///
/// Code-fence syntax highlighting is a separate concern handled by
/// CodeRenderer (TextMateSharp port — TODO).
public sealed class MarkdownRenderer
{
    private readonly MdvTheme _theme;
    private readonly double _scale;
    private readonly bool _smartTypography;
    private readonly bool _loadRemoteImages;
    private readonly string? _baseDirectory;

    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseTaskLists()
        .UseAutoLinks()
        .Build();

    public MarkdownRenderer(MdvTheme theme, double scale, bool smartTypography,
        bool loadRemoteImages, string? baseDirectory)
    {
        _theme = theme;
        _scale = scale;
        _smartTypography = smartTypography && theme.SmartTypographyAllowed;
        _loadRemoteImages = loadRemoteImages;
        _baseDirectory = baseDirectory;
    }

    public sealed record RenderedBlock(string Source, FrameworkElement Element);

    /// Render the document as a flat list of top-level blocks, each with the
    /// markdown source it came from. Lets the caller wrap each block in its
    /// own Border for find-tinting / hover affordances and track block→element
    /// mapping for scroll-to-block.
    public List<RenderedBlock> RenderBlocks(string source)
    {
        var input = _smartTypography ? SmartTypography.Smarten(source) : source;
        var blockSources = DocumentBlocks.Split(input);
        var result = new List<RenderedBlock>();
        foreach (var bs in blockSources)
        {
            var doc = Markdown.Parse(bs, _pipeline);
            foreach (var block in doc)
            {
                var element = RenderBlock(block);
                if (element != null) result.Add(new RenderedBlock(bs, element));
            }
        }
        return result;
    }

    /// Convenience: wrap RenderBlocks in a single FrameworkElement laid out
    /// inside the theme's article column.
    public FrameworkElement Render(string source)
    {
        var blocks = RenderBlocks(source);
        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        foreach (var b in blocks) stack.Children.Add(b.Element);
        var outer = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        var inner = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = _theme.ArticleMaxWidth ?? double.PositiveInfinity,
        };
        inner.Children.Add(stack);
        outer.Children.Add(inner);
        outer.Padding = new Thickness(_theme.ArticleHorizontalPadding, 28, _theme.ArticleHorizontalPadding, 28);
        return outer;
    }

    public Thickness ArticlePadding => new(_theme.ArticleHorizontalPadding, 28, _theme.ArticleHorizontalPadding, 28);
    public double ArticleMaxWidth => _theme.ArticleMaxWidth ?? double.PositiveInfinity;

    private double BodySize => _theme.BaseFontSize * _scale;

    private FrameworkElement? RenderBlock(MdBlock block) => block switch
    {
        MdHeadingBlock h => RenderHeading(h),
        MdParagraphBlock p => RenderParagraph(p),
        MdFencedCodeBlock fc => RenderCodeBlock(fc, fc.Info),
        MdCodeBlock c => RenderCodeBlock(c, null),
        MdQuoteBlock q => RenderQuote(q),
        MdListBlock l => RenderList(l),
        MdThematicBreakBlock => RenderHr(),
        Table t => RenderTable(t),
        MdHtmlBlock h => RenderHtmlBlock(h),
        _ => null,
    };

    // MARK: - Headings

    private FrameworkElement RenderHeading(MdHeadingBlock h)
    {
        double sizeEm = h.Level switch
        {
            1 => _theme.H1SizeEm,
            2 => _theme.H2SizeEm,
            3 => _theme.H3SizeEm,
            4 => 1.0,
            5 => 0.875,
            6 => 0.85,
            _ => 1.0,
        };
        double topMargin = h.Level switch
        {
            1 => _theme.H1TopSpacing,
            2 => _theme.H2TopSpacing,
            3 => _theme.H3TopSpacing,
            _ => 24,
        };
        double bottomMargin = h.Level switch
        {
            1 => _theme.H1BottomSpacing,
            2 => _theme.H2BottomSpacing,
            3 => _theme.H3BottomSpacing,
            _ => 16,
        };

        var rtb = new RichTextBlock
        {
            FontSize = BodySize * sizeEm,
            FontWeight = _theme.HeadingFontWeight,
            Foreground = new SolidColorBrush(h.Level == 6 ? _theme.TertiaryText : _theme.Heading),
            FontFamily = ResolveFamily(_theme.BodyFontFamily),
            TextWrapping = TextWrapping.Wrap,
        };
        var para = new Paragraph();
        if (h.Inline != null) AppendInlines(para.Inlines, h.Inline);
        rtb.Blocks.Add(para);

        bool drawRule = (h.Level == 1 && _theme.ShowH1Rule) || (h.Level == 2 && _theme.ShowH2Rule);
        if (!drawRule)
        {
            rtb.Margin = new Thickness(0, topMargin, 0, bottomMargin);
            return rtb;
        }
        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, topMargin, 0, bottomMargin),
        };
        stack.Children.Add(rtb);
        stack.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(_theme.Divider),
            Margin = new Thickness(0, 4, 0, 0),
        });
        return stack;
    }

    // MARK: - Paragraph

    private FrameworkElement RenderParagraph(MdParagraphBlock p)
    {
        var rtb = new RichTextBlock
        {
            FontSize = BodySize,
            FontFamily = ResolveFamily(_theme.BodyFontFamily),
            Foreground = new SolidColorBrush(_theme.Text),
            LineHeight = BodySize * (1 + _theme.ParagraphLineSpacingEm),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, _theme.ParagraphBottomSpacing),
        };
        var para = new Paragraph();
        if (p.Inline != null) AppendInlines(para.Inlines, p.Inline);
        rtb.Blocks.Add(para);
        return rtb;
    }

    // MARK: - Code blocks

    private static readonly CodeHighlighter _highlighter = new();

    private FrameworkElement RenderCodeBlock(MdLeafBlock block, string? language)
    {
        var content = block.Lines.ToString();
        var palette = _theme.ResolvedCodePalette;
        var bg = palette.Background ?? _theme.SecondaryBackground;
        var border = new Border
        {
            Background = new SolidColorBrush(bg),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 0, 0, 16),
        };
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var chrome = new Grid { Padding = new Thickness(14, 4, 6, 0), Height = 26 };
        chrome.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        chrome.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var langLabel = new TextBlock
        {
            Text = (language ?? "").Trim().Split(' ')[0],
            FontSize = 10.5,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            Foreground = new SolidColorBrush(_theme.TertiaryText),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.85,
        };
        Grid.SetColumn(langLabel, 0);
        chrome.Children.Add(langLabel);

        var copyButton = new Button
        {
            Content = new FontIcon { Glyph = "", FontSize = 11 },
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(copyButton, "Copy code");
        copyButton.Click += (_, _) =>
        {
            var pkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
            pkg.SetText(content);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);
        };
        Grid.SetColumn(copyButton, 1);
        chrome.Children.Add(copyButton);
        Grid.SetRow(chrome, 0);
        grid.Children.Add(chrome);

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(16, 4, 16, 14),
        };
        var codeFont = new FontFamily("Cascadia Mono, Consolas, Courier New");
        var codeSize = Math.Round(_theme.BaseFontSize * 0.90 * 100) / 100;
        var rtb = new RichTextBlock
        {
            FontFamily = codeFont,
            FontSize = codeSize,
            Foreground = new SolidColorBrush(palette.Plain),
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.NoWrap,
            LineHeight = codeSize * 1.45,
        };
        var para = new Paragraph();
        foreach (var token in _highlighter.Highlight(content, language, palette))
        {
            var run = new Run { Text = token.Text, Foreground = new SolidColorBrush(token.Color) };
            para.Inlines.Add(run);
        }
        rtb.Blocks.Add(para);
        scroll.Content = rtb;
        Grid.SetRow(scroll, 1);
        grid.Children.Add(scroll);

        border.Child = grid;
        return border;
    }

    // MARK: - Block quote

    private FrameworkElement RenderQuote(MdQuoteBlock q)
    {
        var bar = new Border
        {
            Width = 4,
            Background = new SolidColorBrush(_theme.BlockquoteBar),
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 0, 12, 0),
        };
        var inner = new StackPanel { Orientation = Orientation.Vertical };
        foreach (MdBlock b in q)
        {
            var child = RenderBlock(b);
            if (child is RichTextBlock rtb)
                rtb.Foreground = new SolidColorBrush(_theme.SecondaryText);
            if (child != null) inner.Children.Add(child);
        }
        var row = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(bar, 0);
        Grid.SetColumn(inner, 1);
        row.Children.Add(bar);
        row.Children.Add(inner);
        return row;
    }

    // MARK: - List

    private FrameworkElement RenderList(MdListBlock l)
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 0, 0, 12),
        };
        int idx = 1;
        foreach (var child in l)
        {
            if (child is MdListItemBlock item)
            {
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var marker = new TextBlock
                {
                    Text = l.IsOrdered ? $"{idx}." : "•",
                    FontSize = BodySize,
                    FontFamily = ResolveFamily(_theme.BodyFontFamily),
                    Foreground = new SolidColorBrush(_theme.SecondaryText),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 8, 0),
                };
                Grid.SetColumn(marker, 0);

                var inner = new StackPanel { Orientation = Orientation.Vertical };
                foreach (MdBlock ib in item)
                {
                    var rendered = RenderBlock(ib);
                    if (rendered is RichTextBlock rtb)
                        rtb.Margin = new Thickness(0, 0, 0, 4);
                    if (rendered != null) inner.Children.Add(rendered);
                }
                Grid.SetColumn(inner, 1);
                row.Children.Add(marker);
                row.Children.Add(inner);
                stack.Children.Add(row);
                idx++;
            }
        }
        return stack;
    }

    // MARK: - Horizontal rule

    private FrameworkElement RenderHr() => new Border
    {
        Height = 1,
        Background = new SolidColorBrush(_theme.Border),
        Margin = new Thickness(0, 24, 0, 24),
    };

    // MARK: - Table

    private FrameworkElement RenderTable(Table t)
    {
        int colCount = t.OfType<TableRow>().FirstOrDefault()?.Count ?? 0;
        int rowCount = t.Count;
        if (colCount == 0 || rowCount == 0)
            return new Border { Margin = new Thickness(0, 0, 0, 16) };

        var inner = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(_theme.Border),
            CornerRadius = new CornerRadius(4),
        };
        for (int c = 0; c < colCount; c++)
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (int r = 0; r < rowCount; r++)
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        int rowIdx = 0;
        foreach (var rowBlock in t.OfType<TableRow>())
        {
            int colIdx = 0;
            bool stripe = !rowBlock.IsHeader && rowIdx > 0 && (rowIdx % 2 == 1);
            var bg = rowBlock.IsHeader ? _theme.SecondaryBackground
                : (stripe ? _theme.SecondaryBackground : _theme.Background);
            foreach (var cellBlock in rowBlock.OfType<TableCell>())
            {
                var cell = new Border
                {
                    Background = new SolidColorBrush(bg),
                    BorderBrush = new SolidColorBrush(_theme.Border),
                    BorderThickness = new Thickness(
                        0,
                        0,
                        colIdx == colCount - 1 ? 0 : 1,
                        rowIdx == rowCount - 1 ? 0 : 1),
                    Padding = new Thickness(13, 8, 13, 8),
                    MinWidth = 60,
                };
                var cellRtb = new RichTextBlock
                {
                    FontSize = BodySize,
                    FontWeight = rowBlock.IsHeader ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(_theme.Text),
                    FontFamily = ResolveFamily(_theme.BodyFontFamily),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = BodySize * 1.4,
                };
                var para = new Paragraph();
                foreach (MdBlock cellChild in cellBlock)
                    if (cellChild is MdParagraphBlock pb && pb.Inline != null)
                        AppendInlines(para.Inlines, pb.Inline);
                cellRtb.Blocks.Add(para);
                cell.Child = cellRtb;
                Grid.SetColumn(cell, colIdx);
                Grid.SetRow(cell, rowIdx);
                inner.Children.Add(cell);
                colIdx++;
            }
            rowIdx++;
        }

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 0, 0, 16),
            Content = inner,
        };
        return scroll;
    }

    // MARK: - HTML passthrough (very minimal)

    private FrameworkElement RenderHtmlBlock(MdHtmlBlock html)
    {
        return new TextBlock
        {
            Text = html.Lines.ToString(),
            FontSize = BodySize,
            FontFamily = ResolveFamily(_theme.BodyFontFamily),
            Foreground = new SolidColorBrush(_theme.SecondaryText),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
            Margin = new Thickness(0, 0, 0, 12),
        };
    }

    // MARK: - Inlines

    private void AppendInlines(InlineCollection sink, MdContainerInline container)
    {
        foreach (var inline in container)
            AppendInline(sink, inline);
    }

    private void AppendInline(InlineCollection sink, MdInline inline)
    {
        switch (inline)
        {
            case MdLiteralInline lit:
                sink.Add(new Run { Text = lit.Content.ToString() });
                break;

            case MdEmphasisInline em:
                Span wrapper = em.DelimiterCount >= 2
                    ? new Bold { FontWeight = _theme.StrongFontWeight, Foreground = new SolidColorBrush(_theme.Strong) }
                    : new Italic();
                AppendInlines(wrapper.Inlines, em);
                sink.Add(wrapper);
                break;

            case MdCodeInline code:
                sink.Add(new Run
                {
                    Text = code.Content,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                    FontSize = BodySize * 0.90,
                });
                break;

            case MdLineBreakInline:
                sink.Add(new LineBreak());
                break;

            case MdLinkInline link when link.IsImage:
                AppendImage(sink, link);
                break;

            case MdLinkInline link:
                {
                    var hyperlink = new Hyperlink { Foreground = new SolidColorBrush(_theme.Link) };
                    if (Uri.TryCreate(link.Url, UriKind.RelativeOrAbsolute, out var uri))
                    {
                        if (uri.IsAbsoluteUri) hyperlink.NavigateUri = uri;
                    }
                    AppendInlines(hyperlink.Inlines, link);
                    sink.Add(hyperlink);
                    break;
                }

            case MdAutolinkInline auto:
                {
                    var hyperlink = new Hyperlink { Foreground = new SolidColorBrush(_theme.Link) };
                    if (Uri.TryCreate(auto.Url, UriKind.Absolute, out var uri))
                        hyperlink.NavigateUri = uri;
                    hyperlink.Inlines.Add(new Run { Text = auto.Url });
                    sink.Add(hyperlink);
                    break;
                }

            case MdHtmlInline html:
                sink.Add(new Run { Text = html.Tag, Foreground = new SolidColorBrush(_theme.TertiaryText) });
                break;

            case MdContainerInline container:
                AppendInlines(sink, container);
                break;
        }
    }

    private void AppendImage(InlineCollection sink, MdLinkInline imageLink)
    {
        if (string.IsNullOrEmpty(imageLink.Url)) return;
        BitmapImage? bmp = null;
        var url = imageLink.Url;
        if (Uri.TryCreate(url, UriKind.Absolute, out var abs))
        {
            if (abs.IsFile) bmp = new BitmapImage(abs);
            else if (_loadRemoteImages && (abs.Scheme == "http" || abs.Scheme == "https")) bmp = new BitmapImage(abs);
        }
        else if (_baseDirectory != null)
        {
            var resolved = Path.Combine(_baseDirectory, url);
            if (File.Exists(resolved)) bmp = new BitmapImage(new Uri(resolved));
        }

        if (bmp == null) return;

        var img = new Image
        {
            Source = bmp,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = _theme.ArticleMaxWidth ?? 860,
        };
        sink.Add(new InlineUIContainer { Child = img });
    }

    private FontFamily ResolveFamily(string? family)
    {
        if (string.IsNullOrEmpty(family)) return new FontFamily("Segoe UI Variable, Segoe UI");
        return family switch
        {
            "Alegreya" => new FontFamily("ms-appx:///Fonts/Alegreya-Regular.otf#Alegreya"),
            "Besley" => new FontFamily("ms-appx:///Fonts/Besley-Regular.otf#Besley"),
            "OpenDyslexic" => new FontFamily("ms-appx:///Fonts/OpenDyslexic-Regular.otf#OpenDyslexic"),
            _ => new FontFamily(family),
        };
    }
}
