namespace Sanathana.Companion.Application.Common.Authorization;

/// <summary>
/// Says which form an endpoint belongs to, so Access Rights can gate it.
/// </summary>
/// <remarks>
/// More than one code is allowed because several endpoints genuinely serve more than one form —
/// the region list fills a filter on the Festivals and Languages screens as well as being the
/// Regions master. The caller needs ANY one of them.
/// <para>
/// An action-level attribute beats a controller-level one for free: MVC lists controller metadata
/// before action metadata, so the filter takes the last one it finds.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresModuleAttribute : Attribute
{
    public RequiresModuleAttribute(params string[] codes) => Codes = codes;

    public IReadOnlyList<string> Codes { get; }
}
