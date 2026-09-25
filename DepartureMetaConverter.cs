using System.Globalization;

namespace DDepartures
{
	// Combines Mot/Platform/Occupancy into a single compact line for the departure
	// list, e.g. "Tram  ·  Pl. 3  ·  Standing only". Empty or uninformative parts
	// ("Unknown" occupancy, a blank platform) are dropped rather than shown as gaps.
	public sealed class DepartureMetaConverter : IMultiValueConverter
	{
		private const string Separator = "  \u00b7  "; // "  ·  "

		public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
		{
			var mot = values.Length > 0 ? values[0] as string : null;
			var platform = values.Length > 1 ? values[1] as string : null;
			var occupancy = values.Length > 2 ? values[2] as string : null;

			var parts = new[]
			{
				FormatMot(mot),
				FormatPlatform(platform),
				FormatOccupancy(occupancy)
			};

			return string.Join(Separator, Array.FindAll(parts, p => !string.IsNullOrEmpty(p)));
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
			=> throw new NotSupportedException();

		private static string? FormatMot(string? mot)
			=> string.IsNullOrWhiteSpace(mot) ? null : mot;

		private static string? FormatPlatform(string? platform)
			=> string.IsNullOrWhiteSpace(platform) ? null : $"P{platform}";

		private static string? FormatOccupancy(string? occupancy) => occupancy switch
		{
			"ManySeats" => "Seats available",
			"StandingOnly" => "Standing only",
			"Full" => "Full",
			"Unknown" => "Unknown occupancy",
			_ => null // missing or an unrecognized future value
		};
	}
}
