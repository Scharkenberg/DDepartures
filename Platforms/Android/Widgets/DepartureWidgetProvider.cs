using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Net;
using Android.Util;

namespace DDepartures.Platforms.Android.Widgets;

[BroadcastReceiver(
	Label = "DDepartures",
	Exported = true)]
[IntentFilter(new[]
{
	AppWidgetManager.ActionAppwidgetUpdate,
	"com.ddepartures.widget.REFRESH"
})]
[MetaData(
	AppWidgetManager.MetaDataAppwidgetProvider,
	Resource = "@xml/departure_widget_provider")]
public class DepartureWidgetProvider : AppWidgetProvider
{
	private const string RefreshAction =
		"com.ddepartures.widget.REFRESH";


	public override void OnUpdate(
		Context? context,
		AppWidgetManager? appWidgetManager,
		int[]? appWidgetIds)
	{
		global::Android.Util.Log.Debug(
			"DDeparturesWidget",
			$"OnUpdate called for {appWidgetIds?.Length ?? 0} widget(s).");
		if (context == null || appWidgetIds == null)
			return;

		// GoAsync extends the receiver's lifetime past this method returning, so the
		// OS won't kill the process mid-fetch before UpdateAsync's HTTP call and
		// RemoteViews update actually complete.
		var pendingResult = GoAsync();

		_ = RunUpdatesAsync(context, appWidgetIds, pendingResult);
	}


	public override void OnReceive(
		Context? context,
		Intent? intent)
	{
		base.OnReceive(context, intent);


		if (context == null || intent == null)
			return;


		if (intent.Action == RefreshAction)
		{
			var widgetId =
				intent.GetIntExtra(
					AppWidgetManager.ExtraAppwidgetId,
					-1);


			if (widgetId != -1)
			{
				var pendingResult = GoAsync();

				_ = RunUpdatesAsync(context, [widgetId], pendingResult);
			}
		}
	}


	public override void OnDeleted(
		Context? context,
		int[]? widgetIds)
	{
		if (context == null || widgetIds == null)
			return;


		foreach (var widgetId in widgetIds)
		{
			WidgetStorage.Delete(
				context,
				widgetId);
		}
	}


	private static async Task RunUpdatesAsync(
		Context context,
		int[] widgetIds,
		BroadcastReceiver.PendingResult pendingResult)
	{
		global::Android.Util.Log.Debug(
			"DDeparturesWidget",
			$"RunUpdatesAsync called for {widgetIds.Length} widget(s).");
		try
		{
			foreach (var widgetId in widgetIds)
			{
				var cm = (ConnectivityManager)context.GetSystemService(Context.ConnectivityService)!;

				var network = cm.ActiveNetwork;
				var caps = cm.GetNetworkCapabilities(network);

				Log.Debug("DDeparturesWidget", $"Network={network}");
				Log.Debug("DDeparturesWidget", $"HasInternet={caps?.HasCapability(NetCapability.Internet)}");
				Log.Debug("DDeparturesWidget", $"Validated={caps?.HasCapability(NetCapability.Validated)}");
				Log.Debug("DDeparturesWidget", $"HasDns={caps?.HasCapability(NetCapability.NotRestricted)}");
				Log.Debug("DDeparturesWidget",	$"PID={(global::Android.OS.Process.MyPid())}");
				Log.Debug("DDeparturesWidget",	$"Thread={Environment.CurrentManagedThreadId}");
				await DepartureWidgetUpdater.UpdateAsync(
					context,
					widgetId);
			}
		}
		catch (Exception ex)
		{
			global::Android.Util.Log.Error("DDeparturesWidget", ex.ToString());
		}
		finally
		{
			pendingResult.Finish();
		}
	}
}