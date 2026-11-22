using System;
using System.Collections.Generic;

namespace PKHaX.Hacks.Records;

public enum RecordShape
{
    Party,
    Stored,
}

public readonly record struct StatSet(int Hp, int Atk, int Def, int SpA, int SpD, int Spe)
{
    public int this[int index] => index switch
    {
        0 => Hp,
        1 => Atk,
        2 => Def,
        3 => SpA,
        4 => SpD,
        5 => Spe,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    public override string ToString() => $"{Hp}/{Atk}/{Def}/{SpA}/{SpD}/{Spe}";
}

public sealed record HackPartyStats(uint Status, byte Level, int CurrentHp, StatSet Stats);

public sealed record HackMon
{
    public required RecordCodecKey Codec { get; init; }
    public required RecordShape Shape { get; init; }
    public required ushort Species { get; init; }
    public ushort? National { get; init; }
    public byte Form { get; init; }
    public uint Pid { get; init; }
    public ushort Tid16 { get; init; }
    public ushort Sid16 { get; init; }
    public string? Nickname { get; init; }
    public string? OtName { get; init; }
    public int? Language { get; init; }
    public uint Exp { get; init; }
    public byte? Level { get; init; }
    public required IReadOnlyList<ushort> Moves { get; init; }
    public IReadOnlyList<byte>? Pp { get; init; }
    public IReadOnlyList<byte>? PpUps { get; init; }
    public ushort HeldItem { get; init; }
    public byte? CatchRate { get; init; }
    public byte? Ball { get; init; }
    public StatSet? Ivs { get; init; }
    public StatSet Evs { get; init; }
    public byte? Nature { get; init; }
    public byte? PidNature { get; init; }
    public byte? AbilitySlot { get; init; }
    public bool? HiddenAbility { get; init; }
    public ushort? Ability { get; init; }
    public bool IsEgg { get; init; }
    public uint? ShinyXor { get; init; }
    public bool? ShinyModifier { get; init; }
    public bool? IsShiny { get; init; }
    public byte? TeraType { get; init; }
    public byte? HiddenNatureModifier { get; init; }
    public byte? Friendship { get; init; }
    public byte? HeaderFlags { get; init; }
    public bool? ChecksumValid { get; init; }
    public HackPartyStats? Party { get; init; }
    public required ReadOnlyMemory<byte> Raw { get; init; }
}
