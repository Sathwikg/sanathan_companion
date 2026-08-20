using Sanathana.Companion.Application.Common;

namespace Sanathana.Companion.Tests;

/// <summary>
/// The style attribute, which used to be filtered by a denylist and is now an allowlist.
/// </summary>
/// <remarks>
/// Threat model, stated plainly: every write path for this HTML is Admin-only, so what these
/// assertions close is an administrator (or anyone holding an admin session) reaching every
/// reader's browser and mobile WebView — not any-user XSS. The denylist named six strings and
/// missed at least four live bypasses, which is what a denylist does.
/// </remarks>
public class HtmlSanitizerStyleTests
{
    private static string Clean(string input) => HtmlSanitizer.Sanitize(input);

    // ---------------------------------------------------------------- what the denylist missed

    [Theory]
    // A full-viewport opaque layer. No denylist ever thought to name "position".
    [InlineData("<p style=\"position:fixed;inset:0;z-index:2147483647;background-color:#fff\">gotcha</p>", "position")]
    [InlineData("<p style=\"color:red;position:fixed\">mixed</p>", "position")]
    // The word "url" never appears in the source text; the CSS escape is resolved first.
    [InlineData("<span style=\"background-image:\\75 rl('https://evil.example/x.png')\">x</span>", "evil.example")]
    // An outbound image load with "url" nowhere in it at all.
    [InlineData("<span style=\"background-image:image-set('https://evil.example/x.png' 1x)\">x</span>", "evil.example")]
    // The denylist named -moz-binding and nothing else in the vendor-prefix family.
    [InlineData("<span style=\"-webkit-user-modify:read-write\">x</span>", "user-modify")]
    public void The_dangerous_declaration_does_not_survive(string input, string mustBeGone)
        => Assert.DoesNotContain(mustBeGone, Clean(input), StringComparison.OrdinalIgnoreCase);

    [Theory]
    [InlineData("<span style=\"background-image:\\75 rl('https://evil.example/x.png')\">x</span>")]
    [InlineData("<span style=\"background-image:image-set('https://evil.example/x.png' 1x)\">x</span>")]
    [InlineData("<span style=\"-webkit-user-modify:read-write\">x</span>")]
    public void And_nothing_else_in_those_declarations_is_worth_keeping_either(string input)
        => Assert.DoesNotContain("style=", Clean(input), StringComparison.Ordinal);

    [Fact]
    public void An_invisible_full_page_click_trap_loses_its_geometry_but_keeps_being_a_link()
    {
        // The sanitizer helpfully adds target="_blank" to links, which is exactly what made this
        // one worth building: an opaque layer over the whole app, every click going elsewhere.
        var result = Clean(
            "<a href=\"https://evil.example\" style=\"position:fixed;inset:0;opacity:0;z-index:9999;display:block\">click trap</a>");

        Assert.Contains("href=\"https://evil.example\"", result, StringComparison.Ordinal);
        Assert.DoesNotContain("style=", result, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- the class, not the instances

    [Theory]
    [InlineData("<p style=\"background-image:var(--x)\">v</p>")]
    [InlineData("<p style=\"width:calc(100vw)\">c</p>")]
    [InlineData("<p style=\"content:attr(href)\">a</p>")]
    [InlineData("<p style=\"background-image:cross-fade(url(a.png), url(b.png))\">x</p>")]
    [InlineData("<p style=\"width:expression(alert(1))\">e</p>")]
    public void Any_function_call_outside_rgb_fails_without_being_named(string input)
        => Assert.DoesNotContain("style=", Clean(input), StringComparison.Ordinal);

    [Fact]
    public void A_declaration_is_rebuilt_from_the_halves_that_parsed_so_nothing_rides_along()
    {
        // "!important" is trailing text on an otherwise valid declaration; the whole thing goes
        // rather than the suffix being trimmed off and the rest trusted.
        Assert.DoesNotContain("style=", Clean("<p style=\"font-size: 12px !important\">imp</p>"), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- what real content needs

    [Fact]
    public void The_alignment_the_editor_writes_still_survives()
    {
        // Substring, not equality: the trailing semicolon execCommand emits is dropped when the
        // declaration is rebuilt.
        Assert.Contains("text-align: center", Clean("<p style=\"text-align: center;\">centred</p>"), StringComparison.Ordinal);
        Assert.Contains("text-align: center", Clean("<p style=\"TEXT-ALIGN: CENTER\">upper</p>"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<p style=\"color: rgb(1, 2, 3)\">rgb</p>", "color: rgb(1, 2, 3)")]
    [InlineData("<p style=\"color:#fff\">hex</p>", "color: #fff")]
    [InlineData("<p style=\"text-decoration: underline line-through\">td</p>", "text-decoration: underline line-through")]
    [InlineData("<p style=\"font-weight: 700\">fw</p>", "font-weight: 700")]
    [InlineData("<p style=\"line-height: 1.5\">lh</p>", "line-height: 1.5")]
    [InlineData("<p style=\"font-size: 1.5rem\">fs</p>", "font-size: 1.5rem")]
    [InlineData("<p style=\"margin-left: 40px\">ml</p>", "margin-left: 40px")]
    public void Styling_that_a_chant_might_legitimately_carry_is_kept(string input, string expected)
        => Assert.Contains(expected, Clean(input), StringComparison.Ordinal);

    [Fact]
    public void A_mixed_declaration_keeps_the_good_half_and_drops_the_bad()
    {
        var result = Clean("<p style=\"color:red;position:fixed\">mixed</p>");

        Assert.Contains("color: red", result, StringComparison.Ordinal);
        Assert.DoesNotContain("position", result, StringComparison.Ordinal);
    }

    [Fact]
    public void An_absurdly_long_style_attribute_is_refused_outright()
    {
        var long_ = "<p style=\"" + string.Concat(Enumerable.Repeat("color:red;", 200)) + "\">x</p>";

        Assert.DoesNotContain("style=", Clean(long_), StringComparison.Ordinal);
    }
}
