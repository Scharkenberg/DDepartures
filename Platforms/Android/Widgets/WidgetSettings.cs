using System.Text.Json;

namespace DDepartures.Platforms.Android.Widgets;

public sealed class WidgetSettings
{
	public string StopId { get; set; } = string.Empty;
	public string StopName { get; set; } = string.Empty;
	public DateTime LastUpdate { get; set; } = DateTime.MinValue;

	public static WidgetSettings Empty => new();

	public string Serialize()
	{
		return JsonSerializer.Serialize(this);
	}

	public static WidgetSettings Deserialize(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return Empty;

		try
		{
			return JsonSerializer.Deserialize<WidgetSettings>(json)
				?? Empty;
		}
		catch
		{
			return Empty;
		}
	}
}