namespace App.Core.Services;

/// <summary>
/// A client-side cache that belongs to the signed-in user. These services are scoped, which in a
/// WebAssembly host means they live for the whole app, so they must be cleared when the user
/// changes — otherwise one user's data would be shown to the next.
/// </summary>
public interface IUserSessionState
{
    void Reset();
}
