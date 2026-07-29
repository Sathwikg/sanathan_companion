namespace App.Core.Config;

/// <summary>Config-driven settings supplied by each host.</summary>
public class AppConfig
{
    public string ApiBaseUrl { get; set; } = "http://localhost:7050/api";

    /// <summary>Which host this is running in — "Web" or "Mobile". Drives per-platform menu access rights.</summary>
    public string Platform { get; set; } = "Web";
}
