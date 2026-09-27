using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using AndroidResource = global::DDepartures.Resource;

namespace DDepartures.Platforms.Android.Widgets;

[Service(Permission = "android.permission.BIND_REMOTEVIEWS", Exported = false)]
public class DepartureRowService : RemoteViewsService
{
	public override IRemoteViewsFactory OnGetViewFactory(Intent? intent)
	{
		return new DepartureRowFactory(this.ApplicationContext!, intent);
	}
}

public class DepartureRowFactory : Java.Lang.Object, RemoteViewsService.IRemoteViewsFactory
{
	private readonly Context _context;
	private readonly int _widgetId;
	private List<DepartureRow> _departures = [];

	public DepartureRowFactory(Context context, Intent intent)
	{
		_context = context;
		_widgetId = intent.GetIntExtra(AppWidgetManager.ExtraAppwidgetId, -1);
	}

	public void OnDataSetChanged()
	{
		var settings = WidgetStorage.Load(_context, _widgetId);
		_departures = settings.Departures;
	}

	public RemoteViews GetViewAt(int position)
	{
		var row = _departures[position];

		var rv = new RemoteViews(
			_context.PackageName,
			AndroidResource.Layout.departure_row);

		var mode = row.Mot switch
		{
			"Tram"
				=> "🚋",

			"CityBus" or
			"Bus" or
			"IntercityBus" or
			"RegioBus" or
			"PlusBus" or
			"CitizenBus" or
			"DemandBus" or
			"SchoolBus" or
			"ClockBus" or
			"BusOnRequest"
				=> "🚌",

			"SuburbanRailway" or
			"RapidTransit" or
			"OverheadRailway"
				=> "🚇",

			"Train"
				=> "🚅",

			"Taxi" or
			"HailedSharedTaxi"
				=> "🚕",

			"Ferry"
				=> "⛴️",

			"Cableway" or
			"Cablecar"
				=> "🚞",

			_ => "❓"
		};

		var line = row.Line?.Length > 10 ? row.Line[..10] : row.Line ?? "";

		rv.SetTextViewText(AndroidResource.Id.line, line);
		rv.SetTextViewText(AndroidResource.Id.mode, mode);
		rv.SetTextViewText(AndroidResource.Id.destination, row.Destination ?? "");
		rv.SetTextViewText(AndroidResource.Id.time, row.Time ?? "");

		// Per-row tap target for the SetPendingIntentTemplate on the ListView
		// (wired up in DepartureWidgetUpdater - see next step).
		var fillIn = new Intent();
		fillIn.PutExtra("stopId", row.Platform);
		rv.SetOnClickFillInIntent(AndroidResource.Id.rowRoot, fillIn);

		return rv;
	}

	public RemoteViews? LoadingView => null;
	public int ViewTypeCount => 1;
	public int Count => _departures.Count;
	public bool HasStableIds => true;

	public long GetItemId(int position) => position;   // stays a method (takes a param)

	public void OnCreate() { }
	public void OnDestroy() { }
}