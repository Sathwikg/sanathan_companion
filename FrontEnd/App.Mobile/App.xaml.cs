namespace App.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Shown in the Windows title bar, the Mac menu bar and the task switcher. The stores take
		// the name from <ApplicationTitle>; this is the one place it also has to be spelled out.
		return new Window(new MainPage()) { Title = "Sanathan Companion" };
	}
}
