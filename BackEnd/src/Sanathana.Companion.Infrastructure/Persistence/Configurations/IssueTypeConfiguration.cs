using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class IssueTypeConfiguration : IEntityTypeConfiguration<IssueType>
{
    public void Configure(EntityTypeBuilder<IssueType> builder)
    {
        builder.ToTable("IssueTypes");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Name).IsRequired().HasMaxLength(150);
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.CreatedBy).HasMaxLength(100);
        builder.Property(t => t.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(t => t.Name).IsUnique().HasDatabaseName("UX_IssueTypes_Name");

        // A handful of common issue types so the feedback form works out of the box.
        builder.HasData(
            new IssueType
            {
                Id = SeedConstants.IssueTypeBugId,
                Name = "Bug / Technical Issue",
                Description = "Something in the app isn't working correctly.",
                DisplayOrder = 1,
                IsActive = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new IssueType
            {
                Id = SeedConstants.IssueTypeContentId,
                Name = "Content Correction",
                Description = "A chant, deity, festival or other detail needs correcting.",
                DisplayOrder = 2,
                IsActive = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new IssueType
            {
                Id = SeedConstants.IssueTypeFeatureId,
                Name = "Feature Request",
                Description = "Suggest a new feature or an improvement.",
                DisplayOrder = 3,
                IsActive = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new IssueType
            {
                Id = SeedConstants.IssueTypePraiseId,
                Name = "Praise / Appreciation",
                Description = "Share what you love about the app.",
                DisplayOrder = 4,
                IsActive = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new IssueType
            {
                Id = SeedConstants.IssueTypeOtherId,
                Name = "Other",
                Description = "Anything else you'd like to share.",
                DisplayOrder = 5,
                IsActive = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            });
    }
}
