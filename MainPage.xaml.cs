using System.Windows.Input;

namespace DDepartures
{
    public partial class MainPage : ContentPage, IDisposable
    {
        readonly DDepartures.RestService svc;
        bool disposed;

        public MainPage()
        {
            InitializeComponent();
            svc = new DDepartures.RestService();
            DepartureListView.ItemsSource = svc.Items;
            StatusLabel.TextColor = (Color)Application.Current.Resources["Warning"];
            StatusLabel.Text = svc.StatusResponse;
            // svc.PeriodicRefresh(svc.LastSuccessfulArgs, 10);
        }

        public async void OnSearchClicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchEntry.Text))
            {
                StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
                StatusLabel.Text = "Enter valid ID";
                DepartureListView.ItemsSource = null;
                return;
            }
            SearchBtn.IsEnabled = false;
            SearchBtn.Text = "Wait";
            SearchEntry.IsEnabled = false;
            RefView.IsRefreshing = true;
            StatusLabel.TextColor = (Color)Application.Current.Resources["Warning"];
            StatusLabel.Text = "Working...";
            SemanticScreenReader.Announce("Searching for departures...");
            var x = await svc.RefreshDataAsync(SearchEntry.Text);
            SearchEntry.IsEnabled = true;
            SearchBtn.Text = "Search";
            SearchBtn.IsEnabled = true;
            StatusLabel.Text = svc.StatusResponse;
            RefView.IsRefreshing = false;
            if (x != ((int)System.Net.HttpStatusCode.OK))
            {
                StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
                DepartureListView.ItemsSource = null;
            }
            else
            {
                StatusLabel.TextColor = (Color)Application.Current.Resources["Success"];
                DepartureListView.ItemsSource = svc.Items;
            }
            return;
        }

        public void OnUnfocused(object? sender, FocusEventArgs e)
        {
            SearchEntry.IsEnabled = false;
            SearchEntry.IsEnabled = true;
        }
        public void OnFocused(object? sender, FocusEventArgs e)
        {
            StatusLabel.TextColor = (Color)Application.Current.Resources["Warning"];
            StatusLabel.Text = "Ready to go";
        }

        public ICommand RefreshCommand => new Command(() =>
        {
            if (SearchEntry.Text == "")
            {
                StatusLabel.TextColor = (Color)Application.Current.Resources["Error"];
                StatusLabel.Text = "Enter valid ID";
                DepartureListView.ItemsSource = null;
                RefView.IsRefreshing = false;
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
                // Managed cleanup
                DepartureListView.ItemsSource = null;

                // Unsubscribe events
                // svc.Updated -= OnSvcUpdated;

                if (svc is IDisposable d)
                    d.Dispose();
            }

            // No unmanaged cleanup expected in a ContentPage
            disposed = true;
        }

        ~MainPage()
        {
            // Safety net only; do not touch UI here
            Dispose(false);
        }
    }
}
