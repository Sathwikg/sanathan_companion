namespace App.Core.Services;

/// <summary>
/// The arithmetic of a japa session: today's total, completed malas, and the count within the mala
/// in progress. Mirrors the API's rule exactly (SadhanaService.LogCountAsync) so the screen and the
/// saved log never disagree about when a mala completed.
/// </summary>
/// <remarks>
/// Completed malas are banked as they happen; only the mala in progress is measured against the
/// current size. That is what lets a seeker change their mala size mid-day without the change
/// inventing malas (re-dividing 115 by 11) or taking finished ones back.
/// </remarks>
public sealed class JapaCounter
{
    /// <summary>The smallest mala a seeker may set; the API enforces the same floor.</summary>
    public const int MinMalaSize = 3;

    /// <summary>The largest; a typo should not make a mala that can never complete.</summary>
    public const int MaxMalaSize = 100_000;

    public JapaCounter(int total, int malas, int inMala, int malaSize)
    {
        Total = Math.Max(0, total);
        Malas = Math.Max(0, malas);
        InMala = Math.Clamp(inMala, 0, Total);
        MalaSize = Math.Max(MinMalaSize, malaSize);
    }

    /// <summary>Repetitions today, across every mala.</summary>
    public int Total { get; private set; }

    /// <summary>Malas completed today.</summary>
    public int Malas { get; private set; }

    /// <summary>Repetitions so far in the mala in progress.</summary>
    public int InMala { get; private set; }

    public int MalaSize { get; private set; }

    /// <summary>How far through the current mala, 0 to 1. Full while a smaller new size waits for its next count.</summary>
    public double Progress => Math.Min(1d, InMala / (double)MalaSize);

    /// <summary>Counts one repetition. True when it completed a mala.</summary>
    public bool Tap()
    {
        Total++;
        InMala++;
        if (InMala < MalaSize) return false;

        // More than one only after the size was lowered below the count already in progress.
        var completed = InMala / MalaSize;
        Malas += completed;
        InMala -= completed * MalaSize;
        return true;
    }

    /// <summary>Clears the mala in progress. Completed malas stay — they are what the streak counts.</summary>
    public void ResetCurrentMala()
    {
        Total -= InMala;
        InMala = 0;
    }

    /// <summary>
    /// Applies a new mala size. The count in progress carries over; if it already meets the new
    /// size, the next repetition completes the mala.
    /// </summary>
    public void ChangeMalaSize(int malaSize) => MalaSize = Math.Clamp(malaSize, MinMalaSize, MaxMalaSize);

    public static bool IsValidMalaSize(int size) => size is >= MinMalaSize and <= MaxMalaSize;
}
