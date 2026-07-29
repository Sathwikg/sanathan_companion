using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class ModuleRoleMappingConfiguration : IEntityTypeConfiguration<ModuleRoleMapping>
{
    public void Configure(EntityTypeBuilder<ModuleRoleMapping> builder)
    {
        builder.ToTable("ModuleRoleMappings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // One access row per role per form.
        builder.HasIndex(x => new { x.RoleId, x.MenuModuleId })
            .IsUnique().HasDatabaseName("UX_ModuleRoleMappings_Role_Module");

        // Deleting a form removes its access rows; a role in use cannot be deleted.
        builder.HasOne(x => x.MenuModule)
            .WithMany()
            .HasForeignKey(x => x.MenuModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Default access for the Sanathan (seeker) role — the devotee-facing forms only.
        // Admin needs no rows (it always has full access); admins can adjust these on the Access Rights form.
        builder.HasData(
            new ModuleRoleMapping
            {
                Id = SeedConstants.SanathanDashboardAccessId,
                RoleId = SeedConstants.SanathanRoleId,
                MenuModuleId = SeedConstants.DashboardModuleId,
                WebEnabled = true,
                MobileEnabled = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new ModuleRoleMapping
            {
                Id = SeedConstants.SanathanSadhanaAccessId,
                RoleId = SeedConstants.SanathanRoleId,
                MenuModuleId = SeedConstants.TodaysSadhanaMenuId,
                WebEnabled = true,
                MobileEnabled = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new ModuleRoleMapping
            {
                Id = SeedConstants.SanathanPanchangamAccessId,
                RoleId = SeedConstants.SanathanRoleId,
                MenuModuleId = SeedConstants.PanchangamMenuId,
                WebEnabled = true,
                MobileEnabled = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new ModuleRoleMapping
            {
                Id = SeedConstants.SanathanFeedbackAccessId,
                RoleId = SeedConstants.SanathanRoleId,
                MenuModuleId = SeedConstants.FeedbackFormMenuId,
                WebEnabled = true,
                MobileEnabled = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new ModuleRoleMapping
            {
                Id = SeedConstants.SanathanFavoritesAccessId,
                RoleId = SeedConstants.SanathanRoleId,
                MenuModuleId = SeedConstants.FavoritesMenuId,
                WebEnabled = true,
                MobileEnabled = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            },
            new ModuleRoleMapping
            {
                Id = SeedConstants.SanathanNotificationsAccessId,
                RoleId = SeedConstants.SanathanRoleId,
                MenuModuleId = SeedConstants.MyNotificationsMenuId,
                WebEnabled = true,
                MobileEnabled = true,
                CreatedBy = "system",
                CreatedDate = SeedConstants.SeedTimestamp
            });
    }
}
