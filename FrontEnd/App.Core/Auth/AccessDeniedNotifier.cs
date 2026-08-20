namespace App.Core.Auth;

/// <summary>
/// Raised when the API refuses a request because the caller's role is not granted that form.
/// </summary>
/// <remarks>
/// Deliberately NOT latched the way <see cref="SessionExpiredNotifier"/> is. An expired session
/// happens once and ends everything; a module denial is per-request and recoverable — the seeker
/// can navigate somewhere they do have access to and carry on — so latching would silence every
/// denial after the first for the rest of the session.
/// </remarks>
public class AccessDeniedNotifier
{
    public event Action? Denied;

    public void Raise() => Denied?.Invoke();
}
