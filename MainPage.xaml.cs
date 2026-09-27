namespace DDepartures
{
	public partial class MainPage : ContentPage, IDisposable
	{
		private readonly RestService svc;
		private CancellationTokenSource? _typingCts;
		private bool disposed;

		// This is only for a selected stop coming from PointFinder.
		// It is cleared as soon as the user edits the text box again.
		private string CurrentStop = string.Empty;

		public MainPage()
		{
			InitializeComponent();

			svc = new RestService();
			BindingContext = svc;

			RefView.IsVisible = false;
			PointFinderView.IsVisible = false;

			PointFinderView.SelectionChanged += OnPointFinderSelectionChanged;
			FindBtn.Clicked += OnFindClicked;
			SearchBtn.Clicked += OnSearchClicked;
			RefView.Refreshing += OnRefreshViewRefreshing;
		}

		private async void OnRefreshViewRefreshing(object? sender, EventArgs e)
			=> await SearchDeparturesAsync(manual: true);

		private void CancelTypingDebounce()
		{
			var cts = Interlocked.Exchange(ref _typingCts, null);
			if (cts == null)
				return;

			try { cts.Cancel(); } catch { }
			cts.Dispose();
		}

		private string GetTypedQuery() => SearchEntry.Text?.Trim() ?? string.Empty;

		private string GetDepartureQuery()
			=> string.IsNullOrWhiteSpace(CurrentStop) ? GetTypedQuery() : CurrentStop.Trim();

		private async Task SearchDeparturesAsync(bool manual)
		{
			var query = GetDepartureQuery();
			if (string.IsNullOrWhiteSpace(query))
			{
				svc.StatusResponse = "Enter valid ID";
				StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
				ShowDeparturesView(false);
				MainThread.BeginInvokeOnMainThread(() => RefView.IsRefreshing = false);
				return;
			}

			if (manual)
				CancelTypingDebounce();

			try
			{
				SearchBtn.IsEnabled = false;
				SearchEntry.IsEnabled = false;

				svc.DeparturesRefreshing = true;
				svc.StatusResponse = "Working...";
				StatusLabel.TextColor = (Color)Application.Current.Resources["Warning"];

				var status = await svc.RefreshDataAsync(query);

				if (status != (int)System.Net.HttpStatusCode.OK)
				{
					StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
					ShowDeparturesView(false);
				}
				else
				{
					StatusLabel.TextColor = (Color)Application.Current.Resources["Success"];
					ShowDeparturesView(true);
				}
			}
			catch (Exception ex)
			{
				svc.StatusResponse = $"Search error: {ex.Message}";
				StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
				ShowDeparturesView(false);
			}
			finally
			{
				svc.DeparturesRefreshing = false;
				SearchEntry.IsEnabled = true;
				SearchBtn.IsEnabled = true;
				MainThread.BeginInvokeOnMainThread(() => RefView.IsRefreshing = false);
			}
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
		}

		private async Task FindStopsAsync(string query, bool manual)
		{
			query = query?.Trim() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(query))
			{
				svc.StatusResponse = "Enter search text for Find";
				StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
				ShowPointFinderView(false);
				return;
			}

			if (manual)
				CancelTypingDebounce();

			try
			{
				FindBtn.IsEnabled = false;
				svc.PointFinderRefreshing = true;
				svc.StatusResponse = "Searching...";
				StatusLabel.TextColor = (Color)Application.Current.Resources["Warning"];

				var status = await svc.QueryPointFinderAsync(query, limit: 50, stopsOnly: true);

				if (status != (int)System.Net.HttpStatusCode.OK || svc.PointResults == null || svc.PointResults.Count == 0)
				{
					StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
					ShowPointFinderView(false);
				}
				else
				{
					StatusLabel.TextColor = (Color)Application.Current.Resources["Success"];
					ShowPointFinderView(true);
				}
			}
			catch (Exception ex)
			{
				svc.StatusResponse = $"Find error: {ex.Message}";
				StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
				ShowPointFinderView(false);
			}
			finally
			{
				svc.PointFinderRefreshing = false;
				FindBtn.IsEnabled = true;
				SearchEntry.Focus();
			}
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
		}

		public async void OnSearchClicked(object? sender, EventArgs e)
			=> await SearchDeparturesAsync(manual: true);

		public async void OnFindClicked(object? sender, EventArgs e)
			=> await FindStopsAsync(GetTypedQuery(), manual: true);

		private async void OnPointFinderSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			try
			{
				if (e.CurrentSelection == null || e.CurrentSelection.Count == 0)
					return;

				if (e.CurrentSelection[0] is not PointResult selected)
					return;

				CurrentStop = selected.Id;
				PointFinderView.SelectedItem = null;
				ShowPointFinderView(false);

				await SearchDeparturesAsync(manual: true);
			}
			catch (Exception ex)
			{
				svc.StatusResponse = $"Selection error: {ex.Message}";
				StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
			}
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
		}

		void ShowDeparturesView(bool show)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				RefView.IsVisible = show;
				PointFinderView.IsVisible = false;
			});
		}

		void ShowPointFinderView(bool show)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				PointFinderView.IsVisible = show;
				RefView.IsVisible = false;
			});
		}

		public void OnUnfocused(object? sender, FocusEventArgs e)
		{
			// Keep existing behavior, but avoid pointless enable/disable churn.
		}

		public void OnFocused(object? sender, FocusEventArgs e)
		{
			StatusLabel.TextColor = (Color)Application.Current.Resources["Warning"];
			svc.StatusResponse = "Ready to go";
		}

		private void OnTapped(object sender, TappedEventArgs e)
		{
			SearchEntry.Focus();
		}

		private async void OnTextChanged(object sender, TextChangedEventArgs e)
		{
			CurrentStop = string.Empty;
			CancelTypingDebounce();

			var text = e.NewTextValue?.Trim() ?? string.Empty;
			if (text.Length <= 2)
				return;

			var cts = new CancellationTokenSource();
			_typingCts = cts;

			try
			{
				await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);

				if (cts.IsCancellationRequested || !ReferenceEquals(_typingCts, cts))
					return;

				await FindStopsAsync(text, manual: false);
			}
			catch (TaskCanceledException)
			{
			}
			catch (Exception ex)
			{
				svc.StatusResponse = $"Input debounce error: {ex.Message}";
				StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
			}
			finally
			{
				if (ReferenceEquals(_typingCts, cts))
				{
					_typingCts = null;
					cts.Dispose();
				}
			}
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
		}

		public async Task LoadWidgetStopAsync(string stopId)
		{
			CurrentStop = stopId;
			SearchEntry.Text = stopId;
			await SearchDeparturesAsync(manual: true);
		}

		protected override async void OnAppearing()
		{
			base.OnAppearing();
#if ANDROID
			var stopId =
				DDepartures.Platforms.Android.WidgetLaunchRequest.PendingStopId;

			if (!string.IsNullOrWhiteSpace(stopId))
			{
				DDepartures.Platforms.Android.WidgetLaunchRequest.PendingStopId = null;

				await LoadWidgetStopAsync(stopId);
			}
#endif
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposed)
				return;

			if (disposing)
			{
				CancelTypingDebounce();

				DepartureListView.ItemsSource = null;
				PointFinderView.ItemsSource = null;

				PointFinderView.SelectionChanged -= OnPointFinderSelectionChanged;
				FindBtn.Clicked -= OnFindClicked;
				SearchBtn.Clicked -= OnSearchClicked;
				RefView.Refreshing -= OnRefreshViewRefreshing;

				if (svc is IDisposable d)
					d.Dispose();
			}

			disposed = true;
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
		}

		~MainPage()
		{
			Dispose(false);
		}
	}
}