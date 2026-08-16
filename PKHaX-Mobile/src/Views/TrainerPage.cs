using PKHaX.Mobile.Services;
using PKHeX.Core;

namespace PKHaX.Mobile.Views;

/// <summary>
/// Save-level trainer data: name, IDs, money, play time and so on. Fields are only shown when the loaded
/// save actually supports them (PKHeX exposes most of these through interfaces the save may not implement).
/// </summary>
public sealed class TrainerPage : ContentPage
{
	private readonly SaveManager saves;
	private readonly GameLists lists;
	private readonly VerticalStackLayout root = new() { Padding = 16, Spacing = 2 };

	public TrainerPage(SaveManager saves, GameLists lists)
	{
		this.saves = saves;
		this.lists = lists;
		Title = "Trainer";
		BackgroundColor = Ui.Bg;
		Content = new ScrollView { Content = root };
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Build();
	}

	private void Build()
	{
		root.Clear();
		var sav = saves.Save;
		if (sav is null) return;

		root.Add(Ui.SectionHeader("Trainer"));
		var v = new VerticalStackLayout { Spacing = 0 };

		var (otRow, otEntry) = Ui.EntryRow("Name", sav.OT, maxLength: sav.MaxStringLengthTrainer);
		otEntry.Unfocused += (_, _) => sav.OT = otEntry.Text ?? sav.OT;
		v.Add(otRow);

		var (tidRow, tidEntry) = Ui.NumberRow("Trainer ID", (int)sav.DisplayTID, "");
		tidEntry.Unfocused += (_, _) =>
		{
			var val = Ui.ParseInt(tidEntry.Text, (int)sav.DisplayTID, 0, int.MaxValue);
			try { sav.DisplayTID = (uint)val; } catch { }
			tidEntry.Text = sav.DisplayTID.ToString();
		};
		v.Add(tidRow);

		var (sidRow, sidEntry) = Ui.NumberRow("Secret ID", (int)sav.DisplaySID, "");
		sidEntry.Unfocused += (_, _) =>
		{
			var val = Ui.ParseInt(sidEntry.Text, (int)sav.DisplaySID, 0, int.MaxValue);
			try { sav.DisplaySID = (uint)val; } catch { }
			sidEntry.Text = sav.DisplaySID.ToString();
		};
		if (sav.Generation >= 3) v.Add(sidRow);

		var genders = new List<NamedValue> { new(0, "♂ Male"), new(1, "♀ Female") };
		var (gRow, gBtn) = Ui.PickerRow("Gender", sav.Gender == 1 ? "♀ Female" : "♂ Male");
		gBtn.Clicked += async (_, _) =>
		{
			var picked = await PickerPage.ShowAsync("Trainer gender", genders, sav.Gender);
			if (picked is null) return;
			sav.Gender = (byte)picked.Value.Value;
			Build();
		};
		if (sav.Generation >= 2) v.Add(gRow);

		if (sav.Generation >= 3 && lists.Languages.Count > 1)
		{
			var cur = lists.Languages.FirstOrDefault(x => x.Value == sav.Language).Name ?? sav.Language.ToString();
			var (lRow, lBtn) = Ui.PickerRow("Language", cur);
			lBtn.Clicked += async (_, _) =>
			{
				var picked = await PickerPage.ShowAsync("Language", lists.Languages, sav.Language);
				if (picked is null) return;
				try { sav.Language = picked.Value.Value; } catch { }
				Build();
			};
			v.Add(lRow);
		}
		root.Add(Ui.Card(v));

		// Money and play time are virtuals on SaveFile itself; saves that do not store them simply
		// ignore the write, so guard with try/catch rather than an interface test.
		root.Add(Ui.SectionHeader("Progress"));
		var p = new VerticalStackLayout { Spacing = 0 };

		var (mRow, mEntry) = Ui.NumberRow("Money", (int)Math.Min(sav.Money, int.MaxValue), "");
		mEntry.Unfocused += (_, _) =>
		{
			var val = Ui.ParseInt(mEntry.Text, (int)Math.Min(sav.Money, int.MaxValue), 0, int.MaxValue);
			try { sav.Money = (uint)val; } catch { }
			mEntry.Text = sav.Money.ToString();
		};
		p.Add(mRow);

		var maxMoney = Ui.Action("Max money");
		maxMoney.Clicked += (_, _) => { try { sav.Money = 9_999_999; } catch { } Build(); };
		p.Add(maxMoney);

		var (hRow, hEntry) = Ui.NumberRow("Hours played", sav.PlayedHours, "");
		hEntry.Unfocused += (_, _) =>
		{
			var val = Ui.ParseInt(hEntry.Text, sav.PlayedHours, 0, 999999);
			try { sav.PlayedHours = val; } catch { }
		};
		p.Add(hRow);

		var (mnRow, mnEntry) = Ui.NumberRow("Minutes", sav.PlayedMinutes, "0-59");
		mnEntry.Unfocused += (_, _) =>
		{
			var val = Ui.ParseInt(mnEntry.Text, sav.PlayedMinutes, 0, 59);
			try { sav.PlayedMinutes = val; } catch { }
		};
		p.Add(mnRow);
		root.Add(Ui.Card(p));

		root.Add(Ui.SectionHeader("Save"));
		var info = new VerticalStackLayout { Spacing = 0 };
		info.Add(Ui.ReadOnlyRow("Game", sav.Version.ToString()));
		info.Add(Ui.ReadOnlyRow("Generation", sav.Generation.ToString()));
		info.Add(Ui.ReadOnlyRow("Boxes", sav.BoxCount.ToString()));
		info.Add(Ui.ReadOnlyRow("Party", sav.PartyCount.ToString()));
		root.Add(Ui.Card(info));
	}
}
