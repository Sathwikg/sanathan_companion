using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class SadhanaLogConfiguration : IEntityTypeConfiguration<SadhanaLog>
{
    public void Configure(EntityTypeBuilder<SadhanaLog> builder)
    {
        builder.ToTable("SadhanaLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.ChantName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DeityName).HasMaxLength(150);
        builder.Property(x => x.CategoryName).HasMaxLength(150);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // One log per user per day per chant.
        builder.HasIndex(x => new { x.UserId, x.Date, x.ChantConfigId }).IsUnique().HasDatabaseName("UX_SadhanaLogs_User_Date_Chant");
        builder.HasIndex(x => new { x.UserId, x.Date }).HasDatabaseName("IX_SadhanaLogs_User_Date");

        builder.HasOne(x => x.ChantConfig)
            .WithMany()
            .HasForeignKey(x => x.ChantConfigId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserChantSettingConfiguration : IEntityTypeConfiguration<UserChantSetting>
{
    public void Configure(EntityTypeBuilder<UserChantSetting> builder)
    {
        builder.ToTable("UserChantSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        // No HasDefaultValue(true) on VibrationEnabled: EF would then treat false as "unset" and
        // let the database default turn a seeker's "off" back on. The entity initialiser is the default.

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // One settings row per user per chant. Plain ids, no foreign keys — the same shape as
        // UserFavorites; UserRepository.PurgeUserDataAsync removes the rows with the account.
        builder.HasIndex(x => new { x.UserId, x.ChantConfigId })
            .IsUnique().HasDatabaseName("UX_UserChantSettings_User_Chant");
    }
}

public class SadhanaStreakConfiguration : IEntityTypeConfiguration<SadhanaStreak>
{
    public void Configure(EntityTypeBuilder<SadhanaStreak> builder)
    {
        builder.ToTable("SadhanaStreaks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // One streak row per user.
        builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("UX_SadhanaStreaks_User");
    }
}
