using PKHaX.Mobile.Services;
using PKHeX.Core;

namespace PKHaX.Mobile.Views;

/// <summary>
/// The remaining desktop fields. Almost all of them are gated behind PKHeX.Core interfaces that only some
/// formats implement (IHyperTrain, IContestStats, ITeraType, IScaledSize, IMemoryOT ...), so each block is
/// emitted only when the loaded Pokemon actually supports it — the same rule the WinForms editor uses to
/// show or hide a tab.
/// </summary>
public sealed partial class EntityEditorPage
{
	private void BuildAdvanced()
	{
		BuildMarkings();
		BuildMetDates();
		BuildTraining();
		BuildContest();
		BuildSizeAndBattle();
		BuildMemories();
		BuildForkExtras();
	}

	// ------------------------------------------------------------- markings
	private void BuildMarkings()
	{
		var names = new[] { "Circle", "Triangle", "Square", "Heart", "Star", "Diamond" };

		if (pk is IAppliedMarkings<bool> mb)
		{
			root.Add(Ui.SectionHeader("Markings"));
			var v = new VerticalStackLayout { Spacing = 0 };
			for (int i = 0; i < Math.Min(names.Length, mb.MarkingCount); i++)
			{
				var idx = i;
				var (row, sw) = Ui.SwitchRow(names[i], mb.GetMarking(idx));
				sw.Toggled += (_, e) => mb.SetMarking(idx, e.Value);
				v.Add(row);
			}
			root.Add(Ui.Card(v));
		}
		else if (pk is IAppliedMarkings<MarkingColor> mc)
		{
			root.Add(Ui.SectionHeader("Markings"));
			var v = new VerticalStackLayout { Spacing = 0 };
			var colors = new List<NamedValue>
			{
				new((int)MarkingColor.None, "None"),
				new((int)MarkingColor.Blue, "Blue"),
				new((int)MarkingColor.Pink, "Pink"),
			};
			for (int i = 0; i < Math.Min(names.Length, mc.MarkingCount); i++)
			{
				var idx = i;
				var cur = mc.GetMarking(idx);
				var (row, btn) = Ui.PickerRow(names[i], cur.ToString());
				btn.Clicked += async (_, _) =>
				{
					var picked = await PickerPage.ShowAsync(names[idx], colors, (int)cur);
					if (picked is null) return;
					mc.SetMarking(idx, (MarkingColor)picked.Value.Value);
					Rebuild();
				};
				v.Add(row);
			}
			root.Add(Ui.Card(v));
		}
	}

	// ------------------------------------------------------------- met dates
	private void BuildMetDates()
	{
		if (pk.Format < 4) return;
		root.Add(Ui.SectionHeader("Dates"));
		var v = new VerticalStackLayout { Spacing = 0 };

		var met = new DatePicker
		{
			Date = pk.MetDate is { } md ? md.ToDateTime(TimeOnly.MinValue) : DateTime.Today,
			TextColor = Ui.Text, BackgroundColor = Ui.SurfaceAlt,
		};
		met.DateSelected += (_, e) => { if (e.NewDate is { } d) pk.MetDate = DateOnly.FromDateTime(d); };
		v.Add(new Label { Text = "Met date", FontSize = 14, TextColor = Ui.Text });
		v.Add(met);

		var egg = new DatePicker
		{
			Date = pk.EggMetDate is { } ed ? ed.ToDateTime(TimeOnly.MinValue) : DateTime.Today,
			TextColor = Ui.Text, BackgroundColor = Ui.SurfaceAlt,
		};
		egg.DateSelected += (_, e) => { if (e.NewDate is { } d) pk.EggMetDate = DateOnly.FromDateTime(d); };
		v.Add(new Label { Text = "Egg met date", FontSize = 14, TextColor = Ui.Text, Margin = new Thickness(0, 8, 0, 0) });
		v.Add(egg);

		var clearEgg = Ui.Action("Clear egg date");
		clearEgg.Clicked += (_, _) => { pk.EggMetDate = null; Rebuild(); };
		v.Add(clearEgg);

		root.Add(Ui.Card(v));
	}

	// ------------------------------------------------------------- hyper training / AV / GV
	private void BuildTraining()
	{
		var statNames = new[] { "HP", "Attack", "Defense", "Sp. Atk", "Sp. Def", "Speed" };

		if (pk is IHyperTrain ht)
		{
			root.Add(Ui.SectionHeader("Hyper training"));
			var v = new VerticalStackLayout { Spacing = 0 };
			var getters = new Func<bool>[] { () => ht.HT_HP, () => ht.HT_ATK, () => ht.HT_DEF, () => ht.HT_SPA, () => ht.HT_SPD, () => ht.HT_SPE };
			var setters = new Action<bool>[]
			{
				x => ht.HT_HP = x, x => ht.HT_ATK = x, x => ht.HT_DEF = x,
				x => ht.HT_SPA = x, x => ht.HT_SPD = x, x => ht.HT_SPE = x,
			};
			for (int i = 0; i < 6; i++)
			{
				var set = setters[i];
				var (row, sw) = Ui.SwitchRow(statNames[i], getters[i]());
				sw.Toggled += (_, e) => set(e.Value);
				v.Add(row);
			}
			var all = Ui.Action("Hyper train all", Ui.Positive);
			all.Clicked += (_, _) => { foreach (var s in setters) s(true); Rebuild(); };
			v.Add(all);
			root.Add(Ui.Card(v));
		}

		if (pk is IAwakened av)
		{
			root.Add(Ui.SectionHeader("Awakening values (LGPE)"));
			var v = new VerticalStackLayout { Spacing = 0 };
			var get = new Func<byte>[] { () => av.AV_HP, () => av.AV_ATK, () => av.AV_DEF, () => av.AV_SPA, () => av.AV_SPD, () => av.AV_SPE };
			var set = new Action<byte>[]
			{
				x => av.AV_HP = x, x => av.AV_ATK = x, x => av.AV_DEF = x,
				x => av.AV_SPA = x, x => av.AV_SPD = x, x => av.AV_SPE = x,
			};
			for (int i = 0; i < 6; i++)
			{
				var s = set[i]; var g = get[i];
				var (row, entry) = Ui.NumberRow(statNames[i], g(), "/200");
				entry.Unfocused += (_, _) =>
				{
					var val = Ui.ParseInt(entry.Text, g(), 0, 200);
					s((byte)val); entry.Text = val.ToString();
				};
				v.Add(row);
			}
			var max = Ui.Action("Max AVs", Ui.Positive);
			max.Clicked += (_, _) => { foreach (var s in set) s(200); Rebuild(); };
			v.Add(max);
			root.Add(Ui.Card(v));
		}

		if (pk is IGanbaru gv)
		{
			root.Add(Ui.SectionHeader("Effort levels (Legends: Arceus)"));
			var v = new VerticalStackLayout { Spacing = 0 };
			var get = new Func<byte>[] { () => gv.GV_HP, () => gv.GV_ATK, () => gv.GV_DEF, () => gv.GV_SPA, () => gv.GV_SPD, () => gv.GV_SPE };
			var set = new Action<byte>[]
			{
				x => gv.GV_HP = x, x => gv.GV_ATK = x, x => gv.GV_DEF = x,
				x => gv.GV_SPA = x, x => gv.GV_SPD = x, x => gv.GV_SPE = x,
			};
			for (int i = 0; i < 6; i++)
			{
				var s = set[i]; var g = get[i];
				var (row, entry) = Ui.NumberRow(statNames[i], g(), "/10");
				entry.Unfocused += (_, _) =>
				{
					var val = Ui.ParseInt(entry.Text, g(), 0, 10);
					s((byte)val); entry.Text = val.ToString();
				};
				v.Add(row);
			}
			root.Add(Ui.Card(v));
		}
	}

	// ------------------------------------------------------------- contest
	private void BuildContest()
	{
		if (pk is not IContestStatsReadOnly cs) return;
		root.Add(Ui.SectionHeader("Contest stats"));
		var v = new VerticalStackLayout { Spacing = 0 };

		var labels = new[] { "Cool", "Beauty", "Cute", "Smart", "Tough", "Sheen" };
		var get = new Func<byte>[] { () => cs.ContestCool, () => cs.ContestBeauty, () => cs.ContestCute, () => cs.ContestSmart, () => cs.ContestTough, () => cs.ContestSheen };

		if (cs is IContestStats m)
		{
			var set = new Action<byte>[]
			{
				x => m.ContestCool = x, x => m.ContestBeauty = x, x => m.ContestCute = x,
				x => m.ContestSmart = x, x => m.ContestTough = x, x => m.ContestSheen = x,
			};
			for (int i = 0; i < 6; i++)
			{
				var s = set[i]; var g = get[i];
				var (row, entry) = Ui.NumberRow(labels[i], g(), "/255");
				entry.Unfocused += (_, _) =>
				{
					var val = Ui.ParseInt(entry.Text, g(), 0, 255);
					s((byte)val); entry.Text = val.ToString();
				};
				v.Add(row);
			}
			var max = Ui.Action("Max contest stats", Ui.Positive);
			max.Clicked += (_, _) => { foreach (var s in set) s(255); Rebuild(); };
			v.Add(max);
		}
		else
		{
			for (int i = 0; i < 6; i++) v.Add(Ui.ReadOnlyRow(labels[i], get[i]().ToString()));
		}
		root.Add(Ui.Card(v));
	}

	// ------------------------------------------------------------- size / battle-era flags
	private void BuildSizeAndBattle()
	{
		var v = new VerticalStackLayout { Spacing = 0 };
		var any = false;

		if (pk is IScaledSize ss)
		{
			any = true;
			var (hRow, hEntry) = Ui.NumberRow("Height scalar", ss.HeightScalar, "/255");
			hEntry.Unfocused += (_, _) => ss.HeightScalar = (byte)Ui.ParseInt(hEntry.Text, ss.HeightScalar, 0, 255);
			v.Add(hRow);

			var (wRow, wEntry) = Ui.NumberRow("Weight scalar", ss.WeightScalar, "/255");
			wEntry.Unfocused += (_, _) => ss.WeightScalar = (byte)Ui.ParseInt(wEntry.Text, ss.WeightScalar, 0, 255);
			v.Add(wRow);
		}

		if (pk is IScaledSize3 s3)
		{
			any = true;
			var (row, entry) = Ui.NumberRow("Scale", s3.Scale, "/255");
			entry.Unfocused += (_, _) => s3.Scale = (byte)Ui.ParseInt(entry.Text, s3.Scale, 0, 255);
			v.Add(row);
		}

		if (pk is IDynamaxLevel dl)
		{
			any = true;
			var (row, entry) = Ui.NumberRow("Dynamax level", dl.DynamaxLevel, "0-10");
			entry.Unfocused += (_, _) => dl.DynamaxLevel = (byte)Ui.ParseInt(entry.Text, dl.DynamaxLevel, 0, 10);
			v.Add(row);
		}

		if (pk is IGigantamax gm)
		{
			any = true;
			var (row, sw) = Ui.SwitchRow("Can Gigantamax", gm.CanGigantamax);
			sw.Toggled += (_, e) => gm.CanGigantamax = e.Value;
			v.Add(row);
		}

		if (pk is ITeraType tt)
		{
			any = true;
			var types = GameInfo.Strings.types;
			var teraList = new List<NamedValue>();
			for (int i = 0; i < types.Length && i < 19; i++)
				if (!string.IsNullOrWhiteSpace(types[i])) teraList.Add(new NamedValue(i, types[i]));

			var (oRow, oBtn) = Ui.PickerRow("Tera type (original)", TypeName((int)tt.TeraTypeOriginal));
			oBtn.Clicked += async (_, _) =>
			{
				var picked = await PickerPage.ShowAsync("Tera type", teraList, (int)tt.TeraTypeOriginal);
				if (picked is null) return;
				tt.TeraTypeOriginal = (MoveType)picked.Value.Value;
				Rebuild();
			};
			v.Add(oRow);

			var (vRow, vBtn) = Ui.PickerRow("Tera type (override)", TypeName((int)tt.TeraTypeOverride));
			vBtn.Clicked += async (_, _) =>
			{
				var picked = await PickerPage.ShowAsync("Tera override", teraList, (int)tt.TeraTypeOverride);
				if (picked is null) return;
				tt.TeraTypeOverride = (MoveType)picked.Value.Value;
				Rebuild();
			};
			v.Add(vRow);
		}

		if (pk is IObedienceLevel ob)
		{
			any = true;
			var (row, entry) = Ui.NumberRow("Obedience level", ob.ObedienceLevel, "0-100");
			entry.Unfocused += (_, _) => ob.ObedienceLevel = (byte)Ui.ParseInt(entry.Text, ob.ObedienceLevel, 0, 100);
			v.Add(row);
		}

		if (pk is ISociability so)
		{
			any = true;
			var (row, entry) = Ui.NumberRow("Sociability", (int)so.Sociability, "");
			entry.Unfocused += (_, _) => so.Sociability = (uint)Ui.ParseInt(entry.Text, (int)so.Sociability, 0, int.MaxValue);
			v.Add(row);
		}

		if (pk is IHomeTrack home)
		{
			any = true;
			v.Add(Ui.ReadOnlyRow("HOME tracker", home.Tracker.ToString("X16")));
			var clear = Ui.Action("Clear HOME tracker");
			clear.Clicked += (_, _) => { home.Tracker = 0; Rebuild(); };
			v.Add(clear);
		}

		if (!any) return;
		root.Add(Ui.SectionHeader("Size & battle data"));
		root.Add(Ui.Card(v));
	}

	private static string TypeName(int i)
	{
		var t = GameInfo.Strings.types;
		return i >= 0 && i < t.Length ? t[i] : i.ToString();
	}

	// ------------------------------------------------------------- memories
	private void BuildMemories()
	{
		var v = new VerticalStackLayout { Spacing = 0 };
		var any = false;

		if (pk is IMemoryOT mo)
		{
			any = true;
			v.Add(Ui.Caption("Original Trainer memory"));
			var (m1, e1) = Ui.NumberRow("Memory", mo.OriginalTrainerMemory, "");
			e1.Unfocused += (_, _) => mo.OriginalTrainerMemory = (byte)Ui.ParseInt(e1.Text, mo.OriginalTrainerMemory, 0, 255);
			v.Add(m1);
			var (m2, e2) = Ui.NumberRow("Intensity", mo.OriginalTrainerMemoryIntensity, "");
			e2.Unfocused += (_, _) => mo.OriginalTrainerMemoryIntensity = (byte)Ui.ParseInt(e2.Text, mo.OriginalTrainerMemoryIntensity, 0, 255);
			v.Add(m2);
			var (m3, e3) = Ui.NumberRow("Feeling", mo.OriginalTrainerMemoryFeeling, "");
			e3.Unfocused += (_, _) => mo.OriginalTrainerMemoryFeeling = (byte)Ui.ParseInt(e3.Text, mo.OriginalTrainerMemoryFeeling, 0, 255);
			v.Add(m3);
			var (m4, e4) = Ui.NumberRow("Variable", mo.OriginalTrainerMemoryVariable, "");
			e4.Unfocused += (_, _) => mo.OriginalTrainerMemoryVariable = (ushort)Ui.ParseInt(e4.Text, mo.OriginalTrainerMemoryVariable, 0, 65535);
			v.Add(m4);
		}

		if (pk is IAffection af)
		{
			any = true;
			var (a1, ae1) = Ui.NumberRow("OT affection", af.OriginalTrainerAffection, "0-255");
			ae1.Unfocused += (_, _) => af.OriginalTrainerAffection = (byte)Ui.ParseInt(ae1.Text, af.OriginalTrainerAffection, 0, 255);
			v.Add(a1);
			var (a2, ae2) = Ui.NumberRow("HT affection", af.HandlingTrainerAffection, "0-255");
			ae2.Unfocused += (_, _) => af.HandlingTrainerAffection = (byte)Ui.ParseInt(ae2.Text, af.HandlingTrainerAffection, 0, 255);
			v.Add(a2);
		}

		if (!any) return;
		root.Add(Ui.SectionHeader("Memories & affection"));
		root.Add(Ui.Card(v));
	}

	// ------------------------------------------------------------- PKHaX fork extras
	private void BuildForkExtras()
	{
		// Gen 1: sprite/type desync (the PikaSav feature this fork made native)
		if (pk is PK1 p1)
		{
			root.Add(Ui.SectionHeader("Gen 1 desync (PKHaX)"));
			var v = new VerticalStackLayout { Spacing = 0 };
			v.Add(Ui.Caption("Give this Pokemon another species' sprite and any typing. Stored in the save's list header."));

			var (spRow, spBtn) = Ui.PickerRow("Sprite species", lists.SpeciesName(p1.SpeciesInternal switch { 0 => pk.Species, var x => SpeciesFromInternal(x) }));
			spBtn.Clicked += async (_, _) =>
			{
				var picked = await PickerPage.ShowAsync("Sprite species", lists.Species, pk.Species);
				if (picked is null) return;
				try { p1.HeaderSpeciesInternal = SpeciesConverter.GetInternal1((ushort)picked.Value.Value); } catch { }
				Rebuild();
			};
			v.Add(spRow);

			var g1types = new List<NamedValue>();
			var tnames = GameInfo.Strings.types;
			for (int i = 0; i < tnames.Length && i < 27; i++)
				if (!string.IsNullOrWhiteSpace(tnames[i])) g1types.Add(new NamedValue(i, tnames[i]));

			var (t1Row, t1Btn) = Ui.PickerRow("Type 1", $"{p1.Type1}");
			t1Btn.Clicked += async (_, _) =>
			{
				var picked = await PickerPage.ShowAsync("Type 1", g1types, p1.Type1);
				if (picked is null) return;
				p1.Type1 = (byte)picked.Value.Value;
				Rebuild();
			};
			v.Add(t1Row);

			var (t2Row, t2Btn) = Ui.PickerRow("Type 2", $"{p1.Type2}");
			t2Btn.Clicked += async (_, _) =>
			{
				var picked = await PickerPage.ShowAsync("Type 2", g1types, p1.Type2);
				if (picked is null) return;
				p1.Type2 = (byte)picked.Value.Value;
				Rebuild();
			};
			v.Add(t2Row);
			root.Add(Ui.Card(v));
		}

		// Gen 3 Deoxys: the fork stores the form in the unused Sanity high byte (0x1F), outside the checksum.
		if (pk is PK3 p3d && p3d.Species == (int)Species.Deoxys)
		{
			root.Add(Ui.SectionHeader("Deoxys form (PKHaX)"));
			var dv = new VerticalStackLayout { Spacing = 0 };
			dv.Add(Ui.Caption("Stored in PK3 byte 0x1F. Pairs with the Emerald engine patch."));
			var formNames = new List<NamedValue>
			{
				new(0, "Speed (game default)"), new(1, "Normal"), new(2, "Attack"), new(3, "Defense"),
			};
			var curForm = p3d.DeoxysFormOverride;
			var curLabel = formNames.FirstOrDefault(x => x.Value == curForm).Name ?? curForm.ToString();
			var (dRow, dBtn) = Ui.PickerRow("Form", curLabel);
			dBtn.Clicked += async (_, _) =>
			{
				var picked = await PickerPage.ShowAsync("Deoxys form", formNames, curForm);
				if (picked is null) return;
				p3d.DeoxysFormOverride = picked.Value.Value;
				Rebuild();
			};
			dv.Add(dRow);
			root.Add(Ui.Card(dv));
		}

		// Trash bytes: leftovers behind the name terminator. Assign the string first so the buffer is current.
		root.Add(Ui.SectionHeader("Trash bytes"));
		var tv = new VerticalStackLayout { Spacing = 6 };
		tv.Add(Ui.Caption("Raw name buffers. Usually invisible, but they are real save data."));
		var nickTrash = Ui.Action("Nickname buffer");
		nickTrash.Clicked += async (_, _) =>
			await Shell.Current.Navigation.PushAsync(new TrashPage("Nickname", () => pk.NicknameTrash));
		tv.Add(nickTrash);
		var otTrash = Ui.Action("OT name buffer");
		otTrash.Clicked += async (_, _) =>
			await Shell.Current.Navigation.PushAsync(new TrashPage("OT name", () => pk.OriginalTrainerTrash));
		tv.Add(otTrash);
		root.Add(Ui.Card(tv));

		// Status condition: the fork surfaces this for every generation
		root.Add(Ui.SectionHeader("Status condition (PKHaX)"));
		var st = new VerticalStackLayout { Spacing = 0 };
		var statuses = new List<NamedValue>
		{
			new(0, "None"), new(1, "Sleep (1)"), new(2, "Sleep (2)"), new(3, "Sleep (3)"),
			new(4, "Sleep (4)"), new(5, "Sleep (5)"), new(6, "Sleep (6)"), new(7, "Sleep (7)"),
			new(8, "Poison"), new(16, "Burn"), new(32, "Freeze"), new(64, "Paralysis"), new(128, "Bad poison"),
		};
		var curName = statuses.FirstOrDefault(x => x.Value == pk.Status_Condition).Name ?? pk.Status_Condition.ToString();
		var (stRow, stBtn) = Ui.PickerRow("Status", curName);
		stBtn.Clicked += async (_, _) =>
		{
			var picked = await PickerPage.ShowAsync("Status", statuses, pk.Status_Condition);
			if (picked is null) return;
			pk.Status_Condition = picked.Value.Value;
			Rebuild();
		};
		st.Add(stRow);
		root.Add(Ui.Card(st));
	}

	private static ushort SpeciesFromInternal(byte internalId)
	{
		try { return SpeciesConverter.GetNational1(internalId); }
		catch { return 0; }
	}
}
