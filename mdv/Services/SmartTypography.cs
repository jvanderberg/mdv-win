using System.Text;

namespace Mdv.Services;

/// SmartyPants-style typography on a markdown block:
///   - straight `"` and `'` curl into directional quotes
///   - `---` → em-dash, `--` between digits/letters → en-dash, ` -- ` → spaced em-dash
///   - `...` → horizontal ellipsis
///
/// Code is left alone: inline backtick spans, fenced blocks, link URLs `](...)`,
/// and autolink / HTML tag spans `<...>` are preserved verbatim.
///
/// Operates on a single block — call site splits markdown into blocks first.
public static class SmartTypography
{
    public static string Smarten(string source)
    {
        var trimmed = source.Trim();
        if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~")) return source;

        var result = new StringBuilder(source.Length);
        int i = 0;
        int codeRun = 0;
        int linkParenDepth = 0;
        int n = source.Length;

        while (i < n)
        {
            char c = source[i];

            if (c == '`')
            {
                int run = 0;
                int j = i;
                while (j < n && source[j] == '`') { run++; j++; }
                if (codeRun == 0) codeRun = run;
                else if (codeRun == run) codeRun = 0;
                result.Append('`', run);
                i = j;
                continue;
            }

            if (codeRun > 0)
            {
                result.Append(c);
                i++;
                continue;
            }

            if (linkParenDepth == 0 && c == ']' && i + 1 < n && source[i + 1] == '(')
            {
                result.Append("](");
                i += 2;
                linkParenDepth = 1;
                continue;
            }
            if (linkParenDepth > 0)
            {
                result.Append(c);
                if (c == '(') linkParenDepth++;
                else if (c == ')') linkParenDepth--;
                i++;
                continue;
            }

            if (c == '<' && i + 1 < n)
            {
                char first = source[i + 1];
                if (char.IsLetter(first) || first == '/')
                {
                    int limit = System.Math.Min(n, i + 256);
                    int close = source.IndexOf('>', i, limit - i);
                    if (close >= 0)
                    {
                        result.Append(source, i, close - i + 1);
                        i = close + 1;
                        continue;
                    }
                }
            }

            if (c == '-' && i + 2 < n && source[i + 1] == '-' && source[i + 2] == '-')
            {
                result.Append('—');
                i += 3;
                continue;
            }

            if (c == '-' && i + 1 < n && source[i + 1] == '-')
            {
                char? prev = i > 0 ? source[i - 1] : null;
                char? next2 = i + 2 < n ? source[i + 2] : null;

                bool prevDigit = prev.HasValue && char.IsDigit(prev.Value);
                bool prevLetter = prev.HasValue && char.IsLetter(prev.Value);
                bool prevSpace = prev == ' ';
                bool nextDigit = next2.HasValue && char.IsDigit(next2.Value);
                bool nextLetter = next2.HasValue && char.IsLetter(next2.Value);
                bool nextSpace = next2 == ' ';

                if ((prevDigit && nextDigit) || (prevLetter && nextLetter))
                {
                    result.Append('–');
                    i += 2;
                    continue;
                }
                if (prevSpace && nextSpace)
                {
                    result.Append('—');
                    i += 2;
                    continue;
                }
                result.Append('-');
                i++;
                continue;
            }

            if (c == '.' && i + 2 < n && source[i + 1] == '.' && source[i + 2] == '.')
            {
                result.Append('…');
                i += 3;
                continue;
            }

            if (c == '"')
            {
                char? prev = i > 0 ? source[i - 1] : null;
                result.Append(IsOpenQuoteContext(prev) ? '“' : '”');
                i++;
                continue;
            }
            if (c == '\'')
            {
                char? prev = i > 0 ? source[i - 1] : null;
                result.Append(IsOpenQuoteContext(prev) ? '‘' : '’');
                i++;
                continue;
            }

            result.Append(c);
            i++;
        }

        return result.ToString();
    }

    private static bool IsOpenQuoteContext(char? prev)
    {
        if (!prev.HasValue) return true;
        char p = prev.Value;
        if (char.IsWhiteSpace(p)) return true;
        return "([{<—–…".IndexOf(p) >= 0;
    }
}
