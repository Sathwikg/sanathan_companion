using Sanathana.Companion.Application.DTOs.Localization;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;

namespace Sanathana.Companion.Tests;

/// <summary>
/// Behaviour of the localization service against a real (in-memory) database: the English
/// fallback chain, hand-edit protection during re-import, and the per-form opt-out.
/// </summary>
public class LocalizationServiceTests
{
    /// <summary>A seed source the test controls, standing in for the embedded JSON files.</summary>
    private sealed class FakeSeed : ILocalizationSeedSource
    {
        private readonly Dictionary<string, Dictionary<string, string>> _data;
        public FakeSeed(Dictionary<string, Dictionary<string, string>> data) => _data = data;
        public IReadOnlyDictionary<string, Dictionary<string, string>> Load() => _data;
    }

    private static FakeSeed DefaultSeed() => new(new()
    {
        ["en"] = new() { ["common.save"] = "Save", ["common.cancel"] = "Cancel", ["nav.masters"] = "Masters" },
        ["te"] = new() { ["common.save"] = "భద్రపరచు", ["nav.masters"] = "మాస్టర్లు" }, // cancel deliberately absent
    });

    private static LocalizationService NewService(TestHarness harness, ILocalizationSeedSource? seed = null)
        => new(harness.UnitOfWork, seed ?? DefaultSeed());

    private static async Task<(Guid English, Guid Telugu)> SeedLanguagesAsync(TestHarness harness)
    {
        var en = new Language { Id = Guid.NewGuid(), Name = "English", Code = "en", IsActive = true };
        var te = new Language { Id = Guid.NewGuid(), Name = "Telugu", Code = "te", NativeName = "తెలుగు", IsActive = true };
        harness.Context.Set<Language>().AddRange(en, te);
        await harness.Context.SaveChangesAsync();
        return (en.Id, te.Id);
    }

    [Fact]
    public async Task Import_loads_seed_entries_for_each_known_language()
    {
        using var harness = new TestHarness();
        await SeedLanguagesAsync(harness);
        var service = NewService(harness);

        var written = await service.ImportSeedFilesAsync();

        // 3 English + 2 Telugu labels, plus any menu names auto-translated from nav.* labels.
        Assert.True(written >= 5, $"Expected at least the 5 seeded labels to be written, got {written}.");

        var english = await service.GetBundleAsync("en");
        Assert.Equal("Save", english.Labels["common.save"]);
        Assert.Equal("Masters", english.Labels["nav.masters"]);

        var telugu = await service.GetBundleAsync("te");
        Assert.Equal("భద్రపరచు", telugu.Labels["common.save"]);
    }

    [Fact]
    public async Task Import_is_idempotent()
    {
        using var harness = new TestHarness();
        await SeedLanguagesAsync(harness);
        var service = NewService(harness);

        await service.ImportSeedFilesAsync();
        var second = await service.ImportSeedFilesAsync();

        Assert.Equal(0, second);
    }

    [Fact]
    public async Task Bundle_falls_back_to_english_for_untranslated_keys()
    {
        using var harness = new TestHarness();
        var (_, te) = await SeedLanguagesAsync(harness);
        var service = NewService(harness);
        await service.ImportSeedFilesAsync();

        var bundle = await service.GetBundleAsync("te");

        Assert.False(bundle.IsBase);
        Assert.Equal("భద్రపరచు", bundle.Labels["common.save"]);   // translated
        Assert.Equal("Cancel", bundle.Labels["common.cancel"]);    // fell back to English
        Assert.Equal(te, bundle.LanguageId);
    }

    [Fact]
    public async Task Bundle_for_an_unknown_code_returns_the_english_base()
    {
        using var harness = new TestHarness();
        await SeedLanguagesAsync(harness);
        var service = NewService(harness);
        await service.ImportSeedFilesAsync();

        var bundle = await service.GetBundleAsync("zz");

        Assert.True(bundle.IsBase);
        Assert.Equal("en", bundle.Code);
        Assert.Equal("Save", bundle.Labels["common.save"]);
    }

    [Fact]
    public async Task Editing_a_label_overrides_the_seed_and_survives_reimport()
    {
        using var harness = new TestHarness();
        var (_, te) = await SeedLanguagesAsync(harness);
        var service = NewService(harness);
        await service.ImportSeedFilesAsync();

        await service.SaveLabelsAsync(te, new SaveLabelsDto
        {
            Items = new() { new SaveLabelItemDto { Key = "common.save", Value = "సేవ్ చేయి" } }
        });

        await service.ImportSeedFilesAsync(); // re-import must not clobber the hand edit

        var bundle = await service.GetBundleAsync("te");
        Assert.Equal("సేవ్ చేయి", bundle.Labels["common.save"]);
    }

    [Fact]
    public async Task Clearing_a_label_restores_the_english_fallback()
    {
        using var harness = new TestHarness();
        var (_, te) = await SeedLanguagesAsync(harness);
        var service = NewService(harness);
        await service.ImportSeedFilesAsync();

        await service.SaveLabelsAsync(te, new SaveLabelsDto
        {
            Items = new() { new SaveLabelItemDto { Key = "common.save", Value = "   " } }
        });

        var bundle = await service.GetBundleAsync("te");
        Assert.Equal("Save", bundle.Labels["common.save"]);
    }

    [Fact]
    public async Task English_cannot_be_edited()
    {
        using var harness = new TestHarness();
        var (en, _) = await SeedLanguagesAsync(harness);
        var service = NewService(harness);
        await service.ImportSeedFilesAsync();

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.SaveLabelsAsync(en, new SaveLabelsDto()));

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.GetLabelEditorAsync(en));
    }

    [Fact]
    public async Task Label_editor_pairs_english_with_the_target_language()
    {
        using var harness = new TestHarness();
        var (_, te) = await SeedLanguagesAsync(harness);
        var service = NewService(harness);
        await service.ImportSeedFilesAsync();

        var editor = await service.GetLabelEditorAsync(te);

        var save = editor.Rows.Single(r => r.Key == "common.save");
        Assert.Equal("Save", save.English);
        Assert.Equal("భద్రపరచు", save.Translation);

        var cancel = editor.Rows.Single(r => r.Key == "common.cancel");
        Assert.Equal("Cancel", cancel.English);
        Assert.Equal(string.Empty, cancel.Translation); // untranslated shows blank, ready to fill in
    }

    [Fact]
    public async Task Forms_translate_by_default_and_only_opt_outs_are_stored()
    {
        using var harness = new TestHarness();
        var (_, te) = await SeedLanguagesAsync(harness);
        var service = NewService(harness);
        await service.ImportSeedFilesAsync();

        var module = harness.Context.Set<MenuModule>().First(m => m.RoutePath != null);

        // Default: nothing disabled.
        var before = await service.GetBundleAsync("te");
        Assert.Empty(before.DisabledRoutes);
        Assert.True((await service.GetFormMatrixAsync(te)).Forms.First(f => f.ModuleId == module.Id).Enabled);

        // Opt the form out.
        await service.SaveFormMatrixAsync(te, new SaveLanguageFormsDto
        {
            Items = new() { new SaveLanguageFormItemDto { ModuleId = module.Id, Enabled = false } }
        });

        var after = await service.GetBundleAsync("te");
        Assert.Contains(LocalizationService.NormaliseRoute(module.RoutePath!), after.DisabledRoutes);
        Assert.False((await service.GetFormMatrixAsync(te)).Forms.First(f => f.ModuleId == module.Id).Enabled);

        // Turning it back on removes the row again.
        await service.SaveFormMatrixAsync(te, new SaveLanguageFormsDto
        {
            Items = new() { new SaveLanguageFormItemDto { ModuleId = module.Id, Enabled = true } }
        });
        Assert.Empty((await service.GetBundleAsync("te")).DisabledRoutes);
    }

    [Fact]
    public async Task Saving_a_form_that_does_not_exist_is_rejected()
    {
        using var harness = new TestHarness();
        var (_, te) = await SeedLanguagesAsync(harness);
        var service = NewService(harness);

        await Assert.ThrowsAsync<BadRequestException>(() => service.SaveFormMatrixAsync(te, new SaveLanguageFormsDto
        {
            Items = new() { new SaveLanguageFormItemDto { ModuleId = Guid.NewGuid(), Enabled = false } }
        }));
    }

    [Fact]
    public async Task Entity_translations_localize_database_content()
    {
        using var harness = new TestHarness();
        var (_, te) = await SeedLanguagesAsync(harness);
        var service = NewService(harness);
        await service.ImportSeedFilesAsync();

        var module = harness.Context.Set<MenuModule>().First();

        await service.SaveEntityRowsAsync(te, new SaveEntityTranslationsDto
        {
            Items = new()
            {
                new EntityTranslationRowDto
                {
                    EntityType = nameof(MenuModule),
                    EntityKey = module.Id.ToString(),
                    Field = nameof(MenuModule.Name),
                    Translation = "పరీక్ష"
                }
            }
        });

        var bundle = await service.GetBundleAsync("te");
        var key = EntityTranslation.BundleKey(nameof(MenuModule), module.Id.ToString(), nameof(MenuModule.Name));
        Assert.Equal("పరీక్ష", bundle.Entities[key]);
    }

    [Fact]
    public async Task Menu_names_are_auto_translated_from_matching_nav_labels()
    {
        using var harness = new TestHarness();
        await SeedLanguagesAsync(harness);

        // "Masters" exists both as a seeded menu module and as a nav.* label.
        var service = NewService(harness);
        await service.ImportSeedFilesAsync();

        var masters = harness.Context.Set<MenuModule>().FirstOrDefault(m => m.Name == "Masters");
        Assert.NotNull(masters);

        var bundle = await service.GetBundleAsync("te");
        var key = EntityTranslation.BundleKey(nameof(MenuModule), masters!.Id.ToString(), nameof(MenuModule.Name));

        Assert.True(bundle.Entities.ContainsKey(key),
            "The sidebar name should be translated automatically from the matching nav label.");
        Assert.Equal("మాస్టర్లు", bundle.Entities[key]);
    }

    [Fact]
    public async Task Export_returns_one_json_file_per_namespace()
    {
        using var harness = new TestHarness();
        var (_, te) = await SeedLanguagesAsync(harness);
        var service = NewService(harness);
        await service.ImportSeedFilesAsync();

        var files = await service.ExportJsonAsync(te);

        Assert.Contains("common/common.te.json", files.Keys);
        Assert.Contains("nav/nav.te.json", files.Keys);
        Assert.Contains("భద్రపరచు", files["common/common.te.json"]);
    }

    [Fact]
    public async Task Locales_exclude_languages_with_no_translations()
    {
        using var harness = new TestHarness();
        await SeedLanguagesAsync(harness);

        var empty = new Language { Id = Guid.NewGuid(), Name = "Sanskrit", Code = "sa", IsActive = true };
        harness.Context.Set<Language>().Add(empty);
        await harness.Context.SaveChangesAsync();

        var service = NewService(harness);
        await service.ImportSeedFilesAsync();

        var locales = await service.GetLocalesAsync();

        Assert.Contains(locales, l => l.Code == "en" && l.IsBase);
        Assert.Contains(locales, l => l.Code == "te");
        Assert.DoesNotContain(locales, l => l.Code == "sa"); // nothing to show, so not offered
    }
}
