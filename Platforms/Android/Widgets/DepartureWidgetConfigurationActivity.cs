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


	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);

		_widgetId =
			Intent?.GetIntExtra(
				AppWidgetManager.ExtraAppwidgetId,
				-1)
			?? -1;

		if (_widgetId == -1)
		{
			SetResult(Result.Canceled);
			Finish();
			return;
		}

		_service = new RestService();

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


		_resultsList = new global::Android.Widget.ListView(this)
		{
			Clickable = true,
			Focusable = true,
			ChoiceMode = global::Android.Widget.ChoiceMode.Single
		};

		_resultsList.ItemClick += (_, e) =>
		{
			global::Android.Util.Log.Debug("DDeparturesWidget",	$"Clicked row {e.Position}");
			if (_service?.PointResults == null)
				return;

			if (e.Position < 0 || e.Position >= _service.PointResults.Count)
				return;

			var point = _service.PointResults[e.Position];

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

		var buttonRow = new global::Android.Widget.LinearLayout(this)
		{
			Orientation = Orientation.Horizontal
		};

		buttonRow.SetGravity(GravityFlags.Center);

		_doneButton = new global::Android.Widget.Button(this)
		{
			Text = "Done",
			Enabled = false
		};

		var cancelButton = new global::Android.Widget.Button(this)
		{
			Text = "Cancel"
		};

		_doneButton.Click += async (_, _) =>
		{
			if (string.IsNullOrWhiteSpace(_selectedStopId))
				return;

			SaveWidget(
				_selectedStopId,
				_selectedStopName ?? "");

			var resultIntent = new Intent();
			resultIntent.PutExtra(
				AppWidgetManager.ExtraAppwidgetId,
				_widgetId);

			SetResult(
				Result.Ok,
				resultIntent);

			await DepartureWidgetUpdater.UpdateAsync(
				this,
				_widgetId);

			Finish();
		};

		cancelButton.Click += (_, _) =>
		{
			SetResult(Result.Canceled);
			Finish();
		};

		buttonRow.AddView(
			_doneButton,
			new global::Android.Widget.LinearLayout.LayoutParams(0,	ViewGroup.LayoutParams.WrapContent,	1));

		buttonRow.AddView(
			cancelButton,
			new global::Android.Widget.LinearLayout.LayoutParams(0,	ViewGroup.LayoutParams.WrapContent,	1));

		layout.AddView(buttonRow, new global::Android.Widget.LinearLayout.LayoutParams(
			global::Android.Views.ViewGroup.LayoutParams.MatchParent,
			global::Android.Views.ViewGroup.LayoutParams.WrapContent));

		SetContentView(layout);

		ApplySystemInsets(layout);


		_searchBox.TextChanged += async (_, _) =>
		{
			await SearchAsync();
		};
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


	private async Task SearchAsync()
	{
		if (_service == null ||
			_searchBox == null ||
			_resultsList == null)
			return;


		var query =
			_searchBox.Text?.Trim();


		if (string.IsNullOrWhiteSpace(query))
		{
			_resultsList.Adapter = null;
			return;
		}


		await _service.QueryPointFinderAsync(
			query,
			limit: 15,
			stopsOnly: true);


		var items =
			_service.PointResults
				.Select(x =>
					$"{x.City}{x.Name}")
				.ToArray();


		_resultsList.Adapter =
			new global::Android.Widget.ArrayAdapter<string>(
				this,
				global::Android.Resource.Layout.SimpleListItem1,
				items);
	}


	private void SaveWidget(
		string stopId,
		string stopName)
	{
		WidgetStorage.Save(
			this,
			_widgetId,
			new WidgetSettings
			{
				StopId = stopId,
				StopName = stopName
			});
	}


	protected override void OnDestroy()
	{
		_service?.Dispose();

		base.OnDestroy();
	}
}