using System.Text;
using System.Text.RegularExpressions;

namespace Sanathana.Companion.Application.Common;

/// <summary>
/// Whitelist sanitizer for rich-text bodies produced by the chant editor. The stored HTML is
/// rendered back verbatim, so everything that is not explicitly allowed is stripped here — on
/// save — rather than trusted at render time.
/// </summary>
public static partial class HtmlSanitizer
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "hr", "div", "span",
        "b", "strong", "i", "em", "u", "s", "strike", "sub", "sup", "mark", "small",
        "ul", "ol", "li", "blockquote", "pre", "code",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "a"
    };

    /// <summary>Elements removed together with their content (not merely unwrapped).</summary>
    [GeneratedRegex(
        @"<(script|style|iframe|object|embed|form|input|button|select|textarea|link|meta|base|svg|math|template|noscript)\b[^>]*>[\s\S]*?</\1\s*>",
        RegexOptions.IgnoreCase)]
    private static partial Regex DangerousElementRegex();

    [GeneratedRegex(@"<!--[\s\S]*?-->")]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"</?([a-zA-Z][a-zA-Z0-9]*)((?:[^>""']|""[^""]*""|'[^']*')*)/?>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"([a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s""'>]+))")]
    private static partial Regex AttributeRegex();

    /// <summary>A "&lt;" that has no closing "&gt;" before the next "&lt;" or end of input — i.e. a
    /// dangling/unterminated tag such as "&lt;svg onload=…" that the whitelist pass cannot see.</summary>
    [GeneratedRegex(@"<(?![^<>]*>)")]
    private static partial Regex DanglingLtRegex();

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var working = CommentRegex().Replace(html, string.Empty);

        // Run to a fixed point so nested/split constructs can't survive one pass.
        string previous;
        do
        {
            previous = working;
            working = DangerousElementRegex().Replace(working, string.Empty);
        }
        while (!ReferenceEquals(previous, working) && previous != working);

        working = TagRegex().Replace(working, RewriteTag);

        // The whitelist above only matches tags that have a closing ">". A dangling tag with no
        // ">" (e.g. "<svg onload=alert(1)" at end of input) would otherwise survive verbatim and,
        // when the body is rendered as raw HTML, let the browser consume following markup as
        // attributes and fire an event handler. Encode any such unterminated "<".
        working = DanglingLtRegex().Replace(working, "&lt;");

        return working.Trim();
    }

    /// <summary>Strips all markup, e.g. for previews and search snippets.</summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var text = Sanitize(html);
        text = TagRegex().Replace(text, " ");
        text = text.Replace("&nbsp;", " ")
                   .Replace("&amp;", "&")
                   .Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&quot;", "\"");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string RewriteTag(Match match)
    {
        var raw = match.Value;
        var tag = match.Groups[1].Value.ToLowerInvariant();

        // Not on the whitelist → unwrap (drop the tag, keep any inner text).
        if (!AllowedTags.Contains(tag)) return string.Empty;

        var isClosing = raw.StartsWith("</", StringComparison.Ordinal);
        if (isClosing) return $"</{tag}>";

        var isSelfClosing = raw.EndsWith("/>", StringComparison.Ordinal) || tag is "br" or "hr";

        var sb = new StringBuilder("<").Append(tag);
        var attributes = match.Groups[2].Value;

        foreach (Match attr in AttributeRegex().Matches(attributes))
        {
            var name = attr.Groups[1].Value.ToLowerInvariant();
            var value = attr.Groups[2].Success ? attr.Groups[2].Value
                      : attr.Groups[3].Success ? attr.Groups[3].Value
                      : attr.Groups[4].Value;

            // Every event handler (onclick, onerror, …) goes.
            if (name.StartsWith("on", StringComparison.Ordinal)) continue;

            switch (name)
            {
                case "style":
                    var style = SanitizeStyle(value);
                    if (style is not null) sb.Append(" style=\"").Append(Escape(style)).Append('"');
                    break;

                case "href" when tag == "a":
                    var href = SanitizeUrl(value);
                    if (href is not null)
                        sb.Append(" href=\"").Append(Escape(href)).Append("\" target=\"_blank\" rel=\"noopener noreferrer\"");
                    break;

                // Everything else (src, srcset, formaction, data-*, class, id…) is dropped.
                default:
                    break;
            }
        }

        return sb.Append(isSelfClosing ? " />" : ">").ToString();
    }

    private static string? SanitizeStyle(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var lower = value.ToLowerInvariant();
        string[] banned = { "expression(", "javascript:", "url(", "@import", "behavior:", "-moz-binding" };
        if (banned.Any(b => lower.Contains(b, StringComparison.Ordinal))) return null;
        return value.Trim();
    }

    private static string? SanitizeUrl(string value)
    {
        var url = value.Trim();
        if (url.Length == 0) return null;

        // Defeat "java\tscript:" style obfuscation before inspecting the scheme.
        var probe = new string(url.Where(c => !char.IsWhiteSpace(c) && c != '\0').ToArray()).ToLowerInvariant();

        if (probe.StartsWith("http://", StringComparison.Ordinal) ||
            probe.StartsWith("https://", StringComparison.Ordinal) ||
            probe.StartsWith("mailto:", StringComparison.Ordinal) ||
            probe.StartsWith('/') || probe.StartsWith('#'))
        {
            return url;
        }
        return null;
    }

    private static string Escape(string value)
        => value.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
}
