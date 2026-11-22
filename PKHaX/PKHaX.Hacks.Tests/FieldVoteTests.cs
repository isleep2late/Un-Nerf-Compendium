using System;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public sealed class FieldVoteTests
{
    internal const int Moves = 400;
    internal const int Stride = 20;
    internal static readonly BitField TypeAt = new(32, 5);
    internal static readonly BitField CategoryAt = new(37, 2);
    internal static readonly BitField PowerAt = new(39, 9);
    internal static readonly BitField AccuracyAt = new(48, 7);
    internal const int FlagBit = 55;
    internal static readonly BitField PpAt = new(64, 8);
    internal const int PhysicalCode = 1;
    internal const int SpecialCode = 2;
    internal const int StatusCode = 0;

    internal static void Write(byte[] row, BitField f, int value)
    {
        for (var k = 0; k < f.Width; k++)
        {
            var b = f.Bit + k;
            if (((value >> k) & 1) != 0)
                row[b >> 3] |= (byte)(1 << (b & 7));
            else
                row[b >> 3] &= (byte)~(1 << (b & 7));
        }
    }

    internal static int PowerOf(int move) => move % 7 == 0 ? 0 : ((move % 20) + 1) * 5;

    internal static List<byte[]> PackedRows(Func<int, MoveAnchor, int>? anchorPower = null, Func<int, int, int>? type = null, bool noCategory = false)
    {
        var rows = new List<byte[]> { new byte[Stride] };
        var types = MoveInfo.GetTypeTable(EntityContext.Gen9);
        for (var m = 1; m <= Moves; m++)
        {
            var row = new byte[Stride];
            var anchor = MoveAnchors.All.FirstOrDefault(a => a.Move == m);
            var power = anchor is null ? PowerOf(m) : anchorPower?.Invoke(m, anchor) ?? anchor.Power;
            var pp = anchor?.Pp ?? (MoveInfo.GetPP(EntityContext.Gen9, (ushort)m) is var p and > 0 ? p : 10);
            var accuracy = anchor?.Accuracy ?? (power == 0 ? 0 : 100);
            var category = anchor is not null ? PhysicalCode : power == 0 ? StatusCode : m % 2 == 0 ? SpecialCode : PhysicalCode;
            Write(row, TypeAt, type?.Invoke(m, types[m]) ?? types[m] + 1);
            Write(row, CategoryAt, noCategory ? 0 : category);
            Write(row, PowerAt, power);
            Write(row, AccuracyAt, accuracy);
            row[FlagBit >> 3] |= (byte)(1 << (FlagBit & 7));
            Write(row, PpAt, pp);
            rows.Add(row);
        }
        return rows;
    }

    internal static List<int?> Identity() => [.. Enumerable.Range(0, Moves + 1).Select(i => i == 0 ? (int?)null : i)];

    [Fact]
    public void BitsReadAcrossByteBoundaries()
    {
        var row = new byte[4];
        Write(row, new BitField(7, 9), 0x1A5);
        Assert.Equal(0x1A5, FieldVote.Bits(row, 7, 9));
        Assert.Equal(0x1A5, new BitField(7, 9).Read(row));
        Assert.True(new BitField(7, 9).Overlaps(new BitField(15, 2)));
        Assert.False(new BitField(7, 9).Overlaps(new BitField(16, 2)));
    }

    [Fact]
    public void TheTenAnchorMovesKeepTheirValuesInThePokeemeraldTable()
    {
        Assert.Equal(10, MoveAnchors.All.Count);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], MoveAnchors.All.Select(a => a.Move));
        Assert.All(MoveAnchors.All, a => Assert.Equal(a.Pp, MoveInfo.GetPP(EntityContext.Gen3, (ushort)a.Move)));
        Assert.All(MoveAnchors.All, a => Assert.Equal(a.Pp, MoveInfo.GetPP(EntityContext.Gen9, (ushort)a.Move)));
    }

    [Fact]
    public void PackedMoveFieldsAreFoundFromTheAnchorsAndTheWholeTable()
    {
        var rows = PackedRows();
        var f = MoveAnchors.Find(rows.Count, i => rows[i], Identity(), out var why);
        Assert.True(f is not null, why);
        Assert.Equal(TypeAt, f!.Type!.Field);
        Assert.Equal(CategoryAt, f.Category);
        Assert.Equal(AccuracyAt, f.Accuracy);
        Assert.Equal(PpAt, f.Pp);
        var firePunch = rows[7];
        Assert.Equal(75, f.Power.Read(firePunch));
        Assert.Equal((int)MoveType.Fire, f.Type.Value(firePunch));
        Assert.Equal(HackMove.Physical, f.CategoryOf(firePunch));
        Assert.Equal(HackMove.StatusCategory, f.CategoryOf(rows[14]));
        Assert.Equal(HackMove.Special, f.CategoryOf(rows[16]));
        Assert.Equal(PowerOf(16), f.Power.Read(rows[16]));
    }

    [Fact]
    public void SixAgreeingAnchorsDoNotGiveAPowerField()
    {
        var rows = PackedRows(anchorPower: (m, a) => m <= 4 ? a.Power + 1 : a.Power);
        Assert.Null(MoveAnchors.Find(rows.Count, i => rows[i], Identity(), out var why));
        Assert.Contains("power of at least 7", why, StringComparison.Ordinal);
    }

    [Fact]
    public void AHalfScrambledTypeColumnIsRefused()
    {
        var rng = new Random(7);
        var rows = PackedRows(type: (m, t) => m > 10 && m % 2 == 0 ? rng.Next(32) : t + 1);
        var f = MoveAnchors.Find(rows.Count, i => rows[i], Identity(), out var why);
        Assert.True(f is not null, why);
        Assert.Null(f!.Type);
    }

    [Fact]
    public void ATypeFieldMovedOneBitMovesTheFoundOffset()
    {
        var types = Enumerable.Range(0, 200).Select(i => (int?)(i % 18)).ToList();
        var rows = types.Select(t => { var r = new byte[4]; Write(r, new BitField(11, 5), (t ?? 0) + 3); return r; }).ToList();
        Assert.Equal(new BitField(11, 5), FieldVote.FindMapped(rows, types, [5])!.Field);
        var moved = types.Select(t => { var r = new byte[4]; Write(r, new BitField(12, 5), (t ?? 0) + 3); return r; }).ToList();
        Assert.Equal(new BitField(12, 5), FieldVote.FindMapped(moved, types, [5])!.Field);
        Assert.Null(FieldVote.FindMapped(rows.Take(99).ToList(), types.Take(99).ToList(), [5]));
    }

    [Fact]
    public void ARowOffByOneIsSeenByTheShiftTest()
    {
        var expected = Enumerable.Range(0, 300).Select(i => (int?)((i * 7) % 18)).ToList();
        var rows = expected.Select((_, i) => { var r = new byte[2]; r[0] = (byte)expected[Math.Min(i + 1, expected.Count - 1)]!.Value; return r; }).ToList();
        var ids = Enumerable.Range(0, rows.Count).ToList();
        var vote = new FieldVoteResult(new BitField(0, 8), 0, 0);
        Assert.Equal(1, FieldVote.BestShift(rows, ids, id => id >= 0 && id < expected.Count ? expected[id] : null, vote));
        var aligned = expected.Select(e => new byte[] { (byte)e!.Value, 0 }).ToList();
        Assert.Equal(0, FieldVote.BestShift(aligned, ids, id => id >= 0 && id < expected.Count ? expected[id] : null, vote));
    }
    [Fact]
    public void TwoTypesThatShareARawValueCostOnlyTheSmallerOne()
    {
        var expected = Enumerable.Range(0, 300).Select(i => (int?)(i % 10 == 0 ? 17 : i % 6)).ToList();
        var rows = expected.Select(e => new byte[] { (byte)(e == 17 ? 1 : e!.Value + 1) }).ToList();
        var vote = FieldVote.FindMapped(rows, expected, [8]);
        Assert.NotNull(vote);
        Assert.Equal(new BitField(0, 8), vote!.Field);
        Assert.Equal(expected.Count(e => e != 17), vote.Agree);
        Assert.Null(vote.Value(new byte[] { 0 }));
        Assert.Equal(3, vote.Value(new byte[] { 4 }));
        var constant = expected.Select(_ => new byte[] { 9 }).ToList();
        Assert.Null(FieldVote.TopMapped(constant, expected, [8]));
    }

    [Fact]
    public void WithoutACategoryFieldTheTypeRuleGivesTheCategory()
    {
        var rows = PackedRows(noCategory: true);
        var f = MoveAnchors.Find(rows.Count, i => rows[i], Identity(), out var why);
        Assert.True(f is not null, why);
        Assert.Null(f!.Category);
        var image = new GbaImage(rows.SelectMany(r => r).ToArray());
        var names = Enumerable.Range(0, rows.Count).Select(i => GameInfo.GetStrings("en").movelist[i]).ToList();
        var notes = new List<string>();
        var moves = GbaHackData.MovesFrom(image, 0, Stride, names, Identity(), f, true, notes);
        Assert.Equal(((int?)HackMove.Special, true), (moves[7].Category, moves[7].CategoryByType));
        Assert.Equal(((int?)HackMove.Physical, true), (moves[1].Category, moves[1].CategoryByType));
        Assert.Equal(((int?)HackMove.StatusCategory, true), (moves[14].Category, moves[14].CategoryByType));
        Assert.Contains(notes, n => n.Contains("Gen 1-3 type rule", StringComparison.Ordinal));
        var split = GbaHackData.MovesFrom(image, 0, Stride, names, Identity(), f, false, []);
        Assert.Equal(((int?)null, false), (split[7].Category, split[7].CategoryByType));
        var withField = PackedRows();
        var fields = MoveAnchors.Find(withField.Count, i => withField[i], Identity(), out _)!;
        var read = GbaHackData.MovesFrom(new GbaImage(withField.SelectMany(r => r).ToArray()), 0, Stride, names, Identity(), fields, true, []);
        Assert.Equal(((int?)HackMove.Physical, false), (read[7].Category, read[7].CategoryByType));
    }
}
