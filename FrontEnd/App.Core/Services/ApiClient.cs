using System.Net;
using System.Net.Http.Json;
using App.Core.Config;
using App.Core.Models;

namespace App.Core.Services;

/// <summary>
/// The single HTTP surface between the apps and the API. Every route it calls comes from
/// <see cref="ApiRoutes"/>, so no URL is spelled out here.
/// </summary>
public class ApiClient : IApiClient
{
    private readonly HttpClient _http;
    private readonly AppConfig _config;

    public ApiClient(HttpClient http, AppConfig config)
    {
        _http = http;
        _config = config;
    }

    public async Task<(bool Success, string Message)> RegisterAsync(RegisterRequest request)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Auth.Register, request);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
            return (true, body?.Message ?? "Registration Successful");
        }
        return (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, AuthResponse? Data, string Error)> LoginAsync(LoginRequest request)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Auth.Login, request);
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<AuthResponse>();
            return (true, data, string.Empty);
        }
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return (false, null, "Invalid email/mobile or password.");
        return (false, null, await ExtractErrorAsync(response));
    }

    public async Task<DashboardModel?> GetDashboardAsync()
        => await _http.GetFromJsonAsync<DashboardModel>(ApiRoutes.Dashboard.Mine);

    public async Task<(bool Ok, AdminDashboardModel? Data, bool Forbidden, string Error)> GetAdminDashboardAsync()
    {
        var response = await _http.GetAsync(ApiRoutes.Dashboard.Admin);
        if (response.IsSuccessStatusCode)
            return (true, await response.Content.ReadFromJsonAsync<AdminDashboardModel>(), false, string.Empty);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return (false, null, true, "Administrator access required.");
        return (false, null, false, await ExtractErrorAsync(response));
    }

    public async Task<TodayBhakti?> GetTodayBhaktiAsync(Guid? regionId = null)
        => await _http.GetFromJsonAsync<TodayBhakti>(ApiRoutes.Dashboard.TodayBhakti(regionId));

    public async Task<PrayersResult?> GetPrayersAsync(Guid? regionId = null)
        => await _http.GetFromJsonAsync<PrayersResult>(ApiRoutes.Dashboard.Prayers(regionId));

    public async Task<List<MenuTreeNode>> GetMenuAsync()
        => await _http.GetFromJsonAsync<List<MenuTreeNode>>(ApiRoutes.MenuModules.Menu(_config.Platform)) ?? new();

    public async Task<List<MenuTreeNode>> GetModuleTreeAsync()
        => await _http.GetFromJsonAsync<List<MenuTreeNode>>(ApiRoutes.MenuModules.Tree) ?? new();

    public async Task<List<MenuModuleModel>> GetModulesAsync()
        => await _http.GetFromJsonAsync<List<MenuModuleModel>>(ApiRoutes.MenuModules.Root) ?? new();

    public async Task<MenuModuleModel?> GetModuleAsync(Guid id)
        => await _http.GetFromJsonAsync<MenuModuleModel>(ApiRoutes.MenuModules.ById(id));

    public async Task<(bool Success, string Error)> CreateModuleAsync(MenuModuleRequest request)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.MenuModules.Root, request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateModuleAsync(Guid id, MenuModuleRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.MenuModules.ById(id), request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetModuleStatusAsync(Guid id, bool isActive)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.MenuModules.Status(id), new { isActive });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Ok, List<RoleModel> Roles, bool Forbidden, string Error)> GetRolesAsync(string? search = null)
    {
        var response = await _http.GetAsync(ApiRoutes.Roles.Search(search));
        if (response.IsSuccessStatusCode)
            return (true, await response.Content.ReadFromJsonAsync<List<RoleModel>>() ?? new(), false, string.Empty);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return (false, new(), true, "Administrator access required.");
        return (false, new(), false, await ExtractErrorAsync(response));
    }

    public async Task<RoleModel?> GetRoleAsync(int roleId)
        => await _http.GetFromJsonAsync<RoleModel>(ApiRoutes.Roles.ById(roleId));

    public async Task<(bool Success, string Error)> CreateRoleAsync(RoleRequest request)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Roles.Root, request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateRoleAsync(int roleId, RoleRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Roles.ById(roleId), request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> DeleteRoleAsync(int roleId)
    {
        var response = await _http.DeleteAsync(ApiRoutes.Roles.ById(roleId));
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    // ---- Localization ----

    public async Task<List<LocaleModel>> GetLocalesAsync()
        => await _http.GetFromJsonAsync<List<LocaleModel>>(ApiRoutes.Localization.Locales) ?? new();

    public async Task<LocalizationBundle?> GetLocalizationBundleAsync(string code)
        => await _http.GetFromJsonAsync<LocalizationBundle>(ApiRoutes.Localization.Bundle(code));

    public async Task<LabelEditorModel?> GetLabelEditorAsync(Guid languageId)
        => await _http.GetFromJsonAsync<LabelEditorModel>(ApiRoutes.Localization.Labels(languageId));

    public async Task<(bool Success, string Error)> SaveLabelsAsync(Guid languageId, SaveLabelsRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Localization.Labels(languageId), request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<LanguageFormMatrixModel?> GetLanguageFormsAsync(Guid languageId)
        => await _http.GetFromJsonAsync<LanguageFormMatrixModel>(ApiRoutes.Localization.Forms(languageId));

    public async Task<(bool Success, string Error)> SaveLanguageFormsAsync(Guid languageId, SaveLanguageFormsRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Localization.Forms(languageId), request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<List<EntityTranslationRow>> GetEntityTranslationsAsync(Guid languageId)
        => await _http.GetFromJsonAsync<List<EntityTranslationRow>>(ApiRoutes.Localization.Entities(languageId)) ?? new();

    public async Task<(bool Success, string Error)> SaveEntityTranslationsAsync(Guid languageId, SaveEntityTranslationsRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Localization.Entities(languageId), request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<Dictionary<string, string>> ExportLocalizationAsync(Guid languageId)
        => await _http.GetFromJsonAsync<Dictionary<string, string>>(ApiRoutes.Localization.Export(languageId)) ?? new();

    public async Task<TranslationMatrix?> GetTranslationMatrixAsync(string? scope)
        => await _http.GetFromJsonAsync<TranslationMatrix>(ApiRoutes.Localization.MatrixScoped(scope));

    public async Task<(bool Success, string Error)> SaveTranslationMatrixAsync(SaveMatrixRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Localization.Matrix, request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<EntityMatrix?> GetEntityMatrixAsync()
        => await _http.GetFromJsonAsync<EntityMatrix>(ApiRoutes.Localization.EntityMatrix);

    public async Task<(bool Success, string Error)> SaveEntityMatrixAsync(SaveEntityMatrixRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Localization.EntityMatrix, request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<DictionaryPage?> GetDictionaryAsync(string? category, string? search, bool missingOnly, int page, int pageSize)
        => await _http.GetFromJsonAsync<DictionaryPage>(
               ApiRoutes.Localization.DictionaryPage(category, search, missingOnly, page, pageSize));

    public async Task<(bool Success, string Error)> SaveDictionaryAsync(SaveDictionaryRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Localization.Dictionary, request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<HarvestResult?> HarvestDictionaryAsync()
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Localization.HarvestDictionary, new { });
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<HarvestResult>() : null;
    }

    public async Task<List<AccessRoleModel>> GetAccessRolesAsync()
        => await _http.GetFromJsonAsync<List<AccessRoleModel>>(ApiRoutes.AccessRights.Roles) ?? new();

    public async Task<AccessMatrixModel?> GetAccessMatrixAsync(int roleId)
        => await _http.GetFromJsonAsync<AccessMatrixModel>(ApiRoutes.AccessRights.ForRole(roleId));

    public async Task<(bool Success, string Error)> SaveAccessRightsAsync(int roleId, SaveAccessRightsRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.AccessRights.ForRole(roleId), request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<List<RegionModel>> GetRegionsAsync()
        => await _http.GetFromJsonAsync<List<RegionModel>>(ApiRoutes.Regions.Root) ?? new();

    public async Task<List<RegionOption>> GetRegionOptionsAsync()
        => await _http.GetFromJsonAsync<List<RegionOption>>(ApiRoutes.Regions.Options) ?? new();

    public async Task<RegionModel?> GetRegionAsync(Guid id)
        => await _http.GetFromJsonAsync<RegionModel>(ApiRoutes.Regions.ById(id));

    public async Task<(bool Success, string Error)> CreateRegionAsync(RegionRequest request)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Regions.Root, request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateRegionAsync(Guid id, RegionRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Regions.ById(id), request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetRegionStatusAsync(Guid id, bool isActive)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Regions.Status(id), new { isActive });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<List<int>> GetFestivalYearsAsync()
        => await _http.GetFromJsonAsync<List<int>>(ApiRoutes.Festivals.Years) ?? new();

    public async Task<List<FestivalModel>> GetFestivalsAsync(int year)
        => await _http.GetFromJsonAsync<List<FestivalModel>>(ApiRoutes.Festivals.ForYear(year)) ?? new();

    public async Task<FestivalModel?> GetFestivalAsync(Guid id)
        => await _http.GetFromJsonAsync<FestivalModel>(ApiRoutes.Festivals.ById(id));

    public async Task<(bool Success, string Error)> CreateFestivalAsync(FestivalRequest request)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Festivals.Root, request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateFestivalAsync(Guid id, FestivalRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Festivals.ById(id), request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetFestivalStatusAsync(Guid id, bool isActive)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Festivals.Status(id), new { isActive });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<List<DeityModel>> GetDeitiesAsync()
        => await _http.GetFromJsonAsync<List<DeityModel>>(ApiRoutes.Deities.Root) ?? new();

    public async Task<DeityModel?> GetDeityAsync(Guid id)
        => await _http.GetFromJsonAsync<DeityModel>(ApiRoutes.Deities.ById(id));

    public async Task<DeityFormOptions> GetDeityFormOptionsAsync()
        => await _http.GetFromJsonAsync<DeityFormOptions>(ApiRoutes.Deities.FormOptions) ?? new();

    public async Task<(bool Success, string Error)> CreateDeityAsync(DeityRequest request)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Deities.Root, request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateDeityAsync(Guid id, DeityRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Deities.ById(id), request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetDeityStatusAsync(Guid id, bool isActive)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Deities.Status(id), new { isActive });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Ok, List<UserListItem> Users, bool Forbidden, string Error)> GetUsersAsync()
    {
        var response = await _http.GetAsync(ApiRoutes.Users.Root);
        if (response.IsSuccessStatusCode)
            return (true, await response.Content.ReadFromJsonAsync<List<UserListItem>>() ?? new(), false, string.Empty);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return (false, new(), true, "Administrator access required.");
        return (false, new(), false, await ExtractErrorAsync(response));
    }

    public async Task<UserProfile?> GetUserProfileAsync(Guid id)
        => await _http.GetFromJsonAsync<UserProfile>(ApiRoutes.Users.ById(id));

    public async Task<MyProfile?> GetMyProfileAsync()
        => await _http.GetFromJsonAsync<MyProfile>(ApiRoutes.Profile.Me);

    public async Task<(bool Success, string Error)> SetDefaultRegionAsync(Guid? regionId)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Profile.Region, new { regionId });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> ChangePasswordAsync(ChangePasswordRequest request)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Auth.ChangePassword, request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Json, string Error)> ExportMyDataAsync()
    {
        var response = await _http.GetAsync(ApiRoutes.Profile.Export);
        return response.IsSuccessStatusCode
            ? (true, await response.Content.ReadAsStringAsync(), string.Empty)
            : (false, string.Empty, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> DeleteMyAccountAsync(DeleteAccountRequest request)
    {
        // DELETE with a body: the password confirms an irreversible action, and it has no business
        // in a query string, where it would land in every access log along the way.
        var message = new HttpRequestMessage(HttpMethod.Delete, ApiRoutes.Profile.DeleteMe)
        {
            Content = JsonContent.Create(request)
        };

        var response = await _http.SendAsync(message);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<SadhanaToday?> GetSadhanaTodayAsync(Guid? regionId = null)
        => await _http.GetFromJsonAsync<SadhanaToday>(ApiRoutes.Sadhana.Today(regionId));

    public async Task<List<SadhanaChant>> GetSadhanaChantsAsync(string? search = null, Guid? regionId = null)
        => await _http.GetFromJsonAsync<List<SadhanaChant>>(ApiRoutes.Sadhana.Chants(search, regionId)) ?? new();

    public async Task<SadhanaChantDetail?> GetSadhanaChantAsync(Guid id)
        => await _http.GetFromJsonAsync<SadhanaChantDetail>(ApiRoutes.Sadhana.Chant(id));

    public async Task<LogCountResult?> LogSadhanaCountAsync(LogCountRequest request)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Sadhana.Log, request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<LogCountResult>() : null;
    }

    public async Task<SadhanaStreak?> GetSadhanaStreakAsync()
        => await _http.GetFromJsonAsync<SadhanaStreak>(ApiRoutes.Sadhana.Streak);

    public async Task<PanchangamOptions> GetPanchangamOptionsAsync()
        => await _http.GetFromJsonAsync<PanchangamOptions>(ApiRoutes.Panchangam.Options) ?? new();

    // The API answers with a page envelope now. The signature stays a plain list because the only
    // caller asks for one day, and ApiRoutes.Panchangam.List deliberately carries no page
    // parameters — the server's defaults are the right ones here.
    public async Task<List<PanchangamModel>> GetPanchangamsAsync(int? year = null, Guid? regionId = null, DateOnly? from = null, DateOnly? to = null, string? search = null)
        => (await _http.GetFromJsonAsync<PanchangamPage>(ApiRoutes.Panchangam.List(year, regionId, from, to, search)))?.Rows ?? new();

    public async Task<PanchangamModel?> GetPanchangamByDateAsync(DateOnly date, Guid regionId)
    {
        var response = await _http.GetAsync(ApiRoutes.Panchangam.ByDate(date, regionId));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PanchangamModel>()
            : null;
    }

    public async Task<PanchangamModel?> ComputePanchangamAsync(double lat, double lon, DateOnly? date = null, string? place = null)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Panchangam.Compute,
            new ComputePanchangamRequest { Latitude = lat, Longitude = lon, Date = date, Place = place });

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PanchangamModel>()
            : null;
    }

    public async Task<(bool Success, GenerateResult? Result, string Error)> GeneratePanchangamAsync(GeneratePanchangamRequest request)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Panchangam.Generate, request);
        if (response.IsSuccessStatusCode)
            return (true, await response.Content.ReadFromJsonAsync<GenerateResult>(), string.Empty);
        return (false, null, await ExtractErrorAsync(response));
    }

    public async Task<List<LanguageModel>> GetLanguagesAsync(Guid? regionId = null, string? search = null)
        => await _http.GetFromJsonAsync<List<LanguageModel>>(ApiRoutes.Languages.List(regionId, search)) ?? new();

    public async Task<LanguageModel?> GetLanguageAsync(Guid id)
        => await _http.GetFromJsonAsync<LanguageModel>(ApiRoutes.Languages.ById(id));

    public async Task<List<RegionLanguagesModel>> GetLanguagesByRegionAsync()
        => await _http.GetFromJsonAsync<List<RegionLanguagesModel>>(ApiRoutes.Languages.ByRegion) ?? new();

    public async Task<(bool Success, string Error)> CreateLanguageAsync(LanguageRequest request)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Languages.Root, request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateLanguageAsync(Guid id, LanguageRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Languages.ById(id), request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetLanguageStatusAsync(Guid id, bool isActive)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Languages.Status(id), new { isActive });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<List<ChantConfigListItem>> GetChantConfigsAsync(Guid? chantId = null, Guid? deityId = null, string? search = null)
        => await _http.GetFromJsonAsync<List<ChantConfigListItem>>(ApiRoutes.ChantConfigs.List(chantId, deityId, search)) ?? new();

    public async Task<ChantConfigModel?> GetChantConfigAsync(Guid id)
        => await _http.GetFromJsonAsync<ChantConfigModel>(ApiRoutes.ChantConfigs.ById(id));

    public async Task<ChantConfigFormOptions> GetChantConfigFormOptionsAsync()
        => await _http.GetFromJsonAsync<ChantConfigFormOptions>(ApiRoutes.ChantConfigs.FormOptions) ?? new();

    public async Task<(bool Success, string Error)> CreateChantConfigAsync(ChantConfigRequest request)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.ChantConfigs.Root, request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateChantConfigAsync(Guid id, ChantConfigRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.ChantConfigs.ById(id), request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetChantConfigStatusAsync(Guid id, bool isActive)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.ChantConfigs.Status(id), new { isActive });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> DeleteChantConfigAsync(Guid id)
    {
        var response = await _http.DeleteAsync(ApiRoutes.ChantConfigs.ById(id));
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Ok, NotificationConfigList? Data, bool Forbidden, string Error)> GetNotificationConfigAsync()
    {
        var response = await _http.GetAsync(ApiRoutes.Notifications.Config);
        if (response.IsSuccessStatusCode)
            return (true, await response.Content.ReadFromJsonAsync<NotificationConfigList>(), false, string.Empty);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return (false, null, true, "Administrator access required.");
        return (false, null, false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SaveNotificationConfigAsync(SaveNotificationConfigRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Notifications.Config, request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<MyNotificationSettings?> GetMyNotificationsAsync()
        => await _http.GetFromJsonAsync<MyNotificationSettings>(ApiRoutes.Notifications.Mine);

    public async Task<(bool Success, string Error)> SaveMyNotificationsAsync(SaveMyNotificationSettingsRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Notifications.Mine, request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Ok, List<IssueTypeModel> Items, bool Forbidden, string Error)> GetIssueTypesAsync()
    {
        var response = await _http.GetAsync(ApiRoutes.IssueTypes.Root);
        if (response.IsSuccessStatusCode)
            return (true, await response.Content.ReadFromJsonAsync<List<IssueTypeModel>>() ?? new(), false, string.Empty);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return (false, new(), true, "Administrator access required.");
        return (false, new(), false, await ExtractErrorAsync(response));
    }

    public async Task<List<IssueTypeModel>> GetActiveIssueTypesAsync()
        => await _http.GetFromJsonAsync<List<IssueTypeModel>>(ApiRoutes.IssueTypes.Active) ?? new();

    public async Task<(bool Success, string Error)> CreateIssueTypeAsync(IssueTypeRequest request)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.IssueTypes.Root, request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateIssueTypeAsync(Guid id, IssueTypeRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.IssueTypes.ById(id), request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetIssueTypeStatusAsync(Guid id, bool isActive)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.IssueTypes.Status(id), new { isActive });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<Favorites> GetFavoritesAsync()
        => await _http.GetFromJsonAsync<Favorites>(ApiRoutes.Favorites.Root) ?? new();

    public async Task<FavoriteIds> GetFavoriteIdsAsync()
        => await _http.GetFromJsonAsync<FavoriteIds>(ApiRoutes.Favorites.Ids) ?? new();

    public async Task<(bool Ok, bool IsFavorite, string Error)> ToggleFavoriteAsync(string type, Guid itemId)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Favorites.Toggle, new { type, itemId });
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ToggleFavoriteResult>();
            return (true, result?.IsFavorite ?? false, string.Empty);
        }
        return (false, false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SubmitFeedbackAsync(SubmitFeedbackRequest request)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Feedback.Root, request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Ok, List<FeedbackItem> Items, bool Forbidden, string Error)> GetFeedbacksAsync()
    {
        var response = await _http.GetAsync(ApiRoutes.Feedback.Root);
        if (response.IsSuccessStatusCode)
            return (true, await response.Content.ReadFromJsonAsync<List<FeedbackItem>>() ?? new(), false, string.Empty);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return (false, new(), true, "Administrator access required.");
        return (false, new(), false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Ok, FeedbackDashboardModel? Data, bool Forbidden, string Error)> GetFeedbackDashboardAsync()
    {
        var response = await _http.GetAsync(ApiRoutes.Feedback.Dashboard);
        if (response.IsSuccessStatusCode)
            return (true, await response.Content.ReadFromJsonAsync<FeedbackDashboardModel>(), false, string.Empty);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return (false, null, true, "Administrator access required.");
        return (false, null, false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetFeedbackStatusAsync(Guid id, string status)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Feedback.Status(id), new { status });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    // ---- Puja process ----

    public async Task<PujaProcessConfigModel?> GetPujaProcessConfigAsync(Guid pujaId)
        => await _http.GetFromJsonAsync<PujaProcessConfigModel>(ApiRoutes.PujaProcess.Config(pujaId));

    public async Task<(bool Success, string Error)> SavePujaProcessConfigAsync(Guid pujaId, SavePujaProcessRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.PujaProcess.Config(pujaId), request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<List<ProcessFestivalModel>> GetProcessFestivalsAsync()
        => await _http.GetFromJsonAsync<List<ProcessFestivalModel>>(ApiRoutes.PujaProcess.Festivals) ?? new();

    public async Task<List<ProcessPujaSummaryModel>> GetProcessPujasAsync(Guid? festivalId = null)
        => await _http.GetFromJsonAsync<List<ProcessPujaSummaryModel>>(ApiRoutes.PujaProcess.Pujas(festivalId)) ?? new();

    public async Task<PujaProcessViewModel?> GetPujaProcessAsync(Guid pujaId)
        => await _http.GetFromJsonAsync<PujaProcessViewModel>(ApiRoutes.PujaProcess.Puja(pujaId));

    // ---- Pujas ----

    public async Task<List<PujaModel>> GetPujasAsync()
        => await _http.GetFromJsonAsync<List<PujaModel>>(ApiRoutes.Pujas.Root) ?? new();

    public async Task<PujaModel?> GetPujaAsync(Guid id)
        => await _http.GetFromJsonAsync<PujaModel>(ApiRoutes.Pujas.ById(id));

    public async Task<PujaFormOptions> GetPujaFormOptionsAsync()
        => await _http.GetFromJsonAsync<PujaFormOptions>(ApiRoutes.Pujas.FormOptions) ?? new();

    public async Task<(bool Success, string Error)> CreatePujaAsync(PujaRequest request)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Pujas.Root, request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdatePujaAsync(Guid id, PujaRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Pujas.ById(id), request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetPujaStatusAsync(Guid id, bool isActive)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Pujas.Status(id), new { isActive });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    // ---- Wallpapers ----

    public async Task<List<WallpaperDeityModel>> GetWallpaperDeitiesAsync(bool onlyWithWallpapers = false)
        => await _http.GetFromJsonAsync<List<WallpaperDeityModel>>(ApiRoutes.Wallpapers.Deities(onlyWithWallpapers)) ?? new();

    public async Task<List<WallpaperModel>> GetWallpapersByDeityAsync(Guid deityId, bool activeOnly = true)
        => await _http.GetFromJsonAsync<List<WallpaperModel>>(ApiRoutes.Wallpapers.ForDeity(deityId, activeOnly)) ?? new();

    public async Task<(bool Success, WallpaperUploadResult? Result, string Error)> UploadWallpapersAsync(CreateWallpapersRequest request)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Wallpapers.Root, request);
        if (!response.IsSuccessStatusCode) return (false, null, await ExtractErrorAsync(response));
        return (true, await response.Content.ReadFromJsonAsync<WallpaperUploadResult>(), string.Empty);
    }

    public async Task<(bool Success, string Error)> UpdateWallpaperAsync(Guid id, UpdateWallpaperRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Wallpapers.ById(id), request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> DeleteWallpaperAsync(Guid id)
    {
        var response = await _http.DeleteAsync(ApiRoutes.Wallpapers.ById(id));
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<List<ChantModel>> GetChantsAsync()
        => await _http.GetFromJsonAsync<List<ChantModel>>(ApiRoutes.Chants.Root) ?? new();

    public async Task<ChantModel?> GetChantAsync(Guid id)
        => await _http.GetFromJsonAsync<ChantModel>(ApiRoutes.Chants.ById(id));

    public async Task<(bool Success, string Error)> CreateChantAsync(ChantRequest request)
    {
        var response = await _http.PostAsJsonAsync(ApiRoutes.Chants.Root, request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateChantAsync(Guid id, ChantRequest request)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Chants.ById(id), request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetChantStatusAsync(Guid id, bool isActive)
    {
        var response = await _http.PutAsJsonAsync(ApiRoutes.Chants.Status(id), new { isActive });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    private static async Task<string> ExtractErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            if (!string.IsNullOrWhiteSpace(error?.Message))
                return error!.Message!;
        }
        catch
        {
            // ignore non-JSON error bodies
        }
        return $"Request failed ({(int)response.StatusCode}).";
    }

    private sealed class MessageResponse
    {
        public string? Message { get; set; }
    }

    private sealed class ErrorResponse
    {
        public string? Message { get; set; }
    }
}
