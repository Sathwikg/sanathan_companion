namespace Sanathana.Companion.Application.Common.Authorization;

/// <summary>
/// Marks an endpoint that belongs to no form, so the module gate lets it through.
/// </summary>
/// <remarks>
/// Two kinds of thing qualify, and nothing else should: session infrastructure every signed-in
/// caller needs whatever their role (the menu itself, the notification bell), and the caller's own
/// data, which is not a form an administrator grants (the profile, favouriting, changing your own
/// password). It exists as an explicit attribute rather than an omission so that the exempt set is
/// greppable and a forgotten mapping is a denial rather than a hole.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class ModuleExemptAttribute : Attribute
{
}
