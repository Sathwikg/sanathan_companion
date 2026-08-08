using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Infrastructure.Persistence.Configurations;

public class TranslationSourceConfiguration : IEntityTypeConfiguration<TranslationSource>
{
    public void Configure(EntityTypeBuilder<TranslationSource> builder)
    {
        builder.ToTable("TranslationSources");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TableName).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ColumnName).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(60);
        builder.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);

        builder.HasIndex(x => new { x.TableName, x.ColumnName })
            .IsUnique().HasDatabaseName("UX_TranslationSources_Table_Column");

        builder.HasData(BuildSeed());
    }

    /// <summary>
    /// The columns worth scanning. Panchangam supplies the bulk of the vocabulary; the rest are
    /// small controlled sets whose values repeat across the app (a "Monday" harvested from
    /// <c>Days.Name</c> also translates the 208 occurrences in <c>Panchangams.DayOfWeek</c>).
    /// </summary>
    private static IEnumerable<TranslationSource> BuildSeed()
    {
        var i = 0;
        TranslationSource Row(string table, string column, string category, HarvestMode mode) => new()
        {
            // Deterministic ids so the seed never drifts between environments.
            Id = new Guid($"7c000000-0000-0000-0000-{++i:D12}"),
            TableName = table,
            ColumnName = column,
            Category = category,
            Mode = mode,
            MaxDistinct = 5000,
            IsActive = true,
            CreatedBy = "system",
            CreatedDate = SeedConstants.SeedTimestamp
        };

        // Low-cardinality Panchangam columns: the whole value is one term.
        yield return Row("Panchangams", "DayOfWeek", "panchangam", HarvestMode.WholeValue);
        yield return Row("Panchangams", "TeluguSamvatsaram", "panchangam", HarvestMode.WholeValue);
        yield return Row("Panchangams", "Ayanam", "panchangam", HarvestMode.WholeValue);
        yield return Row("Panchangams", "Masam", "panchangam", HarvestMode.WholeValue);
        yield return Row("Panchangams", "Paksham", "panchangam", HarvestMode.WholeValue);
        yield return Row("Panchangams", "Rutuvu", "panchangam", HarvestMode.WholeValue);

        // Composite columns: only the words are vocabulary, the times are not.
        yield return Row("Panchangams", "TithiDetails", "panchangam", HarvestMode.Words);
        yield return Row("Panchangams", "NakshatramDetails", "panchangam", HarvestMode.Words);
        yield return Row("Panchangams", "AmruthaKalam", "panchangam", HarvestMode.Words);
        yield return Row("Panchangams", "AbhijitMuhurtham", "panchangam", HarvestMode.Words);
        yield return Row("Panchangams", "Durmuhurtham", "panchangam", HarvestMode.Words);
        yield return Row("Panchangams", "RahuKalam", "panchangam", HarvestMode.Words);
        yield return Row("Panchangams", "Yamagandam", "panchangam", HarvestMode.Words);
        yield return Row("Panchangams", "Varjyam", "panchangam", HarvestMode.Words);
        yield return Row("Panchangams", "Gulika", "panchangam", HarvestMode.Words);

        // Master data whose names are shown all over the app.
        yield return Row("Days", "Name", "day", HarvestMode.WholeValue);
        yield return Row("Deities", "DeityType", "deityType", HarvestMode.WholeValue);
        yield return Row("Deities", "Name", "deity", HarvestMode.WholeValue);
        yield return Row("Chants", "Name", "chantCategory", HarvestMode.WholeValue);
        yield return Row("Festivals", "Name", "festival", HarvestMode.WholeValue);
        yield return Row("Regions", "Name", "region", HarvestMode.WholeValue);
        yield return Row("IssueTypes", "Name", "issueType", HarvestMode.WholeValue);
        yield return Row("Feedbacks", "Status", "status", HarvestMode.WholeValue);
        yield return Row("NotificationConfigs", "Title", "notification", HarvestMode.WholeValue);
    }
}
