using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Graphics;
using Android.Widget;
using static Android.Icu.Text.CaseMap;
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
			var manager =
				AppWidgetManager.GetInstance(context);


			var views = new RemoteViews(
				context.PackageName,
				AndroidResource.Layout.departure_widget);

			var settings =
				WidgetStorage.Load(
					context,
					widgetId);

			AttachRefreshButton(context, views, widgetId);
			AttachTitleClick(context, views, widgetId);
			AttachBodyClick(context, views, widgetId, settings.StopId);
			AttachLaunchButton(context, views, widgetId, settings.StopId);
			AttachConfigurationButton(context, views, widgetId);

			if (string.IsNullOrWhiteSpace(settings.StopId))
			{
				views.SetTextViewText(
					AndroidResource.Id.widgetTitle,
					"Select stop");

				ClearDepartures(views);

				manager.UpdateAppWidget(
					widgetId,
					views);

				return;
			}


			views.SetTextViewText(
				AndroidResource.Id.widgetTitle,
				settings.StopName);


			try
			{
				using var service = new RestService();
				try {
					await service.RefreshDataAsync(
						settings.StopId);
				}
				catch (Exception ex)
				{
					global::Android.Util.Log.Error(
						"DDeparturesWidget",
						$"Error while updating widget {widgetId}: {ex}");
				}

				var departures =
					service.DepItems?
						.Take(6)
						.ToList()
					?? [];

				if (departures.Count == 0)
				{
					// Covers both transport-level failures and VVO's application-level
					// errors (e.g. Status.Code "InvalidRequest"/"NoData") - either way,
					// RefreshDataAsync has already put a human-readable reason here.
					UpdateDepartureRow(views, 0, new DepartureRow
					{
						Line = "Error",
						Mot = "",
						Destination = service.StatusResponse,
						Time = ""
					});
					for (int i = 1; i < 6; i++)
					{
						UpdateDepartureRow(views, i, new DepartureRow
						{
							Line = "Error",
							Mot = "",
							Destination = service.StatusResponse[(60 * i)..],
							Time = ""
						});
					}
				}
				else
				{
					settings.LastUpdate = DateTime.Now;

					WidgetStorage.Save(context,	widgetId, settings);
					for (int i = 0; i < 6; i++)
					{
						if (i < departures.Count)
						{
							UpdateDepartureRow(views, i, departures[i]);
						}
						else
						{
							ClearDepartureRow(views, i);
						}
					}
					if (settings.LastUpdate == default)
					{
						views.SetTextViewText(AndroidResource.Id.widgetTitle, settings.StopName);
					}
					else
					{
						views.SetTextViewText(AndroidResource.Id.widgetTitle, $"({settings.LastUpdate:HH:mm}) {settings.StopName}");
					}
				}
			}
			catch (Exception ex)
			{
				UpdateDepartureRow(views, 0, new DepartureRow
				{
					Line = "Error",
					Mot = "",
					Destination = ex.Message,
					Time = ""
				});

				for (int i = 1; i < 6; i++)
				{
					UpdateDepartureRow(views, i, new DepartureRow
					{
						Line = "Error",
						Mot = "",
						Destination = ex.Message[(60 * i)..],
						Time = ""
					});
				}
			}


			manager.UpdateAppWidget(
				widgetId,
				views);
		}
		catch (Exception ex)
		{
			global::Android.Util.Log.Error("DDeparturesWidget", ex.ToString());
		}
	}

	private static void ClearDepartures(
	RemoteViews views)
	{
		for (int i = 0; i < 6; i++)
		{
			foreach (var part in new[]
			{
			"line",
			"mode",
			"destination",
			"time"
		})
			{
				views.SetTextViewText(
					GetDepartureRowViewId(i, part),
					string.Empty);
			}
		}
	}

	private static void ClearDepartureRow(
	RemoteViews views,
	int index)
	{
		foreach (var part in new[]
		{
		"line",
		"mode",
		"destination",
		"time"
	})
		{
			views.SetTextViewText(
				GetDepartureRowViewId(index, part),
				string.Empty);
		}
	}

	private static int GetDepartureRowViewId(
	int index,
	string part)
	{
		return (index, part) switch
		{
			(0, "line") => AndroidResource.Id.line1,
			(0, "mode") => AndroidResource.Id.mode1,
			(0, "destination") => AndroidResource.Id.destination1,
			(0, "time") => AndroidResource.Id.time1,

			(1, "line") => AndroidResource.Id.line2,
			(1, "mode") => AndroidResource.Id.mode2,
			(1, "destination") => AndroidResource.Id.destination2,
			(1, "time") => AndroidResource.Id.time2,

			(2, "line") => AndroidResource.Id.line3,
			(2, "mode") => AndroidResource.Id.mode3,
			(2, "destination") => AndroidResource.Id.destination3,
			(2, "time") => AndroidResource.Id.time3,

			(3, "line") => AndroidResource.Id.line4,
			(3, "mode") => AndroidResource.Id.mode4,
			(3, "destination") => AndroidResource.Id.destination4,
			(3, "time") => AndroidResource.Id.time4,

			(4, "line") => AndroidResource.Id.line5,
			(4, "mode") => AndroidResource.Id.mode5,
			(4, "destination") => AndroidResource.Id.destination5,
			(4, "time") => AndroidResource.Id.time5,

			(5, "line") => AndroidResource.Id.line6,
			(5, "mode") => AndroidResource.Id.mode6,
			(5, "destination") => AndroidResource.Id.destination6,
			(5, "time") => AndroidResource.Id.time6,

			_ => AndroidResource.Id.line1
		};
	}

	private static string FormatDeparture(
		DepartureRow row)
	{
		var mot = row.Mot switch
		{
			"Tram" => "TRAM",
			"CityBus" or
			"Bus" or
			"IntercityBus" or
			"RegioBus" => "BUS",
			"SuburbanRailway" or
			"RapidTransit" => "S/U",
			"Train" => "TRAIN",
			"Taxi" => "TAXI",
			"Ferry" => "BOAT",
			_ => "OTHER"
		};


		var line =	row.Line?.Length > 10 ? row.Line[..10] : row.Line;

		return
			$"{line,-10} {mot,-5} {row.Destination,-20} {row.Time}";
	}
	private static void AttachRefreshButton(
	Context context,
	RemoteViews views,
	int widgetId)
	{
		global::Android.Util.Log.Debug(
			"DDeparturesWidget",
			$"Attaching refresh button for widget {widgetId}");

		var intent = new Intent(
			context,
			typeof(DepartureWidgetProvider));

		intent.SetAction(
			"com.ddepartures.widget.REFRESH");

		intent.PutExtra(
			AppWidgetManager.ExtraAppwidgetId,
			widgetId);


		var pendingIntent =
			PendingIntent.GetBroadcast(
				context,
				widgetId,
				intent,
				PendingIntentFlags.Immutable |
				PendingIntentFlags.CancelCurrent);


		views.SetOnClickPendingIntent(
			AndroidResource.Id.widgetRefresh,
			pendingIntent);
	}

	private static void AttachLaunchButton(
	Context context,
	RemoteViews views,
	int widgetId,
	string stopId)
	{
		var intent = new Intent(
			context,
			typeof(MainActivity));

		intent.SetAction(
			"com.ddepartures.widget.OPEN");

		intent.PutExtra(
			"stopId",
			stopId);


		var pendingIntent =
			PendingIntent.GetActivity(
				context,
				widgetId + 10000,
				intent,
				PendingIntentFlags.Immutable |
				PendingIntentFlags.UpdateCurrent);


		views.SetOnClickPendingIntent(
			AndroidResource.Id.widgetBody,
			pendingIntent);
	}

	private static void AttachConfigurationButton(
	Context context,
	RemoteViews views,
	int widgetId)
	{
		var intent = new Intent(
			context,
			typeof(DepartureWidgetConfigurationActivity));

		intent.PutExtra(
			AppWidgetManager.ExtraAppwidgetId,
			widgetId);


		var pendingIntent =
			PendingIntent.GetActivity(
				context,
				widgetId + 20000,
				intent,
				PendingIntentFlags.Immutable |
				PendingIntentFlags.UpdateCurrent);


		views.SetOnClickPendingIntent(
			AndroidResource.Id.widgetTitle,
			pendingIntent);
	}

	private static void UpdateDepartureRow(
	RemoteViews views,
	int index,
	DepartureRow row)
	{
		var line =
			row.Line?.Length > 10
				? row.Line[..10]
				: row.Line ?? "";

		var mode =
			row.Mot switch
			{
				"Tram" => "TRAM",
				"CityBus" or
				"Bus" or
				"IntercityBus" or
				"RegioBus" => "BUS",
				"SuburbanRailway" or
				"RapidTransit" => "S/U",
				"Train" => "TRAIN",
				"Taxi" => "TAXI",
				"Ferry" => "BOAT",
				_ => ""
			};


		views.SetTextViewText(
			GetRowViewId(index, "line"),
			line);

		views.SetTextViewText(
			GetRowViewId(index, "mode"),
			mode);

		views.SetTextViewText(
			GetRowViewId(index, "destination"),
			row.Destination ?? "");

		views.SetTextViewText(
			GetRowViewId(index, "time"),
			row.Time ?? "");
	}

	private static int GetRowViewId(
	int index,
	string part)
	{
		return (index, part) switch
		{
			(0, "line") => AndroidResource.Id.line1,
			(0, "mode") => AndroidResource.Id.mode1,
			(0, "destination") => AndroidResource.Id.destination1,
			(0, "time") => AndroidResource.Id.time1,
			(1, "line") => AndroidResource.Id.line2,
			(1, "mode") => AndroidResource.Id.mode2,
			(1, "destination") => AndroidResource.Id.destination2,
			(1, "time") => AndroidResource.Id.time2,
			(2, "line") => AndroidResource.Id.line3,
			(2, "mode") => AndroidResource.Id.mode3,
			(2, "destination") => AndroidResource.Id.destination3,
			(2, "time") => AndroidResource.Id.time3,
			(3, "line") => AndroidResource.Id.line4,
			(3, "mode") => AndroidResource.Id.mode4,
			(3, "destination") => AndroidResource.Id.destination4,
			(3, "time") => AndroidResource.Id.time4,
			(4, "line") => AndroidResource.Id.line5,
			(4, "mode") => AndroidResource.Id.mode5,
			(4, "destination") => AndroidResource.Id.destination5,
			(4, "time") => AndroidResource.Id.time5,
			(5, "line") => AndroidResource.Id.line6,
			(5, "mode") => AndroidResource.Id.mode6,
			(5, "destination") => AndroidResource.Id.destination6,
			(5, "time") => AndroidResource.Id.time6,

			_ => AndroidResource.Id.line1
		};
	}

	private static void AttachBodyClick(
	Context context,
	RemoteViews views,
	int widgetId,
	string stopId)
	{
		var intent =
			context.PackageManager?
			.GetLaunchIntentForPackage(
				context.PackageName);

		if (intent == null)
			return;

		intent.PutExtra(
			"WidgetStopId",
			stopId);

		var pending =
			PendingIntent.GetActivity(
				context,
				widgetId,
				intent,
				PendingIntentFlags.Immutable |
				PendingIntentFlags.UpdateCurrent);

		views.SetOnClickPendingIntent(
			AndroidResource.Id.widgetBody,
			pending);
	}
	private static void AttachTitleClick(
	Context context,
	RemoteViews views,
	int widgetId)
	{
		var intent =
			new Intent(
				context,
				typeof(DepartureWidgetConfigurationActivity));

		intent.PutExtra(
			AppWidgetManager.ExtraAppwidgetId,
			widgetId);

		var pending =
			PendingIntent.GetActivity(
				context,
				widgetId + 10000,
				intent,
				PendingIntentFlags.Immutable |
				PendingIntentFlags.UpdateCurrent);

		views.SetOnClickPendingIntent(
			AndroidResource.Id.widgetTitle,
			pending);
	}
}