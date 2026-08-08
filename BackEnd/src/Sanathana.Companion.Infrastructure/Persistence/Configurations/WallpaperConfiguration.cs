using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class WallpaperConfiguration : IEntityTypeConfiguration<Wallpaper>
{
    public void Configure(EntityTypeBuilder<Wallpaper> builder)
    {
        builder.ToTable("Wallpapers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Title).HasMaxLength(150);
        builder.Property(x => x.ImageData).IsRequired();
        builder.Property(x => x.ImageContentType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        // The gallery always filters by deity, then orders.
        builder.HasIndex(x => new { x.DeityId, x.DisplayOrder })
            .HasDatabaseName("IX_Wallpapers_Deity_Order");

        // A wallpaper has no meaning without its deity, so deleting the deity takes them with it.
        builder.HasOne(x => x.Deity)
            .WithMany()
            .HasForeignKey(x => x.DeityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
