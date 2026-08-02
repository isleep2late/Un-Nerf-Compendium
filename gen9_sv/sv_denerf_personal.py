#!/usr/bin/env python3
# PKHaX / Un-Nerf Compendium - Scarlet/Violet base stat de-nerf patcher.
#
# Restores the version 1.0.0 base stats that the day-one 1.0.1 update nerfed
# (the Treasures of Ruin: Wo-Chien, Chien-Pao, Ting-Lu, Chi-Yu - the only
# Pokemon ever to have base stats changed by a patch), and optionally the
# Generation 8 stats that the Gen 9 transition lowered (Zacian, Zamazenta,
# Cresselia).
#
# Operates on an EXTRACTED romfs personal data FlatBuffer:
#     romfs:/avalon/data/personal_array.bin
# (inside data.trpfs; extract/repack with pkNX or your preferred trpfs tool).
# The stats live in an inline FlatBuffer struct, so patching is exact-size and
# fully in place; nothing else in the file changes.
#
# Usage:
#     python3 sv_denerf_personal.py personal_array.bin                # Ruin quartet only
#     python3 sv_denerf_personal.py personal_array.bin --gen8-legends # + Zacian/Zamazenta/Cresselia
#     python3 sv_denerf_personal.py --selftest                       # verify the parser on synthetic data
#
# A timestamped .bak is written next to the input before any modification.
# The script refuses to write unless the file parses as the SV PersonalTable
# schema, the Pikachu control entry matches, and every target either matches
# its expected patched stats (then it is restored) or already matches the
# restored stats (then it is skipped).
#
# Schema reference (clean-room, from the open-source pkNX project's published
# FlatBuffer schemas for SV): PersonalTable { Table:[PersonalInfo] } where
# PersonalInfo field 0 = Info struct (SpeciesInternal u16, Form u16,
# SpeciesNational u16, ...) and field 22 = Base struct
# (HP,ATK,DEF,SPA,SPD,SPE as six consecutive bytes).

import struct
import sys
import time

INFO_FIELD = 0
BASE_FIELD = 22

# (national dex, form): (current post-nerf stats, restored stats)  [HP,ATK,DEF,SPA,SPD,SPE]
RUIN = {
    (1001, 0): ((85, 85, 100, 95, 135, 70), (85, 90, 100, 100, 135, 70)),   # Wo-Chien  (v1.0.0: +5 Atk, +5 SpA)
    (1002, 0): ((80, 120, 80, 90, 65, 135), (80, 130, 80, 90, 65, 135)),    # Chien-Pao (v1.0.0: +10 Atk)
    (1003, 0): ((155, 110, 125, 60, 80, 45), (165, 110, 130, 55, 80, 45)),  # Ting-Lu   (v1.0.0: +10 HP, +5 Def, -5 SpA)
    (1004, 0): ((55, 80, 80, 135, 120, 100), (55, 80, 80, 145, 120, 100)),  # Chi-Yu    (v1.0.0: +10 SpA)
}
GEN8_LEGENDS = {
    (888, 0): ((92, 120, 115, 80, 115, 138), (92, 130, 115, 80, 115, 138)),  # Zacian
    (888, 1): ((92, 150, 115, 80, 115, 148), (92, 170, 115, 80, 115, 148)),  # Zacian-Crowned
    (889, 0): ((92, 120, 115, 80, 115, 138), (92, 130, 115, 80, 115, 138)),  # Zamazenta
    (889, 1): ((92, 120, 140, 80, 140, 128), (92, 130, 145, 80, 145, 128)),  # Zamazenta-Crowned
    (488, 0): ((120, 70, 110, 75, 120, 85), (120, 70, 120, 75, 130, 85)),    # Cresselia
}
CONTROL = {(25, 0): (35, 55, 40, 50, 50, 90)}  # Pikachu, unchanged in every SV version

NAMES = {
    1001: 'Wo-Chien', 1002: 'Chien-Pao', 1003: 'Ting-Lu', 1004: 'Chi-Yu',
    888: 'Zacian', 889: 'Zamazenta', 488: 'Cresselia', 25: 'Pikachu',
}


def u16(b, o):
    return struct.unpack_from('<H', b, o)[0]


def i16(b, o):
    return struct.unpack_from('<h', b, o)[0]


def u32(b, o):
    return struct.unpack_from('<I', b, o)[0]


def i32(b, o):
    return struct.unpack_from('<i', b, o)[0]


def table_field_offset(buf, table_pos, field_id):
    vtable_pos = table_pos - i32(buf, table_pos)
    vtable_len = u16(buf, vtable_pos)
    slot = 4 + 2 * field_id
    if slot + 2 > vtable_len:
        return None
    voff = u16(buf, vtable_pos + slot)
    if voff == 0:
        return None
    return table_pos + voff


def iter_personal_entries(buf):
    root = u32(buf, 0)
    vec_field = table_field_offset(buf, root, 0)
    if vec_field is None:
        raise ValueError('PersonalTable.Table vector missing')
    vec_pos = vec_field + u32(buf, vec_field)
    count = u32(buf, vec_pos)
    for i in range(count):
        elem_field = vec_pos + 4 + 4 * i
        entry_pos = elem_field + u32(buf, elem_field)
        info_off = table_field_offset(buf, entry_pos, INFO_FIELD)
        base_off = table_field_offset(buf, entry_pos, BASE_FIELD)
        if info_off is None or base_off is None:
            continue
        species = u16(buf, info_off + 4)
        form = u16(buf, info_off + 2)
        yield species, form, base_off


def read_stats(buf, base_off):
    return tuple(buf[base_off:base_off + 6])


def apply(path, include_legends):
    data = bytearray(open(path, 'rb').read())
    targets = dict(RUIN)
    if include_legends:
        targets.update(GEN8_LEGENDS)

    found = {}
    for species, form, base_off in iter_personal_entries(data):
        key = (species, form)
        if key in targets or key in CONTROL:
            found.setdefault(key, []).append(base_off)

    for key, expected in CONTROL.items():
        offs = found.get(key)
        if not offs:
            raise SystemExit('control entry %s not found - not an SV personal_array.bin?' % (key,))
        got = read_stats(data, offs[0])
        if got != expected:
            raise SystemExit('control entry %s stats %s do not match known values %s - refusing to patch' % (key, got, expected))

    planned = []
    for key, (current, restored) in targets.items():
        offs = found.get(key)
        if not offs:
            raise SystemExit('%s (species %d form %d) not found in the table' % (NAMES.get(key[0], '?'), key[0], key[1]))
        for off in offs:
            got = read_stats(data, off)
            if got == restored:
                print('%-18s form %d: already restored (%s)' % (NAMES.get(key[0], '?'), key[1], '/'.join(map(str, got))))
            elif got == current:
                planned.append((key, off, restored, got))
            else:
                raise SystemExit('%s form %d has unexpected stats %s (expected %s or %s) - wrong game version or modified file; refusing to patch'
                                 % (NAMES.get(key[0], '?'), key[1], got, current, restored))

    if not planned:
        print('Nothing to do.')
        return

    backup = path + '.bak-' + time.strftime('%Y%m%d-%H%M%S')
    open(backup, 'wb').write(open(path, 'rb').read())
    print('backup written:', backup)
    for key, off, restored, got in planned:
        data[off:off + 6] = bytes(restored)
        print('%-18s form %d: %s -> %s' % (NAMES.get(key[0], '?'), key[1], '/'.join(map(str, got)), '/'.join(map(str, restored))))
    open(path, 'wb').write(data)
    print('patched %d entries in place; file size unchanged (%d bytes)' % (len(planned), len(data)))


def build_synthetic():
    INFO_SIZE = 24
    def build_entry(species, form, stats):
        vtable_len = 4 + 2 * (BASE_FIELD + 1)
        voffs = [0] * (BASE_FIELD + 1)
        voffs[INFO_FIELD] = 4
        voffs[BASE_FIELD] = 4 + INFO_SIZE
        table_len = 4 + INFO_SIZE + 6
        vtable = struct.pack('<HH', vtable_len, table_len) + struct.pack('<%dH' % len(voffs), *voffs)
        info = struct.pack('<HHHBBHHIII', species, form, species, 0, 0, 0, 0, 0, 0, 0)
        table = struct.pack('<i', vtable_len) + info + bytes(stats)
        return vtable + table, len(vtable)

    specs = [(25, 0, CONTROL[(25, 0)]), (1001, 0, RUIN[(1001, 0)][0]), (1003, 0, RUIN[(1003, 0)][0])]
    blob = bytearray()
    blob += struct.pack('<I', 0)
    vt_root = len(blob)
    blob += struct.pack('<HHH', 6, 8, 4)
    root_pos = len(blob)
    struct.pack_into('<I', blob, 0, root_pos)
    blob += struct.pack('<i', root_pos - vt_root)
    vec_field_pos = len(blob)
    blob += struct.pack('<I', 0)
    vec_pos = len(blob)
    struct.pack_into('<I', blob, vec_field_pos, vec_pos - vec_field_pos)
    blob += struct.pack('<I', len(specs))
    elem_base = len(blob)
    blob += b'\x00' * (4 * len(specs))
    for i, (species, form, stats) in enumerate(specs):
        chunk, vt_len = build_entry(species, form, stats)
        entry_table_pos = len(blob) + vt_len
        struct.pack_into('<I', blob, elem_base + 4 * i, entry_table_pos - (elem_base + 4 * i))
        blob += chunk
    return bytes(blob)


def selftest():
    global RUIN
    import tempfile
    import os
    blob = build_synthetic()
    entries = {(s, f): read_stats(blob, o) for s, f, o in iter_personal_entries(blob)}
    assert entries[(25, 0)] == CONTROL[(25, 0)], entries
    assert entries[(1001, 0)] == RUIN[(1001, 0)][0], entries
    assert entries[(1003, 0)] == RUIN[(1003, 0)][0], entries
    fd, path = tempfile.mkstemp(suffix='.bin')
    os.write(fd, blob)
    os.close(fd)
    saved = RUIN
    RUIN = {k: v for k, v in saved.items() if k in ((1001, 0), (1003, 0))}
    try:
        apply(path, include_legends=False)
        patched = open(path, 'rb').read()
        result = {(s, f): read_stats(patched, o) for s, f, o in iter_personal_entries(patched)}
        assert result[(1001, 0)] == saved[(1001, 0)][1], result
        assert result[(1003, 0)] == saved[(1003, 0)][1], result
        assert result[(25, 0)] == CONTROL[(25, 0)], result
        apply(path, include_legends=False)
    finally:
        RUIN = saved
        os.unlink(path)
        for f in os.listdir(os.path.dirname(path)):
            if f.startswith(os.path.basename(path) + '.bak-'):
                os.unlink(os.path.join(os.path.dirname(path), f))
    print('selftest OK: parser walked the FlatBuffer, verified the control entry, restored both targets in place, and re-run was a no-op')


def main():
    args = [a for a in sys.argv[1:]]
    if '--selftest' in args:
        selftest()
        return
    include_legends = '--gen8-legends' in args
    files = [a for a in args if not a.startswith('--')]
    if len(files) != 1:
        print(__doc__ or 'usage: sv_denerf_personal.py personal_array.bin [--gen8-legends] | --selftest')
        raise SystemExit(2)
    apply(files[0], include_legends)


if __name__ == '__main__':
    main()
