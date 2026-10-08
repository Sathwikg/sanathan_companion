using App.Core.Config;
using App.Core.Services;
using App.Mobile.Services;

namespace App.Mobile;

public partial class App : Application
{
	private readonly IAppLifecycle _lifecycle;

	public App(IAppLifecycle lifecycle)
	{
		_lifecycle = lifecycle;
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Shown in the Windows title bar, the Mac menu bar and the task switcher. The stores take
		// the name from <ApplicationTitle>; both come from FrontEnd/Branding/Branding.props.
		var window = new Window(new MainPage()) { Title = Brand.AppName };

		// Drives IAppLifecycle. Shared components use it to stop periodic work while the app is
		// backgrounded — a .NET timer keeps ticking even when the WebView behind it is paused.
		if (_lifecycle is MauiAppLifecycle lifecycle)
		{
			window.Activated += (_, _) => lifecycle.SetActive(true);
			window.Deactivated += (_, _) => lifecycle.SetActive(false);
			window.Stopped += (_, _) => lifecycle.SetActive(false);
			window.Resumed += (_, _) => lifecycle.SetActive(true);
		}

		return window;
	}
}
