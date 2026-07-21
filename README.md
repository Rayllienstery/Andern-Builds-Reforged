# Andern Builds Reforged

Curated PMC weapon/gear presets for **SPT-AKI 4.0.13**, forked from [BarlogM Andern](https://github.com/barlog-m/spt-andern) (MIT).

This fork keeps Andern’s tiered preset engine and adds map-aware picks, exact spare mags, weighted spawns, ammo overrides / mag load presets, startup integrity checks, and the former **Raylee-AndernPmcPatch** logic in one DLL.

| | |
|--|--|
| **Version** | **0.10.4** |
| GitHub | https://github.com/Rayllienstery/Andern-Builds-Reforged |
| Changelog | [CHANGELOG.md](CHANGELOG.md) |
| Staging | `c:\Games\SPT\dev\Andern-Builds-Reforged` |
| Live | `SPT/user/mods/Andern-Builds-Reforged/` |
| Preset builder | `mods-src/ander-presets/build_t4_presets.py` |

**Do not** run `BarlogM-Andern` or `Raylee-AndernPmcPatch` alongside this mod.

---

## Identity

| Field | Value |
|-------|-------|
| ModGuid | `com.raylee.andern-builds-reforged` |
| Assembly | `Andern-Builds-Reforged.dll` |
| Author | Raylee |
| SPT | `~4.0.0` (tested **4.0.13**) |
| Incompatible with | `li.barlog.andern` |
| License | MIT |

## Versioning (`x.y.z`)

| Segment | When to bump |
|---------|----------------|
| **x** | Only when you explicitly ask for a major bump |
| **y** | New features or significant behavior change |
| **z** | Bugfixes / small corrections |

Release updates touch `ModVersion`, [CHANGELOG.md](CHANGELOG.md), and this README together.

---

## Quick start

1. Disable **BarlogM-Andern** and **Raylee-AndernPmcPatch** in the SPT launcher.
2. Ensure this mod folder exists under `user/mods/Andern-Builds-Reforged/`.
3. Stop **SPT Server**, then from staging:

```powershell
powershell -ExecutionPolicy Bypass -File deploy.ps1
```

4. Start SPT Server — log should show `[Andern-Builds-Reforged] v0.9.1 loaded`.
5. After any preset / `ammo.json5` / `gear.json5` / DLL change → **restart SPT Server**.

---

## What this mod does

### Tiered PMC presets

| Tier folder | Typical levels (`config.json5`) | Role |
|-------------|----------------------------------|------|
| `presets/meta/one` | 1–14 | Soft armor, early guns, soft ammo |
| `presets/meta/two` | 15–31 | Mid kits |
| `presets/meta/three` | 32–41 | High mid |
| `presets/meta/four` | 42–99 | Endgame curated T4 |

Active preset pack is selected in `config/config.json5` → `"Preset": "meta"`.

**T1 balance (0.8.0+):** soft body armor only (Module-3M, PACA, Kora, rare Zhuk-Press / 6B23-1). No plate carriers / AVS / Trooper. G28, SCAR-H, ISO Hemlock are **not** on T1 (T2+). Faction endgame gear does not upgrade bots below level 32.

**T4 source of truth:** edit `mods-src/ander-presets/build_t4_presets.py`, regenerate, run optic audits. **Never hand-edit** live `presets/meta/four/*.json`.

```powershell
python mods-src/ander-presets/build_t4_presets.py
python mods-src/ander-presets/scripts/audit_optics.py
python mods-src/ander-presets/scripts/audit_lpvo_mounts.py
```

Both audits must exit **0**.

---

## Preset JSON fields

All fields below are optional unless noted. Example:

```json
{
  "Name": "M249 T4",
  "Locations": ["woods", "lighthouse"],
  "SpareMags": 1,
  "Weight": 1,
  "PrimaryAmmoTpl": "54527ac44bdc2d36668b4567",
  "SpareAmmoTpl": "59e6906286f7746c9f75e847",
  "Factions": ["usec"],
  "Items": [ ... ]
}
```

### `Locations` — map allow-list (since 0.2.0)

| Value | Behavior |
|-------|----------|
| omitted / `null` / `[]` | Allowed on **all** maps |
| non-empty list | Raid map must match (aliases expanded) |
| empty pool after filter | Fall back to full tier + server warning |

| Alias | Expands to |
|-------|------------|
| `factory` | `factory4_day`, `factory4_night` |
| `streets` | `tarkovstreets` |
| `customs` | `bigmap` |
| `reserve` | `rezervbase` |
| `groundzero` / `gz` | `sandbox`, `sandbox_high` |

Builder seeds open-map bolts/snipers and CQ KS-23 locations via `apply_locations_metadata()`.

### `SpareMags` — exact loose magazines (since 0.3.0)

Does **not** use vanilla PMC bot magazine weights (those flood vests).

| Value | Meaning |
|-------|---------|
| `3` | three spare mags (same tpl as mounted) |
| `0` | gun mag only |
| omitted | capacity heuristic: drum ≥61 → 1, mid 50–60 → 2, else → 3 |

Builder defaults: LMG/drums `1`, sniper/shotgun `2`, AR/SMG/DMR `3`.

### `Weight` — spawn weight (since 0.4.0 / scale 0.8.3)

Relative weight inside the **location-filtered** tier pool. Higher = more often.

| Value | Meaning |
|-------|---------|
| omitted / `≤0` | **1000** |
| `1000` | baseline |
| `3000` | shotgun mild ~3× boost |
| `1` | rare (e.g. M249) |
| `500` | Unheard SCAR T1 (showcase, rarer than baseline) |

Chance ≈ `Weight / sum(Weights of presets allowed on that map)`.

### Ammo on the preset — skip `ammo.json5` (since 0.9.0)

If set, this build **ignores** the tier caliber pool in `ammo.json5`.

**Solid:**

```json
"PrimaryAmmoTpl": "59e6906286f7746c9f75e847",
"SpareAmmoTpl": "54527a984bdc2d4e668b4567"
```

- `PrimaryAmmoTpl` → chamber + mounted magazine  
- `SpareAmmoTpl` → loose spare mags (defaults to primary if omitted)

**Mixed mag** (vanilla *Load from Preset* style — first stack feeds first; `count: -1` = fill remaining capacity):

```json
"AmmoLoad": [
  { "id": "59e6906286f7746c9f75e847", "count": 5 },
  { "id": "54527a984bdc2d4e668b4567", "count": -1 }
],
"SpareAmmoTpl": "54527a984bdc2d4e668b4567"
```

Optional: `SpareAmmoLoad` for mixed spare magazines.

### `Factions`

Emitted by the builder (`bear` / `usec`). Runtime faction filtering uses `faction_weapons.json` / `faction_gear.json` (see PMC post-process). Optional JSON-field filter on presets is still on the roadmap.

---

## `ammo.json5` (per tier)

Path: `presets/meta/{one,two,three,four}/ammo.json5`

### Weighted solid cartridges (since 0.8.2)

```json5
Caliber556x45NATO: [
  { id: "54527a984bdc2d4e668b4567", weight: 3000 }, // M855 ~75%
  { id: "59e6906286f7746c9f75e847", weight: 1000 }, // M856A1 ~25%
],
```

Missing / `≤0` weight → **1000**. Pick is weighted within the caliber. If a tier lacks a caliber, generation falls back to another tier; if still missing, weapon gen aborts cleanly (no empty ammo crash).

### Mag load presets (since 0.9.0)

A pool entry can be a **load preset** with the **same weight scale** as one solid type:

```json5
{
  weight: 1000,
  load: [
    { id: "59e6906286f7746c9f75e847", count: 5 },   // tip / first to fire
    { id: "54527a984bdc2d4e668b4567", count: -1 },  // fill rest
  ],
}
```

- `count: -1` = fill remaining capacity once.
- If **every** `count` is &gt;0 and the pattern is shorter than the mag, the pattern is **repeated** until full (T1 example: 1×M856 + 3×M855).

Optional on a load entry: `spareId` / `spareLoad` for spare magazines.

T1 `Caliber556x45NATO` pool: solid M855 (`3000`) / M856 (`1000`) / repeating 1+3 load (`2000`).

---

## Config: mask instead of helmet (since 0.5.0)

`AndernBuildsReforged/config/config.json5` (SPT mod config UI):

| Key | Default |
|-----|---------|
| `MaskInsteadOfHelmetPercentOne` | 0 |
| `MaskInsteadOfHelmetPercentTwo` | 0 |
| `MaskInsteadOfHelmetPercentThree` | 3 |
| `MaskInsteadOfHelmetPercentFour` | 6 |

On success (and not an NVG raid), PMC gets `gear.mask` FaceCover (T4: Atomic Defense **CQCM**, armor class 4) + headset instead of a helmet. Replaces upstream’s old hardcoded ~30% roll.

Other keys in the same file control trader/insurance/map bot helpers (Andern upstream options) — leave them unless you know what you want.

---

## PMC post-process (since 0.6.0)

Former **Raylee-AndernPmcPatch** is merged into this assembly (`AndernBuildsReforged/Pmc/`). Runs after inventory generation. Faction pools ship next to the DLL:

- `faction_weapons.json`
- `faction_gear.json`

| Behavior | Notes |
|----------|--------|
| **Faction filter** | USEC/BEAR weapon & gear pools; endgame faction gear skipped below lvl 32; gear applied before weapons/loot |
| **Close-quarters bolt ban** | Streets + Factory: strip / reroll bolt snipers (`SNIPER_RIFLE`) |
| **Open-map shotgun limit** | Cap shotgun spam on open maps |
| **Factory biases** | KS-23 / shotgun bias |
| **Map weapon biases** | Surgeon 1581, KATT AMR, SV-98, hunting .762 (from lvl 15), X95 / Mk18 REAP thermals |
| **M249AmmoFix** | Hardcoded ammo for M249 / RPD / KS-23 / Surgeon / AMR / Mk18 / hunting .762 hosts (safety net; prefer preset `PrimaryAmmoTpl` / `SpareAmmoTpl`) |
| **SpareMagazineLimiter** | Safety cap if spare counts still explode |

---

## Startup integrity (since 0.7.0)

After PostDB, every weapon-preset item tpl, `gear.json5` entry, `modules.json5` alt, `ammo.json5` tpl (including load stacks), and faction-pool id is checked against the item DB.

- Missing template → **red** log line  
- That preset / gear / module / ammo / pool entry is **disabled or pruned** so it cannot crash bot gen  

Example (typo tpls — fixed in 0.9.1):

```text
[Andern-Builds-Reforged] REMOVED gear `Armor` tpl `…` (tier `one`): not in item DB
[Andern-Builds-Reforged] integrity summary: disabled 0 weapon preset(s), pruned 2 gear / …
```

---

## Tools

### Spawn chance report

```powershell
python mods-src/ander-presets/scripts/report_preset_chances.py
python mods-src/ander-presets/scripts/report_preset_chances.py -l factory4_day -l tarkovstreets
```

Per location: preset name, primary weapon display name, spawn %.

### Optic audits (required after T4 regenerate)

```powershell
python mods-src/ander-presets/scripts/audit_optics.py
python mods-src/ander-presets/scripts/audit_lpvo_mounts.py
```

---

## Build & deploy

```powershell
# Build only
dotnet build AndernBuildsReforged\AndernBuildsReforged.csproj -c Release

# Full deploy (stop SPT Server first if DLL is locked)
powershell -ExecutionPolicy Bypass -File deploy.ps1
```

`deploy.ps1` builds Release, copies DLL + `fastJSON5.dll`, and syncs `config/`, `presets/`, `trader/`, faction JSON, LICENSE, README, CHANGELOG into `SPT/user/mods/Andern-Builds-Reforged/`.

If copy fails with “file in use” → stop **SPT Server** and rerun.

### Sync upstream Andern

```powershell
git fetch upstream
git merge upstream/main
```

---

## Feature history (summary)

Full notes: [CHANGELOG.md](CHANGELOG.md).

| Ver | Highlights |
|-----|------------|
| **0.10.0** | T1 5.56: 1×M856+3×M855 repeating load; load-pattern tiling |
| **0.9.1** | Fixed T1 Module-3M / PACA armor tpl typos |
| **0.9.0** | Preset ammo override + mag `load` presets in `ammo.json5` |
| **0.8.3** | Weight baseline **1000**; staging T1 ammo weighted |
| **0.8.2** | Weighted `{ id, weight }` ammo pools |
| **0.8.1** | T1–T3 9×19 / .45 ACP ammo (Vityaz / UMP crash fix) |
| **0.8.0** | T1 soft-armor nerf; faction gear gate; no T1 G28/SCAR-H/Hemlock |
| **0.7.0** | Startup integrity prune |
| **0.6.0** | Merged Raylee PMC post-processors |
| **0.5.0** | Per-tier CQCM / mask % |
| **0.4.0** | `Weight` + chance report script |
| **0.3.0** | Exact `SpareMags` |
| **0.2.0** | Map `Locations` allow-list |
| **0.1.0** | Fork / rebrand / deploy layout |

---

## Roadmap

- [x] `Locations` allow-list
- [x] Exact `SpareMags`
- [x] Integer `Weight` + chance report
- [x] Per-tier mask / CQCM %
- [x] Merge Raylee PMC post-processors; single-mod cutover
- [x] Startup integrity for missing item tpls
- [x] Weighted ammo + preset / load-preset ammo overrides
- [ ] Optional runtime filter on preset `Factions` field (pools already exist)

---

## License

MIT — see [LICENSE](LICENSE). Upstream copyright Barlog_M; this fork’s changes are documented in [CHANGELOG.md](CHANGELOG.md).
