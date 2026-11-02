using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Tests;

/// <summary>
/// The japa screen offers a chant in every language it was configured in, whatever UI language
/// the seeker chose — the per-language texts written on the Chants Config form must reach it.
/// </summary>
public class JapaChantLanguagesTests
{
    private sealed class Seeker : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? Email => null;
        public bool IsAuthenticated => true;
    }

    private static Language Lang(string name, string native, string code, bool active = true)
        => new() { Id = Guid.NewGuid(), Name = name, NativeName = native, Code = code, IsActive = active };

    private static async Task<(TestHarness Harness, SadhanaService Service, ChantConfig Chant)> ArrangeAsync(params Language[] languages)
    {
        var harness = new TestHarness();
        var ctx = harness.Context;
        var category = new Chant { Id = Guid.NewGuid(), Name = "LangTestCategory", HasCount = true, Count = 108, IsActive = true };
        ctx.Chants.Add(category);
        var chant = new ChantConfig { Id = Guid.NewGuid(), ChantId = category.Id, Name = "LangTestMantra", ChantText = "<p>om namah shivaya</p>", IsActive = true };
        ctx.ChantConfigs.Add(chant);
        ctx.Languages.AddRange(languages);
        await ctx.SaveChangesAsync();
        return (harness, new SadhanaService(harness.UnitOfWork, new Seeker()), chant);
    }

    private static async Task WriteAsync(TestHarness harness, ChantConfig chant, Language language, string text)
    {
        harness.Context.ChantLanguageConfigs.Add(new ChantLanguageConfig { ChantConfigId = chant.Id, LanguageId = language.Id, Text = text });
        await harness.Context.SaveChangesAsync();
    }

    [Fact]
    public async Task Every_configured_language_reaches_the_japa_screen_in_the_masters_order()
    {
        var english = Lang("English", "English", "en");
        var telugu = Lang("Telugu", "తెలుగు", "te");
        var hindi = Lang("Hindi", "हिन्दी", "hi");
        var (harness, service, chant) = await ArrangeAsync(telugu, hindi, english);
        using var _h = harness;
        await WriteAsync(harness, chant, telugu, "<p>ఓం నమః శివాయ</p>");
        await WriteAsync(harness, chant, hindi, "<p>ॐ नमः शिवाय</p>");
        await WriteAsync(harness, chant, english, "<p>om namah shivaya</p>");

        var detail = await service.GetChantAsync(chant.Id);

        // The Languages master orders by name: English, Hindi, Telugu.
        Assert.Equal(new[] { "en", "hi", "te" }, detail!.LanguageTexts.Select(t => t.Code));
        Assert.Equal("తెలుగు", detail.LanguageTexts.Single(t => t.Code == "te").NativeName);
        Assert.Equal("<p>ॐ नमः शिवाय</p>", detail.LanguageTexts.Single(t => t.Code == "hi").Text);
    }

    [Fact]
    public async Task The_main_text_stands_in_for_a_missing_English_row()
    {
        var english = Lang("English", "English", "en");
        var telugu = Lang("Telugu", "తెలుగు", "te");
        var (harness, service, chant) = await ArrangeAsync(english, telugu);
        using var _h = harness;
        await WriteAsync(harness, chant, telugu, "<p>ఓం నమః శివాయ</p>");

        var detail = await service.GetChantAsync(chant.Id);

        Assert.Equal(new[] { "en", "te" }, detail!.LanguageTexts.Select(t => t.Code));
        Assert.Equal(chant.ChantText, detail.LanguageTexts.Single(t => t.Code == "en").Text);
    }

    [Fact]
    public async Task Blank_rows_and_inactive_languages_are_not_offered()
    {
        var telugu = Lang("Telugu", "తెలుగు", "te");
        var tamil = Lang("Tamil", "தமிழ்", "ta");
        var urdu = Lang("Urdu", "اردو", "ur", active: false);
        var (harness, service, chant) = await ArrangeAsync(telugu, tamil, urdu);
        using var _h = harness;
        await WriteAsync(harness, chant, telugu, "<p>ఓం నమః శివాయ</p>");
        await WriteAsync(harness, chant, tamil, "   ");
        await WriteAsync(harness, chant, urdu, "<p>...</p>");

        var detail = await service.GetChantAsync(chant.Id);

        Assert.Equal(new[] { "te" }, detail!.LanguageTexts.Select(t => t.Code));
    }

    [Fact]
    public async Task A_chant_written_only_in_the_main_editor_has_no_language_tabs()
    {
        var (harness, service, chant) = await ArrangeAsync(Lang("English", "English", "en"));
        using var _h = harness;

        var detail = await service.GetChantAsync(chant.Id);

        Assert.Empty(detail!.LanguageTexts);
        Assert.Equal(chant.ChantText, detail.ChantText);
    }
}
