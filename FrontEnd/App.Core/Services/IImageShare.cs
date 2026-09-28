namespace App.Core.Services;

/// <summary>Hands a rendered image to the platform's share sheet. One implementation per host.</summary>
/// <remarks>
/// The web host has nothing to do here: a browser shares straight from JavaScript through the Web
/// Share API, or falls back to a download, and the bytes never need to cross into .NET. The MAUI
/// host does implement it, because a WebView's <c>navigator.share</c> is not wired to the system
/// chooser on either platform; the bytes come across from JavaScript and go out through MAUI's
/// Share, which is what puts WhatsApp, Instagram and "Save to Photos" in front of the seeker.
/// </remarks>
public interface IImageShare
{
    /// <summary>True when <see cref="ShareAsync"/> opens the platform share sheet.</summary>
    bool IsNative { get; }

    Task ShareAsync(byte[] bytes, string fileName, string title, string text, CancellationToken cancellationToken = default);
}

/// <summary>The web default: sharing happens in the browser, so there is nothing to do here.</summary>
public sealed class BrowserImageShare : IImageShare
{
    public bool IsNative => false;

    public Task ShareAsync(byte[] bytes, string fileName, string title, string text, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
