using FluentValidation;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Feedback;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class FeedbackService : IFeedbackService
{
    private const int RecentCount = 10;

    private readonly IUnitOfWork _uow;
    private readonly IValidator<SubmitFeedbackDto> _submitValidator;

    public FeedbackService(IUnitOfWork uow, IValidator<SubmitFeedbackDto> submitValidator)
    {
        _uow = uow;
        _submitValidator = submitValidator;
    }

    public async Task<Guid> SubmitAsync(Guid userId, SubmitFeedbackDto dto, CancellationToken cancellationToken = default)
    {
        await _submitValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var issueType = await _uow.IssueTypes.GetByIdAsync(dto.IssueTypeId, cancellationToken);
        if (issueType is null || !issueType.IsActive)
            throw new BadRequestException("Please choose a valid issue type.");

        var entity = new Feedback
        {
            Id = Guid.NewGuid(),
            IssueTypeId = dto.IssueTypeId,
            UserId = userId,
            Description = dto.Description.Trim(),
            Status = FeedbackStatuses.New
        };

        await _uow.Feedbacks.AddAsync(entity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<IReadOnlyList<FeedbackDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var feedbacks = await _uow.Feedbacks.GetAllWithTypeAsync(cancellationToken);
        var users = (await _uow.Users.GetAllWithRolesAsync(cancellationToken)).ToDictionary(u => u.UserId, u => u);
        return feedbacks.Select(f => ToDto(f, users)).ToList();
    }

    public async Task<FeedbackDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var feedbacks = await _uow.Feedbacks.GetAllWithTypeAsync(cancellationToken);
        var users = (await _uow.Users.GetAllWithRolesAsync(cancellationToken)).ToDictionary(u => u.UserId, u => u);

        return new FeedbackDashboardDto
        {
            Total = feedbacks.Count,
            New = feedbacks.Count(f => f.Status == FeedbackStatuses.New),
            Reviewed = feedbacks.Count(f => f.Status == FeedbackStatuses.Reviewed),
            Resolved = feedbacks.Count(f => f.Status == FeedbackStatuses.Resolved),
            ByIssueType = feedbacks
                .GroupBy(f => f.IssueType?.Name ?? "Unknown")
                .OrderByDescending(g => g.Count())
                .Select(g => new IssueTypeCountDto { IssueTypeName = g.Key, Count = g.Count() })
                .ToList(),
            Recent = feedbacks.Take(RecentCount).Select(f => ToDto(f, users)).ToList()
        };
    }

    public async Task UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        if (!FeedbackStatuses.IsValid(status))
            throw new BadRequestException($"'{status}' is not a valid feedback status.");

        var entity = await _uow.Feedbacks.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Feedback '{id}' was not found.");

        // Normalise to the canonical casing.
        entity.Status = FeedbackStatuses.All.First(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
        _uow.Feedbacks.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private static FeedbackDto ToDto(Feedback f, IReadOnlyDictionary<Guid, User> users)
    {
        users.TryGetValue(f.UserId, out var user);
        return new FeedbackDto
        {
            Id = f.Id,
            IssueTypeId = f.IssueTypeId,
            IssueTypeName = f.IssueType?.Name ?? string.Empty,
            Description = f.Description,
            Status = f.Status,
            UserId = f.UserId,
            UserName = user?.FullName ?? "Unknown",
            SeekerName = user?.SeekerName,
            SubmittedOn = f.CreatedDate
        };
    }
}
