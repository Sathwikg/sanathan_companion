using System.ComponentModel.DataAnnotations;

namespace App.Core.Models;

public class FestivalModel
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

public class FestivalRequest
{
    [Required(ErrorMessage = "Festival name is required.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Range(1900, 2200, ErrorMessage = "Enter a valid year.")]
    public int Year { get; set; } = DateTime.Now.Year;

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);

    public List<Guid> RegionIds { get; set; } = new();

    public bool IsActive { get; set; } = true;
}
