using App.Core.Models;

namespace App.Core.Services;

/// <summary>A finished mala, as the japa screen saw it, for the page that celebrates it.</summary>
public sealed record JapaCompletion(
    Guid ChantId,
    string ChantName,
    int MalaSize,
    int MalasToday,
    int CountToday,
    SadhanaStreak? Streak,
    bool StreakSecuredToday);

/// <summary>
/// Hands the completed mala from the japa screen to the completion page, so that page opens
/// instantly with nothing to fetch — the save that completed the mala already returned the streak.
/// </summary>
/// <remarks>
/// Only a shortcut: opened directly (a reload, a link), the completion page fetches the same figures
/// from the API. A session state because it describes one seeker's practice.
/// </remarks>
public sealed class JapaCompletionState : IUserSessionState
{
    public JapaCompletion? Last { get; private set; }

    public void Set(JapaCompletion completion) => Last = completion;

    /// <summary>The completion for <paramref name="chantId"/>, consumed so a later visit refetches.</summary>
    public JapaCompletion? Take(Guid chantId)
    {
        var last = Last;
        if (last is null || last.ChantId != chantId) return null;
        Last = null;
        return last;
    }

    public void Reset() => Last = null;
}
