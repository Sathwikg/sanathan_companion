using Sanathana.Companion.Application.Common;

namespace Sanathana.Companion.Tests;

/// <summary>
/// Chant and puja bodies are stored as HTML and rendered back as raw markup, so whatever survives
/// this sanitizer executes in every reader's browser — and, on mobile, inside the WebView that
/// holds the signed-in session. These are the shapes that used to get through.
/// </summary>
public class HtmlSanitizerBypassTests
{
    private static string Clean(string html) => HtmlSanitizer.Sanitize(html);

    [Theory]
    // An unmatched quote makes the tag regex match nothing at all, so the tag used to be copied
    // through verbatim — the browser then parses it and fires the handler.
    [InlineData("<img src=x onerror=alert(1) title=\"")]
    [InlineData("<img src=x onerror=alert(1) title='")]
    [InlineData("<p title=\"unclosed><script>alert(1)</script>")]
    [InlineData("<div class='oops>hello")]
    // The dangling-tag shape, which the previous fix covered.
    [InlineData("<svg onload=alert(1)")]
    [InlineData("text <iframe src=javascript:alert(1)")]
    public void A_malformed_tag_never_survives_as_markup(string html)
    {
        var result = Clean(html);

        // The property that matters is that no ELEMENT can be opened. The handler text itself may
        // remain — once its "<" is encoded, "onerror=alert(1)" is inert prose, and stripping words
        // out of a chant commentary would be the wrong cure.
        AssertNoElementOpens(result);
    }

    /// <summary>
    /// Fails if any "&lt;" is followed by a letter or "/", i.e. anything a browser would begin
    /// parsing as a tag, other than the sanitizer's own whitelisted output.
    /// </summary>
    private static void AssertNoElementOpens(string result)
    {
        var allowed = new[] { "p", "br", "hr", "div", "span", "b", "strong", "i", "em", "u", "s",
                              "strike", "sub", "sup", "mark", "small", "ul", "ol", "li",
                              "blockquote", "pre", "code", "h1", "h2", "h3", "h4", "h5", "h6", "a" };

        foreach (System.Text.RegularExpressions.Match m in
                 System.Text.RegularExpressions.Regex.Matches(result, @"<\s*/?\s*([a-zA-Z][a-zA-Z0-9]*)"))
        {
            var name = m.Groups[1].Value.ToLowerInvariant();
            Assert.True(allowed.Contains(name),
                $"'{name}' was opened as an element. Sanitized output was: {result}");
        }
    }

    [Fact]
    public void A_caller_cannot_forge_the_sanitizers_own_tag_fence()
    {
        // The rewrite fences its output with two control characters. If those survived from the
        // input, an attacker could wrap arbitrary markup and have it decoded back into tags.
        var forged = "\u0001img src=x onerror=alert(1)\u0002";

        var result = Clean(forged);

        // The fence characters are stripped from the input before anything else, so they cannot be
        // decoded back into "<" and ">" at the end.
        AssertNoElementOpens(result);
        Assert.DoesNotContain('', result);
        Assert.DoesNotContain('', result);
    }

    [Theory]
    // Protocol-relative: the browser supplies the scheme and lands on someone else's host. It
    // passed the StartsWith('/') test that exists to allow same-site paths.
    [InlineData("<a href=\"//evil.example\">go</a>")]
    [InlineData("<a href=\"\t//evil.example\">go</a>")]
    public void A_protocol_relative_link_is_dropped(string html)
    {
        var result = Clean(html);

        Assert.DoesNotContain("evil.example", result);
        Assert.Contains("go", result);   // the text stays; only the destination goes
    }

    [Theory]
    [InlineData("<a href=\"https://example.org/page\">ok</a>", "https://example.org/page")]
    [InlineData("<a href=\"/panchangam\">ok</a>", "/panchangam")]
    [InlineData("<a href=\"mailto:someone@example.org\">ok</a>", "mailto:someone@example.org")]
    public void Legitimate_links_still_work(string html, string expected)
        => Assert.Contains(expected, Clean(html));

    [Fact]
    public void Ordinary_formatting_is_untouched()
    {
        var result = Clean("<p>Om <strong>Namah</strong> Shivaya<br />line</p>");

        Assert.Contains("<p>", result);
        Assert.Contains("<strong>", result);
        Assert.Contains("Namah", result);
        Assert.Contains("<br />", result);
    }

    [Fact]
    public void A_literal_less_than_in_text_is_encoded_rather_than_dropped()
    {
        // Chant commentary can legitimately contain "<". It must render, not vanish.
        var result = Clean("<p>a &lt; b, and 3 < 4</p>");

        Assert.Contains("&lt;", result);
        Assert.Contains("4", result);
    }

    [Fact]
    public void Script_and_its_content_are_removed_entirely()
    {
        var result = Clean("<p>before</p><script>alert(1)</script><p>after</p>");

        Assert.DoesNotContain("alert", result);
        Assert.Contains("before", result);
        Assert.Contains("after", result);
    }

    [Fact]
    public void Event_handlers_are_stripped_from_allowed_tags()
    {
        var result = Clean("<p onclick=\"alert(1)\">tap</p>");

        Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tap", result);
    }

    [Fact]
    public void Plain_text_extraction_still_works_after_the_change()
    {
        var text = HtmlSanitizer.ToPlainText("<p>Om <strong>Namah</strong> Shivaya</p>");

        Assert.Contains("Om", text);
        Assert.Contains("Namah", text);
        Assert.DoesNotContain("<", text);
    }
}
