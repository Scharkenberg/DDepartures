using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DDepartures
{
	// --- Request body for POST https://webapi.vvo-online.de/dm ---
	// Property names must match VVO's lowercase JSON keys exactly.
	internal sealed class DmRequest
	{
		[JsonPropertyName("stopid")]
		public string StopId { get; set; } = "";

		[JsonPropertyName("limit")]
		public int Limit { get; set; }

		[JsonPropertyName("mot")]
		public string[]? Mot { get; set; }

		[JsonPropertyName("format")]
		public string Format { get; set; } = "json";
	}

	// --- Response body ---
	internal sealed class DmResponse
	{
		public string? Name { get; set; }
		public string? Place { get; set; }
		public DmStatus? Status { get; set; }
		public List<DmDeparture>? Departures { get; set; }
	}

	internal sealed class DmStatus
	{
		public string? Code { get; set; }
	}

	internal sealed class DmDeparture
	{
		public string? Id { get; set; }
		public string? DlId { get; set; }
		public string? LineName { get; set; }
		public string? Direction { get; set; }
		public DmPlatform? Platform { get; set; }
		public string? Mot { get; set; }

		[JsonConverter(typeof(MicrosoftDateTimeOffsetConverter))]
		public DateTimeOffset? RealTime { get; set; }

		[JsonConverter(typeof(MicrosoftDateTimeOffsetConverter))]
		public DateTimeOffset? ScheduledTime { get; set; }

		// e.g. "InTime", "Delayed". VVO's docs don't confirm a dedicated
		// "Cancelled" value here - CancelReasons is the reliable signal for that.
		public string? State { get; set; }

		// "Unknown", "ManySeats", "StandingOnly", or "Full"
		public string? Occupancy { get; set; }

		public List<DmCancelReason>? CancelReasons { get; set; }
	}

	internal sealed class DmPlatform
	{
		public string? Name { get; set; }
		public string? Type { get; set; }
	}

	internal sealed class DmCancelReason
	{
		public string? Reason { get; set; }
	}

	// --- Converter for VVO's Microsoft-JSON-date strings, e.g. "/Date(1487778279147+0100)/" ---
	// System.Text.Json has no built-in support for this legacy WCF/ASP.NET AJAX format.
	internal sealed class MicrosoftDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
	{
		private static readonly Regex Pattern = new(@"\/Date\((-?\d+)([+-]\d{4})?\)\/", RegexOptions.Compiled);

		public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			var raw = reader.GetString();
			if (string.IsNullOrEmpty(raw))
				return null;

			var match = Pattern.Match(raw);
			if (!match.Success)
				return null;

			var millis = long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
			var utc = DateTimeOffset.FromUnixTimeMilliseconds(millis);

			if (!match.Groups[2].Success)
				return utc;

			// e.g. "+0100" / "-0500"
			var offsetText = match.Groups[2].Value;
			var sign = offsetText[0] == '-' ? -1 : 1;
			var offsetHours = int.Parse(offsetText.Substring(1, 2), CultureInfo.InvariantCulture);
			var offsetMinutes = int.Parse(offsetText.Substring(3, 2), CultureInfo.InvariantCulture);
			var offset = new TimeSpan(sign * offsetHours, sign * offsetMinutes, 0);

			return utc.ToOffset(offset);
		}

		public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
			=> throw new NotSupportedException("Read-only converter - the VVO WebAPI is never sent dates in this format.");
	}
}
