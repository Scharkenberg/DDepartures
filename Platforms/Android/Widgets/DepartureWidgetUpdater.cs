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
		try
		{
			var manager =
				AppWidgetManager.GetInstance(context);


			var views = new RemoteViews(
				context.PackageName,
				AndroidResource.Layout.departure_widget);

			AttachRefreshButton(context, views, widgetId);

			var settings =
				WidgetStorage.Load(
					context,
					widgetId);

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
				views.SetTextViewText(AndroidResource.Id.departure1, "Widget running");

				manager.UpdateAppWidget(
					widgetId,
					views);

				return;

				using var service = new RestService();

				await service.RefreshDataAsync(
					settings.StopId);


				var departures =
					service.DepItems
						.Take(6)
						.ToList();


				for (int i = 0; i < 6; i++)
				{
					var id =
						GetDepartureTextViewId(i);


					var text =
						i < departures.Count
							? FormatDeparture(departures[i])
							: string.Empty;


					views.SetTextViewText(
						id,
						text);
				}
			}
			catch (Exception ex)
			{
				views.SetTextViewText(
					AndroidResource.Id.departure1,
					ex.Message);

				for (int i = 1; i < 6; i++)
				{
					views.SetTextViewText(
						GetDepartureTextViewId(i),
						string.Empty);
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
			views.SetTextViewText(
				GetDepartureTextViewId(i),
				string.Empty);
		}
	}


	private static int GetDepartureTextViewId(
		int index)
	{
		return index switch
		{
			0 => AndroidResource.Id.departure1,
			1 => AndroidResource.Id.departure2,
			2 => AndroidResource.Id.departure3,
			3 => AndroidResource.Id.departure4,
			4 => AndroidResource.Id.departure5,
			5 => AndroidResource.Id.departure6,
			_ => AndroidResource.Id.departure1
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


		return
			$"{row.Line,-5} {mot,-5} {row.Destination,-20} {row.Time}";
	}
	private static void AttachRefreshButton(
	Context context,
	RemoteViews views,
	int widgetId)
	{
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
				PendingIntentFlags.UpdateCurrent);


		views.SetOnClickPendingIntent(
			AndroidResource.Id.widgetRefresh,
			pendingIntent);
	}

}