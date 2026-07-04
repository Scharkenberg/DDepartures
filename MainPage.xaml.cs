using System.Windows.Input;
using System.Threading;
using System.Threading.Tasks;

namespace DDepartures
{
	public partial class MainPage : ContentPage, IDisposable
	{
		readonly RestService svc;
		bool disposed;
		string CurrentStop;

		public MainPage()
		{
			InitializeComponent();

			// single RestService instance used by XAML bindings and code
			svc = new RestService();

			// set page BindingContext so XAML bindings use the same instance
			this.BindingContext = svc;
			CurrentStop = string.Empty;

			// Ensure both views start hidden (XAML already sets IsVisible="False")
			RefView.IsVisible = false;
			PointFinderView.IsVisible = false;

			// Wire up selection handler for PointFinder results
			PointFinderView.SelectionChanged += OnPointFinderSelectionChanged;

			// Wire up buttons (if not already wired in XAML)
			FindBtn.Clicked += OnFindClicked;
			SearchBtn.Clicked += OnSearchClicked;
		}

		public async void OnSearchClicked(object? sender, EventArgs e)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(CurrentStop))
				{
					// StatusResponse is bound to StatusLabel; update service property
					svc.StatusResponse = "Enter valid ID";
					// color still controlled here
					StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
					ShowDeparturesView(false);
					return;
				}

				// UI state
				SearchBtn.IsEnabled = false;
				SearchEntry.IsEnabled = false;

				// Use service-bound DeparturesRefreshing so RefreshView shows spinner
				svc.DeparturesRefreshing = true;

				StatusLabel.TextColor = (Color)Application.Current.Resources["Warning"];
				svc.StatusResponse = "Working...";
				SemanticScreenReader.Announce("Searching for departures...");

				// Perform request
				var status = await svc.RefreshDataAsync(SearchEntry.Text.Trim());

				// Restore UI
				svc.DeparturesRefreshing = false;
				SearchEntry.IsEnabled = true;
				SearchBtn.IsEnabled = true;

				// StatusResponse is already set by service; adjust color and view
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
				svc.DeparturesRefreshing = false;
				svc.StatusResponse = $"Search error: {ex.Message}";
				StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
				ShowDeparturesView(false);
			}
		}

		public async void OnFindClicked(object? sender, EventArgs e)
		{
			try
			{
				var query = CurrentStop?.Trim() ?? string.Empty;
				if (string.IsNullOrWhiteSpace(query))
				{
					svc.StatusResponse = "Enter search text for Find";
					StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
					ShowPointFinderView(false);
					return;
				}

				// UI state
				FindBtn.IsEnabled = false;

				svc.PointFinderRefreshing = true;
				svc.StatusResponse = "Searching...";

				SemanticScreenReader.Announce("Searching for stops...");

				var status = await svc.QueryPointFinderAsync(query, limit: 50, stopsOnly: true);

				svc.PointFinderRefreshing = false;
				FindBtn.IsEnabled = true;

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
				SearchEntry.Focus();
			}
			catch (Exception ex)
			{
				svc.PointFinderRefreshing = false;
				svc.StatusResponse = $"Find error: {ex.Message}";
				StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
				ShowPointFinderView(false);
			}
		}

		private async void OnPointFinderSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			try
			{
				if (e.CurrentSelection == null || e.CurrentSelection.Count == 0)
					return;

				var selected = e.CurrentSelection[0] as PointResult;
				if (selected == null)
					return;

				CurrentStop = selected.Id;

				// Clear selection
				PointFinderView.SelectedItem = null;

				// Hide PointFinder and show departures
				ShowPointFinderView(false);

				// Trigger departure search
				await Task.Yield();
				OnSearchClicked(null, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				svc.StatusResponse = $"Selection error: {ex.Message}";
				StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
			}
		}

		void ShowDeparturesView(bool show)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				RefView.IsVisible = show;
				PointFinderView.IsVisible = !show && PointFinderView.IsVisible;
			});
		}

		void ShowPointFinderView(bool show)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				PointFinderView.IsVisible = show;
				RefView.IsVisible = !show && RefView.IsVisible;
			});
		}

		public void OnUnfocused(object? sender, FocusEventArgs e)
		{
			SearchEntry.IsEnabled = false;
			SearchEntry.IsEnabled = true;
		}

		public void OnFocused(object? sender, FocusEventArgs e)
		{
			StatusLabel.TextColor = (Color)Application.Current.Resources["Warning"];
			svc.StatusResponse = "Ready to go";
		}

		public ICommand RefreshCommand => new Command(() =>
		{
			if (string.IsNullOrWhiteSpace(CurrentStop))
			{
				svc.StatusResponse = "Enter valid ID";
				StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
				RefView.IsRefreshing = false;
				ShowDeparturesView(false);
				return;
			}
			OnSearchClicked(null, new EventArgs());
		});

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
				try
				{
					_typingCts?.Cancel();
					_typingCts?.Dispose();
					_typingCts = null;
				}
				catch { }

				DepartureListView.ItemsSource = null;
				PointFinderView.ItemsSource = null;

				PointFinderView.SelectionChanged -= OnPointFinderSelectionChanged;
				FindBtn.Clicked -= OnFindClicked;
				SearchBtn.Clicked -= OnSearchClicked;

				if (svc is IDisposable d)
					d.Dispose();
			}

			disposed = true;
		}

		~MainPage()
		{
			Dispose(false);
		}

		private void OnTapped(object sender, TappedEventArgs e)
		{
			SearchEntry.IsEnabled = false;
			SearchEntry.IsEnabled = true;
		}

		private CancellationTokenSource? _typingCts;

		private async void OnTextChanged(object sender, TextChangedEventArgs e)
		{
			// Cancel any pending debounce
			try
			{
				_typingCts?.Cancel();
				_typingCts?.Dispose();
			}
			catch { /* ignore */ }

			// Create a new token source for this keystroke
			_typingCts = new CancellationTokenSource();
			var token = _typingCts.Token;

			// Only start debounce when user typed more than 2 characters
			if (SearchEntry.Text.Length <= 2) return;

			try
			{
				// Wait 2 seconds; if cancelled by another keystroke, TaskCanceledException will be thrown
				await Task.Delay(TimeSpan.FromSeconds(2), token);

				// If not cancelled, invoke the Find action on the UI thread
				if (!token.IsCancellationRequested)
				{
					MainThread.BeginInvokeOnMainThread(() =>
					{
						CurrentStop = SearchEntry.Text;
						// Prevent reentrancy if Find is already running/disabled
						if (FindBtn.IsEnabled)
						{
							OnFindClicked(SearchEntry, EventArgs.Empty);
						}
					});
				}
			}
			catch (TaskCanceledException)
			{
				// expected when a new keystroke arrives; swallow silently
			}
			catch (Exception ex)
			{
				// Log or surface unexpected errors via StatusResponse
				svc.StatusResponse = $"Input debounce error: {ex.Message}";
				StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
			}
		}
	}
}
