using System.ComponentModel.DataAnnotations;

namespace App.Core.Models;

public class IssueTypeModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

public class IssueTypeRequest
{
    [Required(ErrorMessage = "Issue type name is required.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SubmitFeedbackRequest
{
    [Required(ErrorMessage = "Please choose an issue type.")]
    public Guid IssueTypeId { get; set; }

    [Required(ErrorMessage = "Please describe your feedback.")]
    [StringLength(2000, ErrorMessage = "Please keep it under 2000 characters.")]
    public string Description { get; set; } = string.Empty;
}

public class FeedbackItem
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

public class IssueTypeCount
{
    public string IssueTypeName { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class FeedbackDashboardModel
{
    public int Total { get; set; }
    public int New { get; set; }
    public int Reviewed { get; set; }
    public int Resolved { get; set; }
    public List<IssueTypeCount> ByIssueType { get; set; } = new();
    public List<FeedbackItem> Recent { get; set; } = new();
}
