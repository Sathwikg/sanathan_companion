using Sanathana.Companion.Application.Common.Translation;

namespace Sanathana.Companion.Application.DTOs.Pujas;

// ---------------------------------------------------------------- admin configuration

/// <summary>Everything the Puja Process configuration screen needs for one puja.</summary>
public class PujaProcessConfigDto
{
    public Guid PujaId { get; set; }

    [NoTranslate]
    public string PujaName { get; set; } = string.Empty;

    /// <summary>The languages the admin gets tabs for.</summary>
    public List<ProcessLanguageDto> Languages { get; set; } = new();

    public List<PujaMaterialDto> Materials { get; set; } = new();
    public List<PujaStepConfigDto> Steps { get; set; } = new();
}

public class ProcessLanguageDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NativeName { get; set; }
    public bool IsBase { get; set; }
}

/// <summary>Admin-authored text, so it is never run through the shared dictionary.</summary>
public class PujaMaterialDto
{
    public Guid? Id { get; set; }

    [NoTranslate]
    public string ItemName { get; set; } = string.Empty;

    [NoTranslate]
    public string? Quantity { get; set; }

    public int DisplayOrder { get; set; }
}

public class PujaStepConfigDto
{
    /// <summary>Null for a step the admin has just added and not yet saved.</summary>
    public Guid? Id { get; set; }

    public int StepNumber { get; set; }

    /// <summary>One entry per language that has content; languages with none are simply absent.</summary>
    public List<PujaStepTextDto> Texts { get; set; } = new();
}

public class PujaStepTextDto
{
    public Guid LanguageId { get; set; }

    [NoTranslate]
    public string? Title { get; set; }

    [NoTranslate]
    public string? Content { get; set; }
}

public class SavePujaProcessDto
{
    public List<PujaMaterialDto> Materials { get; set; } = new();
    public List<PujaStepConfigDto> Steps { get; set; } = new();
}

// ---------------------------------------------------------------- user runtime

/// <summary>A festival in the Puja Process picker.</summary>
public class ProcessFestivalDto
{
    public Guid Id { get; set; }

    [Translatable("Festival", nameof(Id))]
    public string Name { get; set; } = string.Empty;

    public int Year { get; set; }
    public DateOnly Date { get; set; }
    public int PujaCount { get; set; }

    /// <summary>True for the one the screen opens on — today's, or the next one coming up.</summary>
    public bool IsCurrent { get; set; }

    /// <summary>Negative once the festival has passed; 0 means today.</summary>
    public int DaysAway { get; set; }
}

public class ProcessPujaSummaryDto
{
    public Guid PujaId { get; set; }

    [NoTranslate]
    public string Name { get; set; } = string.Empty;

    [NoTranslate]
    public string? Description { get; set; }

    [Translatable(Category = "deity")]
    public string? DeityName { get; set; }

    public int StepCount { get; set; }
    public int CompletedCount { get; set; }
    public bool IsCompleted { get; set; }
}

/// <summary>The process a devotee actually follows, already resolved to their language.</summary>
public class PujaProcessViewDto
{
    public Guid PujaId { get; set; }

    [NoTranslate]
    public string PujaName { get; set; } = string.Empty;

    [NoTranslate]
    public string? Description { get; set; }

    [Translatable("Festival", nameof(FestivalId))]
    public string? FestivalName { get; set; }

    public Guid? FestivalId { get; set; }

    [Translatable(Category = "deity")]
    public string? DeityName { get; set; }

    public List<PujaMaterialDto> Materials { get; set; } = new();
    public List<PujaStepViewDto> Steps { get; set; } = new();

    public int TotalSteps { get; set; }
    public int CompletedCount { get; set; }

    /// <summary>True once every step is done — the screen shows the completion state.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>The lowest-numbered step still outstanding, so the UI can point at what is next.</summary>
    public int? CurrentStepNumber { get; set; }
}

public class PujaStepViewDto
{
    public Guid StepId { get; set; }
    public int StepNumber { get; set; }

    /// <summary>Authored per language and already resolved; never dictionary-translated.</summary>
    [NoTranslate]
    public string? Title { get; set; }

    [NoTranslate]
    public string? Content { get; set; }

    /// <summary>True when the step has no text in the requested language and fell back.</summary>
    public bool IsFallback { get; set; }

    public bool IsCompleted { get; set; }
}

/// <summary>Returned after completing or undoing a step, so the caller need not refetch.</summary>
public class PujaProgressResultDto
{
    public int CompletedCount { get; set; }
    public int TotalSteps { get; set; }
    public bool IsCompleted { get; set; }
    public int? CurrentStepNumber { get; set; }
}
