using System.Globalization;

namespace DDepartures
{
	// Maps DepartureRow.State to the app's existing Warning/Error theme colors.
	// Returns null for "InTime"/"Unknown" so the Label keeps its default TextColor.
	public sealed class DepartureStateToColorConverter : IValueConverter
	{
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			var resources = Application.Current?.Resources;
			if (resources == null)
				return null;

			var key = (value as string) switch
			{
				"Delayed" => "Warning",
				"Cancelled" => "Error",
				_ => null
			};

			if (key != null && resources.TryGetValue(key, out var color))
				return color;

			return null;
		}

		public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
			=> throw new NotSupportedException();
	}
}
