using PKHaX.Mobile.Services;
using PKHeX.Core;

namespace PKHaX.Mobile.Views;

/// <summary>
/// The bag. One collapsible card per pouch; each occupied slot shows its item and quantity, and empty
/// slots at the end let you add items. Quantities are clamped per-item through the bag's own rules
/// (<see cref="PlayerBag.GetMaxCount(InventoryType,int)"/>), so a value the game cannot store never lands.
/// Nothing is committed until Apply, which calls <c>bag.CopyTo(sav)</c> exactly like the desktop editor.
/// </summary>
public sealed class BagPage : ContentPage
{
	private readonly SaveManager saves;
	private readonly VerticalStackLayout root = new() { Padding = 16, Spacing = 2 };
	private PlayerBag bag = null!;
	private string[] itemNames = [];
	private readonly HashSet<InventoryType> expanded = [];

	public BagPage(SaveManager saves)
	{
		this.saves = saves;
		Title = "Bag";
		BackgroundColor = Ui.Bg;
		Content = new ScrollView { Content = root };
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		var sav = saves.Save;
		if (sav is null) { _ = Shell.Current.Navigation.PopAsync(); return; }
		if (root.Count > 0) return;

		bag = sav.Inventory;
		// The item name table depends on the save's context/version, not the generic list.
		itemNames = GameInfo.Strings.GetItemStrings(sav.Context, sav.Version);
		Build();
	}

	private void Build()
	{
		root.Clear();
		if (bag.Pouches.Count == 0)
		{
			root.Add(Ui.Caption("This save format exposes no editable bag."));
			return;
		}

		root.Add(Ui.Caption("Tap a pouch to open it. Quantities are capped by what the game can store."));

		foreach (var pouch in bag.Pouches)
			root.Add(PouchCard(pouch));

		var apply = Ui.Action("Apply bag changes", Ui.Positive);
		apply.Margin = new Thickness(0, 14, 0, 24);
		apply.Clicked += async (_, _) =>
		{
			bag.CopyTo(saves.Save!);
			await Shell.Current.Navigation.PopAsync();
		};
		root.Add(apply);
	}

	private View PouchCard(InventoryPouch pouch)
	{
		var v = new VerticalStackLayout { Spacing = 0 };
		var isOpen = expanded.Contains(pouch.Type);

		var head = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)] };
		head.Add(new Label
		{
			Text = $"{pouch.Type}",
			FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Ui.Text,
			VerticalOptions = LayoutOptions.Center,
		}, 0);
		head.Add(new Label
		{
			Text = $"{pouch.Count} items  {(isOpen ? "▾" : "▸")}",
			FontSize = 12, TextColor = Ui.Muted, VerticalOptions = LayoutOptions.Center,
		}, 1);

		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) =>
		{
			if (!expanded.Remove(pouch.Type)) expanded.Add(pouch.Type);
			Build();
		};
		head.GestureRecognizers.Add(tap);
		v.Add(head);

		if (isOpen)
		{
			var legal = LegalItems(pouch);
			int shown = 0;
			for (int i = 0; i < pouch.Items.Length; i++)
			{
				var item = pouch.Items[i];
				// show every occupied slot, plus one spare row to add into
				if (item.Count == 0 && item.Index == 0)
				{
					if (shown >= pouch.Items.Length) break;
					v.Add(AddRow(pouch, item, legal));
					break;
				}
				v.Add(ItemRow(pouch, item, legal));
				shown++;
			}

			var tools = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
			var maxAll = Ui.Action("Max all");
			maxAll.Clicked += (_, _) =>
			{
				foreach (var it in pouch.Items)
					if (it.Index != 0) it.Count = bag.GetMaxCount(pouch.Type, it.Index);
				Build();
			};
			var giveAll = Ui.Action("Give all items");
			giveAll.Clicked += async (_, _) =>
			{
				if (!await DisplayAlertAsync("Give all", $"Fill the {pouch.Type} pouch with every legal item?", "Fill", "Cancel")) return;
				try { pouch.GiveAllItems(bag, legal); } catch { }
				Build();
			};
			var clear = Ui.Action("Clear pouch");
			clear.Clicked += async (_, _) =>
			{
				if (!await DisplayAlertAsync("Clear", $"Remove everything from {pouch.Type}?", "Clear", "Cancel")) return;
				try { pouch.RemoveAll(); } catch { }
				Build();
			};
			tools.Add(maxAll); tools.Add(clear);
			v.Add(tools);
			v.Add(giveAll);
		}

		return Ui.Card(v);
	}

	private IReadOnlyList<NamedValue> PickList(InventoryPouch pouch)
	{
		var ids = LegalItems(pouch);
		var list = new List<NamedValue>(ids.Length + 1) { new(0, "(empty)") };
		foreach (var id in ids)
		{
			var name = id < itemNames.Length ? itemNames[id] : $"#{id}";
			if (!string.IsNullOrWhiteSpace(name)) list.Add(new NamedValue(id, name));
		}
		list.Sort(static (a, b) => a.Value == 0 ? -1 : b.Value == 0 ? 1
			: string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
		return list;
	}

	private static ushort[] LegalItems(InventoryPouch pouch)
	{
		try { return pouch.GetAllItems().ToArray(); }
		catch { return []; }
	}

	private View ItemRow(InventoryPouch pouch, InventoryItem item, ushort[] legal)
	{
		var name = item.Index < itemNames.Length ? itemNames[item.Index] : $"#{item.Index}";
		var (row, btn) = Ui.PickerRow(string.IsNullOrWhiteSpace(name) ? $"#{item.Index}" : name, item.Count.ToString());

		// left side re-picks the item, right side edits the quantity
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, _) =>
		{
			var picked = await PickerPage.ShowAsync("Item", PickList(pouch), item.Index);
			if (picked is null) return;
			item.Index = picked.Value.Value;
			if (item.Index == 0) item.Clear();
			else if (item.Count == 0) item.Count = 1;
			Build();
		};
		if (row.Children[0] is View lbl) lbl.GestureRecognizers.Add(tap);

		btn.Clicked += async (_, _) =>
		{
			var max = SafeMax(pouch, item.Index);
			var input = await DisplayPromptAsync("Quantity", $"0 - {max}", initialValue: item.Count.ToString(), keyboard: Keyboard.Numeric);
			if (input is null) return;
			item.Count = Ui.ParseInt(input, item.Count, 0, max);
			if (item.Count == 0) item.Clear();
			Build();
		};
		return row;
	}

	private View AddRow(InventoryPouch pouch, InventoryItem slot, ushort[] legal)
	{
		var add = Ui.Action("+ Add item");
		add.Clicked += async (_, _) =>
		{
			var picked = await PickerPage.ShowAsync("Add item", PickList(pouch), 0);
			if (picked is null || picked.Value.Value == 0) return;
			slot.Index = picked.Value.Value;
			slot.SetNewDetails(Math.Min(1, SafeMax(pouch, slot.Index)) is 0 ? 1 : 1);
			Build();
		};
		return add;
	}

	private int SafeMax(InventoryPouch pouch, int itemIndex)
	{
		try
		{
			var m = bag.GetMaxCount(pouch.Type, itemIndex);
			return m > 0 ? m : bag.MaxQuantityHaX;
		}
		catch { return bag.MaxQuantityHaX; }
	}
}
