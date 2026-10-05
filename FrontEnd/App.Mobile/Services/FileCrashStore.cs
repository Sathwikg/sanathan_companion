using System.Text.Json;
using App.Core.Models;
using App.Core.Services;

namespace App.Mobile.Services;

/// <summary>
/// Keeps the crash that is killing the app in a file, so the next start can report it.
/// </summary>
/// <remarks>
/// An unhandled exception on a native thread ends the process before any HTTP call could finish,
/// so the report is written synchronously to app storage at the moment of the crash and sent by
/// <see cref="ClientErrorReporter.SendPendingCrashAsync"/> once the app is running again. Only
/// the most recent crash is kept.
/// </remarks>
public sealed class FileCrashStore : ICrashStore
{
    private static string FilePath => Path.Combine(FileSystem.AppDataDirectory, "pending-crash.json");

    /// <summary>Called from the process-wide unhandled-exception hook. Must not throw.</summary>
    public static void Save(Exception exception)
    {
        try
        {
            var report = new LogErrorRequestModel
            {
                Source = ClientPlatform.ErrorSource,
                Severity = "Critical",
                ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                Message = string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message,
                StackTrace = exception.ToString(),
                InnerException = exception.InnerException?.Message,
                RequestPath = "(app crash)"
            };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(report));
        }
        catch
        {
            // The process is going down; there is nothing better to do.
        }
    }

    public async Task<LogErrorRequestModel?> TakeAsync()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var json = await File.ReadAllTextAsync(FilePath);
            File.Delete(FilePath);
            return JsonSerializer.Deserialize<LogErrorRequestModel>(json);
        }
        catch
        {
            return null;
        }
    }
}
