using PKHaX.Mobile.Services;
using PKHaX.Mobile.ViewModels;
using PKHeX.Core;

namespace PKHaX.Mobile.Views;

/// <summary>The six party slots, tap to edit. Party slots are stored separately from the PC boxes.</summary>
public sealed class PartyPage : ContentPage
{
	private readonly SaveManager saves;
	private readonly GameLists lists;
	private readonly VerticalStackLayout root = new() { Padding = 16, Spacing = 8 };

	public PartyPage(SaveManager saves, GameLists lists)
	{
		this.saves = saves;
		this.lists = lists;
		Title = "Party";
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

		var party = saves.GetParty();
		root.Add(Ui.Caption($"{party.Count} of 6 slots filled — tap to edit"));

		for (int i = 0; i < party.Count; i++)
		{
			var idx = i;
			var pk = party[i];
			root.Add(SlotCard(pk, i, () => OpenEditor(idx)));
		}

		if (party.Count == 0)
			root.Add(Ui.Caption("The party is empty."));
	}

	private View SlotCard(PKM pk, int index, Action onTap)
	{
		var grid = new Grid
		{
			ColumnDefinitions = [new(new GridLength(58)), new(GridLength.Star)],
			ColumnSpacing = 12,
		};
		grid.Add(new Image
		{
			Source = Sprites.Url(pk), HeightRequest = 54, WidthRequest = 54, Aspect = Aspect.AspectFit,
		}, 0);

		var info = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center };
		var name = pk.Species == 0 ? "(empty)"
			: string.IsNullOrWhiteSpace(pk.Nickname) ? lists.SpeciesName(pk.Species) : pk.Nickname;
		info.Add(new Label { Text = $"{index + 1}.  {name}", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Ui.Text });
		if (pk.Species != 0)
		{
			var bits = new List<string> { $"Lv {pk.CurrentLevel}", lists.AbilityName(pk.Ability) };
			if (pk.HeldItem > 0) bits.Add(lists.ItemName(pk.HeldItem));
			if (pk.IsShiny) bits.Add("★");
			info.Add(new Label { Text = string.Join("  ·  ", bits), FontSize = 11, TextColor = Ui.Muted });
			info.Add(new Label
			{
				Text = $"HP {pk.Stat_HPCurrent}/{pk.Stat_HPMax}   IVs {pk.IV_HP}/{pk.IV_ATK}/{pk.IV_DEF}/{pk.IV_SPA}/{pk.IV_SPD}/{pk.IV_SPE}",
				FontSize = 10, TextColor = Ui.Muted,
			});
		}
		grid.Add(info, 1);

		var card = Ui.Card(grid);
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => onTap();
		card.GestureRecognizers.Add(tap);
		return card;
	}

	private async void OpenEditor(int slot)
	{
		await Shell.Current.Navigation.PushAsync(new EntityEditorPage(saves, lists, 0, slot, isParty: true));
	}
}
