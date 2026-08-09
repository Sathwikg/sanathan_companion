namespace App.Core.Models;

// ---- admin configuration ----

public class ProcessLanguageModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NativeName { get; set; }
    public bool IsBase { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(NativeName) ? Name : $"{Name} · {NativeName}";
}

public class PujaMaterialModel
{
    public Guid? Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Quantity { get; set; }
    public int DisplayOrder { get; set; }
}

public class PujaStepTextModel
{
    public Guid LanguageId { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
}

public class PujaStepConfigModel
{
    /// <summary>Null until saved. Keeping it is what preserves devotees' progress across edits.</summary>
    public Guid? Id { get; set; }
    public int StepNumber { get; set; }
    public List<PujaStepTextModel> Texts { get; set; } = new();

    public PujaStepTextModel TextFor(Guid languageId)
    {
        var existing = Texts.FirstOrDefault(t => t.LanguageId == languageId);
        if (existing is not null) return existing;

        var added = new PujaStepTextModel { LanguageId = languageId };
        Texts.Add(added);
        return added;
    }

    public bool HasText(Guid languageId)
    {
        var t = Texts.FirstOrDefault(x => x.LanguageId == languageId);
        return t is not null && (!string.IsNullOrWhiteSpace(t.Title) || !string.IsNullOrWhiteSpace(t.Content));
    }

    /// <summary>Languages this step has been written in — drives the "3 of 5 languages" hint.</summary>
    public int FilledLanguageCount => Texts.Count(t => !string.IsNullOrWhiteSpace(t.Title) || !string.IsNullOrWhiteSpace(t.Content));
}

public class PujaProcessConfigModel
{
    public Guid PujaId { get; set; }
    public string PujaName { get; set; } = string.Empty;
    public List<ProcessLanguageModel> Languages { get; set; } = new();
    public List<PujaMaterialModel> Materials { get; set; } = new();
    public List<PujaStepConfigModel> Steps { get; set; } = new();
}

public class SavePujaProcessRequest
{
    public List<PujaMaterialModel> Materials { get; set; } = new();
    public List<PujaStepConfigModel> Steps { get; set; } = new();
}

// ---- user runtime ----

public class ProcessFestivalModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateOnly Date { get; set; }
    public int PujaCount { get; set; }
    public bool IsCurrent { get; set; }
    public int DaysAway { get; set; }

    public string Label => Year > 0 ? $"{Name} ({Year})" : Name;
}

public class ProcessPujaSummaryModel
{
    public Guid PujaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DeityName { get; set; }
    public Guid? FestivalId { get; set; }
    public string? FestivalName { get; set; }
    public int? FestivalYear { get; set; }
    public int StepCount { get; set; }
    public int CompletedCount { get; set; }
    public bool IsCompleted { get; set; }

    public bool HasFestival => FestivalId is not null && !string.IsNullOrWhiteSpace(FestivalName);
    public string FestivalLabel => FestivalYear is > 0 ? $"{FestivalName} ({FestivalYear})" : FestivalName ?? string.Empty;
}

public class PujaStepViewModel
{
    public Guid StepId { get; set; }
    public int StepNumber { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public bool IsFallback { get; set; }
    public bool IsCompleted { get; set; }
}

public class PujaProcessViewModel
{
    public Guid PujaId { get; set; }
    public string PujaName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? FestivalId { get; set; }
    public string? FestivalName { get; set; }
    public string? DeityName { get; set; }

    public List<PujaMaterialModel> Materials { get; set; } = new();
    public List<PujaStepViewModel> Steps { get; set; } = new();

    public int TotalSteps { get; set; }
    public int CompletedCount { get; set; }
    public bool IsCompleted { get; set; }
    public int? CurrentStepNumber { get; set; }

    public int PercentComplete => TotalSteps == 0 ? 0 : (int)Math.Round(CompletedCount * 100.0 / TotalSteps);
}

public class PujaProgressResult
{
    public int CompletedCount { get; set; }
    public int TotalSteps { get; set; }
    public bool IsCompleted { get; set; }
    public int? CurrentStepNumber { get; set; }
}
