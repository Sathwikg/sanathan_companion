using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class UserFavoriteConfiguration : IEntityTypeConfiguration<UserFavorite>
{
    public void Configure(EntityTypeBuilder<UserFavorite> builder)
    {
        builder.ToTable("UserFavorites");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.FavoriteType).IsRequired().HasMaxLength(20);
        builder.Property(f => f.CreatedBy).HasMaxLength(100);
        builder.Property(f => f.ModifiedBy).HasMaxLength(100);

        // One favorite row per user per item.
        builder.HasIndex(f => new { f.UserId, f.FavoriteType, f.ItemId })
            .IsUnique().HasDatabaseName("UX_UserFavorites_User_Type_Item");
    }
}
