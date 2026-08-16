using PKHaX.Mobile.Services;

namespace PKHaX.Mobile.Views;

/// <summary>
/// A searchable, alphabetically sorted chooser. Used for every long list (species, moves, items,
/// abilities) because scrolling ~1000 entries in internal ID order is unusable on a phone.
/// Await <see cref="ShowAsync"/> to get the picked value, or null if the user backs out.
/// </summary>
public partial class PickerPage : ContentPage
{
	private readonly IReadOnlyList<NamedValue> all;
	private readonly TaskCompletionSource<NamedValue?> tcs = new();
	private bool answered;

	private PickerPage(string title, IReadOnlyList<NamedValue> items, int current)
	{
		InitializeComponent();
		Title = title;
		all = items;
		List.ItemsSource = all;

		var idx = IndexOf(all, current);
		if (idx >= 0)
			Dispatcher.Dispatch(() => List.ScrollTo(idx, position: ScrollToPosition.Center, animate: false));
	}

	private static int IndexOf(IReadOnlyList<NamedValue> items, int value)
	{
		for (int i = 0; i < items.Count; i++)
			if (items[i].Value == value) return i;
		return -1;
	}

	/// <summary>Pushes the picker and waits for a choice. Returns null if dismissed.</summary>
	public static async Task<NamedValue?> ShowAsync(string title, IReadOnlyList<NamedValue> items, int current)
	{
		if (items.Count == 0) return null;
		var page = new PickerPage(title, items, current);
		await Shell.Current.Navigation.PushAsync(page);
		return await page.tcs.Task;
	}

	private void OnSearchChanged(object? sender, TextChangedEventArgs e)
	{
		var q = (e.NewTextValue ?? "").Trim();
		List.ItemsSource = string.IsNullOrEmpty(q)
			? all
			: all.Where(x => x.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
	}

	private async void OnSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is not NamedValue picked) return;
		answered = true;
		tcs.TrySetResult(picked);
		await Shell.Current.Navigation.PopAsync();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		if (!answered) tcs.TrySetResult(null);
	}
}
