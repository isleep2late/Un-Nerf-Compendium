using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Isleep2late.Files.Services.Patch;

internal static class VcdiffPatch
{
	private static ReadOnlySpan<byte> Magic => [0xD6, 0xC3, 0xC4, 0x00];

	private const byte HdrDecompress = 0x01;
	private const byte HdrCodeTable = 0x02;
	private const byte HdrAppHeader = 0x04;

	private const byte WinSource = 0x01;
	private const byte WinTarget = 0x02;
	private const byte WinAdler32 = 0x04;

	private const byte DataCompressed = 0x01;
	private const byte InstCompressed = 0x02;
	private const byte AddrCompressed = 0x04;

	private const byte Noop = 0;
	private const byte Add = 1;
	private const byte Run = 2;
	private const byte Copy = 3;

	private const int NearSlots = 4;
	private const int SameSlots = 3;

	private const long MaxWindow = 256L << 20;

	internal const long MaxSecondaryWindow = 64L << 20;

	internal static bool ProofSourceSegmentCap { get; set; }

	internal static bool ProofNoSecondaryWindowCap { get; set; }

	internal static bool ProofAllowNoWindows { get; set; }

	internal static bool ProofTrustFullLastWindow { get; set; }

	internal static bool ProofIgnoreDeclaredSize { get; set; }

	internal static bool ProofHideUnchecked { get; set; }

	internal static bool ProofIgnoreFillerTail { get; set; }

	internal static bool ProofMixedSaysN { get; set; }

	internal static bool ProofNes2BySignature { get; set; }

	internal const long XdeltaDefaultWindow = 1L << 23;

	private const int HeadBytes = 0x200;

	private const int FirstWindowCapacity = 1 << 20;

	private readonly record struct CodeEntry(byte Inst1, byte Size1, byte Mode1, byte Inst2, byte Size2, byte Mode2);

	private static readonly CodeEntry[] DefaultTable = BuildDefaultTable();

	private static CodeEntry[] BuildDefaultTable()
	{
		var table = new CodeEntry[256];
		var i = 0;
		table[i++] = new CodeEntry(Run, 0, 0, Noop, 0, 0);
		for (var size = 0; size <= 17; size++)
			table[i++] = new CodeEntry(Add, (byte)size, 0, Noop, 0, 0);
		for (var mode = 0; mode <= 8; mode++)
		{
			table[i++] = new CodeEntry(Copy, 0, (byte)mode, Noop, 0, 0);
			for (var size = 4; size <= 18; size++)
				table[i++] = new CodeEntry(Copy, (byte)size, (byte)mode, Noop, 0, 0);
		}
		for (var mode = 0; mode <= 5; mode++)
			for (var addSize = 1; addSize <= 4; addSize++)
				for (var copySize = 4; copySize <= 6; copySize++)
					table[i++] = new CodeEntry(Add, (byte)addSize, 0, Copy, (byte)copySize, (byte)mode);
		for (var mode = 6; mode <= 8; mode++)
			for (var addSize = 1; addSize <= 4; addSize++)
				table[i++] = new CodeEntry(Add, (byte)addSize, 0, Copy, 4, (byte)mode);
		for (var mode = 0; mode <= 8; mode++)
			table[i++] = new CodeEntry(Copy, 4, (byte)mode, Add, 1, 0);

		if (i != 256
			|| table[0] != new CodeEntry(Run, 0, 0, Noop, 0, 0)
			|| table[19] != new CodeEntry(Copy, 0, 0, Noop, 0, 0)
			|| table[163] != new CodeEntry(Add, 1, 0, Copy, 4, 0)
			|| table[255] != new CodeEntry(Copy, 4, 8, Add, 1, 0))
			throw new InvalidOperationException("the VCDIFF default code table did not come out as RFC 3284 section 5.4 lays it out");
		return table;
	}

	private sealed class Header
	{
		public int? SecondaryId { get; init; }
		public bool CustomCodeTable { get; init; }
		public string? AppHeader { get; init; }
		public int WindowsStart { get; init; }
	}

	private static Header ReadHeader(ReadOnlySpan<byte> patch)
	{
		var reader = new PatchReader(patch);
		if (!reader.StartsWith(Magic))
			throw PatchReader.Corrupt("not a VCDIFF patch: the file does not start with D6 C3 C4 00");
		reader.Position = Magic.Length;
		var indicator = reader.ReadByte();
		if ((indicator & ~(HdrDecompress | HdrCodeTable | HdrAppHeader)) != 0)
			throw PatchReader.Corrupt($"the VCDIFF header indicator 0x{indicator:X2} has bits no version of the format defines; the file is corrupt");
		int? secondary = null;
		if ((indicator & HdrDecompress) != 0)
			secondary = reader.ReadByte();
		var custom = (indicator & HdrCodeTable) != 0;
		if (custom)
		{
			var length = reader.ReadVcdiffLength("custom code table length");
			reader.ReadBytes(length);
		}
		string? appHeader = null;
		if ((indicator & HdrAppHeader) != 0)
		{
			var length = reader.ReadVcdiffLength("application header length");
			appHeader = Encoding.UTF8.GetString(reader.ReadBytes(length));
		}
		return new Header { SecondaryId = secondary, CustomCodeTable = custom, AppHeader = appHeader, WindowsStart = reader.Position };
	}

	internal readonly record struct Armor(string? SourceBlake3, string? TargetBlake3)
	{
		public bool Present => SourceBlake3 is not null || TargetBlake3 is not null;
	}

	internal static bool ProofSkipArmor { get; set; }

	internal static Armor ReadArmor(string? appHeader)
	{
		if (string.IsNullOrEmpty(appHeader))
			return default;
		var fields = new List<string>(4);
		var start = 0;
		while (fields.Count < 3)
		{
			var slash = appHeader.IndexOf('/', start);
			if (slash < 0)
				break;
			fields.Add(appHeader[start..slash]);
			start = slash + 1;
		}
		fields.Add(appHeader[start..]);
		var target = fields.Count is 2 or 4 ? DigestOf(fields[0]) : null;
		var source = fields.Count == 4 ? DigestOf(fields[2]) : null;
		return new Armor(source, target);
	}

	private static string? DigestOf(string field)
	{
		var hash = field.LastIndexOf('#');
		if (hash < 0)
			return null;
		var hex = field[(hash + 1)..];
		if (hex.Length != Blake3.OutLength * 2)
			return null;
		foreach (var c in hex)
			if (c is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
				return null;
		return hex;
	}

	private static PatchException? Unsupported(Header h)
	{
		if (h.CustomCodeTable)
			return new PatchException(PatchFailure.Unsupported,
				"this VCDIFF patch carries its own instruction code table (header bit 0x02), which this decoder does not implement and xdelta3 no longer reads either; the patch must be regenerated with xdelta3's default settings or converted to BPS");
		if (h.SecondaryId is { } id && !SecondaryCompression.IsKnown(id))
			return new PatchException(PatchFailure.Unsupported,
				$"this xdelta patch names secondary compressor id {id}, which no xdelta3 release writes (DJW is 1, LZMA 2, FGK 16); the file is damaged or came from a tool this app does not know, and it must be regenerated with xdelta3 or converted to BPS");
		return null;
	}

	public static PatchInfo Inspect(ReadOnlySpan<byte> patch)
	{
		var h = ReadHeader(patch);
		var armor = ReadArmor(h.AppHeader);
		var warnings = new List<string>();
		var refusal = Unsupported(h);
		if (refusal is not null)
			warnings.Add(refusal.Message + ".");

		int? windows = null;
		long? targetSize = null;
		if (refusal is null)
		{
			var reader = new PatchReader(patch, h.WindowsStart);
			var count = 0;
			long total = 0;
			var withAdler = 0;
			while (reader.Remaining > 0)
			{
				var w = ReadWindowHeader(ref reader, patch.Length, h.SecondaryId is not null);
				reader.Position = w.SectionsEnd;
				count++;
				total += w.TargetLength;
				if (w.Adler32 is not null)
					withAdler++;
			}
			windows = count;
			targetSize = total;
			if (count == 0 && !ProofAllowNoWindows)
				throw NoWindows();
			if (count == 0)
				warnings.Add("the patch has no windows at all: applying it writes an empty file.");
			else if (withAdler < count && armor.TargetBlake3 is null)
				warnings.Add($"{count - withAdler} of {count} window(s) carry no Adler-32 (xdelta3 -n): nothing in the file can tell whether the source ROM is the right one.");
		}
		return new PatchInfo(PatchFormat.Vcdiff, SourceSize: null, targetSize, SourceCrc32: null, TargetCrc32: null, PatchCrc32: null,
			h.AppHeader, windows, h.SecondaryId, MaxWriteEnd: null, warnings)
		{
			CustomCodeTable = h.CustomCodeTable,
			SourceBlake3 = armor.SourceBlake3,
			TargetBlake3 = armor.TargetBlake3,
		};
	}

	private readonly record struct WindowHeader(
		byte Indicator, long SegmentLength, long SegmentPosition, long TargetLength, byte DeltaIndicator, uint? Adler32,
		int DataStart, int DataLength, int InstStart, int InstLength, int AddrStart, int AddrLength, int SectionsEnd);

	private static WindowHeader ReadWindowHeader(ref PatchReader reader, int patchLength, bool secondary)
	{
		var at = reader.Position;
		var indicator = reader.ReadByte();
		if ((indicator & ~(WinSource | WinTarget | WinAdler32)) != 0)
			throw PatchReader.Corrupt($"the window at patch offset 0x{at:X} has indicator 0x{indicator:X2} with undefined bits; the file is corrupt");
		if ((indicator & WinSource) != 0 && (indicator & WinTarget) != 0)
			throw PatchReader.Corrupt($"the window at patch offset 0x{at:X} claims both VCD_SOURCE and VCD_TARGET; the file is corrupt");
		long segmentLength = 0, segmentPosition = 0;
		if ((indicator & (WinSource | WinTarget)) != 0)
		{
			segmentLength = (indicator & WinSource) != 0 && !ProofSourceSegmentCap
				? reader.ReadVcdiffInt()
				: reader.ReadVcdiffLength("source segment length", MaxWindow);
			segmentPosition = reader.ReadVcdiffInt();
		}
		var deltaLength = reader.ReadVcdiffLength("delta encoding length");
		var deltaStart = reader.Position;
		var targetLength = reader.ReadVcdiffLength("target window length", MaxWindow);
		var deltaIndicator = reader.ReadByte();
		if ((deltaIndicator & ~(DataCompressed | InstCompressed | AddrCompressed)) != 0)
			throw PatchReader.Corrupt($"the window at patch offset 0x{at:X} has delta_indicator 0x{deltaIndicator:X2} with undefined bits; the file is corrupt");
		if (deltaIndicator != 0 && !secondary)
			throw PatchReader.Corrupt($"the window at patch offset 0x{at:X} marks its sections compressed (delta_indicator 0x{deltaIndicator:X2}) but the header names no secondary compressor; the file is corrupt");
		if (deltaIndicator != 0 && targetLength > MaxSecondaryWindow && !ProofNoSecondaryWindowCap)
			throw PatchReader.Corrupt($"the window at patch offset 0x{at:X} claims {targetLength:N0} bytes with secondary-compressed sections, more than the {MaxSecondaryWindow:N0} any xdelta3 release writes or reads; the file is corrupt");
		var dataLength = reader.ReadVcdiffLength("data section length");
		var instLength = reader.ReadVcdiffLength("instruction section length");
		var addrLength = reader.ReadVcdiffLength("address section length");
		uint? adler = null;
		if ((indicator & WinAdler32) != 0)
			adler = reader.ReadU32BE();
		var dataStart = reader.Position;
		var instStart = (long)dataStart + dataLength;
		var addrStart = instStart + instLength;
		var end = addrStart + addrLength;
		if (end > patchLength)
			throw PatchReader.Corrupt($"the window at patch offset 0x{at:X} runs {end - patchLength:N0} byte(s) past the end of the file; the file is truncated");
		if (end - deltaStart != deltaLength)
			throw PatchReader.Corrupt($"the window at patch offset 0x{at:X} says its delta encoding is {deltaLength:N0} bytes but its sections add up to {end - deltaStart:N0}; the file is corrupt");
		return new WindowHeader(indicator, segmentLength, segmentPosition, targetLength, deltaIndicator, adler,
			dataStart, dataLength, (int)instStart, instLength, (int)addrStart, addrLength, (int)end);
	}

	private sealed class Secondary : IDisposable
	{
		public readonly string Name;
		public readonly ISecondaryDecoder[] Decoders;
		public readonly SectionBuffer[] Buffers = [new(), new(), new()];

		public Secondary(int id)
		{
			Name = SecondaryCompression.Name(id);
			Decoders = [SecondaryCompression.Create(id), SecondaryCompression.Create(id), SecondaryCompression.Create(id)];
		}

		public long HeldBytes
		{
			get
			{
				long held = 0;
				foreach (var b in Buffers)
					held += b.Capacity;
				foreach (var d in Decoders)
					if (d is LzmaSecondaryDecoder lzma)
						held += lzma.HeldBytes;
				return held;
			}
		}

		public void Dispose()
		{
			foreach (var b in Buffers)
				b.Dispose();
		}
	}

	private static readonly string[] SectionNames = ["data", "instruction", "address"];

	public static PatchResult Apply(ReadOnlySpan<byte> patch, Stream sourceStream, Stream targetStream, long ceiling, IProgress<double>? progress, CancellationToken ct)
	{
		var h = ReadHeader(patch);
		if (Unsupported(h) is { } refusal)
			throw refusal;
		var armor = ProofSkipArmor ? default : ReadArmor(h.AppHeader);
		bool? sourceOk = null;
		if (armor.SourceBlake3 is { } wantSource)
		{
			if (!sourceStream.CanSeek || !sourceStream.CanRead)
				throw new ArgumentException("the source ROM stream must be seekable and readable", nameof(sourceStream));
			sourceStream.Position = 0;
			var haveSource = Blake3.Hex(Blake3.Hash(sourceStream, ct));
			sourceStream.Position = 0;
			if (haveSource != wantSource)
			{
				if (haveSource == armor.TargetBlake3)
					throw new PatchException(PatchFailure.AlreadyPatched,
						$"the ROM already has the BLAKE3 {Short(haveSource)} that the patch's armor records for its result");
				throw new PatchException(PatchFailure.WrongSource,
					$"wrong source ROM: its BLAKE3 is {Short(haveSource)} and the patch's armor (xdelta3 3.2) records {Short(wantSource)} for the ROM it was made from");
			}
			sourceOk = true;
		}
		var targetHash = armor.TargetBlake3 is not null ? new Blake3() : null;

		using var source = new SourceReader(sourceStream);
		using var target = new TargetWriter(targetStream, ceiling);
		using var secondary = h.SecondaryId is { } id ? new Secondary(id) : null;
		var reader = new PatchReader(patch, h.WindowsStart);
		using var pagesHolder = new PagesHolder();
		SourcePages? pages = null;
		byte[]? segment = null;
		byte[]? window = null;
		var near = new long[NearSlots];
		var same = new long[SameSlots * 256];
		var windows = 0;
		var verified = 0;
		long uncheckedBytes = 0;
		long peak = 0;
		long firstLength = -1;
		var equalWindows = true;
		var readsSource = false;
		byte[]? head = null;
		var headLength = 0;
		try
		{
			while (reader.Remaining > 0)
			{
				ct.ThrowIfCancellationRequested();
				progress?.Report((double)reader.Position / patch.Length);
				var at = reader.Position;
				var w = ReadWindowHeader(ref reader, patch.Length, secondary is not null);
				reader.Position = w.SectionsEnd;

				if (w.TargetLength > ceiling - target.Length)
					throw RomPatch.OverCeiling($"window {windows} at patch offset 0x{at:X} would bring the output to", target.Length + w.TargetLength, ceiling, source.Length);

				var segmentEnd = (ulong)w.SegmentPosition + (ulong)w.SegmentLength;
				if ((w.Indicator & WinSource) != 0)
				{
					if (w.SegmentLength > source.Length - w.SegmentPosition)
						throw new PatchException(PatchFailure.WrongSource,
							$"wrong source ROM: window {windows} needs source bytes up to 0x{segmentEnd:X} and this file is only {source.Length:N0} bytes long");
					pages ??= pagesHolder.Pages = new SourcePages(sourceStream, source.Length);
				}
				else if ((w.Indicator & WinTarget) != 0)
				{
					if (w.SegmentLength > target.Length - w.SegmentPosition)
						throw PatchReader.Corrupt($"window {windows} at patch offset 0x{at:X} copies from output bytes up to 0x{segmentEnd:X} that have not been written yet; the file is corrupt");
					Ensure(ref segment, (int)w.SegmentLength);
					target.ReadBack(w.SegmentPosition, segment.AsSpan(0, (int)w.SegmentLength));
				}

				var data = patch.Slice(w.DataStart, w.DataLength);
				var inst = patch.Slice(w.InstStart, w.InstLength);
				var addr = patch.Slice(w.AddrStart, w.AddrLength);
				if (secondary is not null)
				{
					data = Decompress(secondary, 0, (w.DeltaIndicator & DataCompressed) != 0, data, w.TargetLength, windows);
					inst = Decompress(secondary, 1, (w.DeltaIndicator & InstCompressed) != 0, inst, w.TargetLength, windows);
					addr = Decompress(secondary, 2, (w.DeltaIndicator & AddrCompressed) != 0, addr, w.TargetLength, windows);
				}

				var targetLength = (int)w.TargetLength;
				var seg = (w.Indicator & WinSource) != 0
					? new Segment(pages, w.SegmentPosition, w.SegmentLength)
					: new Segment(segment is null || w.SegmentLength == 0 ? ReadOnlySpan<byte>.Empty : segment.AsSpan(0, (int)w.SegmentLength));
				DecodeWindow(data, inst, addr, seg, ref window, targetLength, near, same, windows);
				peak = Math.Max(peak, (pages?.HeldBytes ?? 0) + (segment?.Length ?? 0) + (window?.Length ?? 0) + (secondary?.HeldBytes ?? 0));

				var decoded = window is null ? Span<byte>.Empty : window.AsSpan(0, targetLength);
				if (w.Adler32 is { } expected)
				{
					var actual = Checksums.Adler32(decoded);
					if (actual != expected)
						throw new PatchException(PatchFailure.WrongSource,
							$"wrong source ROM or corrupt patch: window {windows} decoded to Adler-32 {actual:X8} where the patch expects {expected:X8}, so the ROM is not the one this patch was made from (or the patch is damaged)");
					verified++;
				}
				else
					uncheckedBytes += decoded.Length;
				target.Write(decoded);
				targetHash?.Update(decoded);
				if (headLength < HeadBytes && decoded.Length > 0)
				{
					head ??= new byte[HeadBytes];
					var take = Math.Min(HeadBytes - headLength, decoded.Length);
					decoded[..take].CopyTo(head.AsSpan(headLength));
					headLength += take;
				}
				if (firstLength < 0)
					firstLength = w.TargetLength;
				else if (w.TargetLength != firstLength)
					equalWindows = false;
				readsSource |= (w.Indicator & WinSource) != 0;
				windows++;
			}
		}
		finally
		{
			if (segment is not null) ArrayPool<byte>.Shared.Return(segment);
			if (window is not null) ArrayPool<byte>.Shared.Return(window);
		}
		if (windows == 0 && !ProofAllowNoWindows)
			throw NoWindows();
		bool? targetOk = null;
		string? mayBeCut = null;
		if (targetHash is not null)
		{
			var haveTarget = Blake3.Hex(targetHash.Finish());
			if (haveTarget != armor.TargetBlake3)
				throw PatchReader.Corrupt(
					$"the patched ROM ({target.Length:N0} bytes from {windows} window(s)) has the BLAKE3 {Short(haveTarget)} and the patch's armor (xdelta3 3.2) records {Short(armor.TargetBlake3!)}: the patch is damaged or was cut off");
			targetOk = true;
		}
		else
		{
			var declared = ProofIgnoreDeclaredSize || head is null ? null : DeclaredImageSize(head.AsSpan(0, headLength));
			if (declared is { } size && target.Length < size.Bytes)
				throw PatchReader.Corrupt(
					$"the ROM this patch built says in its own {size.Header} that it is {size.Bytes:N0} bytes, and the patch's {windows} window(s) wrote only {target.Length:N0}: the file stops at a window boundary before its end, so it was cut off");
			if (declared is null && !ProofTrustFullLastWindow && readsSource && target.Length < source.Length && EndsOnFullWindow(windows, firstLength, equalWindows))
			{
				var full = $"{windows} full window(s) of {firstLength:N0} bytes{(windows == 1 ? " (xdelta3's default window size)" : "")}";
				if (ProofIgnoreFillerTail || FillerAfter(source, target.Length, ct) is not { } fill)
					throw new PatchException(PatchFailure.Corrupt,
						$"the patch ends after {full}, and the {target.Length:N0} bytes it built are fewer than this {source.Length:N0}-byte ROM holds, with more than padding after them. xdelta3 makes every window but the last one the same size, so a file cut off between two windows looks like this; so does a complete patch made for a smaller ROM than this one, or one that really builds a ROM this much smaller, and without xdelta3 3.2's armor the file cannot tell which") { CauseUnknown = true };
				mayBeCut = $"it may be cut off, since it ends after {full} as a file cut off between two windows would, and it builds {target.Length:N0} bytes from this {source.Length:N0}-byte ROM; the rest of the ROM is only padding (0x{fill:X2} bytes), so a complete patch made for the ROM without that padding looks the same";
			}
		}
		target.Complete();
		progress?.Report(1);
		string? notChecked = null;
		if (targetHash is null && verified < windows)
			notChecked = verified == 0 || ProofMixedSaysN
				? $"{windows - verified} of {windows} window(s) carry no checksum (an xdelta made with xdelta3 -n and without xdelta3 3.2's armor), so nothing checked {(sourceOk == true ? "" : "that this is the ROM the patch was made for or ")}that the patch file is undamaged"
				: $"{windows - verified} of {windows} window(s) carry no checksum (the other {verified} passed theirs), so the {uncheckedBytes:N0} bytes those window(s) wrote were not checked: a damaged patch file{(sourceOk == true ? "" : ", or a ROM that differs there from the one the patch was made for,")} would go unnoticed there";
		var named = ProofHideUnchecked ? null : notChecked is not null && mayBeCut is not null ? $"{notChecked}; and {mayBeCut}" : notChecked ?? mayBeCut;
		return new PatchResult(target.Length, sourceOk, targetOk, null, verified) { PeakBufferBytes = peak, Unchecked = named };
	}

	private static bool EndsOnFullWindow(int windows, long firstLength, bool equalWindows) =>
		windows >= 2 ? equalWindows : windows == 1 && firstLength == XdeltaDefaultWindow;

	private static byte? FillerAfter(SourceReader source, long from, CancellationToken ct)
	{
		if (from >= source.Length)
			return null;
		var buffer = ArrayPool<byte>.Shared.Rent(1 << 16);
		try
		{
			byte? fill = null;
			for (var at = from; at < source.Length;)
			{
				ct.ThrowIfCancellationRequested();
				var span = buffer.AsSpan(0, (int)Math.Min(1 << 16, source.Length - at));
				source.ReadAt(at, span);
				fill ??= span[0];
				if (span.IndexOfAnyExcept(fill.Value) >= 0)
					return null;
				at += span.Length;
			}
			return fill;
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	internal readonly record struct DeclaredSize(long Bytes, string Header);

	private static ReadOnlySpan<byte> GameBoyLogo =>
	[
		0xCE, 0xED, 0x66, 0x66, 0xCC, 0x0D, 0x00, 0x0B, 0x03, 0x73, 0x00, 0x83, 0x00, 0x0C, 0x00, 0x0D,
		0x00, 0x08, 0x11, 0x1F, 0x88, 0x89, 0x00, 0x0E, 0xDC, 0xCC, 0x6E, 0xE6, 0xDD, 0xDD, 0xD9, 0x99,
		0xBB, 0xBB, 0x67, 0x63, 0x6E, 0x0E, 0xEC, 0xCC, 0xDD, 0xDC, 0x99, 0x9F, 0xBB, 0xB9, 0x33, 0x3E,
	];

	private const ushort NdsLogoCrc = 0xCF56;

	internal static DeclaredSize? DeclaredImageSize(ReadOnlySpan<byte> head)
	{
		if (head.Length >= 16 && head[0] == 0x4E && head[1] == 0x45 && head[2] == 0x53 && head[3] == 0x1A)
			return InesSize(head);
		if (head.Length >= 0x150 && head.Slice(0x104, GameBoyLogo.Length).SequenceEqual(GameBoyLogo) && GameBoyHeaderSum(head) == head[0x14D] && head[0x148] <= 8)
			return new DeclaredSize(0x8000L << head[0x148], $"Game Boy header (ROM size byte 0x{head[0x148]:X2} at 0x148)");
		if (head.Length >= 0x160 && BinaryPrimitives.ReadUInt16LittleEndian(head[0x15C..]) == NdsLogoCrc
			&& Crc16(head[..0x15E]) == BinaryPrimitives.ReadUInt16LittleEndian(head[0x15E..]))
		{
			var used = BinaryPrimitives.ReadUInt32LittleEndian(head[0x80..]);
			return used >= HeadBytes ? new DeclaredSize(used, $"DS header (used ROM size 0x{used:X} at 0x80)") : null;
		}
		return null;
	}

	private static DeclaredSize? InesSize(ReadOnlySpan<byte> head)
	{
		var nes2 = (head[7] & 0x0C) == 0x08 && (ProofNes2BySignature || Nes2UnusedBitsClear(head));
		var prg = nes2 ? Nes2Size(head[4], head[9] & 0x0F, 16384) : head[4] * 16384L;
		var chr = nes2 ? Nes2Size(head[5], head[9] >> 4, 8192) : head[5] * 8192L;
		if (prg <= 0 || chr < 0)
			return null;
		var trainer = (head[6] & 0x04) != 0 ? 512 : 0;
		return new DeclaredSize(16 + trainer + prg + chr,
			$"{(nes2 ? "NES 2.0" : "iNES")} header ({prg / 1024:N0} KiB of PRG, {chr / 1024:N0} KiB of CHR{(trainer > 0 ? ", a 512-byte trainer" : "")})");
	}

	private static bool Nes2UnusedBitsClear(ReadOnlySpan<byte> head) =>
		(head[12] & 0xFC) == 0 && (head[14] & 0xFC) == 0 && (head[15] & 0xC0) == 0;

	private static long Nes2Size(byte low, int high, long unit)
	{
		if (high != 0x0F)
			return ((long)high << 8 | low) * unit;
		var exponent = low >> 2;
		return exponent > 32 ? -1 : (1L << exponent) * ((low & 3) * 2 + 1);
	}

	private static byte GameBoyHeaderSum(ReadOnlySpan<byte> head)
	{
		byte sum = 0;
		for (var i = 0x134; i <= 0x14C; i++)
			sum = (byte)(sum - head[i] - 1);
		return sum;
	}

	private static ushort Crc16(ReadOnlySpan<byte> data)
	{
		ushort crc = 0xFFFF;
		foreach (var b in data)
		{
			crc ^= b;
			for (var k = 0; k < 8; k++)
				crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
		}
		return crc;
	}

	private static string Short(string hex) => hex[..16] + "...";

	private static PatchException NoWindows() =>
		PatchReader.Corrupt("the patch ends after its header without a single window (xdelta3 writes one even for an empty file, and its own decoder refuses this as 'nothing to output'); the file is cut off");

	private static ReadOnlySpan<byte> Decompress(Secondary secondary, int kind, bool compressed, ReadOnlySpan<byte> raw, long targetLength, int windowIndex)
	{
		var decoder = secondary.Decoders[kind];
		if (!compressed)
		{
			if (raw.Length >= SecondaryCompression.MinInput)
				decoder.PassOver(raw);
			return raw;
		}
		var where = $"the {SectionNames[kind]} section of window {windowIndex} ({secondary.Name})";
		var reader = new PatchReader(raw);
		long declared;
		try
		{
			declared = reader.ReadVcdiffInt();
		}
		catch (PatchException)
		{
			throw PatchReader.Corrupt($"{where} ends inside its decompressed-size prefix; the patch is truncated or corrupt");
		}
		var limit = kind switch
		{
			0 => targetLength,
			1 => 2 * targetLength + 64,
			_ => 5 * targetLength + 64,
		};
		if (declared == 0)
			throw PatchReader.Corrupt($"{where} declares a decompressed size of 0, which xdelta3 never writes; the patch is corrupt");
		if (declared > limit)
			throw PatchReader.Corrupt($"{where} declares {declared:N0} decompressed bytes, more than the {limit:N0} a {targetLength:N0}-byte window can use; the patch is corrupt");
		var size = (int)declared;
		var buffer = secondary.Buffers[kind];
		buffer.Start(size);
		try
		{
			decoder.Decode(raw[reader.Position..], buffer, size, where);
		}
		catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException or InvalidOperationException or OverflowException)
		{
			throw PatchReader.Corrupt($"{where} could not be decoded ({e.GetType().Name}); the patch is corrupt");
		}
		if (buffer.Length != size)
			throw PatchReader.Corrupt($"{where} decoded to {buffer.Length:N0} bytes where it declared {size:N0}; the patch is corrupt");
		return buffer.Span;
	}

	private static void Ensure(ref byte[]? buffer, int length)
	{
		if (buffer is not null && buffer.Length >= length)
			return;
		if (buffer is not null)
			ArrayPool<byte>.Shared.Return(buffer);
		buffer = ArrayPool<byte>.Shared.Rent(Math.Max(length, 1));
	}

	private static void Grow(ref byte[]? window, int needed, int keep, int windowLength)
	{
		if (window is not null && window.Length >= needed)
			return;
		var size = (int)Math.Min(windowLength, Math.Max(needed, Math.Max(FirstWindowCapacity, (long)(window?.Length ?? 0) * 2)));
		var next = ArrayPool<byte>.Shared.Rent(Math.Max(size, 1));
		if (window is not null)
		{
			window.AsSpan(0, keep).CopyTo(next);
			ArrayPool<byte>.Shared.Return(window);
		}
		window = next;
	}

	private static void DecodeWindow(ReadOnlySpan<byte> dataSection, ReadOnlySpan<byte> instSection, ReadOnlySpan<byte> addrSection,
		Segment segment, ref byte[]? window, int targetLength, long[] near, long[] same, int windowIndex)
	{
		var data = new PatchReader(dataSection);
		var inst = new PatchReader(instSection);
		var addr = new PatchReader(addrSection);
		Array.Clear(near);
		Array.Clear(same);
		var nextNear = 0;
		var produced = 0;
		Grow(ref window, Math.Min(targetLength, FirstWindowCapacity), 0, targetLength);

		while (inst.Remaining > 0)
		{
			var entry = DefaultTable[inst.ReadByte()];
			Execute(entry.Inst1, entry.Size1, entry.Mode1, ref data, ref inst, ref addr, segment, ref window, targetLength, ref produced, near, same, ref nextNear, windowIndex);
			if (entry.Inst2 != Noop)
				Execute(entry.Inst2, entry.Size2, entry.Mode2, ref data, ref inst, ref addr, segment, ref window, targetLength, ref produced, near, same, ref nextNear, windowIndex);
		}

		if (produced != targetLength)
			throw PatchReader.Corrupt($"window {windowIndex} decoded to {produced:N0} bytes where it promised {targetLength:N0}; the patch is corrupt");
		if (data.Remaining != 0 || addr.Remaining != 0)
			throw PatchReader.Corrupt($"window {windowIndex} left {data.Remaining} data byte(s) and {addr.Remaining} address byte(s) unused; the patch is corrupt");
	}

	private static void Execute(byte instruction, byte fixedSize, byte mode, ref PatchReader data, ref PatchReader inst, ref PatchReader addr,
		Segment segment, ref byte[]? window, int targetLength, ref int produced, long[] near, long[] same, ref int nextNear, int windowIndex)
	{
		var size = fixedSize != 0 ? fixedSize : inst.ReadVcdiffLength("instruction size");
		if (size > targetLength - produced)
			throw PatchReader.Corrupt($"window {windowIndex}: an instruction writes {size:N0} bytes past the window's end; the patch is corrupt");
		Grow(ref window, produced + size, produced, targetLength);
		var output = window.AsSpan();

		switch (instruction)
		{
			case Add:
				data.ReadBytes(size).CopyTo(output.Slice(produced, size));
				produced += size;
				break;
			case Run:
				output.Slice(produced, size).Fill(data.ReadByte());
				produced += size;
				break;
			case Copy:
			{
				var here = (long)segment.Length + produced;
				var address = DecodeAddress(here, mode, ref addr, near, same, ref nextNear, windowIndex);
				if (address < 0 || address >= here)
					throw PatchReader.Corrupt($"window {windowIndex}: a COPY addresses 0x{address:X}, outside the {here:N0} bytes addressable at that point; the patch is corrupt");
				CopyBytes(segment, output, ref produced, address, size);
				break;
			}
			default:
				throw PatchReader.Corrupt($"window {windowIndex}: code table entry with instruction {instruction}; the patch is corrupt");
		}
	}

	private static void CopyBytes(Segment segment, Span<byte> output, ref int produced, long address, int size)
	{
		if (address < segment.Length)
		{
			var fromSegment = (int)Math.Min(size, segment.Length - address);
			segment.CopyTo(address, output.Slice(produced, fromSegment));
			produced += fromSegment;
			address += fromSegment;
			size -= fromSegment;
			if (size == 0)
				return;
		}
		var from = (int)(address - segment.Length);
		if (from + size <= produced)
		{
			output.Slice(from, size).CopyTo(output.Slice(produced, size));
			produced += size;
			return;
		}
		for (var i = 0; i < size; i++)
			output[produced++] = output[from + i];
	}

	private readonly ref struct Segment
	{
		private readonly ReadOnlySpan<byte> bytes;
		private readonly SourcePages? pages;
		private readonly long position;

		public long Length { get; }

		public Segment(SourcePages? pages, long position, long length)
		{
			this.pages = pages;
			this.position = position;
			Length = length;
		}

		public Segment(ReadOnlySpan<byte> bytes)
		{
			this.bytes = bytes;
			Length = bytes.Length;
		}

		public void CopyTo(long address, Span<byte> into)
		{
			if (pages is not null)
				pages.CopyTo(position + address, into);
			else
				bytes.Slice((int)address, into.Length).CopyTo(into);
		}
	}

	private sealed class PagesHolder : IDisposable
	{
		public SourcePages? Pages { get; set; }

		public void Dispose() => Pages?.Dispose();
	}

	private static long DecodeAddress(long here, byte mode, ref PatchReader addr, long[] near, long[] same, ref int nextNear, int windowIndex)
	{
		long address;
		if (mode == 0)
			address = addr.ReadVcdiffInt();
		else if (mode == 1)
			address = here - addr.ReadVcdiffInt();
		else if (mode < 2 + NearSlots)
			address = near[mode - 2] + addr.ReadVcdiffInt();
		else if (mode < 2 + NearSlots + SameSlots)
			address = same[(mode - 2 - NearSlots) * 256 + addr.ReadByte()];
		else
			throw PatchReader.Corrupt($"window {windowIndex}: COPY address mode {mode} does not exist in the default cache; the patch is corrupt");

		near[nextNear] = address;
		nextNear = (nextNear + 1) % NearSlots;
		if (address >= 0)
			same[(int)(address % (SameSlots * 256))] = address;
		return address;
	}
}
