using System;
using System.Collections.Generic;
using System.Linq;

namespace PKHaX.Hacks.Analysis;

public static class GbaHackData
{
    public const string ReaderGf = "gba-gf";
    public const string ReaderGfShifted = "gba-gf-shifted";
    public const string ReaderRhh = "gba-rhh";
    public const string ReaderFrFollow = "gba-fr-follow";
    public const string ReaderFollow = "gba-follow";
    public const string ReaderGfInline = "gba-gf-inline";

    public static HackDataResult Read(ReadOnlyMemory<byte> bytes, IdMapper? mapper = null)
    {
        GbaImage rom;
        try
        {
            rom = new GbaImage(bytes);
        }
        catch (ArgumentException ex)
        {
            return HackDataResult.Refused(ex.Message);
        }
        if (RhhRomHeader.Read(rom) is { } rhh)
            return ReadRhh(rom, rhh, mapper ?? IdMapper.English());
        var rubySapphire = rom.GameCode.StartsWith("AXV", StringComparison.Ordinal) || rom.GameCode.StartsWith("AXP", StringComparison.Ordinal);
        var header = rubySapphire ? null : GfRomHeader.Read(rom);
        if (header is null && !rubySapphire)
            return HackDataResult.Refused("this image has no GF ROM header at 0x100 that reads as sane, plain or shifted by one word; its tables need another reader.");
        var notes = new List<string>();
        if (header is { Shifted: true })
            notes.Add($"the GF header is shifted by one word ({(header.Shift < 0 ? "early" : "late")}, shift {header.Shift})");
        if (rubySapphire)
            notes.Add("Ruby and Sapphire have no GF header, so every table comes from its anchor and its references");

        TableHit? Resolve(string table, List<TableCandidate> found, int? pointer, Func<GbaImage, int, int?>? stride, int fixedStride)
        {
            if (FrTableFollower.Choose(rom, table, found, pointer) is { } hit)
                return hit;
            if (pointer is not { } p)
                return null;
            var s = stride is null ? fixedStride : stride(rom, p);
            return s is { } st ? new TableHit(table, p, st, 0, true) : null;
        }

        TableHit? Names(string table, IReadOnlyList<string> anchor, IEnumerable<int> strides, int? pointer, int stride)
        {
            var hit = Resolve(table, FrTableFollower.NameTables(rom, anchor, strides), pointer, null, stride);
            if (hit is { FromHeader: true, Pointer: false } && pointer is { } p && Gen3LayoutFinder.IsNamePointerTable(rom, p, anchor))
                return new TableHit(table, p, 4, 0, true, Pointer: true);
            return hit;
        }

        var speciesNamesHit = Names("species names", FrTableFollower.SpeciesAnchor, FrTableFollower.SpeciesNameStrides, header?.SpeciesNames, Gen3Tables.SpeciesNameStride);
        var moveNamesHit = Names("move names", FrTableFollower.MoveAnchor, FrTableFollower.MoveNameStrides, header?.MoveNames, Gen3Tables.MoveNameStride);
        var abilityNamesHit = Names("ability names", FrTableFollower.AbilityAnchor, FrTableFollower.AbilityNameStrides, header?.AbilityNames, Gen3Tables.AbilityNameStride);
        var speciesInfoHit = Resolve("species info", FrTableFollower.SpeciesInfoTables(rom), header?.SpeciesInfo, Gen3Tables.SpeciesInfoStride, 0);
        StatStride? plausible = null;
        if (speciesInfoHit is null && header?.SpeciesInfo is { } infoPointer && Gen3LayoutFinder.FindStatStride(rom, infoPointer) is { } found)
        {
            plausible = found;
            speciesInfoHit = new TableHit("species info", infoPointer, found.Stride, 0, true);
            notes.Add($"species 1 is not Bulbasaur's vanilla 45/49/49/45/65/65, so the GF header's species info at 0x{infoPointer:X} is read at stride {found.Stride}, the smallest where {found.Plausible} of the {found.Window} non-blank species among 1-{Gen3LayoutFinder.Window} have plausible base stats");
        }
        var movesHit = Resolve("moves", FrTableFollower.MoveTables(rom), header?.Moves, Gen3Tables.MoveStride, 0);
        var itemsHit = Resolve("items", FrTableFollower.ItemTables(rom), header?.Items, Gen3Tables.ItemStride, 0);
        if (speciesInfoHit is null)
            return HackDataResult.Refused("species 1 in the species-info table is not Bulbasaur's 45/49/49/45/65/65 at any known stride, so this layout is not the vanilla one.");
        string? itemPointerMiss = null;
        if (itemsHit is null && Gen3LayoutFinder.FindItemNamePointers(rom, header?.Items) is { } itemSlots)
        {
            var where = $"Master Ball, Ultra Ball and Great Ball are pointed at from entries 1-3 every {itemSlots.Stride} bytes, item 0's name pointer at 0x{itemSlots.Slot0:X}{(header?.Items is { } hi ? itemSlots.InHeaderEntry ? $" (+0x{itemSlots.Slot0 - hi:X} from the GF header's item pointer 0x{hi:X})" : $", not within one entry of the GF header's item pointer 0x{hi:X}" : "")}, {itemSlots.Named} items named";
            if (itemSlots.Named >= Gen3LayoutFinder.MinNamedItems)
            {
                itemsHit = new TableHit("items", itemSlots.Slot0, itemSlots.Stride, 0, itemSlots.InHeaderEntry, Pointer: true);
                notes.Add($"item names are pointers: {where}");
            }
            else
                itemPointerMiss = $" Name pointers were found ({where}), but that is under {Gen3LayoutFinder.MinNamedItems}, so it is not taken as the item table.";
        }
        NamedLayout? itemLayout = null;
        if (itemsHit is null && header?.Items is { } headerItems)
        {
            if (RhhTables.ProbeAmong(rom, headerItems, RhhTables.ItemProbe, FrTableFollower.ItemNameLengths, RhhTables.ItemProbeWindow) is { } among)
            {
                itemLayout = among;
                notes.Add($"items at the GF header's item pointer 0x{headerItems:X}: stride {among.Stride}, name {(among.Pointer ? "pointer" : "inline")} at +0x{among.NameOffset:X}, with Master Ball and Ultra Ball among entries 1 to {RhhTables.ItemProbeWindow} (not as items 1-3)");
            }
            else if (NameVote.SearchGba(rom, headerItems, NameVote.StockItems, NameVoteMode.ByMembership, NameVote.Window, FrTableFollower.ItemNameLengths, FrTableFollower.MaxItemStride) is { } vote)
            {
                itemLayout = vote.Layout;
                notes.Add(vote.Describe($"items at the GF header's item pointer 0x{headerItems:X}"));
            }
        }
        string? itemsMissing = null;
        if (itemsHit is null && itemLayout is null)
            itemsMissing = "no item table has MASTER BALL, ULTRA BALL and GREAT BALL as items 1-3 (stride 44 with item id 1, name length 14/17/20 at stride 32-64, or name pointers at an equal stride of 4-256), and the GF header's item pointer is not MASTER BALL at stride 44." + itemPointerMiss
                + (header?.Items is { } hi2 ? $" At the GF header's item pointer 0x{hi2:X} no stride of {RhhTables.MinStride}-{RhhTables.MaxStride} puts Master Ball and Ultra Ball among entries 1 to {RhhTables.ItemProbeWindow}, and no stride of {RhhTables.MinStride}-{FrTableFollower.MaxItemStride} gives at least {NameVote.MinAgree} PKHeX item names ({NameVote.MinShare * 100:0}% of the named rows among the first {NameVote.Window})." : "");
        mapper ??= IdMapper.English();
        if (speciesNamesHit is null)
        {
            if (header is not null && ReadInline(rom, header, speciesInfoHit.Offset, abilityNamesHit, mapper, notes) is { } inline)
                return inline;
            return HackDataResult.Refused("the species name table was found neither by its anchor (BULBASAUR IVYSAUR) nor through the GF header, and the species-info table does not carry Bulbasaur, Ivysaur and Venusaur inline with Bulbasaur's 45/49/49/45/65/65.");
        }
        string? movesMissing = moveNamesHit is null ? "the move name table was found neither by its anchor (POUND KARATE CHOP) nor through the GF header" : null;
        string? abilitiesMissing = abilityNamesHit is null ? "the ability name table was found neither by its anchor (STENCH DRIZZLE) nor through the GF header" : null;
        if (itemsMissing is not null)
            notes.Add($"items are not read: {itemsMissing}");
        if (movesMissing is not null)
            notes.Add($"moves are not read: {movesMissing}");

        TableHit[] hits = [.. new TableHit?[] { speciesNamesHit, moveNamesHit, abilityNamesHit, speciesInfoHit, itemsHit, movesHit }.OfType<TableHit>()];
        var followed = hits.Where(h => !h.FromHeader).ToList();
        foreach (var h in followed.Where(h => !(h.Table == "items" && h.Pointer)))
        {
            int? fromHeader = h.Table switch
            {
                "species names" => header?.SpeciesNames,
                "move names" => header?.MoveNames,
                "ability names" => header?.AbilityNames,
                "species info" => header?.SpeciesInfo,
                "items" => header?.Items,
                _ => header?.Moves,
            };
            notes.Add($"{h.Table} at 0x{h.Offset:X} {(h.Pointer ? "as a name-pointer table" : $"stride {h.Stride}")} ({h.References} references){(fromHeader is { } f ? $", not the GF header's 0x{f:X}" : "")}");
        }
        var reader = header is null ? ReaderFollow : followed.Count > 0 ? ReaderFrFollow : header.Shifted ? ReaderGfShifted : ReaderGf;

        int?[] tables = [.. hits.Select(h => (int?)h.Offset), itemLayout is null ? null : header?.Items];
        var speciesNames = NamesOf(rom, speciesNamesHit, tables);
        var moveNames = moveNamesHit is null ? new List<string>() : NamesOf(rom, moveNamesHit, tables);
        var abilityNames = abilityNamesHit is null ? new List<string>() : NamesOf(rom, abilityNamesHit, tables);
        if (abilityNames.Count < NameVote.MinAgree && abilityNamesHit is not { FromHeader: false, References: > 0 } && header?.AbilityNames is { } headerAbilities
            && AbilityNamesAt(rom, headerAbilities, tables) is { } better && better.Names.Count > abilityNames.Count)
        {
            abilityNames = better.Names;
            abilitiesMissing = null;
            notes.Add(better.Note);
        }
        if (abilitiesMissing is not null)
            notes.Add($"abilities are not read: {abilitiesMissing}");
        var speciesInfoStride = speciesInfoHit.Stride;
        var vanillaInfo = Gen3Tables.SpeciesInfoStrides.Contains(speciesInfoStride);
        SpeciesFieldOffsets? fields = null;
        string? speciesPartial = null;
        if (plausible is not null)
        {
            var nationals = speciesNames.Select((n, i) => i == 0 || n.Length == 0 ? null : mapper.Species(n)).ToList();
            fields = Gen3LayoutFinder.FindFields(rom, speciesInfoHit.Offset, speciesInfoStride, nationals);
            string[] missing = [.. new[] { ("type 1", fields.Type1), ("type 2", fields.Type2) }.Where(x => x.Item2 is null).Select(x => x.Item1)];
            if (missing.Length == 0 && fields.BestShift != 0)
                speciesPartial = $"the species info at stride {speciesInfoStride} matches Emerald's base stats best {fields.BestShift:+0;-0} entries away, not at the entry the names give, so the table does not line up with the species ({fields.Scores}); the names and raw rows are shown and nothing is overlaid";
            else if (missing.Length > 0)
                speciesPartial = $"the species info at stride {speciesInfoStride} has plausible base stats, but no byte agrees with vanilla Emerald's {string.Join(", ", missing)} over {fields.Compared} mapped species, so the stride or the layout is not this one (best per field, {Gen3LayoutFinder.MinFieldShare * 100:0}% needed: {fields.Scores}); the names and raw rows are shown and nothing is overlaid";
            if (speciesPartial is not null)
                notes.Add(speciesPartial);
            string[] unread = [.. new[] { ("catch rate", fields.CatchRate), ("EXP yield", fields.ExpYield), ("gender", fields.Gender), ("growth", fields.Growth) }.Where(x => x.Item2 is null).Select(x => x.Item1)];
            if (speciesPartial is null && unread.Length > 0)
                notes.Add($"no byte of the species info agrees with vanilla Emerald's {string.Join(", ", unread)} ({Gen3LayoutFinder.MinFieldShare * 100:0}% needed), so the hack changed or moved them: they are left unread, never filled with 0 ({fields.Scores})");
            if (speciesPartial is null)
                notes.Add($"species fields found by agreement with vanilla Emerald over {fields.Compared} species: types +{Hex(fields.Type1)}/+{Hex(fields.Type2)}, catch rate {At(fields.CatchRate)}, EXP yield {At(fields.ExpYield)}, gender {At(fields.Gender)}, growth {At(fields.Growth)}; abilities are not read{(fields.TypeMap is { } tm && tm.Any(kv => kv.Key != kv.Value) ? $"; the types are numbered differently ({tm.Count(kv => kv.Key != kv.Value)} of {tm.Count} ids), so they are read through the table learned from Emerald's types" : "")}");
        }
        else if (!vanillaInfo)
            notes.Add($"species-info stride {speciesInfoStride} is not a vanilla one, so only base stats, types and catch rate are read");

        var species = new List<HackSpecies>(speciesNames.Count);
        for (var i = 0; i < speciesNames.Count; i++)
        {
            var b = speciesInfoHit.Offset + i * speciesInfoStride;
            if (!rom.Has(b, fields is not null ? speciesInfoStride : vanillaInfo ? 0x18 : 0x09))
                break;
            if (fields is { } f)
            {
                int U8At(int? o) => o is { } x ? rom.U8(b + x) : 0;
                int? Read(int? o) => o is { } x ? rom.U8(b + x) : null;
                int TypeAt(int? o) => f.TypeMap is { } map && map.TryGetValue((byte)U8At(o), out var emerald) ? emerald : U8At(o);
                species.Add(new HackSpecies(i, speciesNames[i], i == 0 || speciesNames[i].Length == 0 ? null : mapper.Species(speciesNames[i]), 0,
                    speciesPartial is null ? [TypeAt(f.Type1), TypeAt(f.Type2)] : [],
                    [rom.U8(b), rom.U8(b + 1), rom.U8(b + 2), rom.U8(b + 3), rom.U8(b + 4), rom.U8(b + 5)],
                    [], Read(f.Growth), U8At(f.Gender), Read(f.CatchRate), Read(f.ExpYield)) { GenderRead = f.Gender is not null });
                continue;
            }
            species.Add(new HackSpecies(i, speciesNames[i], i == 0 || speciesNames[i].Length == 0 ? null : mapper.Species(speciesNames[i]), 0,
                [rom.U8(b + 0x06), rom.U8(b + 0x07)],
                [rom.U8(b), rom.U8(b + 1), rom.U8(b + 2), rom.U8(b + 3), rom.U8(b + 4), rom.U8(b + 5)],
                vanillaInfo ? [rom.U8(b + 0x16), rom.U8(b + 0x17)] : [],
                vanillaInfo ? rom.U8(b + 0x13) : null, vanillaInfo ? rom.U8(b + 0x10) : 0, rom.U8(b + 0x08), vanillaInfo ? rom.U8(b + 0x09) : null) { GenderRead = vanillaInfo });
        }

        var engine = Gen3Engine.Of(rom, reader);
        var moveNationals = moveNames.Select((n, i) => i == 0 || n.Length == 0 ? null : mapper.Move(i, n)).ToList();
        var moves = new List<HackMove>(moveNames.Count);
        string? movesPartial = null;
        var moveWhy = "the GF header has no move pointer";
        (int Stride, MoveFields Fields)? moveRows = null;
        if (movesHit is null && moveNames.Count > 0 && header?.Moves is { } movePointer)
            moveRows = FindMoveRows(rom, movePointer, moveNationals, moveNames.Count, out moveWhy);
        if (movesHit is not null)
        {
            for (var i = 0; i < moveNames.Count; i++)
            {
                var b = movesHit.Offset + i * movesHit.Stride;
                if (!rom.Has(b, 5))
                    break;
                moves.Add(new HackMove(i, moveNames[i], moveNationals[i], rom.U8(b + 1), rom.U8(b + 2), rom.U8(b + 3), rom.U8(b + 4)));
            }
            moves = RetailCategory(rom, movesHit, moves, engine, notes);
        }
        else if (moveRows is { } rowsFound && header?.Moves is { } headerMoves)
        {
            moves = MovesFrom(rom, headerMoves, rowsFound.Stride, moveNames, moveNationals, rowsFound.Fields, TypeRule(engine), notes);
            notes.Add($"move rows at the GF header's move pointer 0x{headerMoves:X}, stride {rowsFound.Stride}: {rowsFound.Fields.Note}");
            if (rowsFound.Fields.Type is null)
                movesPartial = $"move power, accuracy and PP are read at stride {rowsFound.Stride}, but no field agrees with PKHeX's move types, so the type is not read";
        }
        else
        {
            for (var i = 0; i < moveNames.Count; i++)
                moves.Add(new HackMove(i, moveNames[i], moveNationals[i], 0, 0, 0, 0));
            if (moveNames.Count > 0)
            {
                movesPartial = "no move table has Pound (power 40, Normal, 100 accuracy, 35 PP) as move 1 at stride 12 or 9, so move power, type, accuracy and PP are not read"
                    + (header?.Moves is { } unreadMoves ? $"; at the GF header's move pointer 0x{unreadMoves:X} no stride of {MinMoveStride}-{MaxMoveStride} bytes gives the anchor moves their power, accuracy and PP ({moveWhy})" : "");
            }
        }
        if (movesPartial is not null)
            notes.Add(movesPartial);

        var items = itemsHit is not null ? ReadItems(rom, itemsHit, tables, mapper, notes)
            : itemLayout is not null && header?.Items is { } itemTable ? ItemsAt(rom, itemTable, itemLayout, tables, mapper, notes)
            : new List<HackItem>();

        var abilities = new List<HackAbility>(abilityNames.Count);
        for (var i = 0; i < abilityNames.Count; i++)
            abilities.Add(new HackAbility(i, abilityNames[i], i == 0 ? null : mapper.Ability(i, abilityNames[i])));

        var levelUp = new Dictionary<int, IReadOnlyList<HackLearn>>();
        var learnMoveCount = Math.Max(moves.Count, IdMapper.Gen3LastMove + 1);
        var learnField = fields is null || speciesPartial is not null ? null : Gen3Learnsets.FindField(rom, speciesInfoHit.Offset, speciesInfoStride, species.Count, learnMoveCount);
        if (fields is not null && speciesPartial is not null)
            notes.Add("the level-up lists are not looked for through the species-info rows, since those rows are not shown to line up with the species; only a separate pointer table is tried");
        if ((learnField ?? Gen3Learnsets.FindTable(rom, species.Count, learnMoveCount)) is { } learnTable)
        {
            levelUp = Gen3Learnsets.Read(rom, learnTable, learnField is null ? 4 : speciesInfoStride, learnMoveCount);
            notes.Add($"level-up learnsets: {learnTable.Format.ToString().ToLowerInvariant()} pointer table at 0x{learnTable.Offset:X}, {levelUp.Count} of {learnTable.Count} species");
            if (Gen3Learnsets.Why(learnTable) is { } why)
                notes.Add(why);
        }
        else
        {
            notes.Add("no level-up learnset pointer table was found");
        }

        HackTableStatus[] status =
        [
            speciesPartial is null ? new HackTableStatus(HackTableStatus.Species, HackTableStatus.Read, null, true) : new HackTableStatus(HackTableStatus.Species, HackTableStatus.Partial, speciesPartial, false),
            movesMissing is not null ? new HackTableStatus(HackTableStatus.Moves, HackTableStatus.Missing, movesMissing)
                : movesPartial is not null ? new HackTableStatus(HackTableStatus.Moves, HackTableStatus.Partial, movesPartial)
                : new HackTableStatus(HackTableStatus.Moves, HackTableStatus.Read),
            HackTableStatus.Of(HackTableStatus.Items, itemsMissing is null, itemsMissing),
            HackTableStatus.Of(HackTableStatus.Abilities, abilitiesMissing is null, abilitiesMissing),
            HackTableStatus.Of(HackTableStatus.LevelUp, levelUp.Count > 0, "no level-up learnset pointer table was found"),
        ];
        return new HackDataResult(HackTables.Finish(new HackData
        {
            RomSha1 = rom.Sha1,
            Reader = reader,
            LevelUp = levelUp,
            Species = species,
            Moves = moves,
            Items = items,
            Abilities = abilities,
            Notes = notes,
        }, HackTables.Gba, rom.GameCode, engine, status), null);
    }

    public const int MinMoveStride = 8;
    public const int MaxMoveStride = 64;

    public static (int Stride, MoveFields Fields)? FindMoveRows(GbaImage rom, int table, IReadOnlyList<int?> nationals, int count, out string why)
    {
        ArgumentNullException.ThrowIfNull(rom);
        ArgumentNullException.ThrowIfNull(nationals);
        why = "no move rows were tried";
        for (var stride = MinMoveStride; stride <= MaxMoveStride; stride++)
        {
            if (!rom.Has(table + (11 * stride), stride))
                break;
            var s = stride;
            if (MoveAnchors.Find(count, i => rom.Has(table + (i * s), s) ? rom.Slice(table + (i * s), s).ToArray() : null, nationals, out var w) is { } fields)
            {
                why = "";
                return (stride, fields);
            }
            if (stride == 12 || !w.StartsWith("no field", StringComparison.Ordinal))
                why = $"stride {stride}: {w}";
        }
        return null;
    }

    public static bool TypeRule(string engine) => engine is not (Gen3Engine.Cfru or Gen3Engine.Expansion);

    public static List<HackMove> MovesFrom(GbaImage rom, int table, int stride, IReadOnlyList<string> names, IReadOnlyList<int?> nationals, MoveFields fields, bool typeRule, List<string> notes)
    {
        ArgumentNullException.ThrowIfNull(rom);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(nationals);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(notes);
        var moves = new List<HackMove>(names.Count);
        var unmapped = 0;
        var byType = 0;
        for (var i = 0; i < names.Count; i++)
        {
            var national = i < nationals.Count ? nationals[i] : null;
            var b = table + (i * stride);
            if (i == 0 || !rom.Has(b, stride))
            {
                moves.Add(new HackMove(i, names[i], national, 0, 0, 0, 0));
                continue;
            }
            var row = rom.Slice(b, stride);
            int? mapped = fields.Type is null ? null : fields.Type.Value(row) is { } t and >= 0 and <= byte.MaxValue ? t : null;
            if (fields.Type is not null && mapped is null)
                unmapped++;
            var power = fields.Power.Read(row);
            var category = fields.CategoryOf(row);
            var categoryByType = false;
            if (category is null && fields.Category is null && typeRule && mapped is { } pkhexType)
            {
                category = MoveAnchors.CategoryByType(pkhexType, power);
                categoryByType = true;
                byType++;
            }
            moves.Add(new HackMove(i, names[i], national, power, mapped is { } m ? Gen3LayoutFinder.RawType((byte)m) : fields.Type is null ? 0 : -1, fields.Accuracy.Read(row), fields.Pp.Read(row)) { Category = category, CategoryByType = categoryByType });
        }
        if (unmapped > 0)
            notes.Add($"{unmapped} move row{(unmapped == 1 ? " has" : "s have")} a type value that no named move carried, so the learned numbering does not say which type it is; those moves keep PKHeX's own type and are not used for the category");
        if (byType > 0)
            notes.Add($"no field of the move rows carries a physical/special/status code, so the category of {byType} moves is the Gen 1-3 type rule (power 0 status, Fire to Dark special, the rest physical) and is marked as by type");
        return moves;
    }

    private static List<HackMove> RetailCategory(GbaImage rom, TableHit movesHit, List<HackMove> moves, string engine, List<string> notes)
    {
        var anchorRows = MoveAnchors.Rows([.. moves.Select(m => m.National)], moves.Count).Where(x => rom.Has(movesHit.Offset + (x.Row * movesHit.Stride), movesHit.Stride)).Select(x => rom.Slice(movesHit.Offset + (x.Row * movesHit.Stride), movesHit.Stride).ToArray()).ToList();
        var mapped = moves.Where(m => m.Id != 0 && m.National is not null && rom.Has(movesHit.Offset + (m.Id * movesHit.Stride), movesHit.Stride)).ToList();
        var rows = mapped.Select(m => rom.Slice(movesHit.Offset + (m.Id * movesHit.Stride), movesHit.Stride).ToArray()).ToList();
        BitField[] spare = movesHit.Stride >= 12 ? [new BitField(72, 8), new BitField(80, 8), new BitField(88, 8)] : [];
        var (field, map, agree) = MoveAnchors.FindCategory(rows, [.. mapped.Select(m => m.Power)], anchorRows, spare);
        if (field is { } f && map is { } categoryMap)
        {
            notes.Add($"move category is read from byte {f.Where} of the {movesHit.Stride}-byte move rows ({agree} of {mapped.Count} named moves have power 0 exactly when the byte gives the status code)");
            return [.. moves.Select(m => rom.Has(movesHit.Offset + (m.Id * movesHit.Stride), movesHit.Stride) && categoryMap.TryGetValue(f.Read(rom.Slice(movesHit.Offset + (m.Id * movesHit.Stride), movesHit.Stride)), out var c) && m.Id != 0 ? m with { Category = c } : m)];
        }
        if (engine is Gen3Engine.Cfru or Gen3Engine.Expansion)
            notes.Add($"no byte of +0x9 to +0xB of the move rows carries a physical/special/status code, and this {engine} image splits moves by move, so the category is left unread");
        return moves;
    }

    private static (List<string> Names, string Note)? AbilityNamesAt(GbaImage rom, int table, int?[] tables)
    {
        var bound = Gen3Tables.Bound(table, Gen3Tables.AbilityNameStride, tables);
        if (RhhTables.Probe(rom, table, RhhTables.AbilityProbe, [Gen3Tables.AbilityNameStride, 17, 21]) is { } probe)
        {
            var names = NameVote.Run(rom, table, probe, Math.Max(bound * Gen3Tables.AbilityNameStride / probe.Stride, NameVote.Window), Gen3LayoutFinder.MaxUnnamedRun);
            return (names, $"ability names at the GF header's ability pointer 0x{table:X}: stride {probe.Stride}, name {(probe.Pointer ? "pointer" : "inline")} at +0x{probe.NameOffset:X} ({names.Count} entries), because the anchor table gave fewer than {NameVote.MinAgree}");
        }
        if (NameVote.SearchGba(rom, table, NameVote.StockAbilities, NameVoteMode.ByMembership, NameVote.Window, [Gen3Tables.AbilityNameStride, 17, 21], 64) is { } vote)
        {
            var names = NameVote.Run(rom, table, vote.Layout, Gen3Tables.MaxEntries, Gen3LayoutFinder.MaxUnnamedRun);
            return (names, vote.Describe($"ability names at the GF header's ability pointer 0x{table:X}") + $" ({names.Count} entries)");
        }
        return null;
    }

    private static List<HackItem> ItemsAt(GbaImage rom, int table, NamedLayout layout, int?[] tables, IdMapper mapper, List<string> notes)
    {
        var bound = Gen3Tables.Bound(table, layout.Stride, tables);
        var names = NameVote.Run(rom, table, layout, bound, Gen3LayoutFinder.MaxUnnamedRun);
        var vanillaRows = !layout.Pointer && layout.NameOffset == 0 && layout.Stride == Gen3Tables.ItemStrides[0];
        var idsAgree = vanillaRows && names.Count > 1
            && Enumerable.Range(1, names.Count - 1).Count(i => rom.Has(table + (i * layout.Stride), 0x1C) && rom.U16(table + (i * layout.Stride) + 0x0E) == i) >= 0.9 * (names.Count - 1);
        var items = new List<HackItem>(names.Count);
        for (var i = 0; i < names.Count; i++)
        {
            var b = table + (i * layout.Stride);
            var national = i == 0 || names[i].Length == 0 ? null : mapper.ItemByName(names[i]);
            items.Add(idsAgree && i > 0 && rom.Has(b, 0x1C) ? new HackItem(i, names[i], national, rom.U8(b + 0x1A), rom.U16(b + 0x10)) : new HackItem(i, names[i], national, 0, 0));
        }
        notes.Add(idsAgree ? $"the {items.Count} item rows carry their own id at +0xE, so pocket and price are read at the vanilla places; items map by name" : $"{items.Count} item rows; items map by name only (no pocket or price)");
        return items;
    }

    private static List<string> NamesOf(GbaImage rom, TableHit h, int?[] tables) => h.Pointer
        ? FrTableFollower.PointerNames(rom, h.Offset, Gen3Tables.Bound(h.Offset, 4, tables))
        : Gen3Tables.Names(rom, h.Offset, h.Stride, max: Gen3Tables.Bound(h.Offset, h.Stride, tables));

    private static List<HackItem> ReadItems(GbaImage rom, TableHit itemsHit, int?[] tables, IdMapper mapper, List<string> notes)
    {
        var itemStride = itemsHit.Stride;
        var items = new List<HackItem>();
        var vanillaItems = !itemsHit.Pointer && itemStride == Gen3Tables.ItemStrides[0] && itemsHit.NameLength is 0 or Gen3Tables.ItemNameLength;
        var itemWindow = itemsHit.NameLength == 0 ? Gen3Tables.ItemNameLength : itemsHit.NameLength;
        if (!vanillaItems)
        {
            notes.Add(itemsHit.Pointer ? $"item names are behind pointers every {itemStride} bytes, so items are read by name only (no pocket or price)" : $"item stride {itemStride} is not the vanilla layout, so items are read by name only (no pocket or price)");
            var pointerNames = itemsHit.Pointer ? Gen3LayoutFinder.NamedRun(rom, itemsHit.Offset, itemStride, Gen3Tables.Bound(itemsHit.Offset, itemStride, tables)) : null;
            for (var i = 0; pointerNames is not null && i < pointerNames.Count; i++)
                items.Add(new HackItem(i, pointerNames[i], i == 0 || pointerNames[i].Length == 0 ? null : mapper.ItemByName(pointerNames[i]), 0, 0));
            for (var i = 0; pointerNames is null && i < Gen3Tables.MaxEntries && rom.Has(itemsHit.Offset + i * itemStride, itemWindow); i++)
            {
                var name = Gen3Tables.Name(rom, itemsHit.Offset + i * itemStride, itemWindow);
                if (name is null && i > 0)
                    break;
                items.Add(new HackItem(i, name ?? "", i == 0 || name is null ? null : mapper.ItemByName(name), 0, 0));
            }
        }
        var itemBound = Gen3Tables.Bound(itemsHit.Offset, itemStride, tables);
        for (var i = 0; vanillaItems && i < itemBound; i++)
        {
            var b = itemsHit.Offset + i * itemStride;
            if (!rom.Has(b, 0x1C))
                break;
            var name = Gen3Tables.Name(rom, b, Gen3Tables.ItemNameLength);
            if (name is null && i > 0)
                break;
            var id = rom.U16(b + 0x0E);
            if (i > 0 && id != i && id != 0)
                break;
            items.Add(new HackItem(i, name ?? "", i == 0 ? null : mapper.Item(i, name ?? ""), rom.U8(b + 0x1A), rom.U16(b + 0x10)));
        }
        if (vanillaItems && items.Count < itemBound)
        {
            var loose = LooseVanillaItems(rom, itemsHit.Offset, itemStride, itemBound, mapper, out var skipped, out var full, out var moved);
            var namedLoose = loose.Count(x => x.Name.Length > 0);
            if (namedLoose >= items.Count(x => x.Name.Length > 0) + NameVote.MinAgree && namedLoose >= MinLooseNamedShare * loose.Count)
            {
                notes.Add($"the vanilla item rows stop being readable at item {items.Count}, but {namedLoose} rows read on past it: {skipped} unreadable row{(skipped == 1 ? " is" : "s are")} kept as blank so later ids stay in place, and {full} name{(full == 1 ? " fills" : "s fill")} all {Gen3Tables.ItemNameLength} bytes with no terminator");
                if (moved > 0)
                    notes.Add($"{moved} named item row{(moved == 1 ? " carries" : "s carry")} another item's id at +0xE (within {MaxItemIdDrift} of the row); the game finds an item by its row (pokefirered src/item.c:612, gItems[itemId].name), so each row is read as the item of its own index");
                return loose;
            }
        }
        return items;
    }

    public const int MaxItemIdDrift = 128;
    public const double MinLooseNamedShare = 0.8;

    private static List<HackItem> LooseVanillaItems(GbaImage rom, int table, int stride, int bound, IdMapper mapper, out int skipped, out int full, out int moved)
    {
        var items = new List<HackItem>();
        skipped = 0;
        full = 0;
        moved = 0;
        var movedUpTo = new List<int>();
        var gap = 0;
        var lastGood = 0;
        for (var i = 0; i < Math.Min(bound, Gen3Tables.MaxEntries); i++)
        {
            var b = table + (i * stride);
            if (!rom.Has(b, 0x1C))
                break;
            var name = Gen3Tables.Name(rom, b, Gen3Tables.ItemNameLength);
            var filled = false;
            if (name is null && Gen3Tables.FullName(rom, b, Gen3Tables.ItemNameLength) is { } whole)
            {
                name = whole;
                filled = true;
            }
            var id = rom.U16(b + 0x0E);
            var drifted = id != i && id != 0 && Math.Abs(id - i) <= MaxItemIdDrift;
            if (i > 0 && (name is null || (id != i && id != 0 && !drifted)))
            {
                if (++gap > Gen3LayoutFinder.MaxUnnamedRun)
                    break;
                items.Add(new HackItem(i, "", null, 0, 0));
                continue;
            }
            gap = 0;
            lastGood = i;
            if (filled)
                full++;
            if (i > 0 && drifted)
                movedUpTo.Add(i);
            items.Add(new HackItem(i, name ?? "", i == 0 ? null : mapper.Item(i, name ?? ""), rom.U8(b + 0x1A), rom.U16(b + 0x10)));
        }
        if (items.Count > lastGood + 1)
            items.RemoveRange(lastGood + 1, items.Count - lastGood - 1);
        skipped = items.Count(x => x.Id != 0 && x.Name.Length == 0);
        moved = movedUpTo.Count(row => row <= lastGood);
        return items;
    }

    private static HackDataResult ReadRhh(GbaImage rom, RhhRomHeader rhh, IdMapper mapper)
    {
        var where = $"pokeemerald-expansion {rhh.Version} (RHHEXP at 0x{rhh.Offset:X})";
        var header = GfRomHeader.Read(rom) ?? GfRomHeader.ReadAt(rom, 0);
        if (header?.SpeciesInfo is not { } speciesTable || header.Moves is not { } moveTable || header.Items is not { } itemTable)
            return HackDataResult.Refused($"this image is {where}, but its GF header at 0x100 does not point at gSpeciesInfo, gMovesInfo and gItemsInfo.");
        if (rhh.Species is 0 or > Gen3Tables.MaxEntries || rhh.Moves is 0 or > Gen3Tables.MaxEntries || rhh.Items is 0 or > Gen3Tables.MaxEntries)
            return HackDataResult.Refused($"this image is {where}, but its counts ({rhh.Species} species, {rhh.Moves} moves, {rhh.Items} items) are outside 1 to {Gen3Tables.MaxEntries}.");
        var speciesLayout = RhhTables.Probe(rom, speciesTable, RhhTables.SpeciesProbe, [RhhTables.SpeciesNameLength, Gen3Tables.SpeciesNameStride]);
        NameVoteHit? speciesVote = null;
        if (speciesLayout is null
            && NameVote.SearchGba(rom, speciesTable, NameVote.StockSpecies, NameVoteMode.ByIndex, NameVote.GbaSpeciesWindow, [RhhTables.SpeciesNameLength, Gen3Tables.SpeciesNameStride], RhhTables.MaxStride, RhhTables.MaxSpeciesNameOffset) is { } speciesHit)
        {
            speciesVote = speciesHit;
            speciesLayout = speciesHit.Layout;
        }
        var anchorMiss = $"this image is {where}, but no stride of gSpeciesInfo puts Bulbasaur, Ivysaur and Venusaur at entries 1 to 3 with Bulbasaur's 45/49/49/45/65/65";
        if (speciesLayout is null)
            return HackDataResult.Refused($"{anchorMiss}, and no stride and name place gives at least {NameVote.MinAgree} of species 1-{NameVote.GbaSpeciesWindow} their own national names ({NameVote.MinShare * 100:0}% of the named ones).");
        var bulbasaur = speciesVote is null && RhhTables.HasBulbasaurStats(rom, speciesTable, speciesLayout.Stride);
        var notes = new List<string>
        {
            $"{where}: species-info stride {speciesLayout.Stride}, name at +0x{speciesLayout.NameOffset:X}",
        };
        if (speciesVote is not null)
            notes.Add(speciesVote.Describe("species info"));
        if (!bulbasaur)
        {
            var (plausibleRows, namedRows) = RhhTables.PlausibleNamed(rom, speciesTable, speciesLayout, Math.Min(rhh.Species, Gen3LayoutFinder.Window + 1));
            if (namedRows < NameVote.MinAgree || plausibleRows < Gen3LayoutFinder.MinPlausibleShare * namedRows)
                return HackDataResult.Refused($"{anchorMiss}, and only {plausibleRows} of the {namedRows} named species among 1-{Gen3LayoutFinder.Window} have plausible base stats at +0 ({Gen3LayoutFinder.MinPlausibleShare * 100:0}% of at least {NameVote.MinAgree} named species needed).");
            notes.Add($"species 1 is not Bulbasaur's vanilla 45/49/49/45/65/65, but {plausibleRows} of the {namedRows} named species among 1-{Gen3LayoutFinder.Window} have plausible base stats at +0, so the table is read");
        }
        if (header.Shifted)
            notes.Add($"the GF header is shifted by one word ({(header.Shift < 0 ? "early" : "late")}, shift {header.Shift})");

        var speciesNames = RhhTables.Names(rom, speciesTable, speciesLayout, rhh.Species);
        var lastNamed = speciesNames.FindLastIndex(n => n.Length > 0);
        if (rhh.Species - 1 - lastNamed > RhhTables.DisabledTail)
            return HackDataResult.Refused($"this image is {where}, but the last named species at stride {speciesLayout.Stride} is {lastNamed}, more than {RhhTables.DisabledTail} short of the {rhh.Species - 1} its RHH header counts, so the stride or the count is wrong.");
        if (lastNamed < rhh.Species - 1)
            notes.Add($"species {lastNamed + 1}-{rhh.Species - 1} have no name (left out of this build by its config)");
        var speciesNationals = speciesNames.Select((n, i) => i == 0 || n.Length == 0 ? null : mapper.Species(n)).ToList();
        FieldVoteResult? type1Vote = null;
        FieldVoteResult? type2Vote = null;
        string? speciesPartial = null;
        var typeFamily = "";
        if (!bulbasaur)
        {
            var nameBits = new List<BitField> { RhhTables.NameBits(speciesLayout) };
            var rows = new List<byte[]>();
            var rowIds = new List<int>();
            var expected1 = new List<int?>();
            var expected2 = new List<int?>();
            var baseExpected1 = new List<int?>();
            var baseExpected2 = new List<int?>();
            for (var i = 1; i < speciesNames.Count; i++)
            {
                var b = speciesTable + i * speciesLayout.Stride;
                if (speciesNationals[i] is not { } n || !rom.Has(b, speciesLayout.Stride))
                    continue;
                rows.Add(rom.Slice(b, speciesLayout.Stride).ToArray());
                rowIds.Add(i);
                expected1.Add(FieldVote.StockType1(n));
                expected2.Add(FieldVote.StockType2(n));
                baseExpected1.Add(FieldVote.BaseType1(n));
                baseExpected2.Add(FieldVote.BaseType2(n));
            }
            var nameOnly = new List<BitField>(nameBits);
            var newest1 = FieldVote.FindMapped(rows, expected1, [8, 5], nameOnly);
            var base1 = FieldVote.FindMapped(rows, baseExpected1, [8, 5], nameOnly);
            var useBase = base1 is not null && (newest1 is null || base1.Agree > newest1.Agree);
            type1Vote = useBase ? base1 : newest1;
            Func<int, int?> family1 = useBase ? FieldVote.BaseType1 : FieldVote.StockType1;
            var shift = 0;
            if (type1Vote is not null)
            {
                shift = FieldVote.BestShift(rows, rowIds, id => id >= 1 && id < speciesNationals.Count && speciesNationals[id] is { } sn ? family1(sn) : null, type1Vote);
                nameBits.Add(type1Vote.Field);
                type2Vote = FieldVote.FindMapped(rows, useBase ? baseExpected2 : expected2, [8, 5], nameBits);
            }
            typeFamily = useBase ? "vanilla Emerald's types for species 1-386 (PKHeX's newest for the rest)" : "PKHeX's newest types";
            if (type1Vote is null || type2Vote is null || shift != 0)
            {
                var best = FieldVote.TopMapped(rows, expected1, [8, 5], [RhhTables.NameBits(speciesLayout)]);
                speciesPartial = shift != 0
                    ? $"the species rows are not shown to line up with their names: the type field {type1Vote?.Field.Where} agrees best with the species {shift:+0;-0} rows away, not with the row the names give, so the names and raw rows are shown and nothing is overlaid"
                    : $"the species rows are not shown to line up with their names: no field agrees with the types of the mapped species on {FieldVote.MinShare * 100:0}% of at least {FieldVote.MinCompared} rows (best type 1 {(best is null ? "none" : $"{best.Field.Where} {best.Agree}/{best.Compared}")}, {rows.Count} mapped rows), so the names and raw rows are shown and nothing is overlaid";
                notes.Add(speciesPartial);
                type1Vote = null;
                type2Vote = null;
            }
            else
            {
                notes.Add($"species types found by agreement with {typeFamily}: type 1 {type1Vote.Field.Where} ({type1Vote.Agree}/{type1Vote.Compared}), type 2 {type2Vote.Field.Where} ({type2Vote.Agree}/{type2Vote.Compared}), read through the learned numbering");
            }
        }
        var species = new List<HackSpecies>(speciesNames.Count);
        for (var i = 0; i < speciesNames.Count; i++)
        {
            var b = speciesTable + i * speciesLayout.Stride;
            if (!rom.Has(b, Math.Max(speciesLayout.Stride, 0x09)))
                break;
            var row = rom.Slice(b, speciesLayout.Stride);
            IReadOnlyList<int> types = type1Vote is null || type2Vote is null
                ? [rom.U8(b + 0x06), rom.U8(b + 0x07)]
                : VotedType(type1Vote, row) is { } votedType1 && VotedType(type2Vote, row) is { } votedType2 ? [votedType1, votedType2] : [];
            species.Add(new HackSpecies(i, speciesNames[i], speciesNationals[i], 0,
                types,
                [rom.U8(b), rom.U8(b + 1), rom.U8(b + 2), rom.U8(b + 3), rom.U8(b + 4), rom.U8(b + 5)],
                [], null, 0, rom.U8(b + 0x08), null) { GenderRead = false });
        }

        var moves = new List<HackMove>();
        string? movesReason;
        var moveLayout = RhhTables.Probe(rom, moveTable, RhhTables.MoveProbe, [17, Gen3Tables.MoveNameStride]);
        if (moveLayout is null && NameVote.SearchGba(rom, moveTable, NameVote.StockMoves, NameVoteMode.ByIndex, NameVote.Window, [17, Gen3Tables.MoveNameStride]) is { } moveHit)
        {
            moveLayout = moveHit.Layout;
            notes.Add(moveHit.Describe("gMovesInfo"));
        }
        if (moveLayout is not null)
        {
            var names = RhhTables.Names(rom, moveTable, moveLayout, rhh.Moves);
            var nationals = names.Select((n, i) => i == 0 || n.Length == 0 ? null : mapper.Move(i, n)).ToList();
            var stride = moveLayout.Stride;
            var fields = MoveAnchors.Find(names.Count, i => rom.Has(moveTable + (i * stride), stride) ? rom.Slice(moveTable + (i * stride), stride).ToArray() : null, nationals, out var fieldsWhy, [RhhTables.NameBits(moveLayout)]);
            if (fields is not null)
            {
                moves = MovesFrom(rom, moveTable, stride, names, nationals, fields, false, notes);
                movesReason = fields.Type is null ? $"move power, accuracy and PP are read at stride {stride}, but no field agrees with PKHeX's move types, so the type is not read" : null;
                notes.Add($"move stride {stride}, name {(moveLayout.Pointer ? "pointer" : "inline")} at +0x{moveLayout.NameOffset:X}; {fields.Note}");
            }
            else
            {
                for (var i = 0; i < names.Count; i++)
                    moves.Add(new HackMove(i, names[i], nationals[i], 0, 0, 0, 0));
                movesReason = $"move names only; move power, type, accuracy and PP are not read: {fieldsWhy}";
                notes.Add($"move stride {stride}, name {(moveLayout.Pointer ? "pointer" : "inline")} at +0x{moveLayout.NameOffset:X}; {movesReason}");
            }
        }
        else
        {
            movesReason = "no stride of gMovesInfo puts Pound, Karate Chop and Double Slap at entries 1 to 3";
            notes.Add(movesReason);
        }

        var items = new List<HackItem>();
        string? itemsReason = null;
        int[] itemLengths = [rhh.ItemNameLength, rhh.ItemNameLength + 1, Gen3Tables.ItemNameLength];
        var itemLayout = RhhTables.Probe(rom, itemTable, RhhTables.ItemProbe, itemLengths) ?? RhhTables.ProbeAmong(rom, itemTable, RhhTables.ItemProbe, itemLengths, RhhTables.ItemProbeWindow);
        if (itemLayout is null && NameVote.SearchGba(rom, itemTable, NameVote.StockItems, NameVoteMode.ByMembership, NameVote.Window, itemLengths) is { } itemHit)
        {
            itemLayout = itemHit.Layout;
            notes.Add(itemHit.Describe("gItemsInfo"));
        }
        if (itemLayout is not null)
        {
            var names = RhhTables.Names(rom, itemTable, itemLayout, rhh.Items);
            for (var i = 0; i < names.Count; i++)
                items.Add(new HackItem(i, names[i], i == 0 || names[i].Length == 0 ? null : mapper.ItemByName(names[i]), 0, 0));
            notes.Add($"item stride {itemLayout.Stride}, name at +0x{itemLayout.NameOffset:X}; items map by name only");
        }
        else
        {
            itemsReason = $"no stride of gItemsInfo puts Master Ball and Ultra Ball among entries 1 to {RhhTables.ItemProbeWindow}, and no stride gives at least {NameVote.MinAgree} PKHeX item names";
            notes.Add(itemsReason);
        }

        var abilities = new List<HackAbility>();
        string? abilitiesReason = null;
        NamedLayout? abilityLayout = null;
        if (rhh.AbilitiesTable is { } abilityTable && rhh.Abilities is > 0 and <= Gen3Tables.MaxEntries)
        {
            abilityLayout = RhhTables.Probe(rom, abilityTable, RhhTables.AbilityProbe, [17, Gen3Tables.AbilityNameStride]);
            if (abilityLayout is null && NameVote.SearchGba(rom, abilityTable, NameVote.StockAbilities, NameVoteMode.ByMembership, NameVote.Window, [17, Gen3Tables.AbilityNameStride]) is { } abilityHit)
            {
                abilityLayout = abilityHit.Layout;
                notes.Add(abilityHit.Describe("the RHH header's ability table"));
            }
            if (abilityLayout is not null)
            {
                var names = RhhTables.Names(rom, abilityTable, abilityLayout, rhh.Abilities);
                for (var i = 0; i < names.Count; i++)
                    abilities.Add(new HackAbility(i, names[i], i == 0 || names[i].Length == 0 ? null : mapper.Ability(i, names[i])));
                notes.Add($"ability stride {abilityLayout.Stride}, name at +0x{abilityLayout.NameOffset:X}");
            }
        }
        if (abilityLayout is null)
        {
            abilitiesReason = "the RHH header's ability table does not put Stench, Drizzle and Speed Boost at entries 1 to 3, and no stride gives at least 40 PKHeX ability names";
            notes.Add(abilitiesReason);
        }

        var levelUp = new Dictionary<int, IReadOnlyList<HackLearn>>();
        var learnMoves = Math.Max(rhh.Moves, 2);
        if ((Gen3Learnsets.FindField(rom, speciesTable, speciesLayout.Stride, species.Count, learnMoves) ?? Gen3Learnsets.FindTable(rom, species.Count, learnMoves)) is { } learnTable)
        {
            levelUp = Gen3Learnsets.Read(rom, learnTable, speciesLayout.Stride, learnMoves);
            notes.Add(learnTable.FieldOffset is { } lf
                ? $"level-up learnsets: {learnTable.Format.ToString().ToLowerInvariant()} lists through gSpeciesInfo +0x{lf:X}, {levelUp.Count} of {learnTable.Count} species"
                : $"level-up learnsets: {learnTable.Format.ToString().ToLowerInvariant()} pointer table at 0x{learnTable.Offset:X}, {levelUp.Count} of {learnTable.Count} species");
            if (Gen3Learnsets.Why(learnTable) is { } why)
                notes.Add(why);
        }
        else
        {
            notes.Add("no level-up learnsets were found in gSpeciesInfo or as a pointer table");
        }

        HackTableStatus[] status =
        [
            speciesPartial is null ? new HackTableStatus(HackTableStatus.Species, HackTableStatus.Read, null, true) : new HackTableStatus(HackTableStatus.Species, HackTableStatus.Partial, speciesPartial, false),
            new HackTableStatus(HackTableStatus.Moves, moves.Count == 0 ? HackTableStatus.Missing : movesReason is null ? HackTableStatus.Read : HackTableStatus.Partial, movesReason),
            HackTableStatus.Of(HackTableStatus.Items, itemsReason is null, itemsReason),
            HackTableStatus.Of(HackTableStatus.Abilities, abilitiesReason is null, abilitiesReason),
            HackTableStatus.Of(HackTableStatus.LevelUp, levelUp.Count > 0, "no level-up learnsets were found in gSpeciesInfo or as a pointer table"),
        ];
        return new HackDataResult(HackTables.Finish(new HackData
        {
            RomSha1 = rom.Sha1,
            Reader = ReaderRhh,
            RhhVersion = rhh.Version,
            LevelUp = levelUp,
            Species = species,
            Moves = moves,
            Items = items,
            Abilities = abilities,
            Notes = notes,
        }, HackTables.Gba, rom.GameCode, Gen3Engine.Expansion, status), null);
    }

    private static List<string> NamedRun(GbaImage rom, int table, NamedLayout layout, int bound)
    {
        var names = RhhTables.Names(rom, table, layout, bound);
        if (names.Count == 0)
            return names;
        var last = 0;
        var gap = 0;
        for (var i = 1; i < names.Count && gap <= RhhTables.DisabledTail; i++)
        {
            if (names[i].Length == 0)
            {
                gap++;
                continue;
            }
            gap = 0;
            last = i;
        }
        return names.GetRange(0, last + 1);
    }

    private static HackDataResult? ReadInline(GbaImage rom, GfRomHeader header, int speciesTable, TableHit? abilityNamesHit, IdMapper mapper, List<string> notes)
    {
        var speciesLayout = RhhTables.Probe(rom, speciesTable, RhhTables.SpeciesProbe, [RhhTables.SpeciesNameLength, Gen3Tables.SpeciesNameStride]);
        if (speciesLayout is null || !RhhTables.HasBulbasaurStats(rom, speciesTable, speciesLayout.Stride))
            return null;
        int?[] tables = [speciesTable, header.Moves, header.Items, header.SpeciesNames, header.MoveNames, header.AbilityNames, abilityNamesHit?.Offset];
        notes.Add($"the species names are read inline from the species-info table at 0x{speciesTable:X}: stride {speciesLayout.Stride}, name at +0x{speciesLayout.NameOffset:X}{(header.NamesInline ? " (the GF header has no name pointers)" : "")}");

        var speciesNames = NamedRun(rom, speciesTable, speciesLayout, Gen3Tables.Bound(speciesTable, speciesLayout.Stride, tables));
        var species = new List<HackSpecies>(speciesNames.Count);
        for (var i = 0; i < speciesNames.Count; i++)
        {
            var b = speciesTable + i * speciesLayout.Stride;
            if (!rom.Has(b, Math.Max(speciesLayout.Stride, 0x09)))
                break;
            species.Add(new HackSpecies(i, speciesNames[i], i == 0 || speciesNames[i].Length == 0 ? null : mapper.Species(speciesNames[i]), 0,
                [rom.U8(b + 0x06), rom.U8(b + 0x07)],
                [rom.U8(b), rom.U8(b + 1), rom.U8(b + 2), rom.U8(b + 3), rom.U8(b + 4), rom.U8(b + 5)],
                [], null, 0, rom.U8(b + 0x08), null) { GenderRead = false });
        }
        if (species.Count < 2)
            return null;

        var moves = new List<HackMove>();
        string? movesReason;
        if (header.Moves is { } moveTable && RhhTables.Probe(rom, moveTable, RhhTables.MoveProbe, [17, Gen3Tables.MoveNameStride]) is { } moveLayout)
        {
            var names = NamedRun(rom, moveTable, moveLayout, Gen3Tables.Bound(moveTable, moveLayout.Stride, tables));
            var nationals = names.Select((n, i) => i == 0 || n.Length == 0 ? null : mapper.Move(i, n)).ToList();
            var stride = moveLayout.Stride;
            if (MoveAnchors.Find(names.Count, i => rom.Has(moveTable + (i * stride), stride) ? rom.Slice(moveTable + (i * stride), stride).ToArray() : null, nationals, out var fieldsWhy, [RhhTables.NameBits(moveLayout)]) is { } fields)
            {
                moves = MovesFrom(rom, moveTable, stride, names, nationals, fields, TypeRule(Gen3Engine.Of(rom, ReaderGfInline)), notes);
                movesReason = fields.Type is null ? $"move power, accuracy and PP are read inline at stride {stride}, but no field agrees with PKHeX's move types, so the type is not read" : null;
                notes.Add($"move names inline at stride {stride}; {fields.Note}");
            }
            else
            {
                for (var i = 0; i < names.Count; i++)
                    moves.Add(new HackMove(i, names[i], nationals[i], 0, 0, 0, 0));
                movesReason = $"move names only, read inline at stride {stride}; move power, type, accuracy and PP are not read at this layout: {fieldsWhy}";
            }
        }
        else
        {
            movesReason = "no stride of the GF header's move table puts Pound, Karate Chop and Double Slap at entries 1 to 3";
        }
        if (movesReason is not null)
            notes.Add(movesReason);

        var items = new List<HackItem>();
        string? itemsReason = null;
        if (header.Items is { } itemTable
            && (RhhTables.Probe(rom, itemTable, RhhTables.ItemProbe, FrTableFollower.ItemNameLengths) ?? RhhTables.ProbeAmong(rom, itemTable, RhhTables.ItemProbe, FrTableFollower.ItemNameLengths, RhhTables.ItemProbeWindow)) is { } itemLayout)
        {
            var names = NamedRun(rom, itemTable, itemLayout, Gen3Tables.Bound(itemTable, itemLayout.Stride, tables));
            for (var i = 0; i < names.Count; i++)
                items.Add(new HackItem(i, names[i], i == 0 || names[i].Length == 0 ? null : mapper.ItemByName(names[i]), 0, 0));
            notes.Add($"item stride {itemLayout.Stride}, name at +0x{itemLayout.NameOffset:X}; items map by name only");
        }
        else
        {
            itemsReason = $"no stride of the GF header's item table puts Master Ball and Ultra Ball among entries 1 to {RhhTables.ItemProbeWindow}";
            notes.Add(itemsReason);
        }

        var abilities = new List<HackAbility>();
        string? abilitiesReason = null;
        if (abilityNamesHit is not null)
        {
            var names = NamesOf(rom, abilityNamesHit, tables);
            for (var i = 0; i < names.Count; i++)
                abilities.Add(new HackAbility(i, names[i], i == 0 ? null : mapper.Ability(i, names[i])));
        }
        else
        {
            abilitiesReason = "the ability name table was found neither by its anchor (STENCH DRIZZLE) nor through the GF header";
            notes.Add($"abilities are not read: {abilitiesReason}");
        }

        var levelUp = new Dictionary<int, IReadOnlyList<HackLearn>>();
        var learnMoves = Math.Max(moves.Count, IdMapper.Gen3LastMove + 1);
        if ((Gen3Learnsets.FindField(rom, speciesTable, speciesLayout.Stride, species.Count, learnMoves) ?? Gen3Learnsets.FindTable(rom, species.Count, learnMoves)) is { } learnTable)
        {
            levelUp = Gen3Learnsets.Read(rom, learnTable, speciesLayout.Stride, learnMoves);
            notes.Add($"level-up learnsets: {learnTable.Format.ToString().ToLowerInvariant()} {(learnTable.FieldOffset is { } lf ? $"lists through the species info +0x{lf:X}" : $"pointer table at 0x{learnTable.Offset:X}")}, {levelUp.Count} of {learnTable.Count} species");
            if (Gen3Learnsets.Why(learnTable) is { } why)
                notes.Add(why);
        }
        else
        {
            notes.Add("no level-up learnsets were found in the species info or as a pointer table");
        }

        HackTableStatus[] status =
        [
            new HackTableStatus(HackTableStatus.Species, HackTableStatus.Read, null, true),
            new HackTableStatus(HackTableStatus.Moves, moves.Count == 0 ? HackTableStatus.Missing : movesReason is null ? HackTableStatus.Read : HackTableStatus.Partial, movesReason),
            HackTableStatus.Of(HackTableStatus.Items, itemsReason is null, itemsReason),
            HackTableStatus.Of(HackTableStatus.Abilities, abilitiesReason is null, abilitiesReason),
            HackTableStatus.Of(HackTableStatus.LevelUp, levelUp.Count > 0, "no level-up learnsets were found in the species info or as a pointer table"),
        ];
        return new HackDataResult(HackTables.Finish(new HackData
        {
            RomSha1 = rom.Sha1,
            Reader = ReaderGfInline,
            LevelUp = levelUp,
            Species = species,
            Moves = moves,
            Items = items,
            Abilities = abilities,
            Notes = notes,
        }, HackTables.Gba, rom.GameCode, Gen3Engine.Of(rom, ReaderGfInline), status), null);
    }

    private static int? VotedType(FieldVoteResult vote, ReadOnlySpan<byte> row) =>
        vote.Value(row) is { } t and >= 0 and <= byte.MaxValue ? Gen3LayoutFinder.RawType((byte)t) : null;

    private static string Hex(int? offset) => offset is { } o ? $"0x{o:X}" : "?";

    private static string At(int? offset) => offset is { } o ? $"+0x{o:X}" : "unread";
}
