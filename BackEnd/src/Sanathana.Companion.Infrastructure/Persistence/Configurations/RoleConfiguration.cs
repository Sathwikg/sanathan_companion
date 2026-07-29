using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.RoleId);
        builder.Property(r => r.RoleId).ValueGeneratedOnAdd();
        builder.Property(r => r.RoleName).IsRequired().HasMaxLength(50);
        builder.Property(r => r.Description).HasMaxLength(250);
        builder.Property(r => r.CreatedBy).HasMaxLength(100);
        builder.Property(r => r.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(r => r.RoleName).IsUnique().HasDatabaseName("UX_Roles_RoleName");

        builder.HasData(
            new Role
            {
                RoleId = SeedConstants.AdminRoleId,
                RoleName = "Admin",
                Description = "System administrator with full privileges",
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new Role
            {
                RoleId = SeedConstants.SanathanRoleId,
                RoleName = "Sanathan",
                Description = "Spiritual seeker role aligned with Hindu Dharma values",
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            });
    }
}
