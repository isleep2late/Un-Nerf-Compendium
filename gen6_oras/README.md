# Omega Ruby / Alpha Sapphire

Scripts that rewrite a decrypted `.3ds`/`.cia` in place (each validates the original bytes before writing):
`oras_nobanlist.py`, `oras_no_restrictions.py`, `oras_evcap.py`, `oras_xerneas_ability.py`, `formepersist.py`,
`resort_itemform_persist.py`; the Arceus form-typing code patch is `../gen67_arceus_typefix/code_patch/s2_oras_build.py`.
The `apply_*.bat` files are Windows wrappers. The repo root README describes each feature.

`azahar-mods/` holds the same edits as Azahar `load/mods` IPS/BPS patches (per feature and merged), derived by
running these scripts and diffing the files they change; its README has the layout, the base-file hashes and the
update caveat.
