using PKHaX.Mobile.Services;
using PKHeX.Core;

namespace PKHaX.Mobile.Views;

/// <summary>
/// The full Pokemon editor: the desktop app's Main / Stats / Moves / Met / Ribbons / Misc tabs, laid out as
/// scrolling sections. Every list field opens a searchable, alphabetically sorted <see cref="PickerPage"/>.
/// Nothing is written to the save until Apply, so backing out discards cleanly.
/// </summary>
public sealed partial class EntityEditorPage : ContentPage
{
	private readonly SaveManager saves;
	private readonly GameLists lists;
	private readonly int box, slot;
	private readonly bool isParty;
	private PKM pk = null!;

	private readonly VerticalStackLayout root = new() { Padding = 16, Spacing = 2 };
	private Label header = null!, sub = null!, legality = null!;
	private Image sprite = null!;

	public EntityEditorPage(SaveManager saves, GameLists lists, int box, int slot, bool isParty)
	{
		this.saves = saves;
		this.lists = lists;
		this.box = box;
		this.slot = slot;
		this.isParty = isParty;

		Title = "Edit";
		BackgroundColor = Ui.Bg;
		Content = new ScrollView { Content = root };
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (saves.Save is null) { _ = Shell.Current.Navigation.PopAsync(); return; }
		if (root.Count > 0) return;

		pk = (isParty ? saves.GetParty()[slot] : saves.GetBox(box)[slot]).Clone();
		Build();
	}

	private void Build()
	{
		root.Clear();
		BuildHeader();
		BuildMain();
		BuildStats();
		BuildMoves();
		BuildMet();
		BuildMisc();
		BuildAdvanced();
		BuildRibbons();
		BuildActions();
	}

	private void Rebuild()
	{
		var y = (Content as ScrollView)?.ScrollY ?? 0;
		Build();
		Dispatcher.Dispatch(async () => { if (Content is ScrollView sv) await sv.ScrollToAsync(0, y, false); });
	}

	// ------------------------------------------------------------------ header
	private void BuildHeader()
	{
		sprite = new Image { HeightRequest = 76, WidthRequest = 76, Aspect = Aspect.AspectFit };
		header = new Label { FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Ui.Text };
		sub = new Label { FontSize = 12, TextColor = Ui.Muted };

		var stack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
		stack.Add(header);
		stack.Add(sub);

		var row = new HorizontalStackLayout { Spacing = 14 };
		row.Add(sprite);
		row.Add(stack);
		root.Add(row);
		RefreshHeader();
	}

	private void RefreshHeader()
	{
		header.Text = pk.Species == 0 ? "(empty slot)"
			: (string.IsNullOrWhiteSpace(pk.Nickname) ? lists.SpeciesName(pk.Species) : pk.Nickname);
		var bits = new List<string> { $"Lv {pk.CurrentLevel}" };
		if (pk.IsShiny) bits.Add("★ Shiny");
		if (pk.IsEgg) bits.Add("Egg");
		bits.Add(GenderText(pk.Gender));
		sub.Text = string.Join("  ·  ", bits);
		sprite.Source = ViewModels.Sprites.Url(pk);
	}

	private static string GenderText(int g) => g switch { 0 => "♂ Male", 1 => "♀ Female", _ => "– Genderless" };

	// ------------------------------------------------------------------ main
	private void BuildMain()
	{
		root.Add(Ui.SectionHeader("Main"));
		var v = new VerticalStackLayout { Spacing = 0 };

		var (spRow, spBtn) = Ui.PickerRow("Species", lists.SpeciesName(pk.Species));
		spBtn.Clicked += async (_, _) =>
		{
			var picked = await PickerPage.ShowAsync("Species", lists.Species, pk.Species);
			if (picked is null) return;
			pk.Species = (ushort)picked.Value.Value;
			pk.EXP = Experience.GetEXP(pk.CurrentLevel, pk.PersonalInfo.EXPGrowth);
			pk.Gender = pk.GetSaneGender();
			if (!pk.IsNicknamed) pk.ClearNickname();
			Rebuild();
		};
		v.Add(spRow);

		var (nickRow, nickEntry) = Ui.EntryRow("Nickname", pk.Nickname, maxLength: pk.MaxStringLengthNickname);
		nickEntry.Unfocused += (_, _) =>
		{
			var t = nickEntry.Text ?? "";
			if (t == pk.Nickname) return;
			if (string.IsNullOrWhiteSpace(t)) pk.ClearNickname();
			else pk.SetNickname(t);
			RefreshHeader();
		};
		v.Add(nickRow);

		// The desktop editor reads the STORED level byte, not the EXP-derived one, and PKHaX's level-255
		// feature works by stamping Stat_Level directly while pegging EXP at the level-100 value. Setting
		// CurrentLevel alone would silently clamp back to 100 and lose the fork behaviour.
		var (lvlRow, lvlEntry) = Ui.NumberRow("Level", pk.Stat_Level, saves.IllegalMode ? "1-255" : "1-100");
		lvlEntry.Unfocused += (_, _) =>
		{
			var cap = saves.IllegalMode ? 255 : 100;
			var lv = Ui.ParseInt(lvlEntry.Text, pk.Stat_Level, 1, cap);
			if (lv > 100)
			{
				pk.EXP = Experience.GetEXP(100, pk.PersonalInfo.EXPGrowth);
				pk.Stat_Level = (byte)lv;
			}
			else
			{
				pk.CurrentLevel = (byte)lv;
			}
			lvlEntry.Text = lv.ToString();
			RefreshHeader();
		};
		v.Add(lvlRow);

		var (natRow, natBtn) = Ui.PickerRow("Nature", lists.NatureName((int)pk.Nature));
		natBtn.Clicked += async (_, _) =>
		{
			var picked = await PickerPage.ShowAsync("Nature", lists.Natures, (int)pk.Nature);
			if (picked is null) return;
			pk.SetNature((Nature)picked.Value.Value);
			Rebuild();
		};
		if (lists.Natures.Count > 1 && pk.Format >= 3) v.Add(natRow);

		var (abRow, abBtn) = Ui.PickerRow("Ability", lists.AbilityName(pk.Ability));
		abBtn.Clicked += async (_, _) =>
		{
			var picked = await PickerPage.ShowAsync("Ability", lists.Abilities, pk.Ability);
			if (picked is null) return;
			if (pk is PK3 p3) p3.AbilityOverride = picked.Value.Value;
			else pk.SetAbility(picked.Value.Value);
			Rebuild();
		};
		if (pk.Format >= 3) v.Add(abRow);

		var genders = new List<NamedValue> { new(0, "♂ Male"), new(1, "♀ Female"), new(2, "– Genderless") };
		var (gRow, gBtn) = Ui.PickerRow("Gender", GenderText(pk.Gender));
		gBtn.Clicked += async (_, _) =>
		{
			var picked = await PickerPage.ShowAsync("Gender", genders, pk.Gender);
			if (picked is null) return;
			pk.Gender = (byte)picked.Value.Value;
			Rebuild();
		};
		v.Add(gRow);

		var forms = lists.FormsFor(pk);
		if (forms.Count > 1)
		{
			var cur = pk.Form < forms.Count ? forms[pk.Form].Name : pk.Form.ToString();
			var (fRow, fBtn) = Ui.PickerRow("Form", cur);
			fBtn.Clicked += async (_, _) =>
			{
				var picked = await PickerPage.ShowAsync("Form", forms, pk.Form);
				if (picked is null) return;
				pk.Form = (byte)picked.Value.Value;
				Rebuild();
			};
			v.Add(fRow);
		}

		var (itRow, itBtn) = Ui.PickerRow("Held item", lists.ItemName(pk.HeldItem));
		itBtn.Clicked += async (_, _) =>
		{
			var picked = await PickerPage.ShowAsync("Held item", lists.Items, pk.HeldItem);
			if (picked is null) return;
			pk.HeldItem = picked.Value.Value;
			Rebuild();
		};
		if (pk.Format >= 2) v.Add(itRow);

		if (pk.Format >= 3 && lists.Balls.Count > 1)
		{
			var (bRow, bBtn) = Ui.PickerRow("Ball", pk.Ball < lists.Balls.Count ? lists.Balls.First(x => x.Value == pk.Ball).Name : pk.Ball.ToString());
			bBtn.Clicked += async (_, _) =>
			{
				var picked = await PickerPage.ShowAsync("Ball", lists.Balls, pk.Ball);
				if (picked is null) return;
				pk.Ball = (byte)picked.Value.Value;
				Rebuild();
			};
			v.Add(bRow);
		}

		var (frRow, frEntry) = Ui.NumberRow("Friendship", pk.CurrentFriendship, "0-255");
		frEntry.Unfocused += (_, _) => pk.CurrentFriendship = (byte)Ui.ParseInt(frEntry.Text, pk.CurrentFriendship, 0, 255);
		v.Add(frRow);

		if (pk.Format >= 3 && lists.Languages.Count > 1)
		{
			var langName = lists.Languages.FirstOrDefault(x => x.Value == pk.Language).Name ?? pk.Language.ToString();
			var (lRow, lBtn) = Ui.PickerRow("Language", langName);
			lBtn.Clicked += async (_, _) =>
			{
				var picked = await PickerPage.ShowAsync("Language", lists.Languages, pk.Language);
				if (picked is null) return;
				pk.Language = picked.Value.Value;
				Rebuild();
			};
			v.Add(lRow);
		}

		var (shRow, shSw) = Ui.SwitchRow("Shiny", pk.IsShiny);
		shSw.Toggled += (_, e) => { pk.SetIsShiny(e.Value); Rebuild(); };
		v.Add(shRow);

		var (eggRow, eggSw) = Ui.SwitchRow("Egg", pk.IsEgg);
		eggSw.Toggled += (_, e) => { pk.IsEgg = e.Value; RefreshHeader(); };
		v.Add(eggRow);

		if (pk.Format >= 3)
		{
			var (feRow, feSw) = Ui.SwitchRow("Fateful encounter", pk.FatefulEncounter);
			feSw.Toggled += (_, e) => pk.FatefulEncounter = e.Value;
			v.Add(feRow);
		}

		root.Add(Ui.Card(v));

		// trainer block
		root.Add(Ui.SectionHeader("Original Trainer"));
		var t = new VerticalStackLayout { Spacing = 0 };

		var (otRow, otEntry) = Ui.EntryRow("OT name", pk.OriginalTrainerName, maxLength: pk.MaxStringLengthTrainer);
		otEntry.Unfocused += (_, _) => pk.OriginalTrainerName = otEntry.Text ?? "";
		t.Add(otRow);

		var otGenders = new List<NamedValue> { new(0, "♂ Male"), new(1, "♀ Female") };
		var (otgRow, otgBtn) = Ui.PickerRow("OT gender", pk.OriginalTrainerGender == 1 ? "♀ Female" : "♂ Male");
		otgBtn.Clicked += async (_, _) =>
		{
			var picked = await PickerPage.ShowAsync("OT gender", otGenders, pk.OriginalTrainerGender);
			if (picked is null) return;
			pk.OriginalTrainerGender = (byte)picked.Value.Value;
			Rebuild();
		};
		t.Add(otgRow);

		var (tidRow, tidEntry) = Ui.NumberRow("TID", pk.TID16, "0-65535");
		tidEntry.Unfocused += (_, _) => { pk.TID16 = (ushort)Ui.ParseInt(tidEntry.Text, pk.TID16, 0, 65535); RefreshHeader(); };
		t.Add(tidRow);

		var (sidRow, sidEntry) = Ui.NumberRow("SID", pk.SID16, "0-65535");
		sidEntry.Unfocused += (_, _) => { pk.SID16 = (ushort)Ui.ParseInt(sidEntry.Text, pk.SID16, 0, 65535); RefreshHeader(); };
		if (pk.Format >= 3) t.Add(sidRow);

		root.Add(Ui.Card(t));

		// identity / read-outs
		root.Add(Ui.SectionHeader("Identity"));
		var id = new VerticalStackLayout { Spacing = 0 };
		id.Add(Ui.ReadOnlyRow("PID", pk.PID.ToString("X8")));
		if (pk.Format >= 6) id.Add(Ui.ReadOnlyRow("Encryption constant", pk.EncryptionConstant.ToString("X8")));
		id.Add(Ui.ReadOnlyRow("Species #", pk.Species.ToString()));
		if (pk.Format >= 3) id.Add(Ui.ReadOnlyRow("Characteristic", CharacteristicText()));
		id.Add(Ui.ReadOnlyRow("Nature", lists.NatureName((int)pk.Nature)));
		id.Add(Ui.ReadOnlyRow("Format", $"PK{pk.Format}  (gen {pk.Generation})"));

		if (pk.Format >= 3)
		{
			var reroll = Ui.Action("Reroll PID");
			reroll.Clicked += (_, _) =>
			{
				pk.PID = Util.Rand32();
				if (pk.Format >= 6) pk.EncryptionConstant = Util.Rand32();
				Rebuild();
			};
			id.Add(reroll);
		}
		root.Add(Ui.Card(id));
	}

	private string CharacteristicText()
	{
		try
		{
			var c = pk.Characteristic;
			var names = GameInfo.Strings.characteristics;
			return c >= 0 && c < names.Length ? names[c] : c.ToString();
		}
		catch { return "-"; }
	}

	// ------------------------------------------------------------------ stats
	private void BuildStats()
	{
		root.Add(Ui.SectionHeader("Stats"));

		var evNames = new[] { "HP", "Attack", "Defense", "Sp. Atk", "Sp. Def", "Speed" };
		int evMax = pk.Format >= 3 ? 252 : 65535;
		int ivMax = pk.MaxIV;

		var evBox = new VerticalStackLayout { Spacing = 0 };
		evBox.Add(Ui.Caption($"EVs — max {evMax} each" + (pk.Format >= 3 ? ", 510 total" : "")));
		var evEntries = new Entry[6];
		for (int i = 0; i < 6; i++)
		{
			var idx = i;
			var (row, entry) = Ui.NumberRow(evNames[i], GetEV(idx), $"/{evMax}");
			entry.Unfocused += (_, _) =>
			{
				var val = Ui.ParseInt(entry.Text, GetEV(idx), 0, evMax);
				SetEV(idx, val);
				entry.Text = val.ToString();
			};
			evEntries[i] = entry;
			evBox.Add(row);
		}
		var evBtns = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };
		var clearEv = Ui.Action("Clear EVs");
		clearEv.Clicked += (_, _) => { for (int i = 0; i < 6; i++) SetEV(i, 0); Rebuild(); };
		evBtns.Add(clearEv);
		if (pk.Format >= 3)
		{
			var spread = Ui.Action("252/252/6");
			spread.Clicked += (_, _) =>
			{
				SetEV(0, 0); SetEV(1, 252); SetEV(2, 0); SetEV(3, 0); SetEV(4, 6); SetEV(5, 252);
				Rebuild();
			};
			evBtns.Add(spread);
		}
		evBox.Add(evBtns);
		root.Add(Ui.Card(evBox));

		var ivBox = new VerticalStackLayout { Spacing = 0 };
		ivBox.Add(Ui.Caption(pk.Format <= 2 ? "DVs — max 15 each" : $"IVs — max {ivMax} each"));
		for (int i = 0; i < 6; i++)
		{
			var idx = i;
			var (row, entry) = Ui.NumberRow(evNames[i], GetIV(idx), $"/{ivMax}");
			entry.Unfocused += (_, _) =>
			{
				var val = Ui.ParseInt(entry.Text, GetIV(idx), 0, ivMax);
				SetIV(idx, val);
				entry.Text = val.ToString();
			};
			ivBox.Add(row);
		}
		var ivBtns = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };
		var maxIv = Ui.Action("Max IVs", Ui.Positive);
		maxIv.Clicked += (_, _) => { for (int i = 0; i < 6; i++) SetIV(i, ivMax); Rebuild(); };
		var randIv = Ui.Action("Randomise");
		randIv.Clicked += (_, _) => { pk.SetRandomIVs(); Rebuild(); };
		ivBtns.Add(maxIv); ivBtns.Add(randIv);
		ivBox.Add(ivBtns);
		root.Add(Ui.Card(ivBox));

		// computed stats
		var calc = new VerticalStackLayout { Spacing = 0 };
		calc.Add(Ui.Caption("Current stats (recomputed from base + IV/EV + nature)"));
		try
		{
			pk.ResetPartyStats();
			calc.Add(Ui.ReadOnlyRow("HP", $"{pk.Stat_HPCurrent} / {pk.Stat_HPMax}"));
			calc.Add(Ui.ReadOnlyRow("Attack", pk.Stat_ATK.ToString()));
			calc.Add(Ui.ReadOnlyRow("Defense", pk.Stat_DEF.ToString()));
			calc.Add(Ui.ReadOnlyRow("Sp. Atk", pk.Stat_SPA.ToString()));
			calc.Add(Ui.ReadOnlyRow("Sp. Def", pk.Stat_SPD.ToString()));
			calc.Add(Ui.ReadOnlyRow("Speed", pk.Stat_SPE.ToString()));
		}
		catch { calc.Add(Ui.ReadOnlyRow("stats", "unavailable for this format")); }
		root.Add(Ui.Card(calc));
	}

	private int GetEV(int i) => i switch
	{
		0 => pk.EV_HP, 1 => pk.EV_ATK, 2 => pk.EV_DEF, 3 => pk.EV_SPA, 4 => pk.EV_SPD, _ => pk.EV_SPE,
	};

	private void SetEV(int i, int v)
	{
		switch (i)
		{
			case 0: pk.EV_HP = v; break;
			case 1: pk.EV_ATK = v; break;
			case 2: pk.EV_DEF = v; break;
			case 3: pk.EV_SPA = v; break;
			case 4: pk.EV_SPD = v; break;
			default: pk.EV_SPE = v; break;
		}
	}

	private int GetIV(int i) => i switch
	{
		0 => pk.IV_HP, 1 => pk.IV_ATK, 2 => pk.IV_DEF, 3 => pk.IV_SPA, 4 => pk.IV_SPD, _ => pk.IV_SPE,
	};

	private void SetIV(int i, int v)
	{
		switch (i)
		{
			case 0: pk.IV_HP = v; break;
			case 1: pk.IV_ATK = v; break;
			case 2: pk.IV_DEF = v; break;
			case 3: pk.IV_SPA = v; break;
			case 4: pk.IV_SPD = v; break;
			default: pk.IV_SPE = v; break;
		}
	}

	// ------------------------------------------------------------------ moves
	private void BuildMoves()
	{
		root.Add(Ui.SectionHeader("Moves"));
		var v = new VerticalStackLayout { Spacing = 0 };

		for (int i = 0; i < 4; i++)
		{
			var idx = i;
			var (row, btn) = Ui.PickerRow($"Move {i + 1}", lists.MoveName(GetMove(idx)));
			btn.Clicked += async (_, _) =>
			{
				var picked = await PickerPage.ShowAsync($"Move {idx + 1}", lists.Moves, GetMove(idx));
				if (picked is null) return;
				SetMove(idx, picked.Value.Value);
				Rebuild();
			};
			v.Add(row);
		}

		var heal = Ui.Action("Restore PP");
		heal.Clicked += (_, _) => { pk.HealPP(); Rebuild(); };
		v.Add(heal);
		root.Add(Ui.Card(v));

		if (pk.Format >= 6)
		{
			root.Add(Ui.SectionHeader("Relearn moves"));
			var r = new VerticalStackLayout { Spacing = 0 };
			for (int i = 0; i < 4; i++)
			{
				var idx = i;
				var (row, btn) = Ui.PickerRow($"Relearn {i + 1}", lists.MoveName(GetRelearn(idx)));
				btn.Clicked += async (_, _) =>
				{
					var picked = await PickerPage.ShowAsync($"Relearn {idx + 1}", lists.Moves, GetRelearn(idx));
					if (picked is null) return;
					SetRelearn(idx, picked.Value.Value);
					Rebuild();
				};
				r.Add(row);
			}
			root.Add(Ui.Card(r));
		}
	}

	private int GetMove(int i) => i switch { 0 => pk.Move1, 1 => pk.Move2, 2 => pk.Move3, _ => pk.Move4 };

	private void SetMove(int i, int v)
	{
		switch (i)
		{
			case 0: pk.Move1 = (ushort)v; pk.Move1_PP = pk.GetMovePP((ushort)v, pk.Move1_PPUps); break;
			case 1: pk.Move2 = (ushort)v; pk.Move2_PP = pk.GetMovePP((ushort)v, pk.Move2_PPUps); break;
			case 2: pk.Move3 = (ushort)v; pk.Move3_PP = pk.GetMovePP((ushort)v, pk.Move3_PPUps); break;
			default: pk.Move4 = (ushort)v; pk.Move4_PP = pk.GetMovePP((ushort)v, pk.Move4_PPUps); break;
		}
	}

	private int GetRelearn(int i) => i switch
	{
		0 => pk.RelearnMove1, 1 => pk.RelearnMove2, 2 => pk.RelearnMove3, _ => pk.RelearnMove4,
	};

	private void SetRelearn(int i, int v)
	{
		switch (i)
		{
			case 0: pk.RelearnMove1 = (ushort)v; break;
			case 1: pk.RelearnMove2 = (ushort)v; break;
			case 2: pk.RelearnMove3 = (ushort)v; break;
			default: pk.RelearnMove4 = (ushort)v; break;
		}
	}

	// ------------------------------------------------------------------ met
	private void BuildMet()
	{
		if (pk.Format < 2) return;
		root.Add(Ui.SectionHeader("Met / Origin"));
		var v = new VerticalStackLayout { Spacing = 0 };

		var (mlRow, mlEntry) = Ui.NumberRow("Met level", pk.MetLevel, "0-255");
		mlEntry.Unfocused += (_, _) => pk.MetLevel = (byte)Ui.ParseInt(mlEntry.Text, pk.MetLevel, 0, 255);
		v.Add(mlRow);

		var (locRow, locEntry) = Ui.NumberRow("Met location", pk.MetLocation, "id");
		locEntry.Unfocused += (_, _) => pk.MetLocation = (ushort)Ui.ParseInt(locEntry.Text, pk.MetLocation, 0, 65535);
		if (pk.Format >= 3) v.Add(locRow);

		if (pk.Format >= 3 && lists.Games.Count > 1)
		{
			var gameName = lists.Games.FirstOrDefault(x => x.Value == (int)pk.Version).Name ?? pk.Version.ToString();
			var (gRow, gBtn) = Ui.PickerRow("Origin game", gameName);
			gBtn.Clicked += async (_, _) =>
			{
				var picked = await PickerPage.ShowAsync("Origin game", lists.Games, (int)pk.Version);
				if (picked is null) return;
				pk.Version = (GameVersion)picked.Value.Value;
				Rebuild();
			};
			v.Add(gRow);
		}
		root.Add(Ui.Card(v));
	}

	// ------------------------------------------------------------------ misc
	private void BuildMisc()
	{
		root.Add(Ui.SectionHeader("Misc"));
		var v = new VerticalStackLayout { Spacing = 0 };

		if (pk.Format >= 2)
		{
			var (psRow, psEntry) = Ui.NumberRow("Pokerus strain", pk.PokerusStrain, "0-15");
			psEntry.Unfocused += (_, _) => pk.PokerusStrain = Ui.ParseInt(psEntry.Text, pk.PokerusStrain, 0, 15);
			v.Add(psRow);

			var (pdRow, pdEntry) = Ui.NumberRow("Pokerus days", pk.PokerusDays, "0-4");
			pdEntry.Unfocused += (_, _) => pk.PokerusDays = Ui.ParseInt(pdEntry.Text, pk.PokerusDays, 0, 4);
			v.Add(pdRow);

			var (curedRow, curedSw) = Ui.SwitchRow("Pokerus cured", pk.IsPokerusCured);
			curedSw.Toggled += (_, e) => { pk.IsPokerusCured = e.Value; Rebuild(); };
			v.Add(curedRow);
		}

		if (pk.Format >= 3)
		{
			var (expRow, expEntry) = Ui.NumberRow("EXP", (int)Math.Min(pk.EXP, int.MaxValue), "");
			expEntry.Unfocused += (_, _) =>
			{
				var val = Ui.ParseInt(expEntry.Text, (int)pk.EXP, 0, int.MaxValue);
				pk.EXP = (uint)val;
				RefreshHeader();
			};
			v.Add(expRow);
		}

		root.Add(Ui.Card(v));
	}

	// ------------------------------------------------------------------ ribbons
	private void BuildRibbons()
	{
		List<RibbonInfo> ribbons;
		try { ribbons = RibbonInfo.GetRibbonInfo(pk); }
		catch { return; }
		if (ribbons.Count == 0) return;

		root.Add(Ui.SectionHeader($"Ribbons & marks ({ribbons.Count(r => r.HasRibbon || r.RibbonCount > 0)} set)"));
		var v = new VerticalStackLayout { Spacing = 0 };
		v.Add(Ui.Caption("Every ribbon and mark this format supports."));

		foreach (var rib in ribbons)
		{
			var label = Pretty(rib.Name);
			if (rib.Type == RibbonValueType.Boolean)
			{
				var (row, sw) = Ui.SwitchRow(label, rib.HasRibbon);
				var captured = rib;
				sw.Toggled += (_, e) => ReflectUtil.SetValue(pk, captured.Name, e.Value);
				v.Add(row);
			}
			else
			{
				var captured = rib;
				var (row, entry) = Ui.NumberRow(label, rib.RibbonCount, $"/{rib.MaxCount}");
				entry.Unfocused += (_, _) =>
				{
					var val = Ui.ParseInt(entry.Text, captured.RibbonCount, 0, captured.MaxCount);
					ReflectUtil.SetValue(pk, captured.Name, (byte)val);
					entry.Text = val.ToString();
				};
				v.Add(row);
			}
		}

		var clear = Ui.Action("Clear all ribbons");
		clear.Clicked += (_, _) =>
		{
			foreach (var rib in ribbons)
			{
				if (rib.Type == RibbonValueType.Boolean) ReflectUtil.SetValue(pk, rib.Name, false);
				else ReflectUtil.SetValue(pk, rib.Name, (byte)0);
			}
			Rebuild();
		};
		v.Add(clear);
		root.Add(Ui.Card(v));
	}

	private static string Pretty(string name)
	{
		var s = name.StartsWith("Ribbon", StringComparison.Ordinal) ? name["Ribbon".Length..] : name;
		var sb = new System.Text.StringBuilder(s.Length + 8);
		for (int i = 0; i < s.Length; i++)
		{
			if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1])) sb.Append(' ');
			sb.Append(s[i]);
		}
		return sb.ToString();
	}

	// ------------------------------------------------------------------ actions
	private void BuildActions()
	{
		legality = new Label { FontSize = 12, TextColor = Ui.Muted, Margin = new Thickness(4, 10, 4, 0) };
		try
		{
			var report = new LegalityAnalysis(pk);
			legality.Text = report.Valid ? "Legality: legal." : "Legality: illegal — allowed in PKHaX mode.";
		}
		catch { legality.Text = ""; }
		root.Add(legality);

		// PKHaX: one press = every ribbon, maxed EV/IV, memories, contest stats, Pokerus and shiny.
		// Deliberately illegal (252 EVs x6 = 1512 vs the 510 cap), so it is gated on illegal mode.
		if (saves.IllegalMode)
		{
			var maxHax = Ui.Action("MAX HAX", Ui.Accent);
			maxHax.Margin = new Thickness(0, 10, 0, 0);
			maxHax.Clicked += async (_, _) =>
			{
				if (!await DisplayAlertAsync("Max Hax",
					"Give this Pokemon every ribbon and mark, 252 EVs and max IVs in every stat, both memory "
					+ "ribbon counts, maxed memories/affection, 255 contest stats, Pokerus and shiny?\n\n"
					+ "This is intentionally illegal and will fail legality checks.", "Max it", "Cancel"))
					return;
				MaxHax.Apply(pk);
				Rebuild();
			};
			root.Add(maxHax);
		}

		var apply = Ui.Action("Apply to save", Ui.Positive);
		apply.Margin = new Thickness(0, 10, 0, 0);
		apply.Clicked += async (_, _) =>
		{
			pk.RefreshChecksum();
			if (isParty) saves.SetPartySlot(slot, pk);
			else saves.SetBoxSlot(box, slot, pk);
			await Shell.Current.Navigation.PopAsync();
		};
		root.Add(apply);

		var del = Ui.Action("Delete this Pokemon");
		del.Margin = new Thickness(0, 4, 0, 24);
		del.Clicked += async (_, _) =>
		{
			if (!await DisplayAlertAsync("Delete", "Clear this slot?", "Delete", "Cancel")) return;
			var blank = saves.Save!.BlankPKM;
			if (isParty) saves.SetPartySlot(slot, blank);
			else saves.SetBoxSlot(box, slot, blank);
			await Shell.Current.Navigation.PopAsync();
		};
		root.Add(del);
	}
}
