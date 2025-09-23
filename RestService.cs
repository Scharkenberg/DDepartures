using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace DDepartures
{
	public partial class RestService
	{
		HttpClient _client;
		JsonSerializerOptions _serializerOptions;

		public bool IsRefreshing { get; private set; }

		public string StatusResponse { get; private set; }

        public ObservableCollection<List<string>>? Items { get; private set; }

        public RestService()
		{
			_client = new HttpClient();
			_serializerOptions = new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
				WriteIndented = true
			};
			_client.DefaultRequestHeaders.Clear();
			Items = new ObservableCollection<List<string>>();
			StatusResponse = "Ready to go";
			IsRefreshing = false;
        }

		public async Task<int> RefreshDataAsync(string args)
		{
			short LineCharCount = 0;
			short TimeCharCount = 0;
			_client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
			_client.DefaultRequestHeaders.UserAgent.ParseAdd("curl/8.16.0");
			var response = await _client.GetAsync($"http://widgets.vvo-online.de/abfahrtsmonitor/Abfahrten.do?lim=50&hst={args}");
            var status = (int)response.StatusCode;
            StatusResponse = status + " - " + response.StatusCode.ToString();
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
				return status;
            var json =  await response.Content.ReadAsStringAsync();
			Items = JsonSerializer.Deserialize<ObservableCollection<List<string>>>(json, _serializerOptions);
			foreach (var item in Items)
			{
				if (item.Count != 3)
					item[1] += " - ERROR";
				if (item[2] == "")
					item[2] = "0";
				if (item[0].Length > LineCharCount)
					LineCharCount = (short)item[0].Length;
				if (item[2].Length > TimeCharCount)
					TimeCharCount = (short)item[2].Length;
            }
			foreach (var item in Items)
			{
				item[0] = item[0].PadLeft(LineCharCount);
				item[2] = item[2].PadLeft(TimeCharCount);
			}
            _client.DefaultRequestHeaders.Clear();
            return status;
		}

    }
}
