namespace Sanathana.Companion.Application.Common;

/// <summary>Where an error log row came from. Clients may only claim the two frontend sources.</summary>
public static class AuditErrorSources
{
    public const string BackendApi = "BackendApi";
    public const string FrontendWeb = "FrontendWeb";
    public const string FrontendMobile = "FrontendMobile";

    public static readonly IReadOnlySet<string> ClientReportable =
        new HashSet<string>(StringComparer.Ordinal) { FrontendWeb, FrontendMobile };
}

public static class AuditSeverities
{
    public const string Critical = "Critical";
    public const string Error = "Error";
    public const string Warning = "Warning";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal) { Critical, Error, Warning };
}

/// <summary>The platform names the client stamps in X-Platform. Anything else is stored as null.</summary>
public static class AuditPlatforms
{
    private static readonly string[] Known = { "Web", "Android", "iOS" };

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return Known.FirstOrDefault(k => string.Equals(k, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Size caps for text a client sends in, applied before it is queued.</summary>
public static class AuditLimits
{
    public const int MaxMessage = 2000;
    public const int MaxStackTrace = 16_000;
    public const int MaxInnerException = 4000;
    public const int MaxTimeSpentSeconds = 24 * 60 * 60;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public static string? Cap(string? value, int max)
        => value is null || value.Length <= max ? value : value[..max];
}
