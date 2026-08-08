using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Sanathana.Companion.Api.Filters;
using Sanathana.Companion.Api.Middleware;
using Sanathana.Companion.Api.Services;
using Sanathana.Companion.Application;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Infrastructure;
using Sanathana.Companion.Infrastructure.Identity;
using Sanathana.Companion.Infrastructure.Persistence;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // Bound the request body so a huge upload can't buffer unchecked (10 MB audio base64-inflates to ~14 MB).
    builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 16 * 1024 * 1024);

    // MVC controllers
    // Global: translates DB text on the way out. Annotating a DTO property is the only
    // work needed to localise a new form — see TranslationResultFilter.
    builder.Services.AddControllers(o => o.Filters.Add<TranslationResultFilter>());

    // Swagger + Bearer auth button
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "Sanathana Companion API", Version = "v1" });

        var jwtScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "Enter the JWT as: Bearer {token}",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        };
        options.AddSecurityDefinition("Bearer", jwtScheme);
        options.AddSecurityRequirement(new OpenApiSecurityRequirement { { jwtScheme, Array.Empty<string>() } });
    });

    // Current user (reads claims) + application/infrastructure services
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // JWT authentication. The signing secret MUST be supplied out of band (env var
    // JwtSettings__Secret / user-secrets / secret store) — never a committed placeholder,
    // because an HS256 key that anyone can read lets them forge admin tokens.
    var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
    if (string.IsNullOrWhiteSpace(jwtSettings.Secret)
        || jwtSettings.Secret.Contains("change-me", StringComparison.OrdinalIgnoreCase)
        || Encoding.UTF8.GetByteCount(jwtSettings.Secret) < 32)
    {
        throw new InvalidOperationException(
            "JwtSettings:Secret is missing, shorter than 32 bytes, or still a placeholder. " +
            "Provide a strong random value via the JwtSettings__Secret environment variable or user-secrets.");
    }

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "sub",
                RoleClaimType = ClaimTypes.Role
            };
        });
    builder.Services.AddAuthorization();

    // Throttle the anonymous auth endpoints (per client IP) to blunt credential brute force / stuffing.
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    });

    // CORS: wide open only in Development; production restricts to configured origins.
    builder.Services.AddCors(options =>
        options.AddPolicy("Default", policy =>
        {
            if (builder.Environment.IsDevelopment())
            {
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            }
            else
            {
                var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
            }
        }));

    if (!builder.Environment.IsDevelopment())
        builder.Services.AddHsts(o => { o.MaxAge = TimeSpan.FromDays(365); o.IncludeSubDomains = true; });

    var app = builder.Build();

    // Apply migrations on startup (adequate for a single-instance deployment).
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();

        // Load the shipped translation files. Idempotent, and it never overwrites a label an
        // admin edited in the UI, so it is safe to run on every boot.
        try
        {
            var localization = scope.ServiceProvider.GetRequiredService<ILocalizationService>();
            var written = await localization.ImportSeedFilesAsync();
            if (written > 0) Log.Information("Localization seed import wrote {Count} entries.", written);

            // The DB-text dictionary: vocabulary from the Panchangam code tables, then any
            // shipped translations for it. Both are idempotent and never overwrite admin edits.
            var termSeed = scope.ServiceProvider.GetRequiredService<ITermSeedService>();
            var terms = await termSeed.SeedTermsAsync();
            var termTexts = await termSeed.ImportTermTranslationsAsync();
            if (terms > 0 || termTexts > 0)
                Log.Information("Term dictionary seeded {Terms} terms and {Texts} translations.", terms, termTexts);
        }
        catch (Exception ex)
        {
            // Missing translations must never stop the API from serving.
            Log.Error(ex, "Localization seed import failed; the app will fall back to English.");
        }
    }

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // Baseline security response headers (cheap defense-in-depth on every response).
    app.Use(async (ctx, next) =>
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "no-referrer";
        await next();
    });

    if (app.Environment.IsDevelopment())
    {
        // API surface / interactive console are exposed only in Development.
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    else
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    app.UseSerilogRequestLogging();
    app.UseCors("Default");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Sanathana Companion API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Exposed so integration tests can reference the entry point.</summary>
public partial class Program { }
