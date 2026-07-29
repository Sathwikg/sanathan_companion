namespace Sanathana.Companion.Application.Common;

/// <summary>
/// Helpers for the comma-separated FK-id columns used across the app
/// (Languages.Regions, Festivals.Regions, ChantConfigs.DeityIds).
/// </summary>
public static class CsvIds
{
    public static List<Guid> Split(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return new();
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Select(v => Guid.TryParse(v, out var g) ? g : Guid.Empty)
                  .Where(g => g != Guid.Empty)
                  .Distinct()
                  .ToList();
    }

    public static string? Join(IEnumerable<Guid> ids)
    {
        var cleaned = ids.Where(id => id != Guid.Empty).Distinct().Select(id => id.ToString()).ToList();
        return cleaned.Count == 0 ? null : string.Join(",", cleaned);
    }
}
