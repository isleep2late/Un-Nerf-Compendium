using PKHaX.Mobile.Services;
using PKHaX.Mobile.ViewModels;
using PKHeX.Core;

namespace PKHaX.Mobile.Views;

[QueryProperty(nameof(Box), "box")]
[QueryProperty(nameof(Slot), "slot")]
public partial class EntityEditorPage : ContentPage
{
	private readonly SaveManager saves;
	private PKM? pk;
	private bool loading;

	public int Box { get; set; }
	public int Slot { get; set; }

	public EntityEditorPage(SaveManager saves)
	{
		InitializeComponent();
		this.saves = saves;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (saves.Save is null) { _ = Shell.Current.GoToAsync(".."); return; }

		// Work on a clone; nothing touches the save until "Apply".
		pk = saves.GetBox(Box)[Slot].Clone();
		BindFields();
	}

	private void BindFields()
	{
		if (pk is null) return;
		loading = true;

		var s = saves.Strings;
		FillPicker(SpeciesPicker, s.specieslist, pk.Species);
		FillPicker(AbilityPicker, s.abilitylist, EffectiveAbility(pk));
		FillPicker(Move1, s.movelist, pk.Move1);
		FillPicker(Move2, s.movelist, pk.Move2);
		FillPicker(Move3, s.movelist, pk.Move3);
		FillPicker(Move4, s.movelist, pk.Move4);

		LevelStepper.Value = pk.CurrentLevel;
		LevelValue.Text = pk.CurrentLevel.ToString();
		ShinySwitch.IsToggled = pk.IsShiny;

		AbilityHint.Text = pk is PK3
			? "Gen 3: any ability can be set on any species (stored in the unused Sanity byte)."
			: "Any ability index in this generation's list.";

		RefreshHeader();
		loading = false;
	}

	private static void FillPicker(Picker picker, IReadOnlyList<string> items, int selected)
	{
		picker.ItemsSource = items.ToList();
		if (selected >= 0 && selected < items.Count)
			picker.SelectedIndex = selected;
	}

	private static int EffectiveAbility(PKM pk) => pk.Ability;

	private void RefreshHeader()
	{
		if (pk is null) return;
		HeaderName.Text = string.IsNullOrWhiteSpace(pk.Nickname) ? saves.SpeciesName(pk.Species) : pk.Nickname;
		HeaderDetail.Text = $"Lv {pk.CurrentLevel} · {saves.AbilityName(pk.Ability)}" + (pk.IsShiny ? " · ★" : "");
		Sprite.Source = Sprites.Url(pk);
	}

	private void OnSpeciesChanged(object? sender, EventArgs e)
	{
		if (loading || pk is null || SpeciesPicker.SelectedIndex < 0) return;
		pk.Species = (ushort)SpeciesPicker.SelectedIndex;
		RefreshHeader();
	}

	private void OnLevelChanged(object? sender, ValueChangedEventArgs e)
	{
		if (loading || pk is null) return;
		pk.CurrentLevel = (byte)e.NewValue;
		LevelValue.Text = pk.CurrentLevel.ToString();
		RefreshHeader();
	}

	private void OnAbilityChanged(object? sender, EventArgs e)
	{
		if (loading || pk is null || AbilityPicker.SelectedIndex < 0) return;
		int abilityId = AbilityPicker.SelectedIndex;

		// The headline fork feature: Gen 3 any-ability writes the override byte directly (matches desktop PKHaX).
		if (pk is PK3 p3)
			p3.AbilityOverride = abilityId;
		else
			pk.SetAbility(abilityId);

		RefreshHeader();
	}

	private void OnShinyToggled(object? sender, ToggledEventArgs e)
	{
		if (loading || pk is null) return;
		pk.SetIsShiny(e.Value);
		RefreshHeader();
	}

	private void OnMoveChanged(object? sender, EventArgs e)
	{
		if (loading || pk is null) return;
		Span<ushort> moves =
		[
			(ushort)Math.Max(0, Move1.SelectedIndex),
			(ushort)Math.Max(0, Move2.SelectedIndex),
			(ushort)Math.Max(0, Move3.SelectedIndex),
			(ushort)Math.Max(0, Move4.SelectedIndex),
		];
		pk.SetMoves(moves);
	}

	private async void OnApplyClicked(object? sender, EventArgs e)
	{
		if (pk is null || saves.Save is null) return;

		pk.RefreshChecksum();
		saves.SetBoxSlot(Box, Slot, pk);

		// Show a legality read-out (informational only — illegal mode never blocks the write).
		var report = new LegalityAnalysis(pk);
		LegalityLabel.Text = report.Valid ? "Applied · legal." : "Applied · illegal (PKHaX mode allows it).";

		await Task.Delay(400);
		await Shell.Current.GoToAsync("..");
	}
}
