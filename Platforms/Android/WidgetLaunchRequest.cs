namespace DDepartures.Platforms.Android;

public static class WidgetLaunchRequest
{
	private static (string StopId, string StopName)? _pending;

	public static event Action<string, string>? Requested;

	public static void Raise(string stopId, string stopName)
	{
		if (Requested is { } handler)
			handler.Invoke(stopId, stopName);
		else
			_pending = (stopId, stopName);
	}

	// Drains a launch request that arrived before anyone had subscribed yet
	// (cold start: MainActivity processes the intent before MainPage exists).
	public static (string StopId, string StopName)? Consume()
	{
		var value = _pending;
		_pending = null;
		return value;
	}
}