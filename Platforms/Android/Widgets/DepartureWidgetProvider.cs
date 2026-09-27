using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Net;
using Android.Util;
using Android.Widget;
using static Android.Widget.RemoteViewsService;

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

		var pendingResult = GoAsync();

		_ = RunUpdatesAsync(context, appWidgetIds, pendingResult, forceRefresh: false);
	}


	public override void OnReceive(Context? context, Intent? intent)
	{
		base.OnReceive(context, intent);

		if (context == null || intent == null)
			return;

		if (intent.Action == RefreshAction)
		{
			var widgetId = intent.GetIntExtra(AppWidgetManager.ExtraAppwidgetId, -1);

			if (widgetId != -1)
			{
				var pendingResult = GoAsync();

				// User tapped refresh explicitly - always honor it, interval gating
				// only applies to the OS's own periodic tick.
				_ = RunUpdatesAsync(context, [widgetId], pendingResult, forceRefresh: true);
			}
		}
	}


	public override void OnDeleted(Context? context, int[]? widgetIds)
	{
		if (context == null || widgetIds == null)
			return;

		foreach (var widgetId in widgetIds)
		{
			WidgetStorage.Delete(context, widgetId);
		}
	}


	private static async Task RunUpdatesAsync(
		Context context,
		int[] widgetIds,
		BroadcastReceiver.PendingResult pendingResult,
		bool forceRefresh)
	{
		global::Android.Util.Log.Debug(
			"DDeparturesWidget",
			$"RunUpdatesAsync called for {widgetIds.Length} widget(s), forceRefresh={forceRefresh}.");
		try
		{
			foreach (var widgetId in widgetIds)
			{
				if (!forceRefresh && !ShouldRefresh(context, widgetId))
				{
					global::Android.Util.Log.Debug(
						"DDeparturesWidget",
						$"Skipping widget {widgetId} - interval not yet elapsed.");
					continue;
				}

				await DepartureWidgetUpdater.UpdateAsync(context, widgetId);
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


	private static bool ShouldRefresh(Context context, int widgetId)
	{
		var settings = WidgetStorage.Load(context, widgetId);

		// Unconfigured widget - let UpdateAsync show "Select stop" rather than skip silently.
		if (string.IsNullOrWhiteSpace(settings.StopId))
			return true;

		if (settings.LastUpdate == default)
			return true;

		var elapsed = DateTime.Now - settings.LastUpdate;

		// Small tolerance so a tick landing a minute or two early (unlikely, but the
		// OS's exact firing time isn't contractually guaranteed) doesn't push the
		// widget's real refresh out to the next 30-minute slot.
		var due = TimeSpan.FromMinutes(Math.Max(settings.IntervalMinutes - 2, 1));

		return elapsed >= due;
	}
}
