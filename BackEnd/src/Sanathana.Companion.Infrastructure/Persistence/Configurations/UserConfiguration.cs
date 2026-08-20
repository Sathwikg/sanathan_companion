using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.UserId);
        builder.Property(u => u.UserId).ValueGeneratedNever();
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(150);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.MobileNumber).IsRequired().HasMaxLength(20);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(200);
        builder.Property(u => u.SeekerName).HasMaxLength(150);
        builder.Property(u => u.CreatedBy).HasMaxLength(100);
        builder.Property(u => u.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UX_Users_Email");

        builder.HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Preferred region. Kept nullable — a user without one simply sees every region's content.
        builder.HasOne<Region>()
            .WithMany()
            .HasForeignKey(u => u.DefaultRegionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Seeded default administrator. Email is the literal "admin" so it can be signed in with
        // (login accepts email-or-mobile); real registrations must pass email validation, so they
        // can never collide with it.
        //
        // The account ships LOCKED — SeedConstants.AdminPasswordHash verifies against nothing. It
        // is opened once from the Admin__InitialPassword environment variable at start-up; see
        // AdminAccountBootstrapper.
        builder.HasData(new User
        {
            UserId = SeedConstants.AdminUserId,
            FullName = "Administrator",
            Email = "admin",
            MobileNumber = "0000000000",
            PasswordHash = SeedConstants.AdminPasswordHash,
            SeekerName = null,
            RoleId = SeedConstants.AdminRoleId,
            CreatedBy = "system",
            CreatedDate = SeedConstants.SeedTimestamp
        });
    }
}
