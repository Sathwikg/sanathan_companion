using App.Core.Services;

namespace App.Mobile.Services;

/// <summary>
/// Shares an image through the system share sheet, replacing the browser path the web host uses.
/// </summary>
/// <remarks>
/// A WebView's <c>navigator.share</c> is not connected to the platform chooser on Android or iOS,
/// so the rendered bytes come across from JavaScript and go out through MAUI's Share, which raises
/// the real chooser with WhatsApp, Instagram and "Save to Photos" in it. The file is written to the
/// cache directory: the receiving app copies what it needs, and the OS may reclaim the rest.
/// </remarks>
public class MauiImageShare : IImageShare
{
    public bool IsNative => true;

    public async Task ShareAsync(byte[] bytes, string fileName, string title, string text, CancellationToken cancellationToken = default)
    {
        var dir = Path.Combine(FileSystem.CacheDirectory, "share");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);

        // The sheet has to be presented from the UI thread; Blazor may be calling from another.
        await MainThread.InvokeOnMainThreadAsync(() => Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(path)
        }));
    }
}
