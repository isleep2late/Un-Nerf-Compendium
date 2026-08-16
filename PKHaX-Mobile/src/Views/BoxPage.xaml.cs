using System.Collections.ObjectModel;
using PKHaX.Mobile.Services;
using PKHaX.Mobile.ViewModels;

namespace PKHaX.Mobile.Views;

public partial class BoxPage : ContentPage
{
	private readonly SaveManager saves;
	private int box;

	public ObservableCollection<SlotItem> Slots { get; } = [];

	public BoxPage(SaveManager saves)
	{
		InitializeComponent();
		this.saves = saves;
		SlotGrid.ItemsSource = Slots;

		// Swipe left/right to change boxes — the primary touch gesture.
		var left = new SwipeGestureRecognizer { Direction = SwipeDirection.Left };
		left.Swiped += (_, _) => Step(+1);
		var right = new SwipeGestureRecognizer { Direction = SwipeDirection.Right };
		right.Swiped += (_, _) => Step(-1);
		Content.GestureRecognizers.Add(left);
		Content.GestureRecognizers.Add(right);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		LoadBox();
	}

	private void OnPrevBox(object? sender, EventArgs e) => Step(-1);
	private void OnNextBox(object? sender, EventArgs e) => Step(+1);

	private void Step(int delta)
	{
		if (saves.Save is null) return;
		box = (box + delta + saves.Save.BoxCount) % saves.Save.BoxCount;
		LoadBox();
	}

	private void LoadBox()
	{
		if (saves.Save is null) return;
		BoxTitle.Text = $"Box {box + 1} / {saves.Save.BoxCount}";
		Slots.Clear();
		var data = saves.GetBox(box);
		for (int i = 0; i < data.Count; i++)
			Slots.Add(SlotItem.From(i, data[i], saves));
	}

	private async void OnSlotSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is not SlotItem item) return;
		SlotGrid.SelectedItem = null;
		if (item.IsEmpty) return;

		await Shell.Current.GoToAsync("editor", new Dictionary<string, object>
		{
			["box"] = box,
			["slot"] = item.Index,
		});
	}
}
