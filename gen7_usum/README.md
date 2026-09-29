# Ultra Sun / Ultra Moon

`unnerf.py` (modes `nbl`, `prankster`, `galewings`, `parentalbond`, `souldew`, `all`; `gametext.py` is its text
helper) rewrites a decrypted `.cia` in place, as do `formepersist.py` and `resort_itemform_persist.py`; the
Arceus/Silvally form-typing code patch is `../gen67_arceus_typefix/code_patch/` (`s2_arceus_final.py` for Ultra
Moon, `s2_arceus_port.py` for Ultra Sun). The numbered `.bat` files are Windows wrappers. The repo root README
describes each feature.

`azahar-mods/` holds the same edits as Azahar `load/mods` IPS/BPS patches (per feature and merged), derived by
running these scripts and diffing the files they change; its README has the layout, the base-file hashes and the
update caveat.
