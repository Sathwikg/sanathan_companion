namespace App.Core.Models;

public class FavoriteChant
{
    public Guid ChantConfigId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public List<string> DeityNames { get; set; } = new();
    public bool HasAudio { get; set; }
}

public class FavoriteDeity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DeityType { get; set; } = "God";
    public string? Description { get; set; }
    public bool HasImage { get; set; }
}

public class Favorites
{
    public List<FavoriteChant> Chants { get; set; } = new();
    public List<FavoriteDeity> Gods { get; set; } = new();
}

public class FavoriteIds
{
    public List<Guid> ChantIds { get; set; } = new();
    public List<Guid> DeityIds { get; set; } = new();
}

public class ToggleFavoriteResult
{
    public bool IsFavorite { get; set; }
}
