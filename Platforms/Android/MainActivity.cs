using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace DDepartures;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);

		ProcessWidgetIntent(Intent);
	}


	protected override void OnNewIntent(Intent? intent)
	{
		base.OnNewIntent(intent);

		ProcessWidgetIntent(intent);
	}


	private static void ProcessWidgetIntent(Intent? intent)
	{
		if (intent == null)
			return;

		var stopId =
			intent.GetStringExtra("stopId");

		if (!string.IsNullOrWhiteSpace(stopId))
		{
			Platforms.Android.WidgetLaunchRequest.PendingStopId = stopId;
		}
	}
}