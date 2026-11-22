using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PKHaX.Hacks.Intake;
using PKHaX.Linux.Session;
using PKHaX.Linux.Shared;

namespace PKHaX.Linux.Views;

public sealed class MainWindow : Window
{
	public const string GapLine = "PKHaX for Linux edits the party, boxes and money of a save, and hack-native saves through the hack editor. It has no event flags, mystery gifts, batch editor or legality panel; for those use isleep2late-Files --editor <save>, or the Windows PKHaX under Wine.";

	private static readonly IBrush Page = new SolidColorBrush(Color.Parse("#0F1626"));

	private readonly HackKitStore store;
	private readonly DropPanel drop;
	private readonly TextBlock fileLine = new() { Foreground = DropPanel.Ink, FontSize = 15, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap };
	private readonly TextBlock kindLine = new() { Foreground = DropPanel.Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap };
	private readonly TextBlock writeLine = new() { Foreground = DropPanel.Lit, FontSize = 12, TextWrapping = TextWrapping.Wrap };
	private readonly TextBlock status = new() { Foreground = DropPanel.Ink, FontSize = 12, TextWrapping = TextWrapping.Wrap };
	private readonly TextBlock notes = new() { Foreground = DropPanel.Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap };
	private readonly StackPanel question = new() { Spacing = 6 };
	private readonly ContentControl host = new();
	private readonly Button save;
	private LinuxSaveSession? session;

	public MainWindow(string? start)
	{
		Title = "PKHaX";
		Width = 1180;
		Height = 820;
		MinWidth = 760;
		MinHeight = 520;
		Background = Page;
		store = new HackKitStore(HackKitStore.DefaultRoot());
		drop = new DropPanel(store, OpenSaveAsync);
		save = DropPanel.MakeButton("Save Changes Back", DropPanel.Good);
		save.IsEnabled = false;
		save.Click += (_, _) => Write();
		var open = DropPanel.MakeButton("Open a save...", DropPanel.Blue);
		open.Click += async (_, _) =>
		{
			try
			{
				var picked = await PickAsync(this, "A save file", false);
				if (picked.Count > 0)
					await OpenSaveAsync(picked[0], null);
			}
			catch (Exception ex)
			{
				status.Text = $"No save was opened: {ex.Message}";
			}
		};
		var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { open, save } };
		var right = new StackPanel { Spacing = 8, Margin = new Thickness(12) };
		right.Children.Add(bar);
		right.Children.Add(fileLine);
		right.Children.Add(kindLine);
		right.Children.Add(writeLine);
		right.Children.Add(question);
		right.Children.Add(status);
		right.Children.Add(host);
		right.Children.Add(notes);
		right.Children.Add(new TextBlock { Text = GapLine, Foreground = DropPanel.Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap });
		var leftBorder = new Border { Background = DropPanel.Card, Child = drop };
		var rightScroll = new ScrollViewer { Content = right };
		var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("360,*") };
		Grid.SetColumn(leftBorder, 0);
		Grid.SetColumn(rightScroll, 1);
		grid.Children.Add(leftBorder);
		grid.Children.Add(rightScroll);
		Content = grid;
		fileLine.Text = "No save is open.";
		kindLine.Text = "Drop a save here, or use Open a save. Drop a ROM hack or a patch on the left first, so PKHaX knows the hack's names and save shape.";
		DragDrop.SetAllowDrop(this, true);
		AddHandler(DragDrop.DragEnterEvent, OnDragOver);
		AddHandler(DragDrop.DragOverEvent, OnDragOver);
		AddHandler(DragDrop.DragLeaveEvent, (_, _) => drop.SetHighlight(false));
		AddHandler(DragDrop.DropEvent, OnDrop);
		Closed += (_, _) =>
		{
			session?.Dispose();
			session = null;
			HackOpen.Close();
		};
		if (start is { Length: > 0 } first)
			Opened += async (_, _) =>
			{
				try
				{
					await drop.HandleAsync([first]);
				}
				catch (Exception ex)
				{
					status.Text = $"{Path.GetFileName(first)} could not be opened: {ex.Message}";
				}
			};
	}

	internal static async Task<IReadOnlyList<string>> PickAsync(Visual from, string title, bool many)
	{
		if (TopLevel.GetTopLevel(from) is not { } top)
			return [];
		var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = many });
		return files.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
	}

	private void OnDragOver(object? sender, DragEventArgs e)
	{
		var files = e.DataTransfer.Contains(DataFormat.File);
		e.DragEffects = files ? DragDropEffects.Copy : DragDropEffects.None;
		drop.SetHighlight(files);
	}

	private async void OnDrop(object? sender, DragEventArgs e)
	{
		drop.SetHighlight(false);
		var paths = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().ToList() ?? [];
		if (paths.Count == 0)
			return;
		e.Handled = true;
		try
		{
			await drop.HandleAsync(paths);
		}
		catch (Exception ex)
		{
			status.Text = $"The drop stopped: {ex.Message}";
		}
	}

	public Task OpenSaveAsync(string path, OpenHint? hint)
	{
		session?.Dispose();
		session = null;
		host.Content = null;
		status.Text = "";
		try
		{
			session = LinuxSaveSession.Open(path, store, hint);
		}
		catch (Exception ex)
		{
			fileLine.Text = Path.GetFileName(path);
			kindLine.Text = $"Not opened: {ex.Message}";
			writeLine.Text = "";
			question.Children.Clear();
			notes.Text = "";
			save.IsEnabled = false;
			return Task.CompletedTask;
		}
		Render();
		return Task.CompletedTask;
	}

	private void Render()
	{
		question.Children.Clear();
		if (session is not { } s)
			return;
		var r = s.Result;
		fileLine.Text = s.FileName;
		kindLine.Text = r.Kind switch
		{
			OpenKind.Stock => $"{(r.Kit is { } kit ? $"{kit.Kit.Name}, a hack on " : "")}{r.Stock?.GetType().Name ?? "a save"}{(r.OverlayActive ? "; the hack's names and stats are shown" : "")}",
			OpenKind.Native => $"{r.Kit?.Kit.Name ?? "a ROM hack"}: the hack's own save format{(r.Native is { } model ? $" ({model.Variant})" : "")}",
			OpenKind.Ask => r.Sentence ?? IntakeText.Question,
			_ => r.Sentence ?? IntakeText.NotASave,
		};
		if (r.Banner is { } banner)
			question.Children.Add(new TextBlock { Text = banner, Foreground = DropPanel.Lit, TextWrapping = TextWrapping.Wrap });
		if (r.AsksUnmodified && r.UnmodifiedQuestion is { } asked)
		{
			question.Children.Add(new TextBlock { Text = asked, Foreground = DropPanel.Ink, TextWrapping = TextWrapping.Wrap });
			var yes = DropPanel.MakeButton(r.ConfirmLabel ?? "Yes", DropPanel.Blue);
			yes.Click += (_, _) =>
			{
				r.ConfirmUnmodified();
				Render();
			};
			question.Children.Add(yes);
		}
		if (r.Kind == OpenKind.Ask)
		{
			foreach (var choice in r.Choices)
			{
				var key = choice.Kit.Key;
				var pick = DropPanel.MakeButton($"This is a save of {choice.Kit.Name}", DropPanel.Blue);
				pick.Click += async (_, _) => await OpenSaveAsync(s.SavePath, new OpenHint { KitKey = key, Answered = true });
				question.Children.Add(pick);
			}
			var none = DropPanel.MakeButton("None of these", DropPanel.Blue);
			none.Click += async (_, _) => await OpenSaveAsync(s.SavePath, new OpenHint { NoKit = true });
			question.Children.Add(none);
		}
		host.Content = r.Kind switch
		{
			OpenKind.Stock => new StockSaveView(s, Changed),
			OpenKind.Native when r.Native is { } model => new HackEditorView(model, null, s.CanWrite ? ApplyHack : null),
			_ => null,
		};
		notes.Text = r.Notes.Count == 0 ? "" : "Notes: " + string.Join("; ", r.Notes);
		Changed();
	}

	private string? ApplyHack(PKHaX.Hacks.Editing.HackMonEditModel edit)
	{
		if (session is not { } s)
			return "No save is open.";
		var refusal = s.ApplyHack(edit);
		Changed();
		return refusal;
	}

	private void Changed()
	{
		if (session is not { } s)
		{
			save.IsEnabled = false;
			writeLine.Text = "";
			return;
		}
		save.IsEnabled = s.CanWrite;
		writeLine.Text = s.WriteRefusal is { } refusal
			? $"Read-only: {refusal}"
			: $"{(s.Dirty ? "Changed, not written yet. " : "")}Save Changes Back first copies the file as opened to {s.FileName}.<date-time>.bak beside it, then writes.";
	}

	private void Write()
	{
		if (session is not { } s)
			return;
		try
		{
			status.Text = s.Write().Sentence;
		}
		catch (Exception ex)
		{
			status.Text = $"The write stopped: {ex.Message}";
		}
		Changed();
	}
}
