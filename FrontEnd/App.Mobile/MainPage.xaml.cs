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
