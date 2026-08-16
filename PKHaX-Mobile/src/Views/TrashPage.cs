using PKHeX.Core;

namespace PKHaX.Mobile.Views;

/// <summary>
/// Raw string-buffer ("trash byte") editor. Games write a name into a fixed-width buffer without clearing
/// what was there before, so leftovers from a previous name survive past the terminator. They are normally
/// invisible, but they are part of the save data and legality checks can look at them. Bytes are shown and
/// edited as hex; the caller must have assigned the string property first so the buffer is current.
/// </summary>
public sealed class TrashPage : ContentPage
{
	private readonly Func<Span<byte>> getSpan;
	private readonly VerticalStackLayout root = new() { Padding = 16, Spacing = 2 };
	private readonly string label;

	public TrashPage(string label, Func<Span<byte>> getSpan)
	{
		this.label = label;
		this.getSpan = getSpan;
		Title = $"Trash bytes — {label}";
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
		var span = getSpan();

		root.Add(Ui.Caption($"{label}: {span.Length} bytes. Edit as hex, two characters per byte."));

		var hex = Convert.ToHexString(span);
		var (row, entry) = Ui.EntryRow("Hex", hex);
		entry.FontSize = 12;
		root.Add(Ui.Card(row));

		var apply = Ui.Action("Write bytes", Ui.Positive);
		apply.Clicked += async (_, _) =>
		{
			var text = (entry.Text ?? "").Trim().Replace(" ", "");
			var target = getSpan();
			if (text.Length != target.Length * 2)
			{
				await DisplayAlertAsync("Wrong length", $"Expected {target.Length * 2} hex characters for {target.Length} bytes, got {text.Length}.", "OK");
				return;
			}
			byte[] bytes;
			try { bytes = Convert.FromHexString(text); }
			catch { await DisplayAlertAsync("Bad hex", "That is not valid hexadecimal.", "OK"); return; }
			bytes.CopyTo(target);
			await Shell.Current.Navigation.PopAsync();
		};
		root.Add(apply);

		var clear = Ui.Action("Zero the buffer");
		clear.Clicked += (_, _) => { getSpan().Clear(); Build(); };
		root.Add(clear);
	}
}
