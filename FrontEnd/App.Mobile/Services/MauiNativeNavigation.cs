using App.Core.Services;

namespace App.Mobile.Services;

/// <summary>The BlazorWebView's own history, read natively. See <see cref="INativeNavigation"/>.</summary>
public sealed class MauiNativeNavigation : INativeNavigation
{
#if ANDROID
    /// <summary>Set by MainPage once the WebView exists.</summary>
    internal static global::Android.Webkit.WebView? WebView { get; set; }
#elif IOS || MACCATALYST
    internal static WebKit.WKWebView? WebView { get; set; }
#endif

    public bool IsNative => true;

    public bool CanGoBack()
    {
        try
        {
#if ANDROID
            return WebView?.CanGoBack() == true;
#elif IOS || MACCATALYST
            return WebView?.CanGoBack == true;
#else
            return false;
#endif
        }
        catch (Exception) { return false; }
    }

    public string? PreviousPath()
    {
        try
        {
#if ANDROID
            var list = WebView?.CopyBackForwardList();
            if (list is null || list.CurrentIndex < 1) return null;
            return PathOf(list.GetItemAtIndex(list.CurrentIndex - 1)?.Url);
#elif IOS || MACCATALYST
            return PathOf(WebView?.BackForwardList?.BackItem?.Url?.AbsoluteString);
#else
            return null;
#endif
        }
        catch (Exception) { return null; }
    }

    public void GoBack()
    {
        try
        {
#if ANDROID || IOS || MACCATALYST
            WebView?.GoBack();
#endif
        }
        catch (Exception) { /* nothing to go back to after all */ }
    }

    public void LeaveApp()
    {
        try
        {
#if ANDROID
            // Background, not finish(): the app stays in Recents and reopens where it was, with the
            // seeker still signed in and a half-counted mala still on screen.
            Platform.CurrentActivity?.MoveTaskToBack(true);
#endif
        }
        catch (Exception) { }
    }

    private static string? PathOf(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath.Trim('/') : null;
}
