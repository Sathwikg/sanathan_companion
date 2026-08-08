using App.Core.Models;

namespace App.Core.Services;

public interface IApiClient
{
    Task<(bool Success, string Message)> RegisterAsync(RegisterRequest request);
    Task<(bool Success, AuthResponse? Data, string Error)> LoginAsync(LoginRequest request);
    Task<DashboardModel?> GetDashboardAsync();
    Task<(bool Ok, AdminDashboardModel? Data, bool Forbidden, string Error)> GetAdminDashboardAsync();
    Task<TodayBhakti?> GetTodayBhaktiAsync(Guid? regionId = null);
    Task<PrayersResult?> GetPrayersAsync(Guid? regionId = null);

    // Menu modules
    Task<List<MenuTreeNode>> GetMenuAsync();
    Task<List<MenuTreeNode>> GetModuleTreeAsync();
    Task<List<MenuModuleModel>> GetModulesAsync();
    Task<MenuModuleModel?> GetModuleAsync(Guid id);
    Task<(bool Success, string Error)> CreateModuleAsync(MenuModuleRequest request);
    Task<(bool Success, string Error)> UpdateModuleAsync(Guid id, MenuModuleRequest request);
    Task<(bool Success, string Error)> SetModuleStatusAsync(Guid id, bool isActive);

    // Roles (Role master)
    Task<(bool Ok, List<RoleModel> Roles, bool Forbidden, string Error)> GetRolesAsync(string? search = null);
    Task<RoleModel?> GetRoleAsync(int roleId);
    Task<(bool Success, string Error)> CreateRoleAsync(RoleRequest request);
    Task<(bool Success, string Error)> UpdateRoleAsync(int roleId, RoleRequest request);
    Task<(bool Success, string Error)> DeleteRoleAsync(int roleId);

    // Localization
    Task<List<LocaleModel>> GetLocalesAsync();
    Task<LocalizationBundle?> GetLocalizationBundleAsync(string code);
    Task<LabelEditorModel?> GetLabelEditorAsync(Guid languageId);
    Task<(bool Success, string Error)> SaveLabelsAsync(Guid languageId, SaveLabelsRequest request);
    Task<LanguageFormMatrixModel?> GetLanguageFormsAsync(Guid languageId);
    Task<(bool Success, string Error)> SaveLanguageFormsAsync(Guid languageId, SaveLanguageFormsRequest request);
    Task<List<EntityTranslationRow>> GetEntityTranslationsAsync(Guid languageId);
    Task<(bool Success, string Error)> SaveEntityTranslationsAsync(Guid languageId, SaveEntityTranslationsRequest request);
    Task<Dictionary<string, string>> ExportLocalizationAsync(Guid languageId);
    Task<TranslationMatrix?> GetTranslationMatrixAsync(string? scope);
    Task<(bool Success, string Error)> SaveTranslationMatrixAsync(SaveMatrixRequest request);
    Task<EntityMatrix?> GetEntityMatrixAsync();
    Task<(bool Success, string Error)> SaveEntityMatrixAsync(SaveEntityMatrixRequest request);
    Task<DictionaryPage?> GetDictionaryAsync(string? category, string? search, bool missingOnly, int page, int pageSize);
    Task<(bool Success, string Error)> SaveDictionaryAsync(SaveDictionaryRequest request);
    Task<HarvestResult?> HarvestDictionaryAsync();

    // Access rights (role × form × web/mobile)
    Task<List<AccessRoleModel>> GetAccessRolesAsync();
    Task<AccessMatrixModel?> GetAccessMatrixAsync(int roleId);
    Task<(bool Success, string Error)> SaveAccessRightsAsync(int roleId, SaveAccessRightsRequest request);

    // Regions
    Task<List<RegionModel>> GetRegionsAsync();

    /// <summary>Active regions (id + name). Works anonymously — used by the registration form.</summary>
    Task<List<RegionOption>> GetRegionOptionsAsync();
    Task<RegionModel?> GetRegionAsync(Guid id);
    Task<(bool Success, string Error)> CreateRegionAsync(RegionRequest request);
    Task<(bool Success, string Error)> UpdateRegionAsync(Guid id, RegionRequest request);
    Task<(bool Success, string Error)> SetRegionStatusAsync(Guid id, bool isActive);

    // Festivals
    Task<List<int>> GetFestivalYearsAsync();
    Task<List<FestivalModel>> GetFestivalsAsync(int year);
    Task<FestivalModel?> GetFestivalAsync(Guid id);
    Task<(bool Success, string Error)> CreateFestivalAsync(FestivalRequest request);
    Task<(bool Success, string Error)> UpdateFestivalAsync(Guid id, FestivalRequest request);
    Task<(bool Success, string Error)> SetFestivalStatusAsync(Guid id, bool isActive);

    // Deities
    Task<List<DeityModel>> GetDeitiesAsync();
    Task<DeityModel?> GetDeityAsync(Guid id);
    Task<DeityFormOptions> GetDeityFormOptionsAsync();
    Task<(bool Success, string Error)> CreateDeityAsync(DeityRequest request);
    Task<(bool Success, string Error)> UpdateDeityAsync(Guid id, DeityRequest request);
    Task<(bool Success, string Error)> SetDeityStatusAsync(Guid id, bool isActive);

    // Users (admin)
    Task<(bool Ok, List<UserListItem> Users, bool Forbidden, string Error)> GetUsersAsync();
    Task<UserProfile?> GetUserProfileAsync(Guid id);

    // Signed-in user's own profile
    Task<MyProfile?> GetMyProfileAsync();
    Task<(bool Success, string Error)> SetDefaultRegionAsync(Guid? regionId);

    // Sadhana
    Task<SadhanaToday?> GetSadhanaTodayAsync(Guid? regionId = null);
    Task<List<SadhanaChant>> GetSadhanaChantsAsync(string? search = null, Guid? regionId = null);
    Task<SadhanaChantDetail?> GetSadhanaChantAsync(Guid id);
    Task<LogCountResult?> LogSadhanaCountAsync(LogCountRequest request);
    Task<SadhanaStreak?> GetSadhanaStreakAsync();

    // Panchangam
    Task<PanchangamOptions> GetPanchangamOptionsAsync();
    Task<List<PanchangamModel>> GetPanchangamsAsync(int? year = null, Guid? regionId = null, DateOnly? from = null, DateOnly? to = null, string? search = null);
    Task<PanchangamModel?> GetPanchangamByDateAsync(DateOnly date, Guid regionId);
    Task<PanchangamModel?> ComputePanchangamAsync(double lat, double lon, DateOnly? date = null, string? place = null);
    Task<(bool Success, GenerateResult? Result, string Error)> GeneratePanchangamAsync(GeneratePanchangamRequest request);

    // Languages
    Task<List<LanguageModel>> GetLanguagesAsync(Guid? regionId = null, string? search = null);
    Task<LanguageModel?> GetLanguageAsync(Guid id);
    Task<List<RegionLanguagesModel>> GetLanguagesByRegionAsync();
    Task<(bool Success, string Error)> CreateLanguageAsync(LanguageRequest request);
    Task<(bool Success, string Error)> UpdateLanguageAsync(Guid id, LanguageRequest request);
    Task<(bool Success, string Error)> SetLanguageStatusAsync(Guid id, bool isActive);

    // Chant configs
    Task<List<ChantConfigListItem>> GetChantConfigsAsync(Guid? chantId = null, Guid? deityId = null, string? search = null);
    Task<ChantConfigModel?> GetChantConfigAsync(Guid id);
    Task<ChantConfigFormOptions> GetChantConfigFormOptionsAsync();
    Task<(bool Success, string Error)> CreateChantConfigAsync(ChantConfigRequest request);
    Task<(bool Success, string Error)> UpdateChantConfigAsync(Guid id, ChantConfigRequest request);
    Task<(bool Success, string Error)> SetChantConfigStatusAsync(Guid id, bool isActive);
    Task<(bool Success, string Error)> DeleteChantConfigAsync(Guid id);

    // Notifications
    Task<(bool Ok, NotificationConfigList? Data, bool Forbidden, string Error)> GetNotificationConfigAsync();
    Task<(bool Success, string Error)> SaveNotificationConfigAsync(SaveNotificationConfigRequest request);
    Task<MyNotificationSettings?> GetMyNotificationsAsync();
    Task<(bool Success, string Error)> SaveMyNotificationsAsync(SaveMyNotificationSettingsRequest request);

    // Issue types (feedback master)
    Task<(bool Ok, List<IssueTypeModel> Items, bool Forbidden, string Error)> GetIssueTypesAsync();
    Task<List<IssueTypeModel>> GetActiveIssueTypesAsync();
    Task<(bool Success, string Error)> CreateIssueTypeAsync(IssueTypeRequest request);
    Task<(bool Success, string Error)> UpdateIssueTypeAsync(Guid id, IssueTypeRequest request);
    Task<(bool Success, string Error)> SetIssueTypeStatusAsync(Guid id, bool isActive);

    // Favorites
    Task<Favorites> GetFavoritesAsync();
    Task<FavoriteIds> GetFavoriteIdsAsync();
    Task<(bool Ok, bool IsFavorite, string Error)> ToggleFavoriteAsync(string type, Guid itemId);

    // Feedback
    Task<(bool Success, string Error)> SubmitFeedbackAsync(SubmitFeedbackRequest request);
    Task<(bool Ok, List<FeedbackItem> Items, bool Forbidden, string Error)> GetFeedbacksAsync();
    Task<(bool Ok, FeedbackDashboardModel? Data, bool Forbidden, string Error)> GetFeedbackDashboardAsync();
    Task<(bool Success, string Error)> SetFeedbackStatusAsync(Guid id, string status);

    // Chants
    Task<List<ChantModel>> GetChantsAsync();
    Task<ChantModel?> GetChantAsync(Guid id);
    Task<(bool Success, string Error)> CreateChantAsync(ChantRequest request);
    Task<(bool Success, string Error)> UpdateChantAsync(Guid id, ChantRequest request);
    Task<(bool Success, string Error)> SetChantStatusAsync(Guid id, bool isActive);
}
