using PKHaX.Mobile.Services;
using PKHeX.Core;

namespace PKHaX.Mobile.Views;

/// <summary>
/// Box housekeeping: rename a box, set its wallpaper, and run the bulk operations (sort, clear, compress,
/// heal, max friendship). The bulk helpers are PKHeX.Core's own — they already skip slots the save marks
/// overwrite-protected, so a locked battle-team member is never clobbered by a sort or a clear.
/// </summary>
public sealed class BoxToolsPage : ContentPage
{
	private readonly SaveManager saves;
	private readonly VerticalStackLayout root = new() { Padding = 16, Spacing = 2 };
	private int box;

	public BoxToolsPage(SaveManager saves, int box)
	{
		this.saves = saves;
		this.box = box;
		Title = "Box tools";
		BackgroundColor = Ui.Bg;
		Content = new ScrollView { Content = root };
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (saves.Save is null) { _ = Shell.Current.Navigation.PopAsync(); return; }
		Build();
	}

	private void Build()
	{
		root.Clear();
		var sav = saves.Save!;

		// ---- which box
		root.Add(Ui.SectionHeader("Box"));
		var picker = new VerticalStackLayout { Spacing = 0 };
		var boxes = new List<NamedValue>();
		for (int i = 0; i < sav.BoxCount; i++)
			boxes.Add(new NamedValue(i, NameOf(sav, i)));

		var (bRow, bBtn) = Ui.PickerRow("Editing", NameOf(sav, box));
		bBtn.Clicked += async (_, _) =>
		{
			var picked = await PickerPage.ShowAsync("Box", boxes, box);
			if (picked is null) return;
			box = picked.Value.Value;
			Build();
		};
		picker.Add(bRow);

		// ---- name (only saves implementing IBoxDetailName can store one)
		if (sav is IBoxDetailName namer)
		{
			var (nRow, nEntry) = Ui.EntryRow("Name", NameOf(sav, box));
			nEntry.Unfocused += (_, _) =>
			{
				var t = (nEntry.Text ?? "").Trim();
				if (t.Length == 0) return;
				try { namer.SetBoxName(box, t); } catch { }
				Build();
			};
			picker.Add(nRow);
		}
		else
		{
			picker.Add(Ui.Caption("This save format does not store box names."));
		}

		// ---- wallpaper
		if (sav is IBoxDetailWallpaper paper)
		{
			var names = GameInfo.Strings.wallpapernames;
			var papers = new List<NamedValue>();
			for (int i = 0; i < names.Length && i < 32; i++)
				if (!string.IsNullOrWhiteSpace(names[i])) papers.Add(new NamedValue(i, names[i]));

			int cur;
			try { cur = paper.GetBoxWallpaper(box); } catch { cur = 0; }
			var curName = papers.FirstOrDefault(x => x.Value == cur).Name ?? cur.ToString();

			var (wRow, wBtn) = Ui.PickerRow("Wallpaper", curName);
			wBtn.Clicked += async (_, _) =>
			{
				var picked = await PickerPage.ShowAsync("Wallpaper", papers, cur);
				if (picked is null) return;
				try { paper.SetBoxWallpaper(box, picked.Value.Value); } catch { }
				Build();
			};
			picker.Add(wRow);
		}
		root.Add(Ui.Card(picker));

		// ---- this box
		root.Add(Ui.SectionHeader("This box"));
		var one = new VerticalStackLayout { Spacing = 6 };

		var sortOne = Ui.Action("Sort by species");
		sortOne.Clicked += (_, _) => Run(() => sav.SortBoxes(box, box + 1), "sorted");
		one.Add(sortOne);

		var clearOne = Ui.Action("Clear this box");
		clearOne.Clicked += async (_, _) =>
		{
			if (!await DisplayAlertAsync("Clear box", $"Delete every Pokemon in {NameOf(sav, box)}?", "Clear", "Cancel")) return;
			Run(() => sav.ClearBoxes(box, box + 1), "cleared");
		};
		one.Add(clearOne);

		var healOne = Ui.Action("Heal all in this box");
		healOne.Clicked += (_, _) => Run(() => sav.ModifyBoxes(pk => pk.Heal(), box, box + 1), "healed");
		one.Add(healOne);
		root.Add(Ui.Card(one));

		// ---- all boxes
		root.Add(Ui.SectionHeader("All boxes"));
		var all = new VerticalStackLayout { Spacing = 6 };

		var sortAll = Ui.Action("Sort every box");
		sortAll.Clicked += async (_, _) =>
		{
			if (!await DisplayAlertAsync("Sort all", "Sort every box in the PC?", "Sort", "Cancel")) return;
			Run(() => sav.SortBoxes(), "sorted");
		};
		all.Add(sortAll);

		var compress = Ui.Action("Compress (pull empty slots to the end)");
		compress.Clicked += (_, _) =>
		{
			try
			{
				Span<int> none = stackalloc int[0];
				sav.CompressStorage(out var stored, none);
				Toast($"compressed — {stored} stored");
			}
			catch (Exception ex) { Toast($"failed: {ex.Message}"); }
			Build();
		};
		all.Add(compress);

		var maxFriend = Ui.Action("Max friendship everywhere");
		maxFriend.Clicked += (_, _) => Run(() => sav.ModifyBoxes(pk => pk.CurrentFriendship = 255), "updated");
		all.Add(maxFriend);

		var clearAll = Ui.Action("Clear EVERY box");
		clearAll.Clicked += async (_, _) =>
		{
			if (!await DisplayAlertAsync("Clear all", "Delete every Pokemon in every box? This cannot be undone from the app.", "Delete all", "Cancel")) return;
			Run(() => sav.ClearBoxes(), "cleared");
		};
		all.Add(clearAll);
		root.Add(Ui.Card(all));

		if (sav.IsAnySlotLockedInBox(0, sav.BoxCount - 1))
			root.Add(Ui.Caption("Some slots are locked by a battle team; the bulk actions skip those automatically."));

		root.Add(status);
	}

	private readonly Label status = new() { FontSize = 12, TextColor = Ui.Muted, Margin = new Thickness(4, 10, 4, 24) };

	private void Run(Func<int> op, string verb)
	{
		try
		{
			var n = op();
			Toast($"{verb} {n} Pokemon");
		}
		catch (Exception ex) { Toast($"failed: {ex.Message}"); }
		Build();
	}

	private void Toast(string text) => status.Text = text;

	private static string NameOf(SaveFile sav, int box)
	{
		try
		{
			if (sav is IBoxDetailNameRead r)
			{
				var n = r.GetBoxName(box);
				if (!string.IsNullOrWhiteSpace(n)) return n;
			}
		}
		catch { }
		return BoxDetailNameExtensions.GetDefaultBoxName(box);
	}
}
