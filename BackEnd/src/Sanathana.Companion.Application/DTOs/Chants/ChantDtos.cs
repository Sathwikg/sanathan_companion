namespace Sanathana.Companion.Application.DTOs.Chants;

public class ChantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool HasCount { get; set; }
    public int? Count { get; set; }
    public bool IsActive { get; set; }
}

public class CreateChantDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool HasCount { get; set; }
    public int? Count { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateChantDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool HasCount { get; set; }
    public int? Count { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateChantStatusDto
{
    public bool IsActive { get; set; }
}
