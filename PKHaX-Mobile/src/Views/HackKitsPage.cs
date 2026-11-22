using System.Globalization;
using PKHaX.Hacks.Editing;
using PKHaX.Hacks.Intake;
using PKHaX.Mobile.Services;

namespace PKHaX.Mobile.Views;

public static class HackKitFlow
{
	public static async Task<string> AddAsync(Page page, SaveManager saves, IProgress<string>? progress)
	{
		PickedFile? file;
		try
		{
			file = await HackKits.PickAsync(saves.Gateway);
		}
		catch (Exception ex)
		{
			return $"The file could not be read: {ex.Message}";
		}
		if (file is null)
			return HackKits.Cancelled;
		if (!file.Ok)
			return file.Refusal ?? HackKits.Cancelled;
		try
		{
			return await ReadPickedAsync(page, saves, file, progress);
		}
		catch (Exception ex) when (ex is not OutOfMemoryException)
		{
			return $"PKHaX could not read this file: {ex.Message}";
		}
	}

	private static async Task<string> ReadPickedAsync(Page page, SaveManager saves, PickedFile file, IProgress<string>? progress)
	{
		var verdict = HackKits.Classify(file);
		HackAddResult result;
		switch (verdict.Kind)
		{
			case DropKind.Patch:
				if (HackKits.Inspect(file, out var why) is not { } probe)
					return why ?? "The patch does not read.";
				if (!probe.Known)
					return "The file is not a patch PKHaX applies (IPS, IPS32, UPS, BPS or xdelta).";
				if (!await page.DisplayAlertAsync(IntakeText.Title, HackKits.PatchPrompt(probe), "Choose the clean game", "Cancel"))
					return HackKits.Cancelled;
				PickedFile? baseRom;
				try
				{
					baseRom = await HackKits.PickAsync(saves.Gateway);
				}
				catch (Exception ex)
				{
					return $"The clean game could not be read: {ex.Message}";
				}
				if (baseRom is null)
					return HackKits.Cancelled;
				if (!baseRom.Ok)
					return baseRom.Refusal ?? HackKits.Cancelled;
				if (!HackKits.Classify(baseRom).IsRom)
					return HackKits.NotABase;
				result = await saves.Kits.FromPatchAsync(file, baseRom, progress);
				break;
			case DropKind.GbaRom or DropKind.GbRom or DropKind.NdsRom:
				result = await saves.Kits.DeriveAsync(file, progress);
				break;
			case DropKind.CtrImage:
				return IntakeText.CannotRead;
			case DropKind.Profile or DropKind.Bundle:
				result = await saves.Kits.FromProfileAsync(file);
				break;
			case DropKind.Save:
				return HackKits.SaveNotKit;
			default:
				return verdict.Sentence ?? IntakeText.Unknown(file.Name);
		}
		return result.Sentence;
	}
}

public sealed class HackKitsPage : ContentPage
{
	private readonly SaveManager saves;
	private readonly GameLists lists;
	private readonly VerticalStackLayout root = new() { Padding = 16, Spacing = 8 };
	private readonly Label status = new() { FontSize = 12, TextColor = Ui.Muted, Margin = new Thickness(4, 6) };
	private bool busy;

	public HackKitsPage(SaveManager saves, GameLists lists)
	{
		this.saves = saves;
		this.lists = lists;
		Title = IntakeText.Title;
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
		root.Add(Ui.Caption(IntakeText.Stored));
		var add = Ui.Action("Add a ROM hack or patch", Ui.Accent);
		add.IsEnabled = !busy;
		add.Clicked += OnAddClicked;
		root.Add(add);
		root.Add(status);
		var kits = saves.Kits.List();
		root.Add(Ui.SectionHeader(HackKits.CountLine(kits.Count)));
		foreach (var kit in kits)
			root.Add(KitCard(kit));
	}

	private View KitCard(StoredKit kit)
	{
		var stack = new VerticalStackLayout { Spacing = 6 };
		stack.Add(new Label { Text = kit.Kit.Name, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Ui.Text });
		stack.Add(new Label { Text = kit.Kit.Summary, FontSize = 12, TextColor = Ui.Muted });
		stack.Add(new Label
		{
			Text = string.Create(CultureInfo.InvariantCulture, $"Save shape: {kit.Kit.Shape ?? kit.Kit.SaveFamily} · read {kit.Kit.At.ToLocalTime():yyyy-MM-dd HH:mm}"),
			FontSize = 11,
			TextColor = Ui.Muted,
		});
		if (kit.Kit.Source.FromPatch)
			stack.Add(new Label { Text = $"From the {kit.Kit.Source.PatchFormat} patch {kit.Kit.Source.FileName}{(kit.Kit.Source.Note is { } note ? $" ({note})" : "")}", FontSize = 11, TextColor = Ui.Muted });
		if (kit.Problem is { } problem)
			stack.Add(new Label { Text = problem, FontSize = 12, TextColor = Ui.Accent });

		var open = Ui.Action("Open a save for this hack...");
		open.IsEnabled = kit.Usable && !busy;
		open.Clicked += async (_, _) => await OpenSaveAsync(kit);
		stack.Add(open);

		var report = Ui.Action("What was read");
		report.Clicked += async (_, _) => await DisplayAlertAsync(kit.Kit.Name, string.Join("\n", kit.Kit.Report), "OK");
		stack.Add(report);

		var forget = Ui.Action("Forget");
		forget.IsEnabled = !busy;
		forget.Clicked += async (_, _) => await ForgetAsync(kit);
		stack.Add(forget);
		return Ui.Card(stack);
	}

	private async void OnAddClicked(object? sender, EventArgs e)
	{
		if (busy)
			return;
		busy = true;
		Build();
		status.Text = "Reading…";
		try
		{
			status.Text = await HackKitFlow.AddAsync(this, saves, new Progress<string>(p => status.Text = p));
		}
		finally
		{
			busy = false;
			var text = status.Text;
			Build();
			status.Text = text;
		}
	}

	private async Task OpenSaveAsync(StoredKit kit)
	{
		if (busy)
			return;
		busy = true;
		try
		{
			var error = await saves.OpenForKitAsync(kit);
			if (saves.Save is not null)
				lists.Build(saves.Save);
			if (error is null && saves.IsLoaded)
			{
				await Shell.Current.Navigation.PopAsync();
				return;
			}
			status.Text = error ?? HackKits.Cancelled;
		}
		finally
		{
			busy = false;
		}
	}

	private async Task ForgetAsync(StoredKit kit)
	{
		var inUse = saves.Kit?.Kit.Key == kit.Kit.Key;
		var text = "Its tables are deleted from this device, and saves paired with it are no longer matched to it. Your ROM and your saves are not touched.";
		if (inUse)
			text += " The save open with this hack is closed first; edits not saved to its file are lost.";
		if (!await DisplayAlertAsync($"Forget {kit.Kit.Name}?", text, "Forget", "Cancel"))
			return;
		if (inUse)
			saves.Close();
		var error = saves.Kits.Forget(kit);
		Build();
		status.Text = error ?? $"{kit.Kit.Name} was forgotten.";
	}
}

public sealed class HackPartyPage : ContentPage
{
	private readonly SaveManager saves;
	private readonly VerticalStackLayout root = new() { Padding = 16, Spacing = 8 };

	public HackPartyPage(SaveManager saves)
	{
		this.saves = saves;
		Title = "Party (read-only)";
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
		if (saves.Hack is not { } model)
		{
			root.Add(Ui.Caption("No hack save is open."));
			return;
		}
		if (saves.Kit is { } kit)
			root.Add(Ui.SectionHeader(kit.Kit.Name));
		root.Add(Ui.ReadOnlyRow("Trainer", model.TrainerLine));
		root.Add(Ui.ReadOnlyRow("Money", model.MoneyLine));
		root.Add(Ui.Caption(saves.WriteRefusal ?? HackKits.NativeReadOnly));
		if (model.NamesBanner is { } names)
			root.Add(Ui.Caption(names));
		root.Add(Ui.SectionHeader($"Party: {model.Party.Count(s => s.Mon is not null)} of 6"));
		foreach (var slot in model.Party)
			root.Add(SlotCard(slot));
		if (model.Party.Count == 0)
			root.Add(Ui.Caption("The party is empty."));
		root.Add(Ui.SectionHeader("Boxes"));
		if (model.BoxesReason is { } boxes)
			root.Add(Ui.Caption(boxes));
		foreach (var box in model.Boxes)
			root.Add(Ui.ReadOnlyRow($"Box {box.Number}", string.Create(CultureInfo.InvariantCulture, $"{box.Count} Pokemon")));
	}

	private static View SlotCard(HackSlotModel slot)
	{
		var stack = new VerticalStackLayout { Spacing = 2 };
		stack.Add(new Label { Text = $"{slot.Where.Slot + 1}.  {slot.Title}", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Ui.Text });
		foreach (var line in slot.Detail)
			stack.Add(new Label { Text = line, FontSize = 11, TextColor = Ui.Muted });
		return Ui.Card(stack);
	}
}
