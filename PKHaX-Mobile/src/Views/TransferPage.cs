using PKHaX.Mobile.Services;
using PKHaX.Mobile.ViewModels;
using PKHeX.Core;

namespace PKHaX.Mobile.Views;

/// <summary>
/// Two saves on one screen, and a Pokemon that moves between them.
///
/// The top half is the save the rest of the app has open; the bottom half is a second save this
/// page owns. Each half shows the party or one box at a time as a compact slot grid. Moving a
/// Pokemon works two ways, because touch drag is the flashy one and tap-tap is the reliable one:
/// drag a slot from either half onto a slot in the other (or the same) half, or TAP a Pokemon to
/// pick it up - the slot highlights - and tap where it should go. A move COPIES: the original
/// stays, because a copy that landed twice is deleted in one tap, and a Pokemon lost in a
/// half-finished move is gone.
///
/// Every write goes through <see cref="SaveTransfer.Prepare"/> - format conversion to the
/// destination's own type, the Gen 1/2 language wall, and the save's compatibility complaints as
/// visible warnings. A .pk1-.pk9 file can also be imported straight into either save's first free
/// slot of the box on screen.
/// </summary>
public sealed class TransferPage : ContentPage
{
	private sealed record SlotRef(bool PaneB, bool IsParty, int Box, int Slot);

	private readonly SaveManager savesA;
	private readonly SaveManager savesB;
	private readonly ISaveFileGateway gateway;

	private readonly VerticalStackLayout paneA = new() { Spacing = 4 };
	private readonly VerticalStackLayout paneB = new() { Spacing = 4 };
	private readonly Label status = new() { FontSize = 12, TextColor = Ui.Muted, LineBreakMode = LineBreakMode.WordWrap, Margin = new Thickness(8, 4) };

	private bool partyViewA;
	private bool partyViewB;
	private int boxA;
	private int boxB;

	/// <summary>The slot a tap picked up, waiting for a second tap to place it. Null = nothing armed.</summary>
	private SlotRef? pickedUp;

	/// <summary>The slot a platform drag picked up. Set in DragStarting, consumed in Drop.</summary>
	private SlotRef? dragging;

	public TransferPage(SaveManager saves, ISaveFileGateway gateway)
	{
		savesA = saves;
		this.gateway = gateway;
		savesB = new SaveManager(gateway);

		Title = "Two saves";
		BackgroundColor = Ui.Bg;

		var root = new VerticalStackLayout { Spacing = 4, Padding = new Thickness(8) };
		root.Add(Ui.Caption("Drag a Pokémon between the two halves — or tap one to pick it up, then tap "
			+ "where it goes. Moving COPIES; the original stays. Imports and transfers are converted to "
			+ "the destination save's own format, and refusals say why."));
		root.Add(Ui.Card(paneA));
		root.Add(Ui.Card(paneB));
		root.Add(status);
		Content = new ScrollView { Content = root };
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		RebuildPanes();
	}

	private void RebuildPanes()
	{
		BuildPane(paneA, false);
		BuildPane(paneB, true);
	}

	private SaveManager Manager(bool b) => b ? savesB : savesA;

	private void BuildPane(VerticalStackLayout pane, bool isB)
	{
		pane.Clear();
		var saves = Manager(isB);
		var sav = saves.Save;

		var headerRow = new Grid
		{
			ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)],
			ColumnSpacing = 8,
		};
		var title = sav is null
			? (isB ? "Save B — none open" : "Save A — none open")
			: $"{(isB ? "B" : "A")}: {saves.Handle?.DisplayName ?? "save"} · TID {sav.DisplayTID}";
		headerRow.Add(new Label { Text = title, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Ui.Text, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation }, 0);

		if (isB)
		{
			var open = new Button { Text = sav is null ? "Open…" : "Write back", FontSize = 12, HeightRequest = 34, CornerRadius = 8, BackgroundColor = sav is null ? Ui.SurfaceAlt : Ui.Positive, TextColor = Ui.Text, Padding = new Thickness(10, 0) };
			open.Clicked += async (_, _) =>
			{
				if (savesB.Save is null)
				{
					var err = await savesB.OpenAsync();
					SetStatus(err ?? (savesB.Save is null ? "" : "Save B open. Drag either way."));
					RebuildPanes();
				}
				else
				{
					var err = await savesB.SaveBackAsync();
					SetStatus(err ?? "Save B written back to its file.");
				}
			};
			headerRow.Add(open, 1);
		}
		pane.Add(headerRow);

		if (sav is null)
		{
			if (!isB)
				pane.Add(Ui.Caption("Open a save from the main page first — it appears here as Save A."));
			return;
		}

		// party/box switcher + box pager
		var partyView = isB ? partyViewB : partyViewA;
		var box = isB ? boxB : boxA;
		var bar = new HorizontalStackLayout { Spacing = 6 };
		var toggle = new Button { Text = partyView ? "Party ▾" : $"Box {box + 1} ▾", FontSize = 12, HeightRequest = 30, CornerRadius = 8, BackgroundColor = Ui.SurfaceAlt, TextColor = Ui.Text, Padding = new Thickness(10, 0) };
		toggle.Clicked += (_, _) =>
		{
			if (isB) partyViewB = !partyViewB; else partyViewA = !partyViewA;
			RebuildPanes();
		};
		bar.Add(toggle);
		if (!partyView && sav.BoxCount > 0)
		{
			var prev = PagerButton("‹", () => Turn(isB, -1));
			var next = PagerButton("›", () => Turn(isB, +1));
			bar.Add(prev);
			bar.Add(next);
		}
		var import = new Button { Text = "⤓ .pk file", FontSize = 12, HeightRequest = 30, CornerRadius = 8, BackgroundColor = Ui.SurfaceAlt, TextColor = Ui.Text, Padding = new Thickness(10, 0) };
		import.Clicked += async (_, _) => await ImportPkAsync(isB);
		bar.Add(import);
		pane.Add(bar);

		// the slots
		var grid = new Grid { ColumnSpacing = 4, RowSpacing = 4 };
		const int columns = 6;
		for (var c = 0; c < columns; c++)
			grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

		var slots = partyView
			? Enumerable.Range(0, 6).Select(i => i < sav.PartyCount ? sav.GetPartySlotAtIndex(i) : sav.BlankPKM).ToList()
			: [.. saves.GetBox(box)];
		var rows = (slots.Count + columns - 1) / columns;
		for (var r = 0; r < rows; r++)
			grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

		for (var i = 0; i < slots.Count; i++)
		{
			var at = new SlotRef(isB, partyView, partyView ? 0 : box, i);
			grid.Add(SlotCell(at, slots[i], saves), i % columns, i / columns);
		}
		pane.Add(grid);
	}

	private Button PagerButton(string text, Action turn)
	{
		var b = new Button { Text = text, FontSize = 14, HeightRequest = 30, WidthRequest = 38, CornerRadius = 8, BackgroundColor = Ui.SurfaceAlt, TextColor = Ui.Text, Padding = Thickness.Zero };
		b.Clicked += (_, _) => turn();
		return b;
	}

	private void Turn(bool isB, int direction)
	{
		var sav = Manager(isB).Save;
		if (sav is null || sav.BoxCount == 0)
			return;
		if (isB)
			boxB = ((boxB + direction) % sav.BoxCount + sav.BoxCount) % sav.BoxCount;
		else
			boxA = ((boxA + direction) % sav.BoxCount + sav.BoxCount) % sav.BoxCount;
		RebuildPanes();
	}

	private View SlotCell(SlotRef at, PKM pk, SaveManager saves)
	{
		var occupied = pk.Species != 0;
		var armed = Equals(pickedUp, at);

		var body = new VerticalStackLayout { Spacing = 0 };
		if (occupied)
		{
			body.Add(new Image { Source = Sprites.Url(pk), HeightRequest = 34, Aspect = Aspect.AspectFit });
			body.Add(new Label
			{
				Text = saves.SpeciesName(pk.Species) + (pk.IsShiny ? " ★" : ""),
				FontSize = 9,
				TextColor = pk.IsShiny ? Color.FromArgb("#E4B02E") : Ui.Text,
				HorizontalTextAlignment = TextAlignment.Center,
				LineBreakMode = LineBreakMode.TailTruncation,
			});
		}
		else
		{
			body.Add(new Label { Text = "—", FontSize = 16, TextColor = Color.FromArgb("#3A4763"), HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 12) });
		}

		var cell = new Border
		{
			Content = body,
			BackgroundColor = armed ? Color.FromArgb("#2A3B5E") : occupied ? Ui.SurfaceAlt : Ui.Bg,
			Stroke = armed ? Ui.Accent : Ui.Stroke,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
			Padding = 2,
			MinimumHeightRequest = 52,
		};

		// Tap: pick up / place / cancel. The reliable path on every device.
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) =>
		{
			if (pickedUp is { } armedRef)
			{
				if (Equals(armedRef, at))
				{
					pickedUp = null;
					SetStatus("Put back down.");
				}
				else
				{
					pickedUp = null;
					DoTransfer(armedRef, at);
				}
				RebuildPanes();
			}
			else if (occupied)
			{
				pickedUp = at;
				SetStatus($"Picked up {saves.SpeciesName(pk.Species)} — tap the slot it should go to (tap it again to put it back).");
				RebuildPanes();
			}
		};
		cell.GestureRecognizers.Add(tap);

		// Drag: the direct path where the platform cooperates.
		if (occupied)
		{
			var drag = new DragGestureRecognizer { CanDrag = true };
			drag.DragStarting += (_, e) =>
			{
				dragging = at;
				e.Data.Text = "pkhax-slot";
			};
			cell.GestureRecognizers.Add(drag);
		}
		var drop = new DropGestureRecognizer { AllowDrop = true };
		drop.Drop += (_, e) =>
		{
			e.Handled = true;
			if (dragging is { } from)
			{
				dragging = null;
				DoTransfer(from, at);
				RebuildPanes();
			}
		};
		cell.GestureRecognizers.Add(drop);

		return cell;
	}

	private PKM? ReadSlot(SlotRef at)
	{
		var saves = Manager(at.PaneB);
		if (saves.Save is not { } sav)
			return null;
		if (at.IsParty)
			return at.Slot < sav.PartyCount ? sav.GetPartySlotAtIndex(at.Slot) : null;
		var data = saves.GetBox(at.Box);
		return at.Slot < data.Count ? data[at.Slot] : null;
	}

	private void DoTransfer(SlotRef from, SlotRef to)
	{
		if (Equals(from, to))
			return;
		var source = ReadSlot(from);
		var destSaves = Manager(to.PaneB);
		if (source is null || source.Species == 0)
		{
			SetStatus("That slot is empty — there is nothing to move.");
			return;
		}
		if (destSaves.Save is not { } destSav)
		{
			SetStatus("Open a save on that side first.");
			return;
		}

		Place(SaveTransfer.Prepare(source, destSav, destSaves.IllegalMode), destSaves, to,
			destSaves.SpeciesName(source.Species));
	}

	private void Place(SaveTransfer.Outcome outcome, SaveManager destSaves, SlotRef to, string what)
	{
		if (!outcome.Ok)
		{
			SetStatus(outcome.Refusal!);
			return;
		}
		var pk = outcome.Converted!;
		var destSav = destSaves.Save!;

		int landed;
		if (to.IsParty)
		{
			// A party is continuous - a drop on any empty party cell appends rather than stranding
			// a Pokemon behind a gap the game will never look past.
			landed = Math.Min(to.Slot, destSav.PartyCount);
			if (landed >= 6)
			{
				SetStatus("That party is full — drop it on a box slot instead.");
				return;
			}
			destSaves.SetPartySlot(landed, pk);
		}
		else
		{
			landed = to.Slot;
			destSaves.SetBoxSlot(to.Box, landed, pk);
		}

		RebuildPanes();
		var place = to.IsParty ? $"party slot {landed + 1}" : $"Box {to.Box + 1} slot {landed + 1}";
		var side = to.PaneB ? "Save B" : "Save A";
		var warned = outcome.Warnings.Count > 0 ? $" Notes: {string.Join(" · ", outcome.Warnings)}" : "";
		SetStatus($"Copied {what} into {side}, {place}. The original stays; use Write back (or the main "
			+ $"page's save button for Save A) to make it real.{warned}");
	}

	private async Task ImportPkAsync(bool isB)
	{
		var saves = Manager(isB);
		if (saves.Save is not { } sav)
		{
			SetStatus("Open that save first.");
			return;
		}
		try
		{
			var picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Pick a .pk1-.pk9 Pokémon file" });
			if (picked is null)
				return;
			await using var stream = await picked.OpenReadAsync();
			using var ms = new MemoryStream();
			await stream.CopyToAsync(ms);
			var bytes = ms.ToArray();
			var ext = Path.GetExtension(picked.FileName).TrimStart('.');
			if (!FileUtil.TryGetPKM(bytes, out var pk, ext) || pk is null)
			{
				SetStatus($"{picked.FileName} does not read as a Pokémon file.");
				return;
			}

			// Lands in the first free slot of the box this half is showing (or appends to the party
			// when the party view is up).
			var partyView = isB ? partyViewB : partyViewA;
			var box = isB ? boxB : boxA;
			SlotRef to;
			if (partyView)
			{
				to = new SlotRef(isB, true, 0, sav.PartyCount);
			}
			else
			{
				var data = saves.GetBox(box);
				var free = -1;
				for (var i = 0; i < data.Count; i++)
				{
					if (data[i].Species == 0)
					{
						free = i;
						break;
					}
				}
				if (free < 0)
				{
					SetStatus($"Box {box + 1} is full — turn to another box and try again.");
					return;
				}
				to = new SlotRef(isB, false, box, free);
			}
			Place(SaveTransfer.Prepare(pk, sav, saves.IllegalMode), saves, to, picked.FileName);
		}
		catch (Exception ex)
		{
			SetStatus($"Could not import: {ex.Message}");
		}
	}

	private void SetStatus(string text) => status.Text = text;
}
