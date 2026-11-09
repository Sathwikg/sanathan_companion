using Sanathana.Companion.Application.Common.Translation;

namespace Sanathana.Companion.Tests;

/// <summary>
/// The translated Panchangam must read in the target language's word order: the postposition
/// after the time, never before it, with every digit and separator left exactly as written.
/// </summary>
public class PanchangamPhrasingTests
{
    [Theory]
    [InlineData("ఏకాదశి వరకు 10:36, ద్వాదశి నుండి 10:37", "ఏకాదశి 10:36 వరకు, ద్వాదశి 10:37 నుండి")]
    [InlineData("ఉత్తర వరకు 03:56, 06 Nov, హస్త నుండి 03:57, 06 Nov", "ఉత్తర 03:56, 06 Nov వరకు, హస్త 03:57, 06 Nov నుండి")]
    [InlineData("దశమి పూర్తి రోజు", "దశమి పూర్తి రోజు")]
    [InlineData("13:17 – 14:43", "13:17 – 14:43")]
    public void Telugu_connectives_move_behind_the_time(string substituted, string expected)
        => Assert.Equal(expected, PanchangamPhrasing.Postpose(substituted, "వరకు", "నుండి"));

    [Fact]
    public void Hindi_reads_tak_and_se_after_the_time()
        => Assert.Equal("एकादशी 10:36 तक, द्वादशी 10:37 से",
            PanchangamPhrasing.Postpose("एकादशी तक 10:36, द्वादशी से 10:37", "तक", "से"));

    [Fact]
    public void English_is_never_reordered()
        => Assert.Equal("Ekadasi upto 10:36, Dvadasi from 10:37",
            PanchangamPhrasing.Postpose("Ekadasi upto 10:36, Dvadasi from 10:37", "upto", "from"));

    [Fact]
    public void A_connective_glued_to_another_word_or_without_a_time_is_left_alone()
    {
        Assert.Equal("Xవరకు 10:00", PanchangamPhrasing.Postpose("Xవరకు 10:00", "వరకు", "నుండి"));
        Assert.Equal("వరకు now", PanchangamPhrasing.Postpose("వరకు now", "వరకు", "నుండి"));
    }

    [Fact]
    public void Only_the_panchangam_category_is_reordered()
    {
        Assert.True(PanchangamPhrasing.AppliesTo("panchangam"));
        Assert.True(PanchangamPhrasing.AppliesTo("Panchangam"));
        Assert.False(PanchangamPhrasing.AppliesTo("festivals"));
        Assert.False(PanchangamPhrasing.AppliesTo(null));
    }
}
