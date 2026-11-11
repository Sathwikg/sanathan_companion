using Android.App;
using Android.Runtime;

namespace App.Mobile;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	/// <summary>
	/// Creates the notification channels as soon as the process starts. A push that arrives while
	/// the app is closed is drawn by Google Play services, which can only use a channel that already
	/// exists; without this the first push after an install would land in a generic
	/// "Miscellaneous" channel and stay there.
	/// </summary>
	public override void OnCreate()
	{
		base.OnCreate();
		global::App.Mobile.Platforms.Android.Notifications.AppNotifications.EnsureChannels(this);
	}
}
