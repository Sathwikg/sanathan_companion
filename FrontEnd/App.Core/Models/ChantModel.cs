using System.ComponentModel.DataAnnotations;

namespace App.Core.Models;

public class ChantModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool HasCount { get; set; }
    public int? Count { get; set; }
    public bool IsActive { get; set; }
}

public class ChantRequest
{
    [Required(ErrorMessage = "Chant name is required.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public bool HasCount { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Count must be at least 1.")]
    public int? Count { get; set; }

    public bool IsActive { get; set; } = true;
}
