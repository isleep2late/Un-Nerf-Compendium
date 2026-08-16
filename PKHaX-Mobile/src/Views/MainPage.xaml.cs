using PKHaX.Mobile.Services;

namespace PKHaX.Mobile.Views;

public partial class MainPage : ContentPage
{
	private readonly SaveManager saves;

	public MainPage(SaveManager saves)
	{
		InitializeComponent();
		this.saves = saves;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		HaxSwitch.IsToggled = saves.IllegalMode;
		RefreshSummary();
	}

	private void OnHaxToggled(object? sender, ToggledEventArgs e) => saves.IllegalMode = e.Value;

	private async void OnOpenClicked(object? sender, EventArgs e)
	{
		OpenButton.IsEnabled = false;
		StatusLabel.Text = "Opening…";
		try
		{
			var error = await saves.OpenAsync();
			StatusLabel.Text = error ?? (saves.IsLoaded ? "Loaded." : "Cancelled.");
			RefreshSummary();
		}
		finally
		{
			OpenButton.IsEnabled = true;
		}
	}

	private async void OnBoxesClicked(object? sender, EventArgs e)
	{
		if (!saves.IsLoaded) return;
		await Shell.Current.GoToAsync("box");
	}

	private async void OnSaveClicked(object? sender, EventArgs e)
	{
		SaveButton.IsEnabled = false;
		StatusLabel.Text = "Writing…";
		var error = await saves.SaveBackAsync();
		StatusLabel.Text = error ?? "Saved to file.";
		SaveButton.IsEnabled = true;
	}

	private void RefreshSummary()
	{
		var sav = saves.Save;
		if (sav is null)
		{
			SummaryCard.IsVisible = false;
			return;
		}

		SummaryCard.IsVisible = true;
		GameLabel.Text = $"{sav.Version} · Gen {sav.Generation}";
		TrainerLabel.Text = $"OT {sav.OT}   ID {sav.DisplayTID}/{sav.DisplaySID}";
		CountsLabel.Text = $"{sav.BoxCount} boxes · party {sav.PartyCount}";
	}
}
