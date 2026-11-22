using System;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHeX.Core;

namespace PKHaX.Hacks.Overlay;

public sealed record MoveOverlay(EntityContext Context, ReadOnlyMemory<byte> Types, ReadOnlyMemory<byte> PP, int Taken, IReadOnlyList<string> Notes)
{
    public const int PkhexFairy = 17;
    public const int PkhexLastType = 17;
    public const int MaxPp = 64;

    public bool IsEmpty => Taken == 0;

    public static MoveOverlay None(EntityContext context, params string[] notes) => new(context, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, 0, notes);

    public static MoveOverlay From(HackData data, GameVersion version)
    {
        ArgumentNullException.ThrowIfNull(data);
        return From(data, PersonalOverlay.ContextOf(version), version.Generation);
    }

    public static MoveOverlay From(HackData data, EntityContext context, byte generation)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Moves.All(m => m.Id <= 0))
            return None(context);
        var status = data.Table(HackTableStatus.Moves);
        if (data.StatusOf(HackTableStatus.Moves) != HackTableStatus.Read)
            return None(context, $"the move table is {status?.Status ?? HackTableStatus.Partial}{(status?.Reason is { Length: > 0 } reason ? $" ({reason})" : "")}, so every move keeps PKHeX's own type and PP");
        var count = MoveInfo.GetPPTable(context).Length;
        var types = new byte[count];
        Array.Fill(types, MoveOverrideLayer.NoType);
        var pp = new byte[count];
        var mysteryRows = generation is 3 or 4 ? data.Moves.Count(m => m.Id > 0 && m.Id < count && m.Pp is > 0 and <= MaxPp && m.Type == GameTypes.MysteryRaw34) : 0;
        var mysteryIsFairy = mysteryRows > 1;
        var taken = 0;
        var outside = 0;
        var unread = 0;
        var unknownType = 0;
        var fairy = 0;
        var mysteryKept = 0;
        var curseKept = false;
        foreach (var m in data.Moves)
        {
            if (m.Id <= 0)
                continue;
            if (m.Pp is <= 0 or > MaxPp)
            {
                unread++;
                continue;
            }
            if (m.Id >= count)
            {
                outside++;
                continue;
            }
            pp[m.Id] = (byte)m.Pp;
            taken++;
            if (generation is 3 or 4 && m.Type == GameTypes.MysteryRaw34)
            {
                if (mysteryIsFairy && m.Id == (int)Move.Curse)
                {
                    curseKept = true;
                }
                else if (mysteryIsFairy)
                {
                    types[m.Id] = PkhexFairy;
                    fairy++;
                }
                else
                {
                    mysteryKept++;
                }
                continue;
            }
            if (TypeOf(generation, m.Type) is { } t)
                types[m.Id] = (byte)t;
            else
                unknownType++;
        }
        var notes = new List<string> { $"{taken} moves take the hack's type and PP over PKHeX's own" };
        if (fairy > 0)
            notes.Add($"{fairy} moves use the ??? type id ({GameTypes.MysteryRaw34}), which this hack uses for Fairy; they show as Fairy");
        if (curseKept)
            notes.Add($"Curse also uses the ??? type id ({GameTypes.MysteryRaw34}), as in the base game, so it keeps PKHeX's own type next to the Fairy moves");
        if (mysteryKept > 0)
            notes.Add($"one move uses the ??? type id ({GameTypes.MysteryRaw34}), as the base game's Curse does, so it keeps PKHeX's own type");
        if (unknownType > 0)
            notes.Add($"{unknownType} moves carry a type id PKHeX has no type for, so they keep PKHeX's own type and take only the PP");
        if (outside > 0)
            notes.Add($"{outside} moves lie past PKHeX's {count - 1} moves and keep no type or PP here");
        if (unread > 0)
            notes.Add($"{unread} moves have no PP read, so they keep PKHeX's own type and PP");
        return new MoveOverlay(context, types, pp, taken, notes);
    }

    public static int? TypeOf(byte generation, int raw)
    {
        if (generation <= 2)
            return GbStructure.GbType(generation, raw);
        var pkhex = GameTypes.ToPkhex(generation, raw);
        return pkhex is >= 0 and <= PkhexLastType ? pkhex : null;
    }

    public IDisposable Use() => IsEmpty ? new Nothing() : MoveOverride.Use(Context, Types, PP);

    private sealed class Nothing : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
