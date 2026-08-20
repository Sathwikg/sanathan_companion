using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Sanathana.Companion.Api.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Sanathana.Companion.Tests;

/// <summary>
/// The two start-up settings that were quietly doing nothing: the log levels nobody read, and a
/// database connection that encrypted without checking who answered.
/// </summary>
public class StartupConfigurationTests
{
    private static readonly string ApiSettingsPath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "src", "Sanathana.Companion.Api", "appsettings.json");

    // ------------------------------------------------------------------ TLS

    [Theory]
    // The case a substring search for the obvious keyword would have waved through.
    [InlineData("Host=h;SSL Mode=Require", true)]
    [InlineData("Host=h", true)]                                        // the default mode does not verify
    [InlineData("Host=h;SSL Mode=Prefer", true)]
    [InlineData("Host=h;SSL Mode=Disable", true)]
    [InlineData("Host=h;SSL Mode=VerifyCA;Root Certificate=/ca.crt", false)]
    [InlineData("Host=h;SSL Mode=VerifyFull;Root Certificate=/ca.crt", false)]
    [InlineData("host=h;ssl mode=verifyfull;root certificate=/ca.crt", false)]
    public void The_guard_reads_the_mode_rather_than_hunting_for_a_keyword(string connectionString, bool skips)
        => Assert.Equal(skips, ConnectionStringGuard.SkipsCertificateValidation(connectionString));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("this is not a connection string ;;;")]
    public void An_absent_or_unparseable_string_is_not_reported_as_a_problem(string? connectionString)
    {
        // The DbContext will report a malformed string far better than a start-up warning could,
        // and a guard that throws here would take the site down over a typo.
        Assert.False(ConnectionStringGuard.SkipsCertificateValidation(connectionString));
    }

    // ------------------------------------------------------------------ logging

    [Fact]
    public void The_configured_level_actually_governs_what_is_written()
    {
        // This is what was broken: UseSerilog() replaces the Microsoft.Extensions logging factory,
        // so the Logging:LogLevel block in appsettings was read by nobody.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Serilog:MinimumLevel:Default"] = "Information",
            ["Serilog:MinimumLevel:Override:Microsoft.AspNetCore"] = "Warning"
        }).Build();

        var sink = new CapturingSink();
        using var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.ForContext(Constants.SourceContextPropertyName, "Microsoft.AspNetCore.Hosting.Diagnostics")
              .Information("Request starting GET /api/panchangam/compute?lat=17.385&lon=78.4867");
        logger.ForContext(Constants.SourceContextPropertyName, "Sanathana")
              .Information("something the app itself said");

        var written = Assert.Single(sink.Events);
        Assert.Contains("the app itself", written.RenderMessage());
    }

    [Fact]
    public void The_shipped_settings_carry_the_key_the_logger_reads_and_not_the_one_it_ignores()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ApiSettingsPath));
        var root = document.RootElement;

        Assert.False(root.TryGetProperty("Logging", out _),
            "Logging:LogLevel is inert under Serilog; leaving it invites someone to set it and be ignored.");

        var overrides = root.GetProperty("Serilog").GetProperty("MinimumLevel").GetProperty("Override");
        Assert.Equal("Warning", overrides.GetProperty("Microsoft.AspNetCore").GetString());
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
