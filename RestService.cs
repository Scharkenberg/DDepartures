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
		
		public ObservableCollection<string>? Responses { get; private set; }

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
            Responses = new ObservableCollection<string>();
			StatusResponse = "Ready to go";
			IsRefreshing = false;
        }

		public async Task<int> RefreshDataAsync(string args)
		{
			_client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
			_client.DefaultRequestHeaders.UserAgent.ParseAdd("curl/8.16.0");
			var response = await _client.GetAsync($"http://widgets.vvo-online.de/abfahrtsmonitor/Abfahrten.do?lim=50&hst={args}");
            var status = (int)response.StatusCode;
            StatusResponse = status + " - " + response.StatusCode.ToString();
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
				return status;
			Responses.Clear();
            var json =  await response.Content.ReadAsStringAsync();
			Items = JsonSerializer.Deserialize<ObservableCollection<List<string>>>(json, _serializerOptions);
			foreach (var item in Items)
			{
				if (item.Count != 3)
					continue;
				if (item[2] == "")
					item[2] = "0";
				string entry = $"{item[0],5} {item[1]} {item[2],5}'";
				Responses.Add(entry);
            }
			_client.DefaultRequestHeaders.Clear();
            return status;
		}

    }
}
