namespace Sanathana.Companion.Application.DTOs.Feedback;

/// <summary>What the feedback form submits.</summary>
public class SubmitFeedbackDto
{
    public Guid IssueTypeId { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>A feedback row for the dashboard.</summary>
public class FeedbackDto
{
    public Guid Id { get; set; }
    public Guid IssueTypeId { get; set; }
    public string IssueTypeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? SeekerName { get; set; }

    public DateTime SubmittedOn { get; set; }
}

public class UpdateFeedbackStatusDto
{
    public string Status { get; set; } = string.Empty;
}

/// <summary>Aggregated view of all feedback for the dashboard.</summary>
public class FeedbackDashboardDto
{
    public int Total { get; set; }
    public int New { get; set; }
    public int Reviewed { get; set; }
    public int Resolved { get; set; }

    public List<IssueTypeCountDto> ByIssueType { get; set; } = new();
    public List<FeedbackDto> Recent { get; set; } = new();
}

public class IssueTypeCountDto
{
    public string IssueTypeName { get; set; } = string.Empty;
    public int Count { get; set; }
}
