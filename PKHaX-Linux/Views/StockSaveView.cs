using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PKHaX.Linux.Session;

namespace PKHaX.Linux.Views;

public sealed class StockSaveView : UserControl
{
	private readonly LinuxSaveSession session;
	private readonly Action changed;
	private readonly ListBox party;
	private readonly ListBox box;
	private readonly ComboBox boxes;
	private readonly ContentControl editorHost = new();
	private readonly TextBox money;
	private readonly TextBlock status = new() { Foreground = DropPanel.Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap };
	private bool refreshing;

	public StockSaveView(LinuxSaveSession session, Action changed)
	{
		this.session = session;
		this.changed = changed;
		party = MakeList(220);
		box = MakeList(260);
		boxes = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, Foreground = DropPanel.Ink, Background = DropPanel.Well };
		var names = Enumerable.Range(0, session.BoxCount).Select(session.BoxName).ToList();
		boxes.ItemsSource = names;
		boxes.SelectedIndex = names.Count > 0 ? 0 : -1;
		boxes.SelectionChanged += (_, _) => FillBox();
		party.SelectionChanged += (_, _) => Picked(party);
		box.SelectionChanged += (_, _) => Picked(box);
		money = new TextBox { Text = session.Money?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "", Foreground = DropPanel.Ink, Background = DropPanel.Well, Width = 160 };
		var setMoney = DropPanel.MakeButton("Set money", DropPanel.Blue);
		setMoney.Click += (_, _) =>
		{
			if (!uint.TryParse((money.Text ?? "").Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value))
			{
				status.Text = $"Money: \"{money.Text}\" is not a number of zero or more";
				return;
			}
			var refusal = session.SetMoney(value);
			status.Text = refusal ?? "Money set. Use Save Changes Back to write the file.";
			if (refusal is null)
				changed();
		};
		var panel = new StackPanel { Spacing = 8 };
		panel.Children.Add(DropPanel.Title("Trainer"));
		panel.Children.Add(new TextBlock { Text = session.TrainerLine ?? "", Foreground = DropPanel.Ink, TextWrapping = TextWrapping.Wrap });
		panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "Money", Foreground = DropPanel.Muted, VerticalAlignment = VerticalAlignment.Center }, money, setMoney } });
		panel.Children.Add(DropPanel.Title("Party"));
		panel.Children.Add(party);
		if (names.Count > 0)
		{
			panel.Children.Add(DropPanel.Title("Boxes"));
			panel.Children.Add(boxes);
			panel.Children.Add(box);
		}
		panel.Children.Add(status);
		panel.Children.Add(editorHost);
		Content = panel;
		FillParty();
		FillBox();
	}

	private static ListBox MakeList(double height) => new() { Background = DropPanel.Well, Foreground = DropPanel.Ink, MaxHeight = height, CornerRadius = new CornerRadius(8) };

	private void FillParty()
	{
		refreshing = true;
		var keep = party.SelectedIndex;
		party.ItemsSource = session.PartySlots();
		party.SelectedIndex = keep;
		refreshing = false;
	}

	private void FillBox()
	{
		refreshing = true;
		var keep = box.SelectedIndex;
		box.ItemsSource = session.BoxSlots(boxes.SelectedIndex);
		box.SelectedIndex = keep;
		refreshing = false;
	}

	private void Picked(ListBox from)
	{
		if (refreshing || from.SelectedItem is not LinuxSlot slot)
			return;
		refreshing = true;
		(from == party ? box : party).SelectedIndex = -1;
		refreshing = false;
		Show(slot.Where);
	}

	private void Show(LinuxSlotRef where)
	{
		if (session.Slot(where) is not { } pk || pk.Species == 0)
		{
			editorHost.Content = new TextBlock { Text = $"{where}: empty. Filling an empty slot is not supported.", Foreground = DropPanel.Muted };
			return;
		}
		editorHost.Content = new SlotEditorView(session, where, pk, () =>
		{
			FillParty();
			FillBox();
			changed();
			Show(where);
			status.Text = $"Applied to {where}. Use Save Changes Back to write the file.";
		});
	}
}
