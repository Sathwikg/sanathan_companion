using FluentValidation;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Pujas;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class PujaProcessService : IPujaProcessService
{
    private const int MaxSteps = 100;
    private const int MaxMaterials = 100;
    private const int MaxItemLength = 150;
    private const int MaxQuantityLength = 50;
    private const int MaxTitleLength = 200;

    private readonly IUnitOfWork _uow;

    public PujaProcessService(IUnitOfWork uow) => _uow = uow;

    // ------------------------------------------------------------ admin configuration

    public async Task<PujaProcessConfigDto> GetConfigAsync(Guid pujaId, CancellationToken cancellationToken = default)
    {
        var puja = await _uow.Pujas.GetByIdAsync(pujaId, cancellationToken)
            ?? throw new NotFoundException($"Puja '{pujaId}' was not found.");

        var languages = await _uow.Languages.GetAllOrderedAsync(cancellationToken);
        var materials = await _uow.PujaProcess.GetMaterialsAsync(pujaId, cancellationToken);
        var steps = await _uow.PujaProcess.GetStepsWithTextsAsync(pujaId, cancellationToken);

        return new PujaProcessConfigDto
        {
            PujaId = pujaId,
            PujaName = puja.Name,
            Languages = languages
                .Where(l => l.IsActive)
                .OrderByDescending(IsBaseLanguage).ThenBy(l => l.Name)
                .Select(l => new ProcessLanguageDto
                {
                    Id = l.Id, Code = l.Code ?? string.Empty, Name = l.Name,
                    NativeName = l.NativeName, IsBase = IsBaseLanguage(l)
                })
                .ToList(),

            Materials = materials.Select(m => new PujaMaterialDto
            {
                Id = m.Id, ItemName = m.ItemName, Quantity = m.Quantity, DisplayOrder = m.DisplayOrder
            }).ToList(),

            Steps = steps.Select(s => new PujaStepConfigDto
            {
                Id = s.Id,
                StepNumber = s.StepNumber,
                Texts = s.Texts.Select(t => new PujaStepTextDto
                {
                    LanguageId = t.LanguageId, Title = t.Title, Content = t.Content
                }).ToList()
            }).ToList()
        };
    }

    /// <summary>
    /// Saves the whole process in one call.
    /// </summary>
    /// <remarks>
    /// Steps are matched by id rather than replaced wholesale. Deleting and re-creating them would
    /// be simpler, but progress rows cascade off the step, so every devotee mid-way through the
    /// puja would silently lose their place the moment an admin fixed a typo. Only steps the admin
    /// actually removed are deleted.
    /// </remarks>
    public async Task SaveConfigAsync(Guid pujaId, SavePujaProcessDto dto, CancellationToken cancellationToken = default)
    {
        _ = await _uow.Pujas.GetByIdAsync(pujaId, cancellationToken)
            ?? throw new NotFoundException($"Puja '{pujaId}' was not found.");

        if (dto.Steps.Count > MaxSteps)
            throw new ValidationException($"A puja can have at most {MaxSteps} steps.");
        if (dto.Materials.Count > MaxMaterials)
            throw new ValidationException($"A puja can have at most {MaxMaterials} materials.");

        await SaveMaterialsAsync(pujaId, dto.Materials, cancellationToken);
        await SaveStepsAsync(pujaId, dto.Steps, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveMaterialsAsync(Guid pujaId, List<PujaMaterialDto> incoming, CancellationToken ct)
    {
        // Materials carry no progress, so a straight replace is safe and keeps the code honest.
        var existing = await _uow.PujaProcess.GetMaterialsTrackedAsync(pujaId, ct);
        foreach (var row in existing) _uow.PujaProcess.RemoveMaterial(row);

        var order = 0;
        foreach (var m in incoming)
        {
            var name = Trim(m.ItemName, MaxItemLength);
            if (string.IsNullOrEmpty(name)) continue;   // a blank row is just an unfilled input

            await _uow.PujaProcess.AddMaterialAsync(new PujaMaterial
            {
                PujaId = pujaId,
                ItemName = name,
                Quantity = Trim(m.Quantity, MaxQuantityLength),
                DisplayOrder = ++order
            }, ct);
        }
    }

    private async Task SaveStepsAsync(Guid pujaId, List<PujaStepConfigDto> incoming, CancellationToken ct)
    {
        var existing = await _uow.PujaProcess.GetStepsTrackedAsync(pujaId, ct);
        var keptIds = incoming.Where(s => s.Id is not null).Select(s => s.Id!.Value).ToHashSet();

        foreach (var gone in existing.Where(s => !keptIds.Contains(s.Id)))
            _uow.PujaProcess.RemoveStep(gone);

        var byId = existing.ToDictionary(s => s.Id);
        var number = 0;

        foreach (var s in incoming)
        {
            number++;

            PujaStep step;
            if (s.Id is { } id && byId.TryGetValue(id, out var found))
            {
                step = found;
                // Renumber in place so the order reflects what the admin sees, without
                // disturbing the step's identity or anyone's progress against it.
                if (step.StepNumber != number)
                {
                    step.StepNumber = number;
                    _uow.PujaProcess.UpdateStep(step);
                }
            }
            else
            {
                step = new PujaStep { PujaId = pujaId, StepNumber = number };
                await _uow.PujaProcess.AddStepAsync(step, ct);
            }

            await SaveStepTextsAsync(step, s.Texts, ct);
        }
    }

    private async Task SaveStepTextsAsync(PujaStep step, List<PujaStepTextDto> incoming, CancellationToken ct)
    {
        var existing = step.Texts.ToDictionary(t => t.LanguageId);

        foreach (var t in incoming)
        {
            var title = Trim(t.Title, MaxTitleLength);
            var content = HtmlSanitizer.Sanitize(t.Content);
            var isEmpty = string.IsNullOrEmpty(title)
                          && string.IsNullOrWhiteSpace(HtmlSanitizer.ToPlainText(content));

            if (existing.TryGetValue(t.LanguageId, out var row))
            {
                if (isEmpty)
                {
                    // Cleared in the editor: drop the row so the language falls back rather than
                    // rendering an empty step.
                    _uow.PujaProcess.RemoveStepText(row);
                }
                else
                {
                    row.Title = title;
                    row.Content = content;
                    _uow.PujaProcess.UpdateStepText(row);
                }
                continue;
            }

            if (isEmpty) continue;

            await _uow.PujaProcess.AddStepTextAsync(new PujaStepText
            {
                PujaStepId = step.Id,
                LanguageId = t.LanguageId,
                Title = title,
                Content = content
            }, ct);
        }
    }

    // ------------------------------------------------------------ user runtime

    public async Task<IReadOnlyList<ProcessFestivalDto>> GetFestivalsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var pujas = await _uow.Pujas.GetAllWithLinksAsync(cancellationToken);
        var stepCounts = await _uow.PujaProcess.GetStepCountsAsync(cancellationToken);

        // Festivals here are only the filter's options, so a puja with no festival simply
        // contributes nothing to this list — it must not be excluded from the pujas themselves,
        // which is what happened when the festival was still mandatory.
        var configured = pujas
            .Where(p => p.IsActive && p.FestivalId is not null && stepCounts.ContainsKey(p.Id))
            .GroupBy(p => p.FestivalId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        if (configured.Count == 0) return Array.Empty<ProcessFestivalDto>();

        var festivals = (await _uow.Festivals.ListAllAsync(cancellationToken))
            .Where(f => configured.ContainsKey(f.Id))
            .ToList();

        var today = DateOnly.FromDateTime(DateTime.Now);

        var list = festivals
            .Select(f => new ProcessFestivalDto
            {
                Id = f.Id,
                Name = f.Name,
                Year = f.Year,
                Date = f.Date,
                PujaCount = configured[f.Id],
                DaysAway = f.Date.DayNumber - today.DayNumber
            })
            .OrderBy(f => f.Date)
            .ToList();

        // "Current" means today or the next one coming up. If every configured festival is in
        // the past, fall back to the most recent, so the screen still opens on something useful.
        var current = list.FirstOrDefault(f => f.DaysAway >= 0) ?? list.LastOrDefault();
        if (current is not null) current.IsCurrent = true;

        return list;
    }

    public async Task<IReadOnlyList<ProcessPujaSummaryDto>> GetPujasAsync(
        Guid userId, Guid? festivalId, CancellationToken cancellationToken = default)
    {
        var pujas = await _uow.Pujas.GetAllWithLinksAsync(cancellationToken);
        var stepCounts = await _uow.PujaProcess.GetStepCountsAsync(cancellationToken);

        // The only hard requirement is a configured process; the festival narrows the list when
        // one is supplied. A puja mapped to no festival is still perfectly performable.
        var matching = pujas
            .Where(p => p.IsActive
                        && stepCounts.ContainsKey(p.Id)
                        && (festivalId is null || p.FestivalId == festivalId))
            .ToList();

        var result = new List<ProcessPujaSummaryDto>(matching.Count);
        foreach (var p in matching)
        {
            var total = stepCounts[p.Id];
            var done = (await _uow.PujaProcess.GetProgressAsync(userId, p.Id, cancellationToken)).Count;

            result.Add(new ProcessPujaSummaryDto
            {
                PujaId = p.Id,
                Name = p.Name,
                Description = p.Description,
                DeityName = p.Deity?.Name,
                FestivalId = p.FestivalId,
                FestivalName = p.Festival?.Name,
                FestivalYear = p.Festival?.Year,
                StepCount = total,
                CompletedCount = Math.Min(done, total),
                IsCompleted = total > 0 && done >= total
            });
        }

        return result.OrderBy(r => r.Name).ToList();
    }

    public async Task<PujaProcessViewDto?> GetProcessAsync(
        Guid userId, Guid pujaId, string? languageCode, CancellationToken cancellationToken = default)
    {
        var pujas = await _uow.Pujas.GetAllWithLinksAsync(cancellationToken);
        var puja = pujas.FirstOrDefault(p => p.Id == pujaId);
        if (puja is null) return null;

        var languages = await _uow.Languages.GetAllOrderedAsync(cancellationToken);
        var wanted = languages.FirstOrDefault(
            l => string.Equals(l.Code, languageCode, StringComparison.OrdinalIgnoreCase));
        var baseLang = languages.FirstOrDefault(IsBaseLanguage);

        var materials = await _uow.PujaProcess.GetMaterialsAsync(pujaId, cancellationToken);
        var steps = await _uow.PujaProcess.GetStepsWithTextsAsync(pujaId, cancellationToken);
        var doneStepIds = (await _uow.PujaProcess.GetProgressAsync(userId, pujaId, cancellationToken))
            .Select(p => p.PujaStepId).ToHashSet();

        var view = new PujaProcessViewDto
        {
            PujaId = puja.Id,
            PujaName = puja.Name,
            Description = puja.Description,
            FestivalId = puja.FestivalId,
            FestivalName = puja.Festival?.Name,
            DeityName = puja.Deity?.Name,
            Materials = materials.Select(m => new PujaMaterialDto
            {
                Id = m.Id, ItemName = m.ItemName, Quantity = m.Quantity, DisplayOrder = m.DisplayOrder
            }).ToList(),
            TotalSteps = steps.Count
        };

        foreach (var s in steps)
        {
            var (text, isFallback) = ResolveText(s, wanted?.Id, baseLang?.Id);

            view.Steps.Add(new PujaStepViewDto
            {
                StepId = s.Id,
                StepNumber = s.StepNumber,
                Title = text?.Title,
                Content = text?.Content,
                IsFallback = isFallback,
                IsCompleted = doneStepIds.Contains(s.Id)
            });
        }

        view.CompletedCount = view.Steps.Count(s => s.IsCompleted);
        view.IsCompleted = view.TotalSteps > 0 && view.CompletedCount >= view.TotalSteps;
        view.CurrentStepNumber = view.Steps.FirstOrDefault(s => !s.IsCompleted)?.StepNumber;

        return view;
    }

    /// <summary>
    /// Requested language, then the base language, then whatever exists. A step configured only
    /// in Telugu should still be readable by an English user rather than rendering blank.
    /// </summary>
    private static (PujaStepText? Text, bool IsFallback) ResolveText(PujaStep step, Guid? wantedId, Guid? baseId)
    {
        if (wantedId is { } w)
        {
            var exact = step.Texts.FirstOrDefault(t => t.LanguageId == w);
            if (exact is not null) return (exact, false);
        }

        if (baseId is { } b)
        {
            var fallback = step.Texts.FirstOrDefault(t => t.LanguageId == b);
            if (fallback is not null) return (fallback, wantedId is not null && wantedId != baseId);
        }

        var any = step.Texts.FirstOrDefault();
        return (any, any is not null);
    }

    public async Task<PujaProgressResultDto> CompleteStepAsync(
        Guid userId, Guid stepId, CancellationToken cancellationToken = default)
    {
        var step = await FindStepAsync(stepId, cancellationToken);

        var existing = await _uow.PujaProcess.GetProgressEntryAsync(userId, stepId, cancellationToken);
        if (existing is null)
        {
            await _uow.PujaProcess.AddProgressAsync(new UserPujaStepProgress
            {
                UserId = userId,
                PujaId = step.PujaId,
                PujaStepId = stepId,
                CompletedAtUtc = DateTime.UtcNow
            }, cancellationToken);

            await _uow.SaveChangesAsync(cancellationToken);
        }
        // Already complete: treat a repeat tap as a no-op rather than an error.

        return await BuildProgressAsync(userId, step.PujaId, cancellationToken);
    }

    public async Task<PujaProgressResultDto> UndoStepAsync(
        Guid userId, Guid stepId, CancellationToken cancellationToken = default)
    {
        var step = await FindStepAsync(stepId, cancellationToken);

        var existing = await _uow.PujaProcess.GetProgressEntryAsync(userId, stepId, cancellationToken);
        if (existing is not null)
        {
            _uow.PujaProcess.RemoveProgress(existing);
            await _uow.SaveChangesAsync(cancellationToken);
        }

        return await BuildProgressAsync(userId, step.PujaId, cancellationToken);
    }

    public async Task<PujaProgressResultDto> ResetAsync(
        Guid userId, Guid pujaId, CancellationToken cancellationToken = default)
    {
        var rows = await _uow.PujaProcess.GetProgressTrackedAsync(userId, pujaId, cancellationToken);
        foreach (var row in rows) _uow.PujaProcess.RemoveProgress(row);

        if (rows.Count > 0) await _uow.SaveChangesAsync(cancellationToken);

        return await BuildProgressAsync(userId, pujaId, cancellationToken);
    }

    private async Task<PujaStep> FindStepAsync(Guid stepId, CancellationToken ct)
    {
        // Steps are only reachable through their puja, so there is no by-id repository read;
        // the counts dictionary tells us which pujas exist and the step list is small.
        var pujas = await _uow.Pujas.GetAllWithLinksAsync(ct);
        foreach (var p in pujas)
        {
            var steps = await _uow.PujaProcess.GetStepsWithTextsAsync(p.Id, ct);
            var match = steps.FirstOrDefault(s => s.Id == stepId);
            if (match is not null) return match;
        }

        throw new NotFoundException($"Puja step '{stepId}' was not found.");
    }

    private async Task<PujaProgressResultDto> BuildProgressAsync(Guid userId, Guid pujaId, CancellationToken ct)
    {
        var steps = await _uow.PujaProcess.GetStepsWithTextsAsync(pujaId, ct);
        var done = (await _uow.PujaProcess.GetProgressAsync(userId, pujaId, ct))
            .Select(p => p.PujaStepId).ToHashSet();

        var completed = steps.Count(s => done.Contains(s.Id));

        return new PujaProgressResultDto
        {
            CompletedCount = completed,
            TotalSteps = steps.Count,
            IsCompleted = steps.Count > 0 && completed >= steps.Count,
            CurrentStepNumber = steps.OrderBy(s => s.StepNumber)
                                     .FirstOrDefault(s => !done.Contains(s.Id))?.StepNumber
        };
    }

    /// <summary>
    /// English is the base language, identified by code exactly as the localization layer does —
    /// there is no IsBase column on Language, and inventing one here would let the two drift.
    /// </summary>
    private static bool IsBaseLanguage(Language l)
        => string.Equals(l.Code, LocalizationService.BaseCode, StringComparison.OrdinalIgnoreCase);

    private static string? Trim(string? value, int max)
    {
        var t = value?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length <= max ? t : t[..max];
    }
}
