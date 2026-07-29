namespace Sanathana.Companion.Application.DTOs.Favorites;

public class ToggleFavoriteDto
{
    public string Type { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
}

public class ToggleFavoriteResultDto
{
    public bool IsFavorite { get; set; }
}

/// <summary>The ids the current user has favorited, so the mark buttons can render filled.</summary>
public class FavoriteIdsDto
{
    public List<Guid> ChantIds { get; set; } = new();
    public List<Guid> DeityIds { get; set; } = new();
}

/// <summary>The current user's favorites, resolved for display.</summary>
public class FavoritesDto
{
    public List<FavoriteChantDto> Chants { get; set; } = new();
    public List<FavoriteDeityDto> Gods { get; set; } = new();
}

public class FavoriteChantDto
{
    public Guid ChantConfigId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public List<string> DeityNames { get; set; } = new();
    public bool HasAudio { get; set; }
}

public class FavoriteDeityDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DeityType { get; set; } = "God";
    public string? Description { get; set; }
    public bool HasImage { get; set; }
}
