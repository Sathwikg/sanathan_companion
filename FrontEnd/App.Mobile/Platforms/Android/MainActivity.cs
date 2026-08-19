using Android.App;
using Android.Content.PM;
using Android.Views;
using AWebkit = Android.Webkit;

namespace App.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    /// <summary>
    /// Stops the WebView doing work the seeker cannot see.
    /// </summary>
    /// <remarks>
    /// MAUI does not pause the Android WebView when the activity pauses, and a Blazor Hybrid app is
    /// entirely a WebView — so without this every JS timer, every CSS animation and the whole
    /// compositor keep running behind the lock screen. This app has enough of each to matter: the
    /// notification bell swings on a loop, the loader spins, and the dashboard rotates deities.
    /// <para>
    /// PauseTimers is process-wide (there is one WebView here) and stops JavaScript and animation
    /// timers; OnPause additionally halts drawing and media for this instance. Both are reversed on
    /// resume, and Blazor's own circuit is untouched — the .NET side is not suspended, only the
    /// browser engine hosting it.
    /// </para>
    /// </remarks>
    protected override void OnPause()
    {
        base.OnPause();
        WithWebView(webView =>
        {
            webView.OnPause();
            webView.PauseTimers();
        });
    }

    protected override void OnResume()
    {
        base.OnResume();
        WithWebView(webView =>
        {
            webView.ResumeTimers();
            webView.OnResume();
        });
    }

    /// <summary>
    /// Runs an action against the hosted WebView, if it has been created yet. Lifecycle callbacks
    /// arrive before and after the view tree exists, so every lookup has to tolerate not finding it.
    /// </summary>
    private void WithWebView(Action<AWebkit.WebView> action)
    {
        try
        {
            var root = Window?.DecorView?.RootView as ViewGroup;
            if (root is null) return;

            var webView = FindWebView(root);
            if (webView is not null) action(webView);
        }
        catch (Exception)
        {
            // Pausing is an optimisation. Never let it take the activity down.
        }
    }

    private static AWebkit.WebView? FindWebView(ViewGroup group)
    {
        for (var i = 0; i < group.ChildCount; i++)
        {
            var child = group.GetChildAt(i);
            if (child is AWebkit.WebView webView) return webView;
            if (child is ViewGroup nested && FindWebView(nested) is { } found) return found;
        }

        return null;
    }
}
