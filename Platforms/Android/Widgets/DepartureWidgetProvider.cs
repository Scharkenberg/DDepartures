using Android.App;
using Android.Appwidget;
using Android.Content;

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
		if (context == null || appWidgetIds == null)
			return;


		foreach (var widgetId in appWidgetIds)
		{
			_ = DepartureWidgetUpdater.UpdateAsync(
				context,
				widgetId);
		}
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
				_ = DepartureWidgetUpdater.UpdateAsync(
					context,
					widgetId);
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
}