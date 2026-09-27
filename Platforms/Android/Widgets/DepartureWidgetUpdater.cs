using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using AndroidResource = global::DDepartures.Resource;

namespace DDepartures.Platforms.Android.Widgets;

public static class DepartureWidgetUpdater
{
	public static async Task UpdateAsync(
		Context context,
		int widgetId)
	{
		global::Android.Util.Log.Debug(
			"DDeparturesWidget",
			$"UpdateAsync called for widget {widgetId}");

		try
		{
			var manager = AppWidgetManager.GetInstance(context);

			var views = new RemoteViews(
				context.PackageName,
				AndroidResource.Layout.departure_widget);

			var settings = WidgetStorage.Load(context, widgetId);

			AttachRefreshButton(context, views, widgetId);
			AttachTitleClick(context, views, widgetId);
			AttachRowClickTemplate(context, views, widgetId, settings);
			AttachConfigurationButton(context, views, widgetId);
			AttachRemoteAdapter(context, views, widgetId);

			if (string.IsNullOrWhiteSpace(settings.StopId))
			{
				views.SetTextViewText(AndroidResource.Id.widgetTitle, "Select stop");

				settings.Departures = [];
				WidgetStorage.Save(context, widgetId, settings);
				manager.NotifyAppWidgetViewDataChanged(widgetId, AndroidResource.Id.widgetBody);
				manager.UpdateAppWidget(widgetId, views);
				return;
			}

			views.SetTextViewText(AndroidResource.Id.widgetTitle, settings.StopName);

			try
			{
				using var service = new RestService();
				try
				{
					await service.RefreshDataAsync(settings.StopId);
				}
				catch (Exception ex)
				{
					global::Android.Util.Log.Error(
						"DDeparturesWidget",
						$"Error while updating widget {widgetId}: {ex}");
				}

				var departures = service.DepItems?.Take(30).ToList() ?? [];

				if (departures.Count == 0)
				{
					// Single error row - the factory's GetCount() reflects this list's
					// length, so no more manual 6-row padding/clearing.
					settings.Departures =
					[
						new DepartureRow
						{
							Line = "Error",
							Mot = "",
							Destination = service.StatusResponse,
							Time = ""
						}
					];
				}
				else
				{
					settings.Departures = departures;
					settings.LastUpdate = DateTime.Now;
				}

				WidgetStorage.Save(context, widgetId, settings);

				views.SetTextViewText(
					AndroidResource.Id.widgetTitle,
					settings.LastUpdate == default
						? settings.StopName
						: $"{settings.LastUpdate:HH:mm} {settings.StopName}");
			}
			catch (Exception ex)
			{
				settings.Departures =
				[
					new DepartureRow { Line = "Error", Mot = "", Destination = ex.Message, Time = "" }
				];
				WidgetStorage.Save(context, widgetId, settings);
			}

			// Must come after WidgetStorage.Save - OnDataSetChanged() in the factory
			// reads from storage synchronously when this fires.
			manager.NotifyAppWidgetViewDataChanged(widgetId, AndroidResource.Id.widgetBody);
			manager.UpdateAppWidget(widgetId, views);
		}
		catch (Exception ex)
		{
			global::Android.Util.Log.Error("DDeparturesWidget", ex.ToString());
		}
	}

	private static void AttachRemoteAdapter(
		Context context,
		RemoteViews views,
		int widgetId)
	{
		var intent = new Intent(context, typeof(DepartureRowService));
		intent.PutExtra(AppWidgetManager.ExtraAppwidgetId, widgetId);
		intent.SetData(global::Android.Net.Uri.Parse($"content://widget/{widgetId}"));

		views.SetRemoteAdapter(AndroidResource.Id.widgetBody, intent);
	}

	private static void AttachRowClickTemplate(
	Context context,
	RemoteViews views,
	int widgetId,
	WidgetSettings settings)
	{
		var intent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName);
		if (intent == null)
			return;

		intent.PutExtra("stopId", settings.StopId);
		intent.PutExtra("stopName", settings.StopName);

		var template = PendingIntent.GetActivity(
			context, widgetId, intent,
			PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

		views.SetPendingIntentTemplate(AndroidResource.Id.widgetBody, template);
	}

	private static void AttachRefreshButton(Context context, RemoteViews views, int widgetId)
	{
		var intent = new Intent(context, typeof(DepartureWidgetProvider));
		intent.SetAction("com.ddepartures.widget.REFRESH");
		intent.PutExtra(AppWidgetManager.ExtraAppwidgetId, widgetId);

		var pendingIntent = PendingIntent.GetBroadcast(
			context,
			widgetId,
			intent,
			PendingIntentFlags.Immutable | PendingIntentFlags.CancelCurrent);

		views.SetOnClickPendingIntent(AndroidResource.Id.widgetRefresh, pendingIntent);
	}

	private static void AttachConfigurationButton(Context context, RemoteViews views, int widgetId)
	{
		var intent = new Intent(context, typeof(DepartureWidgetConfigurationActivity));
		intent.PutExtra(AppWidgetManager.ExtraAppwidgetId, widgetId);

		var pendingIntent = PendingIntent.GetActivity(
			context,
			widgetId + 20000,
			intent,
			PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

		views.SetOnClickPendingIntent(AndroidResource.Id.widgetTitle, pendingIntent);
	}

	private static void AttachTitleClick(Context context, RemoteViews views, int widgetId)
	{
		var intent = new Intent(context, typeof(DepartureWidgetConfigurationActivity));
		intent.PutExtra(AppWidgetManager.ExtraAppwidgetId, widgetId);

		var pending = PendingIntent.GetActivity(
			context,
			widgetId + 10000,
			intent,
			PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

		views.SetOnClickPendingIntent(AndroidResource.Id.widgetTitle, pending);
	}
}