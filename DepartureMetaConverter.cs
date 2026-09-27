using System.Globalization;

namespace DDepartures
{
	// Combines Mot/Platform/Occupancy into a single compact line for the departure
	// list, e.g. "Tram  ·  P3  ·  Standing only". Empty or uninformative parts
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
		{
			if (string.IsNullOrWhiteSpace(mot))
				return null;

			return mot switch
			{
				"Tram"
					=> "TRAM",

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
					=> "BUS",

				"SuburbanRailway" or
				"RapidTransit" or
				"OverheadRailway"
					=> "S/U",

				"Train"
					=> "TRAIN",

				"Taxi" or
				"HailedSharedTaxi"
					=> "TAXI",

				"Ferry"
					=> "BOAT",

				"Cableway" or
				"Cablecar"
					=> "CABLE",

				_ => mot.Length <= 5 ? mot : "OTHER"
			};
		}

		private static string? FormatPlatform(string? platform)
			=> string.IsNullOrWhiteSpace(platform) ? null : $"{platform}";

		private static string? FormatOccupancy(string? occupancy) => occupancy switch
		{
			"ManySeats" => "●○○",
			"StandingOnly" => "●●○",
			"Full" => "●●●",
			"Unknown" => "○○○",
			_ => null // missing or unrecognized future value
		};
	}
}
