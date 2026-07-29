using System.Net;
using System.Net.Http.Json;
using App.Core.Config;
using App.Core.Models;

namespace App.Core.Services;

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
        var response = await _http.PostAsJsonAsync("auth/register", request);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
            return (true, body?.Message ?? "Registration Successful");
        }
        return (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, AuthResponse? Data, string Error)> LoginAsync(LoginRequest request)
    {
        var response = await _http.PostAsJsonAsync("auth/login", request);
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
        => await _http.GetFromJsonAsync<DashboardModel>("dashboard");

    public async Task<(bool Ok, AdminDashboardModel? Data, bool Forbidden, string Error)> GetAdminDashboardAsync()
    {
        var response = await _http.GetAsync("dashboard/admin");
        if (response.IsSuccessStatusCode)
            return (true, await response.Content.ReadFromJsonAsync<AdminDashboardModel>(), false, string.Empty);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return (false, null, true, "Administrator access required.");
        return (false, null, false, await ExtractErrorAsync(response));
    }

    public async Task<TodayBhakti?> GetTodayBhaktiAsync(Guid? regionId = null)
        => await _http.GetFromJsonAsync<TodayBhakti>(
            regionId is null ? "dashboard/today-bhakti" : $"dashboard/today-bhakti?regionId={regionId}");

    public async Task<PrayersResult?> GetPrayersAsync(Guid? regionId = null)
        => await _http.GetFromJsonAsync<PrayersResult>(
            regionId is null ? "dashboard/prayers" : $"dashboard/prayers?regionId={regionId}");

    public async Task<List<MenuTreeNode>> GetMenuAsync()
        => await _http.GetFromJsonAsync<List<MenuTreeNode>>($"menumodules/menu?platform={Uri.EscapeDataString(_config.Platform)}") ?? new();

    public async Task<List<MenuTreeNode>> GetModuleTreeAsync()
        => await _http.GetFromJsonAsync<List<MenuTreeNode>>("menumodules/tree") ?? new();

    public async Task<List<MenuModuleModel>> GetModulesAsync()
        => await _http.GetFromJsonAsync<List<MenuModuleModel>>("menumodules") ?? new();

    public async Task<MenuModuleModel?> GetModuleAsync(Guid id)
        => await _http.GetFromJsonAsync<MenuModuleModel>($"menumodules/{id}");

    public async Task<(bool Success, string Error)> CreateModuleAsync(MenuModuleRequest request)
    {
        var response = await _http.PostAsJsonAsync("menumodules", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateModuleAsync(Guid id, MenuModuleRequest request)
    {
        var response = await _http.PutAsJsonAsync($"menumodules/{id}", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetModuleStatusAsync(Guid id, bool isActive)
    {
        var response = await _http.PutAsJsonAsync($"menumodules/{id}/status", new { isActive });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Ok, List<RoleModel> Roles, bool Forbidden, string Error)> GetRolesAsync(string? search = null)
    {
        var url = string.IsNullOrWhiteSpace(search) ? "roles" : $"roles?search={Uri.EscapeDataString(search.Trim())}";
        var response = await _http.GetAsync(url);
        if (response.IsSuccessStatusCode)
            return (true, await response.Content.ReadFromJsonAsync<List<RoleModel>>() ?? new(), false, string.Empty);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return (false, new(), true, "Administrator access required.");
        return (false, new(), false, await ExtractErrorAsync(response));
    }

    public async Task<RoleModel?> GetRoleAsync(int roleId)
        => await _http.GetFromJsonAsync<RoleModel>($"roles/{roleId}");

    public async Task<(bool Success, string Error)> CreateRoleAsync(RoleRequest request)
    {
        var response = await _http.PostAsJsonAsync("roles", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateRoleAsync(int roleId, RoleRequest request)
    {
        var response = await _http.PutAsJsonAsync($"roles/{roleId}", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> DeleteRoleAsync(int roleId)
    {
        var response = await _http.DeleteAsync($"roles/{roleId}");
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<List<AccessRoleModel>> GetAccessRolesAsync()
        => await _http.GetFromJsonAsync<List<AccessRoleModel>>("accessrights/roles") ?? new();

    public async Task<AccessMatrixModel?> GetAccessMatrixAsync(int roleId)
        => await _http.GetFromJsonAsync<AccessMatrixModel>($"accessrights/{roleId}");

    public async Task<(bool Success, string Error)> SaveAccessRightsAsync(int roleId, SaveAccessRightsRequest request)
    {
        var response = await _http.PutAsJsonAsync($"accessrights/{roleId}", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<List<RegionModel>> GetRegionsAsync()
        => await _http.GetFromJsonAsync<List<RegionModel>>("regions") ?? new();

    public async Task<List<RegionOption>> GetRegionOptionsAsync()
        => await _http.GetFromJsonAsync<List<RegionOption>>("regions/options") ?? new();

    public async Task<RegionModel?> GetRegionAsync(Guid id)
        => await _http.GetFromJsonAsync<RegionModel>($"regions/{id}");

    public async Task<(bool Success, string Error)> CreateRegionAsync(RegionRequest request)
    {
        var response = await _http.PostAsJsonAsync("regions", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateRegionAsync(Guid id, RegionRequest request)
    {
        var response = await _http.PutAsJsonAsync($"regions/{id}", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetRegionStatusAsync(Guid id, bool isActive)
    {
        var response = await _http.PutAsJsonAsync($"regions/{id}/status", new { isActive });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<List<int>> GetFestivalYearsAsync()
        => await _http.GetFromJsonAsync<List<int>>("festivals/years") ?? new();

    public async Task<List<FestivalModel>> GetFestivalsAsync(int year)
        => await _http.GetFromJsonAsync<List<FestivalModel>>($"festivals?year={year}") ?? new();

    public async Task<FestivalModel?> GetFestivalAsync(Guid id)
        => await _http.GetFromJsonAsync<FestivalModel>($"festivals/{id}");

    public async Task<(bool Success, string Error)> CreateFestivalAsync(FestivalRequest request)
    {
        var response = await _http.PostAsJsonAsync("festivals", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateFestivalAsync(Guid id, FestivalRequest request)
    {
        var response = await _http.PutAsJsonAsync($"festivals/{id}", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetFestivalStatusAsync(Guid id, bool isActive)
    {
        var response = await _http.PutAsJsonAsync($"festivals/{id}/status", new { isActive });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<List<DeityModel>> GetDeitiesAsync()
        => await _http.GetFromJsonAsync<List<DeityModel>>("deities") ?? new();

    public async Task<DeityModel?> GetDeityAsync(Guid id)
        => await _http.GetFromJsonAsync<DeityModel>($"deities/{id}");

    public async Task<DeityFormOptions> GetDeityFormOptionsAsync()
        => await _http.GetFromJsonAsync<DeityFormOptions>("deities/form-options") ?? new();

    public async Task<(bool Success, string Error)> CreateDeityAsync(DeityRequest request)
    {
        var response = await _http.PostAsJsonAsync("deities", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateDeityAsync(Guid id, DeityRequest request)
    {
        var response = await _http.PutAsJsonAsync($"deities/{id}", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetDeityStatusAsync(Guid id, bool isActive)
    {
        var response = await _http.PutAsJsonAsync($"deities/{id}/status", new { isActive });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Ok, List<UserListItem> Users, bool Forbidden, string Error)> GetUsersAsync()
    {
        var response = await _http.GetAsync("users");
        if (response.IsSuccessStatusCode)
            return (true, await response.Content.ReadFromJsonAsync<List<UserListItem>>() ?? new(), false, string.Empty);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return (false, new(), true, "Administrator access required.");
        return (false, new(), false, await ExtractErrorAsync(response));
    }

    public async Task<UserProfile?> GetUserProfileAsync(Guid id)
        => await _http.GetFromJsonAsync<UserProfile>($"users/{id}");

    public async Task<MyProfile?> GetMyProfileAsync()
        => await _http.GetFromJsonAsync<MyProfile>("profile/me");

    public async Task<(bool Success, string Error)> SetDefaultRegionAsync(Guid? regionId)
    {
        var response = await _http.PutAsJsonAsync("profile/region", new { regionId });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<SadhanaToday?> GetSadhanaTodayAsync(Guid? regionId = null)
    {
        var url = regionId is null ? "sadhana/today" : $"sadhana/today?regionId={regionId}";
        return await _http.GetFromJsonAsync<SadhanaToday>(url);
    }

    public async Task<List<SadhanaChant>> GetSadhanaChantsAsync(string? search = null, Guid? regionId = null)
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) q.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (regionId is not null) q.Add($"regionId={regionId}");
        var url = q.Count == 0 ? "sadhana/chants" : $"sadhana/chants?{string.Join("&", q)}";
        return await _http.GetFromJsonAsync<List<SadhanaChant>>(url) ?? new();
    }

    public async Task<SadhanaChantDetail?> GetSadhanaChantAsync(Guid id)
        => await _http.GetFromJsonAsync<SadhanaChantDetail>($"sadhana/chants/{id}");

    public async Task<LogCountResult?> LogSadhanaCountAsync(LogCountRequest request)
    {
        var response = await _http.PostAsJsonAsync("sadhana/log", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<LogCountResult>() : null;
    }

    public async Task<SadhanaStreak?> GetSadhanaStreakAsync()
        => await _http.GetFromJsonAsync<SadhanaStreak>("sadhana/streak");

    public async Task<PanchangamOptions> GetPanchangamOptionsAsync()
        => await _http.GetFromJsonAsync<PanchangamOptions>("panchangam/options") ?? new();

    public async Task<List<PanchangamModel>> GetPanchangamsAsync(int? year = null, Guid? regionId = null, DateOnly? from = null, DateOnly? to = null, string? search = null)
    {
        var q = new List<string>();
        if (year is not null) q.Add($"year={year}");
        if (regionId is not null) q.Add($"regionId={regionId}");
        if (from is not null) q.Add($"from={from:yyyy-MM-dd}");
        if (to is not null) q.Add($"to={to:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(search)) q.Add($"search={Uri.EscapeDataString(search.Trim())}");
        var url = q.Count == 0 ? "panchangam" : $"panchangam?{string.Join("&", q)}";
        return await _http.GetFromJsonAsync<List<PanchangamModel>>(url) ?? new();
    }

    public async Task<PanchangamModel?> GetPanchangamByDateAsync(DateOnly date, Guid regionId)
    {
        var response = await _http.GetAsync($"panchangam/by-date?date={date:yyyy-MM-dd}&regionId={regionId}");
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PanchangamModel>()
            : null;
    }

    public async Task<PanchangamModel?> ComputePanchangamAsync(double lat, double lon, DateOnly? date = null, string? place = null)
    {
        var q = new List<string> { $"lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}", $"lon={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}" };
        if (date is not null) q.Add($"date={date:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(place)) q.Add($"place={Uri.EscapeDataString(place.Trim())}");
        return await _http.GetFromJsonAsync<PanchangamModel>($"panchangam/compute?{string.Join("&", q)}");
    }

    public async Task<(bool Success, GenerateResult? Result, string Error)> GeneratePanchangamAsync(GeneratePanchangamRequest request)
    {
        var response = await _http.PostAsJsonAsync("panchangam/generate", request);
        if (response.IsSuccessStatusCode)
            return (true, await response.Content.ReadFromJsonAsync<GenerateResult>(), string.Empty);
        return (false, null, await ExtractErrorAsync(response));
    }

    public async Task<List<LanguageModel>> GetLanguagesAsync(Guid? regionId = null, string? search = null)
    {
        var query = new List<string>();
        if (regionId is not null) query.Add($"regionId={regionId}");
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        var url = query.Count == 0 ? "languages" : $"languages?{string.Join("&", query)}";
        return await _http.GetFromJsonAsync<List<LanguageModel>>(url) ?? new();
    }

    public async Task<LanguageModel?> GetLanguageAsync(Guid id)
        => await _http.GetFromJsonAsync<LanguageModel>($"languages/{id}");

    public async Task<List<RegionLanguagesModel>> GetLanguagesByRegionAsync()
        => await _http.GetFromJsonAsync<List<RegionLanguagesModel>>("languages/by-region") ?? new();

    public async Task<(bool Success, string Error)> CreateLanguageAsync(LanguageRequest request)
    {
        var response = await _http.PostAsJsonAsync("languages", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateLanguageAsync(Guid id, LanguageRequest request)
    {
        var response = await _http.PutAsJsonAsync($"languages/{id}", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetLanguageStatusAsync(Guid id, bool isActive)
    {
        var response = await _http.PutAsJsonAsync($"languages/{id}/status", new { isActive });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<List<ChantConfigListItem>> GetChantConfigsAsync(Guid? chantId = null, Guid? deityId = null, string? search = null)
    {
        var query = new List<string>();
        if (chantId is not null) query.Add($"chantId={chantId}");
        if (deityId is not null) query.Add($"deityId={deityId}");
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        var url = query.Count == 0 ? "chantconfigs" : $"chantconfigs?{string.Join("&", query)}";
        return await _http.GetFromJsonAsync<List<ChantConfigListItem>>(url) ?? new();
    }

    public async Task<ChantConfigModel?> GetChantConfigAsync(Guid id)
        => await _http.GetFromJsonAsync<ChantConfigModel>($"chantconfigs/{id}");

    public async Task<ChantConfigFormOptions> GetChantConfigFormOptionsAsync()
        => await _http.GetFromJsonAsync<ChantConfigFormOptions>("chantconfigs/form-options") ?? new();

    public async Task<(bool Success, string Error)> CreateChantConfigAsync(ChantConfigRequest request)
    {
        var response = await _http.PostAsJsonAsync("chantconfigs", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateChantConfigAsync(Guid id, ChantConfigRequest request)
    {
        var response = await _http.PutAsJsonAsync($"chantconfigs/{id}", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetChantConfigStatusAsync(Guid id, bool isActive)
    {
        var response = await _http.PutAsJsonAsync($"chantconfigs/{id}/status", new { isActive });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> DeleteChantConfigAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"chantconfigs/{id}");
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Ok, NotificationConfigList? Data, bool Forbidden, string Error)> GetNotificationConfigAsync()
    {
        var response = await _http.GetAsync("notificationconfig");
        if (response.IsSuccessStatusCode)
            return (true, await response.Content.ReadFromJsonAsync<NotificationConfigList>(), false, string.Empty);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return (false, null, true, "Administrator access required.");
        return (false, null, false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SaveNotificationConfigAsync(SaveNotificationConfigRequest request)
    {
        var response = await _http.PutAsJsonAsync("notificationconfig", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<MyNotificationSettings?> GetMyNotificationsAsync()
        => await _http.GetFromJsonAsync<MyNotificationSettings>("notifications/me");

    public async Task<(bool Success, string Error)> SaveMyNotificationsAsync(SaveMyNotificationSettingsRequest request)
    {
        var response = await _http.PutAsJsonAsync("notifications/me", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Ok, List<IssueTypeModel> Items, bool Forbidden, string Error)> GetIssueTypesAsync()
    {
        var response = await _http.GetAsync("issuetypes");
        if (response.IsSuccessStatusCode)
            return (true, await response.Content.ReadFromJsonAsync<List<IssueTypeModel>>() ?? new(), false, string.Empty);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return (false, new(), true, "Administrator access required.");
        return (false, new(), false, await ExtractErrorAsync(response));
    }

    public async Task<List<IssueTypeModel>> GetActiveIssueTypesAsync()
        => await _http.GetFromJsonAsync<List<IssueTypeModel>>("issuetypes/active") ?? new();

    public async Task<(bool Success, string Error)> CreateIssueTypeAsync(IssueTypeRequest request)
    {
        var response = await _http.PostAsJsonAsync("issuetypes", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateIssueTypeAsync(Guid id, IssueTypeRequest request)
    {
        var response = await _http.PutAsJsonAsync($"issuetypes/{id}", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetIssueTypeStatusAsync(Guid id, bool isActive)
    {
        var response = await _http.PutAsJsonAsync($"issuetypes/{id}/status", new { isActive });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<Favorites> GetFavoritesAsync()
        => await _http.GetFromJsonAsync<Favorites>("favorites") ?? new();

    public async Task<FavoriteIds> GetFavoriteIdsAsync()
        => await _http.GetFromJsonAsync<FavoriteIds>("favorites/ids") ?? new();

    public async Task<(bool Ok, bool IsFavorite, string Error)> ToggleFavoriteAsync(string type, Guid itemId)
    {
        var response = await _http.PostAsJsonAsync("favorites/toggle", new { type, itemId });
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ToggleFavoriteResult>();
            return (true, result?.IsFavorite ?? false, string.Empty);
        }
        return (false, false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SubmitFeedbackAsync(SubmitFeedbackRequest request)
    {
        var response = await _http.PostAsJsonAsync("feedback", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Ok, List<FeedbackItem> Items, bool Forbidden, string Error)> GetFeedbacksAsync()
    {
        var response = await _http.GetAsync("feedback");
        if (response.IsSuccessStatusCode)
            return (true, await response.Content.ReadFromJsonAsync<List<FeedbackItem>>() ?? new(), false, string.Empty);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return (false, new(), true, "Administrator access required.");
        return (false, new(), false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Ok, FeedbackDashboardModel? Data, bool Forbidden, string Error)> GetFeedbackDashboardAsync()
    {
        var response = await _http.GetAsync("feedback/dashboard");
        if (response.IsSuccessStatusCode)
            return (true, await response.Content.ReadFromJsonAsync<FeedbackDashboardModel>(), false, string.Empty);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return (false, null, true, "Administrator access required.");
        return (false, null, false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetFeedbackStatusAsync(Guid id, string status)
    {
        var response = await _http.PutAsJsonAsync($"feedback/{id}/status", new { status });
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<List<ChantModel>> GetChantsAsync()
        => await _http.GetFromJsonAsync<List<ChantModel>>("chants") ?? new();

    public async Task<ChantModel?> GetChantAsync(Guid id)
        => await _http.GetFromJsonAsync<ChantModel>($"chants/{id}");

    public async Task<(bool Success, string Error)> CreateChantAsync(ChantRequest request)
    {
        var response = await _http.PostAsJsonAsync("chants", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> UpdateChantAsync(Guid id, ChantRequest request)
    {
        var response = await _http.PutAsJsonAsync($"chants/{id}", request);
        return response.IsSuccessStatusCode ? (true, string.Empty) : (false, await ExtractErrorAsync(response));
    }

    public async Task<(bool Success, string Error)> SetChantStatusAsync(Guid id, bool isActive)
    {
        var response = await _http.PutAsJsonAsync($"chants/{id}/status", new { isActive });
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
