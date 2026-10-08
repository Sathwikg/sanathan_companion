using System.IdentityModel.Tokens.Jwt;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.Common.Authorization;
using Sanathana.Companion.Application.Interfaces;

namespace Sanathana.Companion.Api.Services;

/// <summary>Reads the audit details of the current request from its HttpContext.</summary>
public sealed class HttpAuditRequestContext : IAuditRequestContext
{
    /// <summary>The header the clients stamp with Web, Android or iOS.</summary>
    public const string PlatformHeader = "X-Platform";

    private readonly IHttpContextAccessor _accessor;

    public HttpAuditRequestContext(IHttpContextAccessor accessor) => _accessor = accessor;

    private HttpContext? Context => _accessor.HttpContext;

    public bool IsHttpRequest => Context is not null;

    // Already the client's address: UseForwardedHeaders runs first in the pipeline.
    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var value = Context?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    public string? Platform => AuditPlatforms.Normalize(Context?.Request.Headers[PlatformHeader].ToString());

    public string? Endpoint => Context is { } c ? $"{c.Request.Method} {c.Request.Path}" : null;

    public string? ModuleCode
        => Context?.GetEndpoint()?.Metadata.GetOrderedMetadata<RequiresModuleAttribute>().LastOrDefault()?.Codes.FirstOrDefault();

    public Guid? SessionId
        => Guid.TryParse(Context?.User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value, out var sid) ? sid : null;
}
