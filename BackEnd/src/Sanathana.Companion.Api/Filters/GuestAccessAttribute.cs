using Microsoft.AspNetCore.Authorization;

namespace Sanathana.Companion.Api.Filters;

/// <summary>
/// Opens a read endpoint to a visitor browsing as a guest — a caller with no token at all.
/// </summary>
/// <remarks>
/// It is <see cref="IAllowAnonymous"/> only to get past [Authorize] and the fallback policy; it is
/// NOT public. <see cref="ModuleAccessFilter"/> then holds an anonymous caller to the Guest role's
/// grants on the Access Rights screen, so the module the endpoint names must be granted to Guest.
/// A signed-in caller is checked exactly as before, so marking an endpoint changes nothing for
/// seekers or administrators.
/// <para>
/// Only for endpoints that read shared content. Anything that reads or writes a person's own data
/// (sadhana counts, streaks, favorites, profile, reminders) must stay [Authorize]: a guest has no
/// account to read it from, and asking for one is what sends them to sign in.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class GuestAccessAttribute : Attribute, IAllowAnonymous
{
}
