using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PKHaX.Hacks.Editing;

namespace PKHaX.Linux.Shared;

public sealed record HackEditorCheckOutcome(bool Passed, string Said);

public sealed record HackEditorCheck(string Offer, string Button, string Running, Func<Task<HackEditorCheckOutcome>> Run);

public sealed class HackEditorView : UserControl
{
	private static readonly IBrush Ink = new SolidColorBrush(Color.Parse("#EAF0FA"));
	private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#93A1BC"));
	private static readonly IBrush Well = new SolidColorBrush(Color.Parse("#0B1220"));

	private readonly ListBox slots;
	private readonly TextBlock detail;
	private bool refreshing;
	private readonly StackPanel editor = new() { Spacing = 6, IsVisible = false };
	private readonly Func<HackMonEditModel, string?>? apply;
	private readonly string? appRefusal;
	private readonly StackPanel checkPanel = new() { Spacing = 6, IsVisible = false };
	private HackEditorCheck? check;

	public HackEditorView(HackSaveModel model, string? appRefusal, Func<HackMonEditModel, string?>? apply = null)
	{
		Model = model;
		this.apply = apply;
		this.appRefusal = appRefusal;
		slots = new ListBox { Background = Well, Foreground = Ink, MinHeight = 200, MaxHeight = 420, CornerRadius = new CornerRadius(8), ItemsSource = HackEditorText.Rows(model) };
		detail = new TextBlock { Foreground = Ink, FontSize = 13, TextWrapping = TextWrapping.Wrap, Text = "Select a Pokemon above." };
		slots.SelectionChanged += (_, _) =>
		{
			if (!refreshing && slots.SelectedItem is HackSlotRow row)
			{
				detail.Text = HackEditorText.Detail(Model, row.Where, appRefusal);
				BuildEditor(row.Where);
			}
		};
		var panel = new StackPanel { Spacing = 8 };
		if (model.BoxesReason is { } why)
			panel.Children.Add(new TextBlock { Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Text = "Boxes: " + why });
		panel.Children.Add(checkPanel);
		panel.Children.Add(slots);
		panel.Children.Add(new TextBlock { Text = "SELECTED POKÉMON", FontSize = 12, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#E4572E")) });
		panel.Children.Add(detail);
		panel.Children.Add(editor);
		Content = panel;
	}

	public bool Editing => editor.IsVisible;

	public event Action? Checked;

	public HackEditorCheck? Check
	{
		get => check;
		set
		{
			check = value;
			BuildCheck(null);
		}
	}

	private void BuildCheck(string? said)
	{
		checkPanel.Children.Clear();
		var offer = check?.Offer;
		checkPanel.IsVisible = check is not null && (offer is not null || said is not null);
		if (!checkPanel.IsVisible)
			return;
		checkPanel.Children.Add(new TextBlock { Text = "THIS HACK'S SAVES", FontSize = 12, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#E4572E")) });
		var status = new TextBlock { Foreground = Ink, FontSize = 12, TextWrapping = TextWrapping.Wrap, Text = said ?? offer };
		if (check is { } offered)
		{
			var button = new Button { Content = offered.Button, Background = new SolidColorBrush(Color.Parse("#2F6FA3")), Foreground = Brushes.White, CornerRadius = new CornerRadius(8) };
			button.Click += async (_, _) =>
			{
				if (check is not { } run)
					return;
				button.IsEnabled = false;
				status.Text = run.Running;
				HackEditorCheckOutcome result;
				try
				{
					result = await run.Run();
				}
				catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException or IOException or UnauthorizedAccessException)
				{
					result = new HackEditorCheckOutcome(false, $"Nothing changed: the check stopped ({ex.Message}).");
				}
				BuildCheck(result.Said);
				if (result.Passed)
					Checked?.Invoke();
			};
			checkPanel.Children.Add(button);
		}
		checkPanel.Children.Add(status);
	}

	private void BuildEditor(HackSlotRef where, string? said = null)
	{
		editor.Children.Clear();
		editor.IsVisible = false;
		if (apply is null || Model.WhyNotEditable(where) is not null || Model.Edit(where) is not { } edit)
			return;
		var values = new List<(HackEditField Field, Func<string> Text)>();
		var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("110,*"), RowSpacing = 4 };
		foreach (var field in HackEditFields.For(edit, Model.Names))
		{
			var (view, text) = Build(field, edit);
			view.IsEnabled = field.CanEdit(edit);
			if (!field.CanEdit(edit) && field.WhyLocked(edit) is { } locked)
				ToolTip.SetTip(view, locked);
			var row = grid.RowDefinitions.Count;
			grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			var label = new TextBlock { Text = field.Label, Foreground = Muted, VerticalAlignment = VerticalAlignment.Center };
			Grid.SetRow(label, row);
			Grid.SetRow(view, row);
			Grid.SetColumn(view, 1);
			grid.Children.Add(label);
			grid.Children.Add(view);
			values.Add((field, text));
		}
		var status = new TextBlock { Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Text = said ?? "Apply changes the save in memory; Save Changes Back writes the file." };
		var button = new Button { Content = "Apply to this slot", Background = new SolidColorBrush(Color.Parse("#2FA36B")), Foreground = Brushes.White, CornerRadius = new CornerRadius(8) };
		button.Click += (_, _) =>
		{
			if (Model.Edit(where) is not { } fresh)
			{
				status.Text = $"{where} no longer holds a Pokemon.";
				return;
			}
			var error = HackEditFields.Fill(fresh, [.. values.Select(v => (v.Field, v.Text()))]);
			var changed = fresh.Changed.Count > 0;
			error ??= changed ? apply(fresh) : null;
			if (error is not null || !changed)
			{
				status.Text = error ?? "Nothing changed.";
				return;
			}
			var keep = slots.SelectedIndex;
			refreshing = true;
			slots.ItemsSource = HackEditorText.Rows(Model);
			slots.SelectedIndex = keep;
			refreshing = false;
			detail.Text = HackEditorText.Detail(Model, where, appRefusal);
			var note = HackEditorText.AppliedNote(fresh);
			BuildEditor(where, "Applied. Use Save Changes Back to write the file." + (note is null ? "" : " " + note));
		};
		editor.Children.Add(new TextBlock { Text = "EDIT", FontSize = 12, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#E4572E")) });
		if (Model.NamesBanner is { } banner)
			editor.Children.Add(new TextBlock { Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Text = banner });
		editor.Children.Add(grid);
		editor.Children.Add(button);
		editor.Children.Add(status);
		editor.IsVisible = true;
	}

	private static (Control View, Func<string> Text) Build(HackEditField field, HackMonEditModel edit)
	{
		var current = field.Read(edit);
		if (field.Kind == HackEditRowKind.Picker && field.Named)
		{
			var picker = new HackPickerBox(field, current, Ink, Well);
			return (picker, () => picker.Text);
		}
		if (field.Kind is HackEditRowKind.Choice or HackEditRowKind.Flag)
		{
			var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, Foreground = Ink, Background = Well };
			var at = -1;
			for (var i = 0; i < field.Choices.Count; i++)
			{
				combo.Items.Add(field.Choices[i]);
				if (field.Choices[i] == current)
					at = i;
			}
			if (at < 0 && current.Length > 0)
			{
				combo.Items.Add(current);
				at = combo.Items.Count - 1;
			}
			combo.SelectedIndex = at;
			return (combo, () => combo.SelectedItem as string ?? current);
		}
		var box = new TextBox { Text = current, PlaceholderText = field.Placeholder, Foreground = Ink, Background = Well };
		return (box, () => box.Text ?? "");
	}

	public HackSaveModel Model { get; }

	public int RowCount => slots.ItemCount;
}

public sealed class HackPickerBox : UserControl
{
	private readonly TextBox box;
	private readonly ListBox results;
	private bool picking;

	public HackPickerBox(HackEditField field, string text, IBrush ink, IBrush well)
	{
		Field = field;
		box = new TextBox { Text = text, PlaceholderText = field.Placeholder, Foreground = ink, Background = well };
		results = new ListBox { MaxHeight = 180, IsVisible = false, Foreground = ink, Background = well };
		box.TextChanged += (_, _) => Refill();
		results.SelectionChanged += (_, _) =>
		{
			if (picking || results.SelectedItem is not string chosen)
				return;
			picking = true;
			box.Text = chosen;
			results.ItemsSource = null;
			results.IsVisible = false;
			picking = false;
		};
		var stack = new StackPanel { Spacing = 2 };
		stack.Children.Add(box);
		stack.Children.Add(results);
		Content = stack;
	}

	public HackEditField Field { get; }

	public string Text => box.Text ?? "";

	public IReadOnlyList<string> Suggestions { get; private set; } = [];

	private void Refill()
	{
		if (picking)
			return;
		Suggestions = HackEditFields.Suggest(Field, box.Text);
		picking = true;
		results.ItemsSource = Suggestions;
		results.IsVisible = Suggestions.Count > 0;
		picking = false;
	}
}
