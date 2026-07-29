using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>A user's favorite item — a chant (ChantConfig) or a god (Deity).</summary>
public class UserFavorite : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// <summary>"Chant" or "Deity".</summary>
    public string FavoriteType { get; set; } = string.Empty;

    /// <summary>The ChantConfig id or Deity id being favorited.</summary>
    public Guid ItemId { get; set; }
}
