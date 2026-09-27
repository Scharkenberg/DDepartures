using Android.Content;

namespace DDepartures.Platforms.Android.Widgets;

public static class WidgetStorage
{
	private const string PreferencesName = "DDeparturesWidgets";

	private static ISharedPreferences GetPreferences(Context context)
	{
		return context.GetSharedPreferences(
			PreferencesName,
			FileCreationMode.Private)!;
	}


	private static string GetKey(int widgetId)
	{
		return $"widget_{widgetId}";
	}


	public static void Save(
		Context context,
		int widgetId,
		WidgetSettings settings)
	{
		var prefs = GetPreferences(context);

		prefs.Edit()
			.PutString(
				GetKey(widgetId),
				settings.Serialize())
			.Apply();
	}


	public static WidgetSettings Load(
		Context context,
		int widgetId)
	{
		var prefs = GetPreferences(context);

		var json = prefs.GetString(
			GetKey(widgetId),
			null);

		return WidgetSettings.Deserialize(json);
	}


	public static void Delete(
		Context context,
		int widgetId)
	{
		var prefs = GetPreferences(context);

		prefs.Edit()
			.Remove(GetKey(widgetId))
			.Apply();
	}
}