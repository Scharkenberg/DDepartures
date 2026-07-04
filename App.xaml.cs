namespace DDepartures
{
	public partial class App : Application
	{
		public App()
		{
			InitializeComponent();
		}

		protected override Window CreateWindow(IActivationState? activationState)
		{
			var window = new Window(new AppShell())
			{
#if WINDOWS
				Height = 600,
				Width = 400,
				MaximumHeight = 960,
				MaximumWidth = 720,
#endif
				MinimumHeight = 320,
				MinimumWidth = 320
			};
			return window;
		}
	}
}