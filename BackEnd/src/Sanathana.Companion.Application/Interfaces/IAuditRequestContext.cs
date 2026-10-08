namespace Sanathana.Companion.Application.Interfaces;

/// <summary>
/// What the audit trail records about the request in progress (implemented in the API layer).
/// Outside a request, such as start-up seeding or a background job, everything is null and
/// <see cref="IsHttpRequest"/> is false.
/// </summary>
public interface IAuditRequestContext
{
    bool IsHttpRequest { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }

    /// <summary>Web, Android or iOS, from the X-Platform header the client sends. Client-supplied, so informational only.</summary>
    string? Platform { get; }

    /// <summary>"PUT /api/deities/{id}"-style method and path.</summary>
    string? Endpoint { get; }

    /// <summary>The form the endpoint belongs to, from its [RequiresModule] attribute.</summary>
    string? ModuleCode { get; }

    /// <summary>The "sid" claim of the caller's access token: the session this request belongs to.</summary>
    Guid? SessionId { get; }
}
