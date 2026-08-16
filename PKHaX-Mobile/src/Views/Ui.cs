namespace PKHaX.Mobile.Views;

/// <summary>
/// Small builders for the editor's rows. The entity editor has well over a hundred fields, so the UI is
/// composed in code rather than XAML: repeated rows (6 EVs, 6 IVs, 4 moves, N ribbons) come from loops.
/// </summary>
public static class Ui
{
	public static Color Bg => Get("Background", Color.FromArgb("#0B1220"));
	public static Color Surface => Get("Surface", Color.FromArgb("#151E30"));
	public static Color SurfaceAlt => Get("SurfaceAlt", Color.FromArgb("#1F2A40"));
	public static Color Stroke => Get("SurfaceStroke", Color.FromArgb("#2A3650"));
	public static Color Text => Get("Text", Color.FromArgb("#EAF0FA"));
	public static Color Muted => Get("Muted", Color.FromArgb("#93A1BC"));
	public static Color Accent => Get("Accent", Color.FromArgb("#E4572E"));
	public static Color Positive => Get("Positive", Color.FromArgb("#2FA36B"));

	private static Color Get(string key, Color fallback) =>
		Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c ? c : fallback;

	public static Label SectionHeader(string text) => new()
	{
		Text = text.ToUpperInvariant(),
		FontSize = 12,
		FontAttributes = FontAttributes.Bold,
		TextColor = Accent,
		Margin = new Thickness(4, 18, 4, 2),
	};

	public static Label Caption(string text) => new()
	{
		Text = text, FontSize = 11, TextColor = Muted, Margin = new Thickness(4, 0, 4, 6),
	};

	public static Border Card(View content) => new()
	{
		Content = content,
		BackgroundColor = Surface,
		Stroke = Stroke,
		StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
		Padding = 12,
	};

	/// <summary>A label on the left, a value-button on the right that opens a picker.</summary>
	public static (Grid Row, Button Value) PickerRow(string label, string value)
	{
		var grid = new Grid
		{
			ColumnDefinitions = [new(GridLength.Star), new(new GridLength(1.35, GridUnitType.Star))],
			ColumnSpacing = 8,
			Margin = new Thickness(0, 3),
		};
		grid.Add(new Label { Text = label, FontSize = 14, TextColor = Text, VerticalOptions = LayoutOptions.Center }, 0);
		var btn = new Button
		{
			Text = value, FontSize = 14, HeightRequest = 42, CornerRadius = 10,
			BackgroundColor = SurfaceAlt, TextColor = Text, Padding = new Thickness(10, 0),
		};
		grid.Add(btn, 1);
		return (grid, btn);
	}

	/// <summary>A label on the left, a free-text entry on the right.</summary>
	public static (Grid Row, Entry Field) EntryRow(string label, string value, Keyboard? keyboard = null, int maxLength = 0)
	{
		var grid = new Grid
		{
			ColumnDefinitions = [new(GridLength.Star), new(new GridLength(1.35, GridUnitType.Star))],
			ColumnSpacing = 8,
			Margin = new Thickness(0, 3),
		};
		grid.Add(new Label { Text = label, FontSize = 14, TextColor = Text, VerticalOptions = LayoutOptions.Center }, 0);
		var entry = new Entry
		{
			Text = value, FontSize = 14, TextColor = Text, BackgroundColor = SurfaceAlt,
			Keyboard = keyboard ?? Keyboard.Default, HeightRequest = 42,
		};
		if (maxLength > 0) entry.MaxLength = maxLength;
		grid.Add(entry, 1);
		return (grid, entry);
	}

	/// <summary>A label and an on/off switch.</summary>
	public static (Grid Row, Switch Toggle) SwitchRow(string label, bool value)
	{
		var grid = new Grid
		{
			ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)],
			Margin = new Thickness(0, 3),
		};
		grid.Add(new Label { Text = label, FontSize = 14, TextColor = Text, VerticalOptions = LayoutOptions.Center }, 0);
		var sw = new Switch { IsToggled = value, OnColor = Accent, VerticalOptions = LayoutOptions.Center };
		grid.Add(sw, 1);
		return (grid, sw);
	}

	/// <summary>Label, numeric entry, and a live max/among-total hint - used for EVs, IVs and counters.</summary>
	public static (Grid Row, Entry Field) NumberRow(string label, int value, string hint = "")
	{
		var grid = new Grid
		{
			ColumnDefinitions = [new(GridLength.Star), new(new GridLength(90)), new(new GridLength(52))],
			ColumnSpacing = 8,
			Margin = new Thickness(0, 3),
		};
		grid.Add(new Label { Text = label, FontSize = 14, TextColor = Text, VerticalOptions = LayoutOptions.Center }, 0);
		var entry = new Entry
		{
			Text = value.ToString(), FontSize = 14, TextColor = Text, BackgroundColor = SurfaceAlt,
			Keyboard = Keyboard.Numeric, HeightRequest = 42, HorizontalTextAlignment = TextAlignment.Center,
		};
		grid.Add(entry, 1);
		grid.Add(new Label { Text = hint, FontSize = 11, TextColor = Muted, VerticalOptions = LayoutOptions.Center }, 2);
		return (grid, entry);
	}

	public static Label ReadOnlyRow(string label, string value) => new()
	{
		FormattedText = new FormattedString
		{
			Spans =
			{
				new Span { Text = label + "   ", FontSize = 13, TextColor = Muted },
				new Span { Text = value, FontSize = 13, TextColor = Text, FontAttributes = FontAttributes.Bold },
			},
		},
		Margin = new Thickness(0, 3),
	};

	public static Button Action(string text, Color? color = null) => new()
	{
		Text = text, FontSize = 14, HeightRequest = 44, CornerRadius = 10,
		BackgroundColor = color ?? SurfaceAlt, TextColor = color is null ? Text : Colors.White,
	};

	public static int ParseInt(string? s, int fallback, int min, int max)
	{
		if (!int.TryParse(s?.Trim(), out var v)) return fallback;
		return Math.Clamp(v, min, max);
	}
}
