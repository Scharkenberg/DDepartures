using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DDepartures
{

	// RestService used by the UI (parameterless ctor for XAML)
	public partial class RestService : NotifyBase, IDisposable
	{
		// Shared HttpClient support
		private static HttpClient? _sharedHttpClient;
		private readonly HttpClient _client;
		private readonly bool _ownsClient;

		public static void SetSharedHttpClient(HttpClient client)
		{
			_sharedHttpClient = client ?? throw new ArgumentNullException(nameof(client));
		}

		// Parameterless constructor required for XAML binding
		public RestService()
		{
			if (_sharedHttpClient != null)
			{
				_client = _sharedHttpClient;
				_ownsClient = false;
			}
			else
			{
				_client = new HttpClient();
				_ownsClient = true;
			}
		}

		// Optional constructor for programmatic creation
		public RestService(HttpClient httpClient)
		{
			_client = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			_ownsClient = false;
		}

		private bool _headersInitialized;

		private void EnsureJsonHeaders()
		{
			if (_headersInitialized)
				return;

			_client.DefaultRequestHeaders.Accept.Clear();
			_client.DefaultRequestHeaders.Accept.Add(
				new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

			_client.DefaultRequestHeaders.UserAgent.Clear();
			_client.DefaultRequestHeaders.UserAgent.ParseAdd("curl/8.22.0");

			_headersInitialized = true;
		}

		private readonly JsonSerializerOptions _serializerOptions = new()
		{
			PropertyNameCaseInsensitive = true
		};

		private const int MAX_DEPARTURES = 50;
		private const string DepartureMonitorUrl = "https://webapi.vvo-online.de/dm";

		private static readonly string[] DefaultModesOfTransport =
		[
			"Tram", "CityBus", "IntercityBus", "SuburbanRailway",
			"Train", "Cableway", "Ferry", "HailedSharedTaxi"
		];

		// Backing fields
		private ObservableCollection<DepartureRow> _depItems = [];
		private ObservableCollection<PointResult> _pointResults = [];
		private string _statusResponse = string.Empty;
		private int _lastStatus = 0;
		private string _lastSuccessfulArgs = string.Empty;

		// Public properties (raise PropertyChanged via NotifyBase)
		public ObservableCollection<DepartureRow> DepItems
		{
			get => _depItems;
			private set => SetProperty(ref _depItems, value ?? []);
		}

		public ObservableCollection<PointResult> PointResults
		{
			get => _pointResults;
			private set => SetProperty(ref _pointResults, value ?? []);
		}

		public string StatusResponse
		{
			get => _statusResponse;
			set => SetProperty(ref _statusResponse, value ?? string.Empty);
		}

		public int LastStatus
		{
			get => _lastStatus;
			private set => SetProperty(ref _lastStatus, value);
		}

		public string LastSuccessfulArgs
		{
			get => _lastSuccessfulArgs;
			private set => SetProperty(ref _lastSuccessfulArgs, value ?? string.Empty);
		}

		// inside RestService (inherits NotifyBase)
		private bool _departuresRefreshing;
		public bool DeparturesRefreshing
		{
			get => _departuresRefreshing;
			set => SetProperty(ref _departuresRefreshing, value);
		}

		private bool _pointFinderRefreshing;
		public bool PointFinderRefreshing
		{
			get => _pointFinderRefreshing;
			set => SetProperty(ref _pointFinderRefreshing, value);
		}

		// --- RefreshDataAsync (departures) ---
		// Uses the VVO WebAPI departure monitor (POST /dm) - the same backend as the
		// official DVB mobil app. Gives real-time state, platform and occupancy that
		// the old Widget API (Abfahrten.do) doesn't expose.
		//
		// allowShortcutFallback: if the given args isn't a plain numeric stop ID and /dm
		// rejects it, try resolving it as a stop shortcut (e.g. "POP" -> Postplatz) via
		// PointFinder and retry once with the resolved ID. Set to false on the retry
		// itself to avoid looping.
		public async Task<int> RefreshDataAsync(string args, bool allowShortcutFallback = true)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(args))
				{
					StatusResponse = "Error: No station ID provided";
					LastStatus = 400;
					return LastStatus;
				}

				short LineCharCount = 0;
				short TimeCharCount = 0;

				EnsureJsonHeaders();

				var requestBody = new DmRequest
				{
					StopId = args,
					Limit = MAX_DEPARTURES,
					Mot = DefaultModesOfTransport
				};

				HttpResponseMessage response;
				try
				{
					using var requestContent = new StringContent(
						JsonSerializer.Serialize(requestBody, _serializerOptions),
						Encoding.UTF8,
						"application/json");
					using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));

					response = await _client.PostAsync(DepartureMonitorUrl, requestContent, cts.Token);
				}
				catch (System.Threading.Tasks.TaskCanceledException)
				{
					StatusResponse = "Network timeout - no internet or server not responding";
					LastStatus = 0;
					DepItems?.Clear();
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
					return LastStatus;
				}
				catch (HttpRequestException ex)
				{
					StatusResponse = $"{ex.InnerException?.ToString() ?? ex.ToString()}";
					LastStatus = 0;
					DepItems?.Clear();
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
					return LastStatus;
				}
				catch (Exception ex)
				{
					StatusResponse = $"Request failed: {ex.ToString()}";
					LastStatus = 500;
					DepItems?.Clear();
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
					return LastStatus;
				}

				if (response == null)
				{
					StatusResponse = "Error: No response from server";
					LastStatus = 500;
					DepItems?.Clear();
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
					return LastStatus;
				}

				LastStatus = (int)response.StatusCode;

				if (response.StatusCode != System.Net.HttpStatusCode.OK)
				{
					if (allowShortcutFallback && !LooksLikeNumericStopId(args))
					{
						StatusResponse = "Resolving stop code...";
						var resolvedId = await TryResolveStopShortcutAsync(args);
						if (resolvedId != null)
						{
							GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
							return await RefreshDataAsync(resolvedId, allowShortcutFallback: false);
						}
					}

					StatusResponse = LastStatus + " - " + response.StatusCode.ToString();
					DepItems?.Clear();
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
					return LastStatus;
				}

				StatusResponse = LastStatus + " - " + response.StatusCode.ToString();

				LastSuccessfulArgs = args;

				DmResponse? doc;
				try
				{
					using var json = await response.Content.ReadAsStreamAsync();
					using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
					doc = await JsonSerializer.DeserializeAsync<DmResponse>(json, _serializerOptions, cts.Token);

					if (doc == null)
					{
						StatusResponse = "Error: Empty response from server";
						LastStatus = 500;
						DepItems = [];
						GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
						return LastStatus;
					}
				}
				catch (Exception ex)
				{
					StatusResponse = $"Failed to read response: {ex.Message}";
					LastStatus = 500;
					DepItems = [];
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
					return LastStatus;
				}

				// The /dm endpoint can return HTTP 200 with an application-level error
				// in the body (e.g. an unknown stop ID) - check Status.Code, not just the HTTP code.
				if (!string.Equals(doc.Status?.Code, "Ok", StringComparison.OrdinalIgnoreCase))
				{
					if (allowShortcutFallback
						&& !LooksLikeNumericStopId(args)
						&& (doc.Status?.Code == "InvalidRequest" || doc.Status?.Code == "NoData"))
					{
						StatusResponse = "Resolving stop code...";
						var resolvedId = await TryResolveStopShortcutAsync(args);
						if (resolvedId != null)
						{
							GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
							return await RefreshDataAsync(resolvedId, allowShortcutFallback: false);
						}
					}

					StatusResponse = doc.Status?.Code switch
					{
						"NoData" => "No departures found for this station",
						"InvalidRequest" => "Error: Invalid station ID",
						_ => $"Server reported: {doc.Status?.Code ?? "unknown status"}"
					};
					DepItems = [];
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
					return LastStatus;
				}

				try
				{
					var raw = doc.Departures;

					if (raw == null || raw.Count == 0)
					{
						StatusResponse = "No departures found for this station";
						DepItems = [];
						GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
						return LastStatus;
					}

					var parsed = new ObservableCollection<DepartureRow>();
					var now = DateTimeOffset.Now;

					foreach (var d in raw)
					{
						if (d == null)
							continue;

						// Prefer RealTime (accounts for delay); fall back to ScheduledTime.
						var reference = d.RealTime ?? d.ScheduledTime;
						String timeText = "?";
						String destination = d.Direction?.Trim() ?? "";
						if (reference.HasValue)
						{
							var minutes = (int)Math.Round((reference.Value - now).TotalMinutes, MidpointRounding.AwayFromZero);
							timeText = Math.Max(minutes, 0).ToString(CultureInfo.InvariantCulture);

							// Delay/early suffix - only when both timestamps are present, so an
							// "InTime" departure with no live tracking shows a plain number, not "+0".
							if (d.RealTime.HasValue && d.ScheduledTime.HasValue)
							{
								var delayMinutes = (int)Math.Round(
									(d.RealTime.Value - d.ScheduledTime.Value).TotalMinutes,
									MidpointRounding.AwayFromZero);

								if (delayMinutes != 0)
								{
									String sign = delayMinutes > 0 ? "+" : "-";
									if (delayMinutes < 0) d.State = "Early";
									destination += " " + sign + Math.Abs(delayMinutes).ToString(CultureInfo.InvariantCulture);
								}
							}
						}

						// CancelReasons is the reliable cancellation signal - State alone isn't documented to carry it.
						var isCancelled = d.CancelReasons is { Count: > 0 };
						if (isCancelled)
							timeText = "X" + timeText + "X";

						parsed.Add(new DepartureRow
						{
							Line = NormalizeLineNumber(d.LineName),
							Destination = destination,
							Time = timeText,
							Platform = d.Platform?.Name?.Trim() is string platform && !string.IsNullOrEmpty(platform) ? d.Platform.Type?.Trim() switch
							{
								"Platform" => $"Pl. {platform}",
								"Railtrack" => $"Tr. {platform}",
								_ => platform
							} : "",
							Mot = d.Mot ?? "",
							State = isCancelled ? "Cancelled" : (d.State ?? "Unknown"),
							Occupancy = d.Occupancy ?? "Unknown"
						});
					}

					DepItems = parsed;

					// Trim to max (safety net - limit is also requested server-side)
					if (DepItems.Count > MAX_DEPARTURES)
					{
						while (DepItems.Count > MAX_DEPARTURES)
							DepItems.RemoveAt(DepItems.Count - 1);
					}
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
				}
				catch (JsonException ex)
				{
					StatusResponse = $"Invalid JSON response: {ex.Message}";
					DepItems = [];
					LastStatus = 500;
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
					return LastStatus;
				}

				// Format departures
				try
				{
					if (DepItems != null && DepItems.Count > 0)
					{
						foreach (var item in DepItems)
						{
							if (item == null)
								continue;
							if (item.Line.Length > LineCharCount)
								LineCharCount = (short)item.Line.Length;
							if (item.Time.Length > TimeCharCount)
								TimeCharCount = (short)item.Time.Length;
						}
						foreach (var item in DepItems)
						{
							item?.Line = item.Line.PadLeft(LineCharCount > 9 ? 9 : LineCharCount);
							item?.Time = item.Time.PadLeft(TimeCharCount);
						}
					}
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
				}
				catch (Exception ex)
				{
					StatusResponse = $"Error formatting departures: {ex.Message}";
					LastStatus = 500;
				}
				GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
				return LastStatus;
			}
			catch (Exception ex)
			{
				StatusResponse = $"Unexpected error: {ex.Message}";
				DepItems = [];
				LastStatus = 500;
				GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
				return LastStatus;
			}
		}

		private static bool LooksLikeNumericStopId(string value)
			=> !string.IsNullOrEmpty(value) && value.All(char.IsDigit);

		// Resolves a stop shortcut (e.g. "POP" for Postplatz) to a numeric stop ID via
		// PointFinder's stopShortcuts flag - the WebAPI equivalent of what the old Widget
		// API's `hst` parameter used to resolve transparently server-side. Best-effort:
		// any failure here just means the original error is reported instead.
		private async Task<string?> TryResolveStopShortcutAsync(string query)
		{
			try
			{
				using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));

				var url = $"https://webapi.vvo-online.de/tr/pointfinder?query={Uri.EscapeDataString(query)}" +
					"&format=json&limit=1&stopsOnly=true&regionalOnly=false&stopShortcuts=true";

				using var response = await _client.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseContentRead, cts.Token);
				if (response.StatusCode != System.Net.HttpStatusCode.OK)
					return null;

				using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
				using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

				if (!doc.RootElement.TryGetProperty("Points", out var points) || points.ValueKind != JsonValueKind.Array)
					return null;

				foreach (var p in points.EnumerateArray())
				{
					if (p.ValueKind != JsonValueKind.String)
						continue;

					// Point strings are pipe-delimited; index 0 is the ID. Only a plain
					// numeric ID is a real stop - street/POI/coordinate results use other
					// ID shapes (e.g. "streetID:...") and can't be used as /dm's stopid.
					var id = (p.GetString() ?? "").Split('|').ElementAtOrDefault(0);
					if (!string.IsNullOrEmpty(id) && LooksLikeNumericStopId(id))
						return id;
				}

				GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
				return null;
			}
			catch
			{
				GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
				return null;
			}
		}

		// --- PointFinder query method ---
		public async Task<int> QueryPointFinderAsync(string query, int limit = 10, bool stopsOnly = true)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(query))
				{
					StatusResponse = "Error: No query provided";
					LastStatus = 400;
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
					return LastStatus;
				}

				EnsureJsonHeaders();

				HttpResponseMessage response;
				try
				{
					using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10)))
					{
						var url = $"https://webapi.vvo-online.de/tr/pointfinder?query={Uri.EscapeDataString(query)}&format=json&limit={limit}&stopsOnly={(stopsOnly ? "true" : "false")}";
						response = await _client.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseContentRead, cts.Token);
					}
				}
				catch (System.Threading.Tasks.TaskCanceledException)
				{
					StatusResponse = "Network timeout - no internet or server not responding";
					LastStatus = 0;
					PointResults?.Clear();
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
					return LastStatus;
				}
				catch (HttpRequestException ex)
				{
					StatusResponse = $"Network error: {ex.Message}";
					LastStatus = 0;
					PointResults?.Clear();
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
					return LastStatus;
				}
				catch (Exception ex)
				{
					StatusResponse = $"Request failed: {ex.Message}";
					LastStatus = 500;
					PointResults?.Clear();
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
					return LastStatus;
				}

				if (response == null)
				{
					StatusResponse = "Error: No response from server";
					LastStatus = 500;
					PointResults?.Clear();
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
					return LastStatus;
				}

				LastStatus = (int)response.StatusCode;
				StatusResponse = LastStatus + " - " + response.StatusCode.ToString();

				if (response.StatusCode != System.Net.HttpStatusCode.OK)
				{
					PointResults?.Clear();
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
					return LastStatus;
				}

				string json;
				try
				{
					json = await response.Content.ReadAsStringAsync();
					if (string.IsNullOrWhiteSpace(json))
					{
						StatusResponse = "Error: Empty response from server";
						LastStatus = 500;
						PointResults?.Clear();
						GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
						return LastStatus;
					}
				}
				catch (Exception ex)
				{
					StatusResponse = $"Failed to read response: {ex.Message}";
					LastStatus = 500;
					PointResults?.Clear();
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
					return LastStatus;
				}

				try
				{
					using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
					using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
					using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);
					{
						if (!doc.RootElement.TryGetProperty("Points", out var pointsElement) || pointsElement.ValueKind != JsonValueKind.Array)
						{
							StatusResponse = "No points found in response";
							PointResults = [];
							GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
							return LastStatus;
						}

						var parsed = new ObservableCollection<PointResult>();

						foreach (var p in pointsElement.EnumerateArray())
						{
							if (p.ValueKind != JsonValueKind.String)
								continue;

							var pointString = p.GetString() ?? string.Empty;
							if (string.IsNullOrWhiteSpace(pointString))
								continue;

							var parts = pointString.Split('|');

							string id = parts.Length > 0 ? parts[0].Trim() : string.Empty;
							string city = parts.Length > 2 ? parts[2].Trim() : string.Empty;
							string name = parts.Length > 3 ? parts[3].Trim() : string.Empty;

							if (string.IsNullOrEmpty(city)) city = "";
							else city += " \u00b7\u00A0";

							var result = new PointResult
							{
								Id = id,
								City = city,
								Name = name
							};

							parsed.Add(result);
						}

						PointResults = parsed;

						if (PointResults.Count > limit)
						{
							while (PointResults.Count > limit)
								PointResults.RemoveAt(PointResults.Count - 1);
						}

						LastSuccessfulArgs = query;
					}
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
				}
				catch (JsonException ex)
				{
					StatusResponse = $"Invalid JSON response: {ex.Message}";
					PointResults = [];
					LastStatus = 500;
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
					return LastStatus;
				}
				catch (Exception ex)
				{
					StatusResponse = $"Error parsing points: {ex.Message}";
					PointResults = [];
					LastStatus = 500;
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
					return LastStatus;
				}

				GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
				return LastStatus;
			}
			catch (Exception ex)
			{
				StatusResponse = $"Unexpected error: {ex.Message}";
				PointResults = [];
				LastStatus = 500;
				GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
				return LastStatus;
			}
		}

		// --- Periodic refresh (keeps original signature) ---
		public async void PeriodicRefresh(string args, int interval)
		{
			try
			{
				do
				{
					await Task.Delay(1000 * interval);
					if (args != "")
					{
						DeparturesRefreshing = true;
						LastStatus = await RefreshDataAsync(args);
						DeparturesRefreshing = false;
						if (LastStatus != (int)System.Net.HttpStatusCode.OK)
							break;
					}
				} while (LastSuccessfulArgs != "");
			}
			catch (Exception ex)
			{
				StatusResponse = $"Periodic refresh error: {ex.Message}";
				DeparturesRefreshing = false;
			}
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
		}

		private static string NormalizeLineNumber(string? line)
		{
			if (string.IsNullOrWhiteSpace(line))
				return line ?? string.Empty;

			var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

			var cutIndex = Array.FindIndex(tokens, t => t.Any(char.IsDigit));

			// No digit anywhere - nothing to anchor the cut on, leave untouched
			// rather than guess.
			return cutIndex >= 0
				? string.Join(' ', tokens.Take(cutIndex + 1))
				: line.Trim();
		}

		// IDisposable
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private bool _disposed = false;
		protected virtual void Dispose(bool disposing)
		{
			if (_disposed)
				return;

			if (disposing)
			{
				if (_ownsClient)
				{
					_client?.Dispose();
				}
			}

			GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
			_disposed = true;
		}

		~RestService()
		{
			Dispose(false);
		}
	}
}
