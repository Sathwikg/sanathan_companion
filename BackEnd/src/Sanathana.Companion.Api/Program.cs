using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Sanathana.Companion.Api.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Sanathana.Companion.Api.Filters;
using Sanathana.Companion.Api.Middleware;
using Sanathana.Companion.Api.Services;
using Sanathana.Companion.Application;
using Sanathana.Companion.Api.Configuration;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Infrastructure;
using Sanathana.Companion.Infrastructure.Identity;
using Sanathana.Companion.Infrastructure.Persistence;
using Serilog;
using Serilog.Events;

// The window before configuration exists. Framework diagnostics are held at Warning even here,
// because "Request starting"/"Request finished" carry the full URL including its query string, and
// this app puts a seeker's coordinates through one of them.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    // Reads Serilog:MinimumLevel from configuration. UseSerilog() replaces the Microsoft.Extensions
    // logging factory outright, which is why the old Logging:LogLevel block was dead — nothing read
    // it, and nothing will, since writeToProviders stays at its default of false.
    builder.Host.UseSerilog((context, services, cfg) => cfg
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console());

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

    // Warn, never throw: a database link that encrypts without validating is worth shouting about,
    // but taking the whole site down over a connection-string keyword is worse than the keyword.
    if (!builder.Environment.IsDevelopment()
        && ConnectionStringGuard.SkipsCertificateValidation(builder.Configuration.GetConnectionString("DefaultConnection")))
    {
        Log.Warning(
            "ConnectionStrings:DefaultConnection does not validate the database server's certificate. " +
            "Use SSL Mode=VerifyFull with Root Certificate pointing at the provider's CA. " +
            "SSL Mode=Require encrypts but verifies nothing.");
    }

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

    // Behind Render's router — and behind the nginx in docker-compose — every request arrives from
    // the proxy, so Connection.RemoteIpAddress is the proxy's address for ALL of them. Without
    // this, the per-IP limiter below degrades into ONE global bucket: ten sign-in attempts a minute
    // from anybody would lock every seeker out of the app.
    //
    // KnownIPNetworks/KnownProxies are cleared because the hop count, not the address, is what can be
    // trusted here: Render does not publish a stable proxy range, and the container only ever
    // receives traffic through it. ForwardLimit stays at 1 so a client-supplied X-Forwarded-For
    // cannot prepend a spoofed address and escape its own bucket.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // Throttle the anonymous auth endpoints to blunt credential brute force / stuffing.
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                // Partition on the credential being tried as well as the caller's address. One
                // seeker mistyping their password must not consume the budget of everyone else
                // behind the same carrier NAT or corporate egress, and an attacker spraying one
                // password across many accounts is still bounded by the address half.
                partitionKey: AuthRateLimitPartition.For(httpContext),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        // The four media endpoints are anonymous by necessity — an <img> or <audio> cannot carry
        // a bearer token — so the URL alone pulls megabytes. Partitioned on the address only: the
        // auth policy's second, credential dimension has no equivalent here, since the request
        // carries nothing that identifies a caller.
        //
        // 240 rather than something tighter because the audio endpoint enables range processing,
        // so one seeker scrubbing through a chant spends many permits on a single file, and a 429
        // on an <img> is a silently broken element rather than a visible error.
        options.AddPolicy("media", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Configuration.GetValue("RateLimits:MediaPerMinute", 240),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        // Panchangam compute is a few thousand series evaluations per call. The cache in front of
        // it only helps for coordinates someone has already asked about, so the endpoint still
        // needs a ceiling of its own.
        options.AddPolicy("compute", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ComputeRateLimitPartition.For(httpContext),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Configuration.GetValue("RateLimits:ComputePerMinute", 30),
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

        // The seeded administrator ships locked — its stored hash verifies against nothing. This
        // opens it once from Admin__InitialPassword, and only while it is still locked, so it can
        // never reset a password that has since been rotated.
        try
        {
            await scope.ServiceProvider.GetRequiredService<AdminAccountBootstrapper>().ApplyAsync();
        }
        catch (Exception ex)
        {
            // An unreachable administrator is not a reason to take the site down for seekers.
            Log.Warning(ex, "Could not apply the administrator account bootstrap.");
        }

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

    // FIRST, before anything reads RemoteIpAddress or the scheme — the rate limiter, the request
    // log and HTTPS redirection all depend on the forwarded values being applied by now.
    app.UseForwardedHeaders();

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

    // Already the default in Serilog.AspNetCore 10, pinned so neither an upgrade nor a
    // copy-pasted options lambda can put a seeker's coordinates back into the log line.
    app.UseSerilogRequestLogging(o => o.IncludeQueryInRequestPath = false);
    app.UseCors("Default");
    app.UseAuthentication();
    // After authentication, deliberately: the compute policy partitions on the signed-in user, and
    // before this the limiter saw an anonymous principal and fell back to the address for
    // everybody. The "auth" policy is unaffected by the move — it reads only the remote address
    // and a request header, both available anywhere in the pipeline.
    app.UseRateLimiter();
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
