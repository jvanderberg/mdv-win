using System.Collections.Generic;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using Windows.UI;

namespace Mdv.Services;

public sealed record HighlightedToken(string Text, Color Color);

/// TextMateSharp wrapper. One Registry per app, one Grammar per language.
/// Returns flat token spans for a code string + theme-derived palette.
public sealed class CodeHighlighter
{
    private static readonly RegistryOptions _options = new(ThemeName.DarkPlus);
    private static readonly Registry _registry = new(_options);
    private static readonly Theme _theme = _registry.GetTheme();

    private readonly Dictionary<string, IGrammar?> _grammarCache = new();

    public List<HighlightedToken> Highlight(string code, string? languageHint, CodePalette palette)
    {
        var grammar = ResolveGrammar(languageHint);
        var result = new List<HighlightedToken>();
        if (grammar == null)
        {
            result.Add(new HighlightedToken(code, palette.Plain));
            return result;
        }

        IStateStack? ruleStack = null;
        var lines = code.Split('\n');
        for (int li = 0; li < lines.Length; li++)
        {
            var line = lines[li];
            var tokens = grammar.TokenizeLine(line, ruleStack, System.TimeSpan.MaxValue);
            ruleStack = tokens.RuleStack;

            int cursor = 0;
            foreach (var token in tokens.Tokens)
            {
                if (token.StartIndex > cursor)
                    result.Add(new HighlightedToken(line.Substring(cursor, token.StartIndex - cursor), palette.Plain));
                int end = System.Math.Min(token.EndIndex, line.Length);
                if (end <= token.StartIndex) continue;
                var text = line.Substring(token.StartIndex, end - token.StartIndex);
                var color = MapScopesToColor(token.Scopes, palette);
                result.Add(new HighlightedToken(text, color));
                cursor = end;
            }
            if (cursor < line.Length)
                result.Add(new HighlightedToken(line.Substring(cursor), palette.Plain));
            if (li < lines.Length - 1)
                result.Add(new HighlightedToken("\n", palette.Plain));
        }
        return result;
    }

    private IGrammar? ResolveGrammar(string? hint)
    {
        if (string.IsNullOrWhiteSpace(hint)) return null;
        var key = hint.Trim().Split(' ')[0].ToLowerInvariant();
        if (_grammarCache.TryGetValue(key, out var g)) return g;

        var lang = LanguageFromHint(key);
        if (lang == null) { _grammarCache[key] = null; return null; }
        var scope = _options.GetScopeByLanguageId(lang);
        if (string.IsNullOrEmpty(scope)) { _grammarCache[key] = null; return null; }
        var grammar = _registry.LoadGrammar(scope);
        _grammarCache[key] = grammar;
        return grammar;
    }

    private static string? LanguageFromHint(string hint) => hint switch
    {
        "c" => "c",
        "cpp" or "c++" or "cxx" or "h" or "hpp" => "cpp",
        "go" or "golang" => "go",
        "rust" or "rs" => "rust",
        "bash" or "sh" or "zsh" or "shell" or "console" => "shellscript",
        "javascript" or "js" or "jsx" or "node" => "javascript",
        "typescript" or "ts" or "tsx" => "typescript",
        "yaml" or "yml" => "yaml",
        "toml" => "toml",
        "python" or "py" or "python3" => "python",
        "ruby" or "rb" => "ruby",
        "json" => "json",
        "xml" or "html" => "html",
        "css" => "css",
        "swift" => "swift",
        "java" => "java",
        "kotlin" or "kt" => "kotlin",
        "csharp" or "cs" or "c#" => "csharp",
        "sql" => "sql",
        "markdown" or "md" => "markdown",
        "diff" or "patch" => "diff",
        "objective-c" or "objc" => "objective-c",
        _ => null,
    };

    private static Color MapScopesToColor(System.Collections.Generic.IList<string> scopes, CodePalette palette)
    {
        // Walk scopes deepest-first; first match wins. Maps TextMate's
        // hierarchical scope strings (e.g. "keyword.control.flow.go") to
        // mdv's palette slots — the same trick as the macOS app's
        // `colorForCaptureNameComponents`.
        for (int i = scopes.Count - 1; i >= 0; i--)
        {
            var s = scopes[i];
            if (s.StartsWith("comment")) return palette.Comment;
            if (s.StartsWith("string")) return palette.String;
            if (s.StartsWith("constant.numeric") || s.StartsWith("constant.language")) return palette.Number;
            if (s.StartsWith("constant")) return palette.Constant;
            if (s.StartsWith("keyword.operator")) return palette.Operator;
            if (s.StartsWith("keyword")) return palette.Keyword;
            if (s.StartsWith("storage.type")) return palette.Type;
            if (s.StartsWith("storage")) return palette.Keyword;
            if (s.StartsWith("entity.name.function") || s.StartsWith("support.function") || s.StartsWith("meta.function-call")) return palette.Function;
            if (s.StartsWith("entity.name.type") || s.StartsWith("support.type") || s.StartsWith("support.class")) return palette.Type;
            if (s.StartsWith("variable.parameter")) return palette.Variable;
            if (s.StartsWith("variable")) return palette.Variable;
            if (s.StartsWith("entity.other.attribute-name") || s.StartsWith("meta.attribute") || s.StartsWith("meta.tag")) return palette.Attribute;
            if (s.StartsWith("punctuation")) return palette.Operator;
        }
        return palette.Plain;
    }
}
