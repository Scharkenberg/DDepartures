// File: Services/RestService.cs
using System.Collections.ObjectModel;
using System.Text.Json;

namespace DDepartures
{

	// RestService used by the UI (parameterless ctor for XAML)
	public class RestService : NotifyBase, IDisposable
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

		private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		};

		private const int MAX_DEPARTURES = 50;

		// Backing fields
		private ObservableCollection<List<string>> _depItems = new ObservableCollection<List<string>>();
		private ObservableCollection<PointResult> _pointResults = new ObservableCollection<PointResult>();
		private string _statusResponse = string.Empty;
		private int _lastStatus = 0;
		private string _lastSuccessfulArgs = string.Empty;

		// Public properties (raise PropertyChanged via NotifyBase)
		public ObservableCollection<List<string>> DepItems
		{
			get => _depItems;
			private set => SetProperty(ref _depItems, value ?? new ObservableCollection<List<string>>());
		}

		public ObservableCollection<PointResult> PointResults
		{
			get => _pointResults;
			private set => SetProperty(ref _pointResults, value ?? new ObservableCollection<PointResult>());
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

				_client.DefaultRequestHeaders.Accept.Add(
					new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
				);
				_client.DefaultRequestHeaders.UserAgent.ParseAdd("curl/8.21.0");

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

				string json;
				try
				{
					json = await response.Content.ReadAsStringAsync();
					if (string.IsNullOrWhiteSpace(json))
					{
						StatusResponse = "Error: Empty response from server";
						LastStatus = 500;
						DepItems = new ObservableCollection<List<string>>();
						_client.DefaultRequestHeaders.Clear();
						return LastStatus;
					}
				}
				catch (Exception ex)
				{
					StatusResponse = $"Failed to read response: {ex.Message}";
					LastStatus = 500;
					DepItems = new ObservableCollection<List<string>>();
					_client.DefaultRequestHeaders.Clear();
					return LastStatus;
				}

				try
				{
					var deserialized = JsonSerializer.Deserialize<ObservableCollection<List<string>>>(
						json,
						_serializerOptions
					);

					if (deserialized == null || deserialized.Count == 0)
					{
						StatusResponse = "No departures found for this station";
						DepItems = new ObservableCollection<List<string>>();
						_client.DefaultRequestHeaders.Clear();
						return LastStatus;
					}

					DepItems = deserialized;

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
					DepItems = new ObservableCollection<List<string>>();
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
							if (item == null || item.Count == 0)
								continue;

							if (item.Count != 3)
								item[1] += " - ERROR";
							if (item[2] == "")
								item[2] = "0";
							if (item[0].Length > LineCharCount)
								LineCharCount = (short)item[0].Length;
							if (item[2].Length > TimeCharCount)
								TimeCharCount = (short)item[2].Length;
						}
						foreach (var item in DepItems)
						{
							if (item != null && item.Count > 0)
							{
								item[0] = item[0].PadLeft(LineCharCount);
								item[2] = item[2].PadLeft(TimeCharCount);
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
				DepItems = new ObservableCollection<List<string>>();
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

				_client.DefaultRequestHeaders.Accept.Add(
					new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
				);
				_client.DefaultRequestHeaders.UserAgent.ParseAdd("curl/8.21.0");

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
					using (var doc = JsonDocument.Parse(json))
					{
						if (!doc.RootElement.TryGetProperty("Points", out var pointsElement) || pointsElement.ValueKind != JsonValueKind.Array)
						{
							StatusResponse = "No points found in response";
							PointResults = new ObservableCollection<PointResult>();
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
					PointResults = new ObservableCollection<PointResult>();
					LastStatus = 500;
					_client.DefaultRequestHeaders.Clear();
					return LastStatus;
				}
				catch (Exception ex)
				{
					StatusResponse = $"Error parsing points: {ex.Message}";
					PointResults = new ObservableCollection<PointResult>();
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
				PointResults = new ObservableCollection<PointResult>();
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
