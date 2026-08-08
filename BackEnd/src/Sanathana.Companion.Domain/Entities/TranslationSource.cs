using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>How a registered column contributes terms to the dictionary.</summary>
public enum HarvestMode
{
    /// <summary>Each distinct column value becomes one term (Masam, DayOfWeek, DeityType…).</summary>
    WholeValue = 0,
    /// <summary>
    /// Each distinct value is split into words and each word becomes a term. This is what makes
    /// composite columns tractable — <c>TithiDetails</c> has 716 distinct values but only 59
    /// distinct words, because the rest of every value is clock times.
    /// </summary>
    Words = 1
}

/// <summary>
/// A database column the harvester scans for new vocabulary. Registering a column is what makes
/// the dictionary self-maintaining: new rows or a newly generated year surface as untranslated
/// terms with no code change.
/// </summary>
public class TranslationSource : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Table name. Resolved against the EF model before use — never interpolated into SQL as
    /// supplied, so a hostile value cannot reach the database.
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Column name, resolved against the EF model the same way.</summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>Category assigned to every term this source discovers.</summary>
    public string Category { get; set; } = "general";

    public HarvestMode Mode { get; set; } = HarvestMode.WholeValue;

    /// <summary>Safety valve so a scan of a huge table cannot run away.</summary>
    public int MaxDistinct { get; set; } = 5000;

    public bool IsActive { get; set; } = true;
}
