using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Text;
using Windows.UI;
using Windows.UI.ViewManagement;
using FontWeight = Windows.UI.Text.FontWeight;

namespace Mdv.Services;

/// Per-element / per-token color slots for fenced code blocks.
public sealed record CodePalette(
    Color? Background,
    Color Plain,
    Color Keyword,
    Color String,
    Color Number,
    Color Comment,
    Color Type,
    Color Function,
    Color Attribute,
    Color Variable,
    Color Constant,
    Color Operator,
    Color LineNumber,
    Color LineHighlight,
    Color DiffAdd,
    Color DiffRemove,
    Color DiffAddBg,
    Color DiffRemoveBg)
{
    /// Walk a dotted capture name from most- to least-specific
    /// (`function.method.builtin` → `function.method` → `function`),
    /// returning the first slot that maps. Unmapped → Plain.
    public Color ColorForCapture(IReadOnlyList<string> components)
    {
        for (int len = components.Count; len > 0; len--)
        {
            var prefix = string.Join('.', components.Take(len));
            var c = ColorForCaptureName(prefix);
            if (c.HasValue) return c.Value;
        }
        return Plain;
    }

    private Color? ColorForCaptureName(string name) => name switch
    {
        "keyword" or "keyword.control" or "keyword.function" or "keyword.operator"
            or "keyword.return" or "keyword.import" or "keyword.export"
            or "keyword.coroutine" or "keyword.repeat" or "keyword.conditional"
            or "keyword.exception" or "keyword.directive" or "keyword.modifier"
            or "keyword.storage" or "keyword.type"
            or "include" or "conditional" or "repeat" or "exception" => Keyword,
        "string" or "string.special" or "string.special.url" or "string.regex"
            or "string.escape" or "string.documentation"
            or "character" or "character.special" => String,
        "number" or "number.float" or "float" => Number,
        "comment" or "comment.line" or "comment.block" or "comment.documentation" => Comment,
        "type" or "type.builtin" or "type.definition" or "type.qualifier"
            or "storage.type" or "storageclass" or "class" => Type,
        "function" or "function.call" or "function.method" or "function.method.call"
            or "function.macro" or "function.builtin"
            or "method" or "constructor" => Function,
        "attribute" or "tag" or "tag.attribute" or "annotation"
            or "decorator" or "label" => Attribute,
        "variable" or "variable.parameter" or "variable.builtin" or "variable.member"
            or "variable.other"
            or "parameter" or "field" or "property" => Variable,
        "constant" or "constant.builtin" or "constant.macro" or "boolean" => Constant,
        "operator" or "punctuation.special" or "punctuation.delimiter" => Operator,
        _ => null,
    };
}

public static class CodePalettes
{
    private static Color C(uint rgba)
    {
        byte r = (byte)((rgba >> 24) & 0xFF);
        byte g = (byte)((rgba >> 16) & 0xFF);
        byte b = (byte)((rgba >> 8) & 0xFF);
        byte a = (byte)(rgba & 0xFF);
        return Color.FromArgb(a, r, g, b);
    }

    public static readonly CodePalette GithubLight = new(
        Background: null,
        Plain: C(0x1F2328FF), Keyword: C(0xCF222EFF), String: C(0x0A3069FF),
        Number: C(0x0550AEFF), Comment: C(0x6E7781FF), Type: C(0x953800FF),
        Function: C(0x8250DFFF), Attribute: C(0x116329FF), Variable: C(0x1F2328FF),
        Constant: C(0x0550AEFF), Operator: C(0xCF222EFF),
        LineNumber: C(0x8C959FFF), LineHighlight: C(0xFFF8C5FF),
        DiffAdd: C(0x1A7F37FF), DiffRemove: C(0xCF222EFF),
        DiffAddBg: C(0xDAFBE1FF), DiffRemoveBg: C(0xFFEBE9FF));

    public static readonly CodePalette OneDark = new(
        Background: null,
        Plain: C(0xABB2BFFF), Keyword: C(0xC678DDFF), String: C(0x98C379FF),
        Number: C(0xD19A66FF), Comment: C(0x7F848EFF), Type: C(0xE5C07BFF),
        Function: C(0x61AFEFFF), Attribute: C(0xD19A66FF), Variable: C(0xE06C75FF),
        Constant: C(0xD19A66FF), Operator: C(0xC678DDFF),
        LineNumber: C(0x636D83FF), LineHighlight: C(0x3E4451FF),
        DiffAdd: C(0x98C379FF), DiffRemove: C(0xE06C75FF),
        DiffAddBg: C(0x1E3A2BFF), DiffRemoveBg: C(0x3E1C20FF));

    public static readonly CodePalette Sevilla = new(
        Background: null,
        Plain: C(0x42372CFF), Keyword: C(0x8C2A1AFF), String: C(0x5C4030FF),
        Number: C(0x7A4A1FFF), Comment: C(0x968874FF), Type: C(0x7B5C3DFF),
        Function: C(0x2C5F8DFF), Attribute: C(0xB0623EFF), Variable: C(0x42372CFF),
        Constant: C(0x7A4A1FFF), Operator: C(0x42372CFF),
        LineNumber: C(0xC0B49AFF), LineHighlight: C(0xE6DEC2FF),
        DiffAdd: C(0x4F7138FF), DiffRemove: C(0x8C2A1AFF),
        DiffAddBg: C(0xE3E5C9FF), DiffRemoveBg: C(0xF0DCD0FF));

    public static readonly CodePalette Charcoal = new(
        Background: null,
        Plain: C(0xC9D1D9FF), Keyword: C(0xFF7B72FF), String: C(0xA5D6FFFF),
        Number: C(0x79C0FFFF), Comment: C(0x8B949EFF), Type: C(0xFFA657FF),
        Function: C(0xD2A8FFFF), Attribute: C(0x7EE787FF), Variable: C(0xC9D1D9FF),
        Constant: C(0x79C0FFFF), Operator: C(0xFF7B72FF),
        LineNumber: C(0x484F58FF), LineHighlight: C(0x2A2F37FF),
        DiffAdd: C(0x7EE787FF), DiffRemove: C(0xFF7B72FF),
        DiffAddBg: C(0x0E2B1AFF), DiffRemoveBg: C(0x3D1416FF));

    public static readonly CodePalette SolarizedLight = new(
        Background: null,
        Plain: C(0x586E75FF), Keyword: C(0x859900FF), String: C(0x2AA198FF),
        Number: C(0xD33682FF), Comment: C(0x93A1A1FF), Type: C(0xB58900FF),
        Function: C(0x268BD2FF), Attribute: C(0xCB4B16FF), Variable: C(0x586E75FF),
        Constant: C(0x6C71C4FF), Operator: C(0x859900FF),
        LineNumber: C(0x93A1A1FF), LineHighlight: C(0xEEE8D5FF),
        DiffAdd: C(0x859900FF), DiffRemove: C(0xDC322FFF),
        DiffAddBg: C(0xEAE9CDFF), DiffRemoveBg: C(0xF0D8D2FF));

    public static readonly CodePalette SolarizedDark = new(
        Background: null,
        Plain: C(0x93A1A1FF), Keyword: C(0x859900FF), String: C(0x2AA198FF),
        Number: C(0xD33682FF), Comment: C(0x586E75FF), Type: C(0xB58900FF),
        Function: C(0x268BD2FF), Attribute: C(0xCB4B16FF), Variable: C(0x93A1A1FF),
        Constant: C(0x6C71C4FF), Operator: C(0x859900FF),
        LineNumber: C(0x586E75FF), LineHighlight: C(0x073642FF),
        DiffAdd: C(0x859900FF), DiffRemove: C(0xDC322FFF),
        DiffAddBg: C(0x0F2E1AFF), DiffRemoveBg: C(0x3A1817FF));

    public static readonly CodePalette Phosphor = new(
        Background: null,
        Plain: C(0xF5F5F5FF), Keyword: C(0xFFB84DFF), String: C(0xFFD43BFF),
        Number: C(0xFFAA00FF), Comment: C(0x888888FF), Type: C(0xFFD080FF),
        Function: C(0xFFA500FF), Attribute: C(0xFFC966FF), Variable: C(0xF5F5F5FF),
        Constant: C(0xFFAA00FF), Operator: C(0xF5F5F5FF),
        LineNumber: C(0x6E5A2EFF), LineHighlight: C(0x2A2210FF),
        DiffAdd: C(0xCFCFCFFF), DiffRemove: C(0x888888FF),
        DiffAddBg: C(0x1A1A1AFF), DiffRemoveBg: C(0x141414FF));

    public static readonly CodePalette DyslexiaLight = new(
        Background: null,
        Plain: C(0x2C2A26FF), Keyword: C(0x6A4A87FF), String: C(0x2A6A52FF),
        Number: C(0x8A4A1FFF), Comment: C(0x8B8576FF), Type: C(0x5A5034FF),
        Function: C(0x1B4F8AFF), Attribute: C(0xB0623EFF), Variable: C(0x2C2A26FF),
        Constant: C(0x8A4A1FFF), Operator: C(0x2C2A26FF),
        LineNumber: C(0xC0B49AFF), LineHighlight: C(0xF0E9CFFF),
        DiffAdd: C(0x4F7138FF), DiffRemove: C(0x8C2A1AFF),
        DiffAddBg: C(0xE3E5C9FF), DiffRemoveBg: C(0xF0DCD0FF));

    public static readonly CodePalette DyslexiaDark = new(
        Background: null,
        Plain: C(0xE5DCC5FF), Keyword: C(0xC99A4AFF), String: C(0xD2BD8AFF),
        Number: C(0xE8B270FF), Comment: C(0x847C6AFF), Type: C(0xD2BD8AFF),
        Function: C(0xA8C3E0FF), Attribute: C(0xC9846AFF), Variable: C(0xE5DCC5FF),
        Constant: C(0xE8B270FF), Operator: C(0xE5DCC5FF),
        LineNumber: C(0x52596CFF), LineHighlight: C(0x252D40FF),
        DiffAdd: C(0xC9C19AFF), DiffRemove: C(0x847C6AFF),
        DiffAddBg: C(0x222B1AFF), DiffRemoveBg: C(0x2A211AFF));

    public static readonly CodePalette Twilight = new(
        Background: null,
        Plain: C(0xB0B5BCFF), Keyword: C(0xF8B3B0FF), String: C(0xA6E3B0FF),
        Number: C(0xFBD78DFF), Comment: C(0x5B6168FF), Type: C(0xFFD580FF),
        Function: C(0xA8C9F0FF), Attribute: C(0xC8B5E8FF), Variable: C(0xB0B5BCFF),
        Constant: C(0xFBD78DFF), Operator: C(0xF8B3B0FF),
        LineNumber: C(0x3F454CFF), LineHighlight: C(0x1A2129FF),
        DiffAdd: C(0xA6E3B0FF), DiffRemove: C(0xF8B3B0FF),
        DiffAddBg: C(0x122319FF), DiffRemoveBg: C(0x271419FF));
}

/// A document theme. Affects only the article render surface and sidebar tint —
/// chrome stays system-light/dark so the app still feels native.
public sealed record MdvTheme(
    string Id,
    string Name,
    bool IsDark,
    Color Background,
    Color SecondaryBackground,
    Color Text,
    Color SecondaryText,
    Color TertiaryText,
    Color Heading,
    Color Link,
    Color Strong,
    Color Border,
    Color Divider,
    Color BlockquoteBar,
    Color SidebarTint,
    double SidebarTintOpacity,
    string? BodyFontFamily = null,
    double BaseFontSize = 16,
    double ParagraphLineSpacingEm = 0.30,
    double ArticleHorizontalPadding = 40,
    double? ArticleMaxWidth = 860,
    bool SmartTypographyAllowed = true,
    double H1SizeEm = 1.75,
    double H2SizeEm = 1.4,
    double H3SizeEm = 1.15,
    FontWeight? HeadingFontWeightOverride = null,
    FontWeight? StrongFontWeightOverride = null,
    bool ShowH1Rule = true,
    bool ShowH2Rule = false,
    double ParagraphBottomSpacing = 16,
    double H1TopSpacing = 24, double H1BottomSpacing = 16,
    double H2TopSpacing = 24, double H2BottomSpacing = 16,
    double H3TopSpacing = 24, double H3BottomSpacing = 16,
    Color? Accent = null,
    CodePalette? CodePalette = null)
{
    public FontWeight HeadingFontWeight => HeadingFontWeightOverride ?? FontWeights.SemiBold;
    public FontWeight StrongFontWeight => StrongFontWeightOverride ?? FontWeights.SemiBold;
    public Color AccentColor => Accent ?? Link;
    public CodePalette ResolvedCodePalette =>
        CodePalette ?? (IsDark ? CodePalettes.OneDark : CodePalettes.GithubLight);
}

public static class MdvThemes
{
    private static Color C(uint rgba)
    {
        byte r = (byte)((rgba >> 24) & 0xFF);
        byte g = (byte)((rgba >> 16) & 0xFF);
        byte b = (byte)((rgba >> 8) & 0xFF);
        byte a = (byte)(rgba & 0xFF);
        return Color.FromArgb(a, r, g, b);
    }

    /// "GitHub README, light, all business" — sane default.
    public static readonly MdvTheme HighContrast = new(
        Id: "high-contrast",
        Name: "High Contrast",
        IsDark: false,
        Background: C(0xFFFFFFFF),
        SecondaryBackground: C(0xF3F4F6FF),
        Text: C(0x0A0A0AFF),
        SecondaryText: C(0x4B5563FF),
        TertiaryText: C(0x6B7280FF),
        Heading: C(0x000000FF),
        Link: C(0x1D6FE0FF),
        Strong: C(0x0A0A0AFF),
        Border: C(0xE5E7EBFF),
        Divider: C(0xD1D5DBFF),
        BlockquoteBar: C(0xD1D5DBFF),
        SidebarTint: C(0xFFFFFFFF),
        SidebarTintOpacity: 0.0,
        Accent: C(0x1D6FE0FF),
        CodePalette: CodePalettes.GithubLight);

    /// Operational dark theme — GitHub-Dark-flavored, tuned for technical reading.
    public static readonly MdvTheme Charcoal = new(
        Id: "charcoal",
        Name: "Charcoal",
        IsDark: true,
        Background: C(0x1B1B21FF),
        SecondaryBackground: C(0x25262CFF),
        Text: C(0xC7D1DBFF),
        SecondaryText: C(0x8C99A8FF),
        TertiaryText: C(0x6E7785FF),
        Heading: C(0xF5F7FCFF),
        Link: C(0x5CA3FAFF),
        Strong: C(0xE8EDF5FF),
        Border: C(0x3B4252FF),
        Divider: C(0x3B4252FF),
        BlockquoteBar: C(0x3B4252FF),
        SidebarTint: C(0x1B1B21FF),
        SidebarTintOpacity: 0.18,
        BaseFontSize: 16.5,
        ParagraphLineSpacingEm: 0.25,
        ArticleHorizontalPadding: 48,
        ArticleMaxWidth: 920,
        H1SizeEm: 1.82,
        H2SizeEm: 1.45,
        H3SizeEm: 1.15,
        ShowH1Rule: true,
        ShowH2Rule: false,
        ParagraphBottomSpacing: 11,
        H1TopSpacing: 0, H1BottomSpacing: 12,
        H2TopSpacing: 22, H2BottomSpacing: 13,
        H3TopSpacing: 18, H3BottomSpacing: 8,
        Accent: C(0x2E7AEBFF),
        CodePalette: CodePalettes.Charcoal);

    /// Solarized Light + Besley slab serif — sustained-reading article theme.
    public static readonly MdvTheme SolariumDaylight = new(
        Id: "solarium-daylight",
        Name: "Solarium Daylight",
        IsDark: false,
        Background: C(0xFDF6E3FF),
        SecondaryBackground: C(0xEEE8D5FF),
        Text: C(0x586E75FF),
        SecondaryText: C(0x657B83FF),
        TertiaryText: C(0x93A1A1FF),
        Heading: C(0x073642FF),
        Link: C(0x268BD2FF),
        Strong: C(0x586E75FF),
        Border: C(0xDED7C2FF),
        Divider: C(0xDED7C2FF),
        BlockquoteBar: C(0xCB4B16FF),
        SidebarTint: C(0xFDF6E3FF),
        SidebarTintOpacity: 0.55,
        BodyFontFamily: "Besley",
        ParagraphLineSpacingEm: 0.20,
        ArticleHorizontalPadding: 36,
        ArticleMaxWidth: 720,
        H1SizeEm: 1.55,
        H2SizeEm: 1.28,
        H3SizeEm: 1.1,
        Accent: C(0xCB4B16FF),
        CodePalette: CodePalettes.SolarizedLight);

    /// Solarized Dark + Besley slab serif.
    public static readonly MdvTheme SolariumMoonlight = new(
        Id: "solarium-moonlight",
        Name: "Solarium Moonlight",
        IsDark: true,
        Background: C(0x002B36FF),
        SecondaryBackground: C(0x073642FF),
        Text: C(0x93A1A1FF),
        SecondaryText: C(0x839496FF),
        TertiaryText: C(0x657B83FF),
        Heading: C(0xEEE8D5FF),
        Link: C(0x268BD2FF),
        Strong: C(0x93A1A1FF),
        Border: C(0x0E4753FF),
        Divider: C(0x0E4753FF),
        BlockquoteBar: C(0xB58900FF),
        SidebarTint: C(0x002B36FF),
        SidebarTintOpacity: 0.32,
        BodyFontFamily: "Besley",
        ParagraphLineSpacingEm: 0.20,
        ArticleHorizontalPadding: 36,
        ArticleMaxWidth: 720,
        H1SizeEm: 1.55,
        H2SizeEm: 1.28,
        H3SizeEm: 1.1,
        Accent: C(0xB58900FF),
        CodePalette: CodePalettes.SolarizedDark);

    /// Long-form reading theme using bundled Alegreya serif. Warm parchment.
    public static readonly MdvTheme Sevilla = new(
        Id: "sevilla",
        Name: "Sevilla",
        IsDark: false,
        Background: C(0xF4EFE3FF),
        SecondaryBackground: C(0xEAE5D6FF),
        Text: C(0x42372CFF),
        SecondaryText: C(0x6A5C4DFF),
        TertiaryText: C(0x968874FF),
        Heading: C(0x2D2118FF),
        Link: C(0x2C5F8DFF),
        Strong: C(0x42372CFF),
        Border: C(0xE6DEC2FF),
        Divider: C(0xE6DEC2FF),
        BlockquoteBar: C(0xB0623EFF),
        SidebarTint: C(0xF4EFE3FF),
        SidebarTintOpacity: 0.55,
        BodyFontFamily: "Alegreya",
        BaseFontSize: 17,
        ParagraphLineSpacingEm: 0.55,
        ArticleHorizontalPadding: 30,
        ArticleMaxWidth: 620,
        H1SizeEm: 1.7,
        H2SizeEm: 1.25,
        H3SizeEm: 1.1,
        ShowH1Rule: true,
        ShowH2Rule: false,
        ParagraphBottomSpacing: 14,
        H1TopSpacing: 28, H1BottomSpacing: 18,
        H2TopSpacing: 32, H2BottomSpacing: 12,
        H3TopSpacing: 22, H3BottomSpacing: 8,
        Accent: C(0xB0623EFF),
        CodePalette: CodePalettes.Sevilla);

    /// Amber-on-black CRT homage. Smart typography intentionally disabled
    /// (curling quotes breaks the typewriter / terminal vibe).
    public static readonly MdvTheme Phosphor = new(
        Id: "phosphor",
        Name: "Phosphor",
        IsDark: true,
        Background: C(0x000000FF),
        SecondaryBackground: C(0x141414FF),
        Text: C(0xF5F5F5FF),
        SecondaryText: C(0xB8B8B8FF),
        TertiaryText: C(0x7A7A7AFF),
        Heading: C(0xFFA500FF),
        Link: C(0xFFD43BFF),
        Strong: C(0xF5F5F5FF),
        Border: C(0x2A2A2AFF),
        Divider: C(0x2A2A2AFF),
        BlockquoteBar: C(0xFFA500FF),
        SidebarTint: C(0x000000FF),
        SidebarTintOpacity: 0.30,
        SmartTypographyAllowed: false,
        Accent: C(0xFFA500FF),
        CodePalette: CodePalettes.Phosphor);

    /// Deep navy + mint heading + cream link — calm low-light palette.
    public static readonly MdvTheme Twilight = new(
        Id: "twilight",
        Name: "Twilight",
        IsDark: true,
        Background: C(0x0A0F14FF),
        SecondaryBackground: C(0x121820FF),
        Text: C(0xB0B5BCFF),
        SecondaryText: C(0x8C9298FF),
        TertiaryText: C(0x5B6168FF),
        Heading: C(0x6EBA7FFF),
        Link: C(0xFFD580FF),
        Strong: C(0xD8DEE9FF),
        Border: C(0x1E252DFF),
        Divider: C(0x1E252DFF),
        BlockquoteBar: C(0x6EBA7FFF),
        SidebarTint: C(0x0A0F14FF),
        SidebarTintOpacity: 0.32,
        Accent: C(0xFFD580FF),
        CodePalette: CodePalettes.Twilight);

    /// OpenDyslexic body, light. Heading weight forced to Regular and Strong
    /// to Bold because OpenDyslexic only ships 400 + 800; default SemiBold
    /// would resolve to ExtraBold and make every heading shout.
    public static readonly MdvTheme StandardErinLight = new(
        Id: "standard-erin-light",
        Name: "Standard Erin Light",
        IsDark: false,
        Background: C(0xFBF7E8FF),
        SecondaryBackground: C(0xF0EAD3FF),
        Text: C(0x2C2A26FF),
        SecondaryText: C(0x5C584FFF),
        TertiaryText: C(0x8B8576FF),
        Heading: C(0x1A1814FF),
        Link: C(0x1B4F8AFF),
        Strong: C(0x2C2A26FF),
        Border: C(0xE0D8BEFF),
        Divider: C(0xE0D8BEFF),
        BlockquoteBar: C(0xB0623EFF),
        SidebarTint: C(0xFBF7E8FF),
        SidebarTintOpacity: 0.55,
        BodyFontFamily: "OpenDyslexic",
        BaseFontSize: 15,
        SmartTypographyAllowed: false,
        HeadingFontWeightOverride: FontWeights.Normal,
        StrongFontWeightOverride: FontWeights.Bold,
        Accent: C(0xB0623EFF),
        CodePalette: CodePalettes.DyslexiaLight);

    public static readonly MdvTheme StandardErinDark = new(
        Id: "standard-erin-dark",
        Name: "Standard Erin Dark",
        IsDark: true,
        Background: C(0x1B2233FF),
        SecondaryBackground: C(0x252D40FF),
        Text: C(0xE5DCC5FF),
        SecondaryText: C(0xB0A88FFF),
        TertiaryText: C(0x847C6AFF),
        Heading: C(0xF0E8CFFF),
        Link: C(0xF5C97AFF),
        Strong: C(0xE5DCC5FF),
        Border: C(0x2E3A50FF),
        Divider: C(0x2E3A50FF),
        BlockquoteBar: C(0xC99A4AFF),
        SidebarTint: C(0x1B2233FF),
        SidebarTintOpacity: 0.32,
        BodyFontFamily: "OpenDyslexic",
        BaseFontSize: 15,
        SmartTypographyAllowed: false,
        HeadingFontWeightOverride: FontWeights.Normal,
        StrongFontWeightOverride: FontWeights.Bold,
        Accent: C(0xC99A4AFF),
        CodePalette: CodePalettes.DyslexiaDark);

    public static readonly IReadOnlyList<MdvTheme> All = new[]
    {
        HighContrast,
        Sevilla,
        Charcoal,
        SolariumDaylight,
        SolariumMoonlight,
        Phosphor,
        Twilight,
        StandardErinLight,
        StandardErinDark,
    };

    public static readonly MdvTheme Default = HighContrast;

    public static MdvTheme ById(string id) =>
        All.FirstOrDefault(t => t.Id == id) ?? Default;
}

/// Owns the user's current theme + zoom level. Mirrors the macOS
/// `ThemeManager`: persistent selection (with `system` sentinel),
/// persistent font scale, and live tracking of the OS appearance when
/// the user is on `system`.
public sealed partial class ThemeManager : ObservableObject
{
    public const string SystemId = "system";
    public const string SystemDisplayName = "System";

    public const double FontScaleStep = 0.10;
    public const double FontScaleMin = 0.60;
    public const double FontScaleMax = 2.50;

    private const string ThemeIdKey = "mdv_theme_id";
    private const string FontScaleKey = "mdv_font_scale";

    [ObservableProperty] private string _selectedId;
    [ObservableProperty] private MdvTheme _current;
    [ObservableProperty] private double _fontScale;

    private readonly UISettings _uiSettings = new();

    public ThemeManager()
    {
        _selectedId = Settings.Get(ThemeIdKey, MdvThemes.Default.Id) ?? MdvThemes.Default.Id;
        _fontScale = ClampScale(Settings.Get(FontScaleKey, 1.0));
        _current = Resolve(_selectedId);

        // Live OS-appearance tracking: only takes effect when the user is on
        // the System sentinel; explicit picks stay sticky.
        _uiSettings.ColorValuesChanged += OnSystemColorsChanged;
    }

    public void Set(MdvTheme theme) => SetSelection(theme.Id);

    public void SetSelection(string id)
    {
        SelectedId = id;
        Settings.Set(ThemeIdKey, id);
        Current = Resolve(id);
    }

    public void ZoomIn() => SetFontScale(FontScale + FontScaleStep);
    public void ZoomOut() => SetFontScale(FontScale - FontScaleStep);
    public void ResetZoom() => SetFontScale(1.0);

    private void SetFontScale(double s)
    {
        var clamped = ClampScale(s);
        var snapped = Math.Round(clamped * 10) / 10.0;
        if (snapped == FontScale) return;
        FontScale = snapped;
        Settings.Set(FontScaleKey, snapped);
    }

    private static double ClampScale(double s) => Math.Min(Math.Max(s, FontScaleMin), FontScaleMax);

    private void OnSystemColorsChanged(UISettings sender, object args)
    {
        if (SelectedId != SystemId) return;
        var resolved = Resolve(SystemId);
        if (resolved.Id != Current.Id) Current = resolved;
    }

    private static MdvTheme Resolve(string id) =>
        id == SystemId
            ? (SystemIsDark() ? MdvThemes.Twilight : MdvThemes.HighContrast)
            : MdvThemes.ById(id);

    private static bool SystemIsDark()
    {
        var fg = new UISettings().GetColorValue(UIColorType.Foreground);
        // High foreground brightness ⇒ dark mode (text is light against a dark bg).
        return ((5 * fg.G) + (2 * fg.R) + fg.B) > (8 * 128);
    }
}
