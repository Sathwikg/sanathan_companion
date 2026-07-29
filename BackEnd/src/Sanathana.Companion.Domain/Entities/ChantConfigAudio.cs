using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// The audio blob for a <see cref="ChantConfig"/>, held in its own table (1:1) so that
/// listing or editing a chant never drags megabytes of audio into memory.
/// </summary>
public class ChantConfigAudio : BaseEntity
{
    /// <summary>Primary key and foreign key — one audio row per chant config.</summary>
    public Guid ChantConfigId { get; set; }

    public byte[] Data { get; set; } = Array.Empty<byte>();

    public ChantConfig? ChantConfig { get; set; }
}
