namespace DDepartures
{
	public sealed class DepartureRow
	{
		public string Line { get; set; } = "";
		public string Destination { get; set; } = "";
		public string Time { get; set; } = "";

		// Populated from the VVO WebAPI /dm response.
		public string Platform { get; set; } = "";
		public string Mot { get; set; } = "";

		// "InTime", "Delayed", "Cancelled", or "Unknown".
		public string State { get; set; } = "Unknown";

		// "Unknown", "ManySeats", "StandingOnly", or "Full".
		public string Occupancy { get; set; } = "Unknown";
	}
}
