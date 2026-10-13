namespace Sanathana.Companion.Api.Filters;

/// <summary>
/// Opts a non-GET action into <see cref="TranslationResultFilter"/>.
/// </summary>
/// <remarks>
/// The filter skips writes because they echo back what the client sent. A POST that is really a
/// read — it carries its arguments in the body only to keep them out of access logs, like the
/// Panchangam compute — returns fresh server text and must be translated like any GET.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true)]
public sealed class TranslateResponseAttribute : Attribute;
