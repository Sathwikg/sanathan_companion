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

    /// <summary>Control characters used to fence off tags this sanitizer itself emitted.</summary>
    /// <remarks>
    /// Stripped from the input first, so nothing arriving from outside can forge a fence.
    /// </remarks>
    private const char TagOpen = '\u0001';
    private const char TagClose = '\u0002';

    [GeneratedRegex(@"[\u0000-\u0008\u000B\u000C\u000E-\u001F]")]
    private static partial Regex ControlCharRegex();

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        // Control characters go first: the rewrite below fences the tags it emits with two of
        // them, and an input that could carry its own fence would escape the encoding pass.
        var working = ControlCharRegex().Replace(html, string.Empty);
        working = CommentRegex().Replace(working, string.Empty);

        // Run to a fixed point so nested/split constructs can't survive one pass.
        string previous;
        do
        {
            previous = working;
            working = DangerousElementRegex().Replace(working, string.Empty);
        }
        while (!ReferenceEquals(previous, working) && previous != working);

        // Fence every tag this sanitizer emits, so the encoding pass below can tell them apart
        // from anything the whitelist pass failed to match.
        working = TagRegex().Replace(working, match =>
        {
            var rewritten = RewriteTag(match);
            // The fence chars stand IN PLACE OF the tag's own angle brackets, so the encoding
            // pass below cannot touch them. RewriteTag always emits "<...>" and escapes any
            // angle bracket inside an attribute value, so the first and last chars are safe to drop.
            return rewritten.Length == 0
                ? string.Empty
                : TagOpen + rewritten[1..^1] + TagClose;
        });

        // Anything still carrying a "<" was NOT rewritten, which means TagRegex could not match it.
        // That is not only the obvious dangling "<svg onload=…" with no ">": TagRegex reads
        // attributes as unquoted runs or COMPLETE quoted strings, so a tag containing an unmatched
        // quote — <img src=x onerror=alert(1) title=" — matches nothing at all and used to be
        // emitted verbatim, straight into a body that is rendered as raw HTML. Encoding every
        // surviving "<" closes the whole class rather than the one shape of it.
        working = working.Replace("<", "&lt;");

        // Restore the sanitizer's own tags.
        working = working.Replace(TagOpen, '<').Replace(TagClose, '>');

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

    /// <summary>
    /// CSS the stored HTML may carry, and the shape each value must take.
    /// </summary>
    /// <remarks>
    /// This used to be a denylist, and a denylist cannot hold the line here. "url(" can be spelled
    /// "\75 rl(" — the CSS escape is resolved before the function name is matched, so the literal
    /// substring never appears. An image loads just as well through image-set("…") with the word
    /// url nowhere in it. And position:fixed, which no denylist ever thought to name, lets one
    /// chant paint an opaque layer over the whole app inside the same WebView that holds the
    /// reader's session. Writes here are Admin-only, so this is an administrator reaching every
    /// reader rather than any-user XSS — which is still worth closing properly.
    /// <para>
    /// The allowlist kills that whole class from the character sets alone. A backslash matches
    /// neither the property pattern nor any value pattern. A "(" appears in exactly one
    /// alternative, rgb()/rgba() with plain numbers, so image-set, cross-fade, var, attr,
    /// expression and every function invented after this commit fail without being enumerated.
    /// position, inset, z-index, opacity, display, transform, every *-image property and every
    /// vendor prefix fail simply by not being keys.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, StyleValue> AllowedStyleProperties = new(StringComparer.Ordinal)
    {
        // The only declaration the editor itself writes; its toolbar has justifyLeft and
        // justifyCenter, and justifyLeft emits nothing at all.
        ["text-align"] = StyleValue.Keyword,
        // The rest are tolerance for content saved before this tightening, or by an engine that
        // answers the same buttons with styleWithCSS.
        ["font-weight"] = StyleValue.Keyword,
        ["font-style"] = StyleValue.Keyword,
        ["text-decoration"] = StyleValue.KeywordList,
        ["text-decoration-line"] = StyleValue.KeywordList,
        ["vertical-align"] = StyleValue.Keyword,
        ["text-transform"] = StyleValue.Keyword,
        ["white-space"] = StyleValue.Keyword,
        ["direction"] = StyleValue.Keyword,
        ["unicode-bidi"] = StyleValue.Keyword,
        ["list-style-type"] = StyleValue.Keyword,
        ["color"] = StyleValue.Color,
        ["background-color"] = StyleValue.Color,
        ["font-size"] = StyleValue.Length,
        ["line-height"] = StyleValue.Length,
        ["letter-spacing"] = StyleValue.Length,
        ["word-spacing"] = StyleValue.Length,
        ["text-indent"] = StyleValue.Length,
        ["margin-left"] = StyleValue.Length,
        ["padding-left"] = StyleValue.Length,
    };

    private enum StyleValue { Keyword, KeywordList, Color, Length }

    /// <summary>
    /// Keywords accepted for the properties above. One flat set is enough — a keyword that is
    /// meaningless for its property is just a declaration the browser discards. bidi-override is
    /// absent on purpose: it renders text in an order other than the one it is written in.
    /// </summary>
    private static readonly HashSet<string> AllowedStyleKeywords = new(StringComparer.Ordinal)
    {
        "left", "right", "center", "justify", "start", "end",
        "normal", "bold", "bolder", "lighter", "italic", "oblique",
        "100", "200", "300", "400", "500", "600", "700", "800", "900",
        "none", "underline", "line-through", "overline",
        "baseline", "sub", "super", "top", "middle", "bottom", "text-top", "text-bottom",
        "uppercase", "lowercase", "capitalize",
        "nowrap", "pre", "pre-wrap", "pre-line",
        "ltr", "rtl", "embed", "isolate",
        "disc", "circle", "square", "decimal",
        "lower-alpha", "upper-alpha", "lower-roman", "upper-roman",
        "inherit", "initial", "unset"
    };

    /// <summary>Letters and hyphens only — which is also what kills "\62 ackground-image".</summary>
    [GeneratedRegex(@"^[a-z][a-z-]{0,30}$")]
    private static partial Regex StylePropertyRegex();

    /// <summary>The only value shape in which a "(" is allowed to appear at all.</summary>
    [GeneratedRegex(@"^(?:#[0-9a-f]{3,8}|[a-z]{3,20}|rgba?\(\s*\d{1,3}\s*[,\s]\s*\d{1,3}\s*[,\s]\s*\d{1,3}\s*(?:[,/]\s*(?:0|1|0?\.\d{1,3}|\d{1,3}%)\s*)?\))$")]
    private static partial Regex StyleColorRegex();

    /// <summary>One length. Three digits is deliberate: a chant does not need to render at 9999px.</summary>
    [GeneratedRegex(@"^-?\d{1,3}(?:\.\d{1,2})?(?:px|pt|em|rem|%)?$")]
    private static partial Regex StyleLengthRegex();

    /// <summary>The real bound on parse work. Duplicate declarations are permitted; the last wins.</summary>
    private const int MaxStyleLength = 512;

    private const int MaxStyleDeclarations = 16;

    private static string? SanitizeStyle(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxStyleLength) return null;

        var kept = new List<string>();

        foreach (var declaration in value.Split(';',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (kept.Count == MaxStyleDeclarations) break;

            var colon = declaration.IndexOf(':');
            if (colon <= 0) continue;

            var name = declaration[..colon].Trim().ToLowerInvariant();
            var raw = declaration[(colon + 1)..].Trim().ToLowerInvariant();

            if (!StylePropertyRegex().IsMatch(name)) continue;
            if (!AllowedStyleProperties.TryGetValue(name, out var kind)) continue;
            if (!IsAllowedStyleValue(kind, raw)) continue;

            // Rebuilt from the two halves that passed, so a trailing "!important", a second colon
            // or any trailing text cannot ride along on an otherwise valid declaration.
            kept.Add($"{name}: {raw}");
        }

        return kept.Count == 0 ? null : string.Join("; ", kept);
    }

    private static bool IsAllowedStyleValue(StyleValue kind, string value) => kind switch
    {
        StyleValue.Keyword => AllowedStyleKeywords.Contains(value),
        StyleValue.KeywordList => value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                       is { Length: > 0 and <= 4 } parts
                                  && parts.All(AllowedStyleKeywords.Contains),
        StyleValue.Color => StyleColorRegex().IsMatch(value),
        StyleValue.Length => StyleLengthRegex().IsMatch(value),
        _ => false
    };

    private static string? SanitizeUrl(string value)
    {
        var url = value.Trim();
        if (url.Length == 0) return null;

        // Defeat "java\tscript:" style obfuscation before inspecting the scheme.
        var probe = new string(url.Where(c => !char.IsWhiteSpace(c) && c != '\0').ToArray()).ToLowerInvariant();

        // "//evil.com" is protocol-relative: the browser resolves it against the page's own scheme
        // and lands on someone else's host. It passed the StartsWith('/') test that was meant to
        // allow same-site paths, so it has to be rejected before that test is reached.
        if (probe.StartsWith("//", StringComparison.Ordinal)) return null;

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
