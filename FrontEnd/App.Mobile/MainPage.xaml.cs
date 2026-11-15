using Microsoft.AspNetCore.Components.WebView;

namespace App.Mobile;

public partial class MainPage : ContentPage
{
	/// <summary>
	/// The host BlazorWebView serves the app from this address; anything else is somebody else's
	/// site. See <see cref="Microsoft.AspNetCore.Components.WebView.Maui.BlazorWebView"/>.
	/// </summary>
	private const string AppHost = "0.0.0.0";

	public MainPage()
	{
		InitializeComponent();

		blazorWebView.UrlLoading += OnUrlLoading;
		blazorWebView.BlazorWebViewInitialized += OnWebViewInitialized;
	}

	/// <summary>
	/// Android's WebView applies the phone's Font size by itself, as a zoom on text alone that
	/// leaves the boxes around the text where they were: labels outgrow their bars and buttons. The
	/// app applies the same setting properly instead (IDeviceFontScale, as the root font size), so
	/// the WebView's own zoom is pinned at 100% or the text would be scaled twice.
	/// </summary>
	private static void OnWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
	{
#if ANDROID
		if (e.WebView?.Settings is { } settings) settings.TextZoom = 100;
		global::App.Mobile.Services.MauiNativeNavigation.WebView = e.WebView;
#elif IOS || MACCATALYST
		if (e.WebView is { } webView)
		{
			// The iPhone's own Back: swiping in from the left edge returns to the previous page, as
			// in Safari and every app built on navigation stacks. The app's history is the WebView's,
			// so this is the same Back as the arrow in the top bar.
			webView.AllowsBackForwardNavigationGestures = true;
			global::App.Mobile.Services.MauiNativeNavigation.WebView = webView;
		}
#endif
	}

	/// <summary>
	/// Android's Back button (and the back gesture). The default closed the app from any screen;
	/// now it closes what is open, goes to the previous page, and only on the home screen puts the
	/// app in the background, as every app does. The work is async, so the press is always claimed
	/// here (true) and the outcome decided once the app has answered.
	/// </summary>
	protected override bool OnBackButtonPressed()
	{
		_ = HandleBackAsync();
		return true;
	}

	private static async Task HandleBackAsync()
	{
		var services = IPlatformApplication.Current?.Services;
		var handled = false;

		try
		{
			if (services?.GetService<global::App.Core.Services.HardwareBackButton>() is { } back)
				handled = await back.PressAsync();
		}
		catch (Exception)
		{
			// The page could not answer (still starting, or tearing down): treat it as the home
			// screen, which backgrounds the app rather than doing something surprising.
		}

		if (!handled) services?.GetService<global::App.Core.Services.INativeNavigation>()?.LeaveApp();
	}

	/// <summary>
	/// Keeps other people's pages out of the app's own WebView.
	/// </summary>
	/// <remarks>
	/// This WebView holds the signed-in session: its origin is where the Blazor app lives, and
	/// letting it navigate to an arbitrary site would put that site inside the same browsing
	/// context as the app. Admin-authored chant and puja HTML can carry links — the server's
	/// sanitizer permits http, https and mailto — so this is a reachable path, not a theoretical one.
	/// <para>
	/// Opening externally also gives the seeker the thing they actually want: a real browser with an
	/// address bar, back button and their own extensions, instead of a chromeless frame with no way
	/// back except the OS gesture.
	/// </para>
	/// </remarks>
	private static void OnUrlLoading(object? sender, UrlLoadingEventArgs e)
	{
		if (string.Equals(e.Url.Host, AppHost, StringComparison.OrdinalIgnoreCase))
		{
			e.UrlLoadingStrategy = UrlLoadingStrategy.OpenInWebView;
			return;
		}

		// Hand it to the system browser / mail client rather than rendering it in here.
		e.UrlLoadingStrategy = UrlLoadingStrategy.OpenExternally;
	}
}
