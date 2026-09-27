using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Core.View;
using AndroidResource = global::DDepartures.Resource;

namespace DDepartures.Platforms.Android.Widgets;

[Activity(
	Theme = "@style/Maui.MainTheme",
	Exported = true,
	WindowSoftInputMode = SoftInput.AdjustResize)]
public class DepartureWidgetConfigurationActivity : Activity
{
	private int _widgetId = -1;

	private EditText? _searchBox;
	private global::Android.Widget.ListView? _resultsList;
	private global::Android.Widget.Button? _doneButton;

	private RestService? _service;

	private string? _selectedStopId;
	private string? _selectedStopName;

	private static readonly int[] IntervalMinutesOptions = [30, 60, 120, 180, 240, 600];
	private static readonly string[] IntervalLabels =
		["30 min", "1 hour", "2 hours", "3 hours", "4 hours", "10 hours"];
	private global::Android.Widget.Spinner? _intervalSpinner;

	// The exact set of results the currently-visible adapter was built from - the
	// tap handler resolves against this, never against the live _service.PointResults,
	// so a race with an overlapping search can't make a tap resolve to the wrong stop.
	private List<PointResult> _currentResults = new();

	private CancellationTokenSource? _searchDebounceCts;

	private WidgetSettings _existingSettings = WidgetSettings.Empty;

	private global::Android.Widget.TextView? _resultsStatus;

	private void SetStatus(string text)
	{
		_resultsStatus!.Text = text;
		_resultsStatus.Visibility = ViewStates.Visible;
	}

	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		_widgetId = Intent?.GetIntExtra(AppWidgetManager.ExtraAppwidgetId, -1) ?? -1;
		if (_widgetId == -1) { SetResult(Result.Canceled); Finish(); return; }

		_service = new RestService();
		_existingSettings = WidgetStorage.Load(this, _widgetId);   // add this line
		BuildUi();
	}


	private void BuildUi()
	{
		var layout = new global::Android.Widget.LinearLayout(this)
		{
			Orientation = Orientation.Vertical
		};

		layout.SetPadding(
			24,
			24,
			24,
			24);


		var title = new global::Android.Widget.TextView(this)
		{
			Text = "Select departure stop",
			TextSize = 22
		};

		title.SetTypeface(
			null,
			global::Android.Graphics.TypefaceStyle.Bold);


		layout.AddView(title);


		_searchBox = new global::Android.Widget.EditText(this)
		{
			Hint = "Search stop..."
		};

		layout.AddView(
			_searchBox,
			new global::Android.Widget.LinearLayout.LayoutParams(
				global::Android.Views.ViewGroup.LayoutParams.MatchParent,
				global::Android.Views.ViewGroup.LayoutParams.WrapContent));

		if (!string.IsNullOrWhiteSpace(_existingSettings.StopId))
		{
			_selectedStopId = _existingSettings.StopId;
			_selectedStopName = _existingSettings.StopName;
			_searchBox.Text = _existingSettings.StopName;
		}

		_resultsList = new global::Android.Widget.ListView(this)
		{
			Clickable = true,
			Focusable = true,
			ChoiceMode = global::Android.Widget.ChoiceMode.Single
		};

		_resultsList.ItemClick += (_, e) =>
		{
			if (e.Position < 0 || e.Position >= _currentResults.Count)
				return;

			var point = _currentResults[e.Position];

			_selectedStopId = point.Id;
			_selectedStopName = point.City + point.Name;

			if (_doneButton != null)
			{
				_doneButton.Enabled = true;
				_doneButton.Text = "Done";
			}
		};

		layout.AddView(
			_resultsList,
			new global::Android.Widget.LinearLayout.LayoutParams(
				global::Android.Views.ViewGroup.LayoutParams.MatchParent,
				0,
				1));

		_resultsStatus = new global::Android.Widget.TextView(this) { Visibility = ViewStates.Gone };
		layout.AddView(_resultsStatus);

		var intervalLabel = new global::Android.Widget.TextView(this)
		{
			Text = "Update frequency",
			TextSize = 16
		};
		layout.AddView(intervalLabel);

		_intervalSpinner = new global::Android.Widget.Spinner(this);
		var intervalAdapter = new global::Android.Widget.ArrayAdapter<string>(
			this,
			global::Android.Resource.Layout.SimpleSpinnerItem,
			IntervalLabels);
		intervalAdapter.SetDropDownViewResource(global::Android.Resource.Layout.SimpleSpinnerDropDownItem);
		_intervalSpinner.Adapter = intervalAdapter;
		var savedIndex = Array.IndexOf(IntervalMinutesOptions, _existingSettings.IntervalMinutes);
		_intervalSpinner.SetSelection(savedIndex >= 0 ? savedIndex : 1);

		layout.AddView(
			_intervalSpinner,
			new global::Android.Widget.LinearLayout.LayoutParams(
				global::Android.Views.ViewGroup.LayoutParams.MatchParent,
				global::Android.Views.ViewGroup.LayoutParams.WrapContent));

		var buttonRow = new global::Android.Widget.LinearLayout(this)
		{
			Orientation = Orientation.Horizontal
		};

		buttonRow.SetGravity(GravityFlags.Center);

		_doneButton = new global::Android.Widget.Button(this)
		{
			Text = "Done",
			Enabled = !string.IsNullOrWhiteSpace(_selectedStopId)
		};

		var cancelButton = new global::Android.Widget.Button(this)
		{
			Text = "Cancel"
		};

		_doneButton.Click += async (_, _) =>
		{
			if (string.IsNullOrWhiteSpace(_selectedStopId))
				return;

			var intervalIndex = _intervalSpinner?.SelectedItemPosition ?? 2;
			var intervalMinutes = IntervalMinutesOptions[intervalIndex];

			SaveWidget(_selectedStopId, _selectedStopName ?? "", intervalMinutes);

			var resultIntent = new Intent();
			resultIntent.PutExtra(AppWidgetManager.ExtraAppwidgetId, _widgetId);
			SetResult(Result.Ok, resultIntent);

			await DepartureWidgetUpdater.UpdateAsync(this, _widgetId);

			Finish();
		};

		cancelButton.Click += (_, _) =>
		{
			SetResult(Result.Canceled);
			Finish();
		};

		buttonRow.AddView(
			_doneButton,
			new global::Android.Widget.LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));

		buttonRow.AddView(
			cancelButton,
			new global::Android.Widget.LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));

		layout.AddView(buttonRow, new global::Android.Widget.LinearLayout.LayoutParams(
			global::Android.Views.ViewGroup.LayoutParams.MatchParent,
			global::Android.Views.ViewGroup.LayoutParams.WrapContent));

		SetContentView(layout);

		ApplySystemInsets(layout);


		_searchBox.TextChanged += (_, _) => QueueSearch();
	}


	private void ApplySystemInsets(
		global::Android.Views.View root)
	{
		ViewCompat.SetOnApplyWindowInsetsListener(
			root,
			new InsetsListener());

		ViewCompat.RequestApplyInsets(root);
	}


	private sealed class InsetsListener :
		Java.Lang.Object,
		IOnApplyWindowInsetsListener
	{
		public WindowInsetsCompat OnApplyWindowInsets(
			global::Android.Views.View view,
			WindowInsetsCompat insets)
		{
			var bars =
				insets.GetInsets(
					WindowInsetsCompat.Type.SystemBars());

			view.SetPadding(
				view.PaddingLeft,
				bars.Top + 24,
				view.PaddingRight,
				bars.Bottom + 24);

			return insets;
		}
	}


	// Debounces the search box the same way MainPage does for the phone app - waits
	// for a pause in typing before actually querying, and a later keystroke's timer
	// cancels any earlier one still waiting.
	private void QueueSearch()
	{
		_searchDebounceCts?.Cancel();
		_searchDebounceCts?.Dispose();

		var cts = new CancellationTokenSource();
		_searchDebounceCts = cts;

		_ = DebounceAndSearchAsync(cts);
	}


	private async Task DebounceAndSearchAsync(CancellationTokenSource cts)
	{
		try
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), cts.Token);
		}
		catch (TaskCanceledException)
		{
			return;
		}

		if (cts.IsCancellationRequested)
			return;

		await SearchAsync(cts.Token);
	}


	private async Task SearchAsync(CancellationToken cancellationToken)
	{
		if (_service == null || _searchBox == null || _resultsList == null) return;

		var query = _searchBox.Text?.Trim();
		if (string.IsNullOrWhiteSpace(query))
		{
			_resultsList.Adapter = null;
			_currentResults = new List<PointResult>();
			_resultsStatus!.Visibility = ViewStates.Gone;
			return;
		}

		try
		{
			await _service.QueryPointFinderAsync(query, limit: 15, stopsOnly: true);
		}
		catch (Exception)
		{
			if (cancellationToken.IsCancellationRequested) return;
			_resultsList.Adapter = null;
			SetStatus("Search failed — check your connection.");
			return;
		}

		if (cancellationToken.IsCancellationRequested) return;

		_currentResults = _service.PointResults?.ToList() ?? new List<PointResult>();

		if (_currentResults.Count == 0)
		{
			_resultsList.Adapter = null;
			SetStatus("No stops found.");
			return;
		}

		_resultsStatus!.Visibility = ViewStates.Gone;
		_resultsList.Adapter = new global::Android.Widget.ArrayAdapter<string>(
			this,
			global::Android.Resource.Layout.SimpleListItemSingleChoice,   // see #4
			_currentResults.Select(x => $"{x.City}{x.Name}").ToArray());
	}


	private void SaveWidget(string stopId, string stopName, int intervalMinutes)
	{
		WidgetStorage.Save(this, _widgetId, new WidgetSettings
		{
			StopId = stopId,
			StopName = stopName,
			IntervalMinutes = intervalMinutes
		});

	}


	protected override void OnDestroy()
	{
		_searchDebounceCts?.Cancel();
		_searchDebounceCts?.Dispose();
		_service?.Dispose();

		base.OnDestroy();
	}
}