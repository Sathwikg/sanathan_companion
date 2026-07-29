using Sanathana.Companion.Application.DTOs.Feedback;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Application.Validators;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Tests;

public class FeedbackServiceTests
{
    private static FeedbackService NewService(TestHarness h)
        => new(h.UnitOfWork, new SubmitFeedbackValidator());

    [Fact]
    public async Task Submit_saves_feedback_and_dashboard_reflects_it()
    {
        using var harness = new TestHarness();
        var service = NewService(harness);

        var id = await service.SubmitAsync(SeedConstants.AdminUserId, new SubmitFeedbackDto
        {
            IssueTypeId = SeedConstants.IssueTypeBugId,
            Description = "The audio doesn't play on the chant screen."
        });

        Assert.NotEqual(Guid.Empty, id);

        var mine = Assert.Single(await service.GetAllAsync());
        Assert.Equal("Bug / Technical Issue", mine.IssueTypeName);
        Assert.Equal("New", mine.Status);
        Assert.Equal(SeedConstants.AdminUserId, mine.UserId);

        var dash = await service.GetDashboardAsync();
        Assert.Equal(1, dash.Total);
        Assert.Equal(1, dash.New);
        Assert.Contains(dash.ByIssueType, x => x.IssueTypeName == "Bug / Technical Issue" && x.Count == 1);
    }

    [Fact]
    public async Task Submit_rejects_an_unknown_issue_type()
    {
        using var harness = new TestHarness();
        var service = NewService(harness);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.SubmitAsync(SeedConstants.AdminUserId, new SubmitFeedbackDto
            {
                IssueTypeId = Guid.NewGuid(),
                Description = "orphaned"
            }));
    }

    [Fact]
    public async Task UpdateStatus_is_case_insensitive_and_updates_the_dashboard()
    {
        using var harness = new TestHarness();
        var service = NewService(harness);

        var id = await service.SubmitAsync(SeedConstants.AdminUserId, new SubmitFeedbackDto
        {
            IssueTypeId = SeedConstants.IssueTypeFeatureId,
            Description = "Add a dark-mode toggle to the profile."
        });

        await service.UpdateStatusAsync(id, "reviewed");

        var dash = await service.GetDashboardAsync();
        Assert.Equal(0, dash.New);
        Assert.Equal(1, dash.Reviewed);
    }
}
