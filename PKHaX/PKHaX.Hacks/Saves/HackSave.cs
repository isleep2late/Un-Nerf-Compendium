using System;
using System.Collections.Generic;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Saves.Gen3;

namespace PKHaX.Hacks.Saves;

public enum WriteEvidence
{
    Unproven,
    RoundTrip,
    Confined,
    GameAccepted,
}

public sealed record HackTrainer(string Name, ushort Tid16, ushort Sid16, byte Gender);

public sealed record HackBox(int Number, string Source, IReadOnlyList<HackMon?> Slots, IReadOnlyDictionary<int, string> Refused)
{
    public int Undecodable => Refused.Count;
}

public abstract class HackSave
{
    protected HackSave(string variant, ReadOnlyMemory<byte> original, WriteEvidence evidence)
    {
        Variant = variant;
        Original = original;
        Evidence = evidence;
    }

    public string Variant { get; }
    public ReadOnlyMemory<byte> Original { get; }
    public WriteEvidence Evidence { get; }

    public ReadOnlyMemory<byte> Footer { get; internal set; }

    public bool ReadOnly => ReadOnlyReason is not null;

    public WriteGateVerdict? Gate { get; private set; }

    internal void Admit(WriteGateVerdict verdict) => Gate = verdict;

    protected string? EvidenceReason => Evidence == WriteEvidence.GameAccepted
        ? null
        : $"{Variant} saves open read-only until an edited save is shown to load in the game (write evidence: {Evidence}).";

    public virtual string? ReadOnlyReason => Gate is { } gate ? gate.Reason : EvidenceReason;

    public string? FieldRefusal(string field, bool party) => Gate?.FieldRefusal(field, party);

    public string? SlotRefusal(bool party, int box, int slot) => Gate?.SlotRefusal(party, box, slot);

    public abstract IReadOnlyList<HackMon?> Party { get; }
    public abstract HackTrainer Trainer { get; }
    public abstract uint? Money { get; }
    public abstract string? BoxesReason { get; }

    public virtual IReadOnlyList<HackBox> Boxes => [];

    public virtual IReadOnlyDictionary<int, string> PartyRefused { get; } = new Dictionary<int, string>();

    public virtual string? BoxWriteRefusal(int box) => $"{Variant}: box slots are not written ({BoxesReason ?? "this save has no box writer yet"})";

    public abstract void SetPartySlot(int slot, HackMon mon);

    public virtual void SetBoxSlot(int box, int slot, HackMon mon) =>
        throw new FaithfulWriteException($"{Variant}: box slots are not written ({BoxesReason ?? "this save has no box writer yet"})");
    public abstract void SetMoney(uint money);

    public abstract WriteResult Plan();

    public WriteResult Write()
    {
        if (ReadOnlyReason is { } reason)
            throw new FaithfulWriteException(reason);
        return Plan();
    }
}
