namespace Sanathana.Companion.Application.DTOs.Festivals;

public class FestivalDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Year { get; set; }
    public DateOnly Date { get; set; }
    public List<Guid> RegionIds { get; set; } = new();
    public List<string> RegionNames { get; set; } = new();
    public bool IsActive { get; set; }
}

public class CreateFestivalDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Year { get; set; }
    public DateOnly Date { get; set; }
    public List<Guid> RegionIds { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class UpdateFestivalDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Year { get; set; }
    public DateOnly Date { get; set; }
    public List<Guid> RegionIds { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class UpdateFestivalStatusDto
{
    public bool IsActive { get; set; }
}
