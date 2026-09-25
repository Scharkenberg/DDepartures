using System.Collections.ObjectModel;
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
			_client.DefaultRequestHeaders.UserAgent.ParseAdd("curl/8.21.0");

			_headersInitialized = true;
		}

		private readonly JsonSerializerOptions _serializerOptions = new()
		{
			PropertyNameCaseInsensitive = true
		};

		private const int MAX_DEPARTURES = 50;

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
		public async Task<int> RefreshDataAsync(string args)
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

				HttpResponseMessage response;
				try
				{
					using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10)))
					{
						response = await _client.GetAsync(
							$"http://widgets.vvo-online.de/abfahrtsmonitor/Abfahrten.do?lim=50&hst={Uri.EscapeDataString(args)}",
							System.Net.Http.HttpCompletionOption.ResponseContentRead,
							cts.Token
						);
					}
				}
				catch (System.Threading.Tasks.TaskCanceledException)
				{
					StatusResponse = "Network timeout - no internet or server not responding";
					LastStatus = 0;
					DepItems?.Clear();
					_client.DefaultRequestHeaders.Clear();
					return LastStatus;
				}
				catch (HttpRequestException ex)
				{
					StatusResponse = $"Network error: {ex.Message}";
					LastStatus = 0;
					DepItems?.Clear();
					_client.DefaultRequestHeaders.Clear();
					return LastStatus;
				}
				catch (Exception ex)
				{
					StatusResponse = $"Request failed: {ex.Message}";
					LastStatus = 500;
					DepItems?.Clear();
					_client.DefaultRequestHeaders.Clear();
					return LastStatus;
				}

				if (response == null)
				{
					StatusResponse = "Error: No response from server";
					LastStatus = 500;
					DepItems?.Clear();
					_client.DefaultRequestHeaders.Clear();
					return LastStatus;
				}

				LastStatus = (int)response.StatusCode;
				StatusResponse = LastStatus + " - " + response.StatusCode.ToString();

				if (response.StatusCode != System.Net.HttpStatusCode.OK)
				{
					DepItems?.Clear();
					_client.DefaultRequestHeaders.Clear();
					return LastStatus;
				}

				LastSuccessfulArgs = args;

				JsonDocument doc;
				try
				{
					using var json = await response.Content.ReadAsStreamAsync();
					using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
					doc = await JsonDocument.ParseAsync(json, cancellationToken: cts.Token);
					if (string.IsNullOrWhiteSpace(doc.ToString()))
					{
						StatusResponse = "Error: Empty response from server";
						LastStatus = 500;
						DepItems = [];
						_client.DefaultRequestHeaders.Clear();
						return LastStatus;
					}
				}
				catch (Exception ex)
				{
					StatusResponse = $"Failed to read response: {ex.Message}";
					LastStatus = 500;
					DepItems = [];
					_client.DefaultRequestHeaders.Clear();
					return LastStatus;
				}

				try
				{
					var raw = JsonSerializer.Deserialize<ObservableCollection<List<string>>>(doc, _serializerOptions);

					if (raw == null || raw.Count == 0)
					{
						StatusResponse = "No departures found for this station";
						DepItems = [];
						return LastStatus;
					}

					var parsed = new ObservableCollection<DepartureRow>();

					foreach (var row in raw)
					{
						if (row == null || row.Count < 3)
							continue;

						parsed.Add(new DepartureRow
						{
							Line = row[0]?.Trim() ?? "",
							Destination = row[1]?.Trim() ?? "",
							Time = row[2]?.Trim() ?? ""
						});
					}

					DepItems = parsed;

					// Trim to max
					if (DepItems.Count > MAX_DEPARTURES)
					{
						while (DepItems.Count > MAX_DEPARTURES)
							DepItems.RemoveAt(DepItems.Count - 1);
					}
				}
				catch (JsonException ex)
				{
					StatusResponse = $"Invalid JSON response: {ex.Message}";
					DepItems = [];
					LastStatus = 500;
					_client.DefaultRequestHeaders.Clear();
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
							if (item.Time == "")
								item.Time = "0";
							if (item.Line.Length > LineCharCount)
								LineCharCount = (short)item.Line.Length;
							if (item.Time.Length > TimeCharCount)
								TimeCharCount = (short)item.Time.Length;
						}
						foreach (var item in DepItems)
						{
							if (item != null)
							{
								item.Line = item.Line.PadLeft(LineCharCount);
								item.Time = item.Time.PadLeft(TimeCharCount);
							}
						}
					}
				}
				catch (Exception ex)
				{
					StatusResponse = $"Error formatting departures: {ex.Message}";
					LastStatus = 500;
				}

				_client.DefaultRequestHeaders.Clear();
				return LastStatus;
			}
			catch (Exception ex)
			{
				StatusResponse = $"Unexpected error: {ex.Message}";
				DepItems = [];
				LastStatus = 500;
				_client.DefaultRequestHeaders.Clear();
				return LastStatus;
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
					_client.DefaultRequestHeaders.Clear();
					return LastStatus;
				}
				catch (HttpRequestException ex)
				{
					StatusResponse = $"Network error: {ex.Message}";
					LastStatus = 0;
					PointResults?.Clear();
					_client.DefaultRequestHeaders.Clear();
					return LastStatus;
				}
				catch (Exception ex)
				{
					StatusResponse = $"Request failed: {ex.Message}";
					LastStatus = 500;
					PointResults?.Clear();
					_client.DefaultRequestHeaders.Clear();
					return LastStatus;
				}

				if (response == null)
				{
					StatusResponse = "Error: No response from server";
					LastStatus = 500;
					PointResults?.Clear();
					_client.DefaultRequestHeaders.Clear();
					return LastStatus;
				}

				LastStatus = (int)response.StatusCode;
				StatusResponse = LastStatus + " - " + response.StatusCode.ToString();

				if (response.StatusCode != System.Net.HttpStatusCode.OK)
				{
					PointResults?.Clear();
					_client.DefaultRequestHeaders.Clear();
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
						_client.DefaultRequestHeaders.Clear();
						return LastStatus;
					}
				}
				catch (Exception ex)
				{
					StatusResponse = $"Failed to read response: {ex.Message}";
					LastStatus = 500;
					PointResults?.Clear();
					_client.DefaultRequestHeaders.Clear();
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
							_client.DefaultRequestHeaders.Clear();
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

							if (string.IsNullOrEmpty(city))
								city = "Dresden";

							var result = new PointResult
							{
								Id = id,
								City = city + " -",
								Name = " " + name
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
				}
				catch (JsonException ex)
				{
					StatusResponse = $"Invalid JSON response: {ex.Message}";
					PointResults = [];
					LastStatus = 500;
					_client.DefaultRequestHeaders.Clear();
					return LastStatus;
				}
				catch (Exception ex)
				{
					StatusResponse = $"Error parsing points: {ex.Message}";
					PointResults = [];
					LastStatus = 500;
					_client.DefaultRequestHeaders.Clear();
					return LastStatus;
				}

				_client.DefaultRequestHeaders.Clear();
				return LastStatus;
			}
			catch (Exception ex)
			{
				StatusResponse = $"Unexpected error: {ex.Message}";
				PointResults = [];
				LastStatus = 500;
				_client.DefaultRequestHeaders.Clear();
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

			_disposed = true;
		}

		~RestService()
		{
			Dispose(false);
		}
	}
}
