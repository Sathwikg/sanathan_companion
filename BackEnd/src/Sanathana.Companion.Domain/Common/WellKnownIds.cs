namespace Sanathana.Companion.Domain.Common;

/// <summary>
/// Seeded identifiers that layers above Infrastructure also have to recognise.
/// </summary>
/// <remarks>
/// The full seed lives in Infrastructure's SeedConstants, which reads its values from here so the
/// two cannot drift. Only ids that carry a RULE belong in this file — the built-in administrator
/// is here because closing that account would leave nobody able to open it again, and that is a
/// decision the Application layer has to be able to make.
/// </remarks>
public static class WellKnownIds
{
    public static readonly Guid AdminUser = new("11111111-1111-1111-1111-111111111111");
}
