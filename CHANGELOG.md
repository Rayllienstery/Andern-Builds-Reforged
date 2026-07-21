# Changelog

All notable changes to **Andern Builds Reforged** are documented in this file.

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

Versioning is `x.y.z`:

| Segment | Bump when |
|---------|-----------|
| **x** | User explicitly requests a major bump |
| **y** | New features or significant behavior change |
| **z** | Bugfixes / small corrections |

## [0.10.4] — 2026-07-21

### Fixed

- T1/T2 5.45 AK presets: `modules.json5` no longer swaps mounted/spare mags to 6L31 60-round drums; only 30-round alts (6L20 / 6L23 / PMAG).

## [0.10.3] — 2026-07-20

### Fixed

- Empty mounted magazines (`0/25`, `0/30`): `FillMagazine` now always runs Andern load expander, then SPT fill, then a forced `CreateCartridges` fallback; logs when a mag stays empty.
- `MagAmmoSanity` post-pass refills empty primary mags and tops up missing spares (vest → pockets → backpack) after loot/biases.

## [0.10.2] — 2026-07-20

### Fixed

- T1 (and other) PMC spare magazines missing: SPT MagGen only tries vest/pockets. After a shortfall we now top up via **vest → pockets → backpack**. Applies to initial weapon gen, faction re-rolls, and map biases.

## [0.10.1] — 2026-07-20

### Fixed

- Empty magazines / missing spare mags: solid ammo again uses SPT `FillMagazineWithCartridge`; custom load fill falls back if it writes no cartridges.
- Faction weapon re-roll now uses exact `SpareMags` (not bot JSON magazine weights) and applies spare ammo policy.

## [0.10.0] — 2026-07-20

### Added

- Mag load patterns with **only fixed counts** (no `count: -1`) are **tiled** to magazine capacity (e.g. 1×M856 + 3×M855 repeats until full).
- T1 `Caliber556x45NATO`: solid M855/M856 (3:1 weights) plus a **1×M856 + 3×M855** repeating load preset (`weight: 2000`).

### Changed

- M16A2 T1 no longer forces `PrimaryAmmoTpl` M855 — uses T1 `ammo.json5` pool like ADAR/M4/AUG/SCAR.
- **T1–T3 backpack pools** retuned by capacity (T1 had wrong tpl: “Flyye” was actually SSO Attack 2 / 35 slots):
  - **T1**: Scav / Flyye MBSS / duffle / Transformer / VKBO / sling (+ rare Sanitar / Berkut)
  - **T2**: Berkut / Day Pack / Pillbox / 3Day / T20 (+ uncommon Takedown; no F5 / Mechanism)
  - **T3**: Beta 2 / T20 / F5 / Takedown / 3Day (+ uncommon Mechanism / T30)

## [0.9.1] — 2026-07-20

### Fixed

- T1 `gear.json5` Armor: corrected typo tpls for **Module-3M** (`…1095`) and **PACA** (`…4583`) — integrity validator was pruning them as missing.

## [0.9.0] — 2026-07-20

### Added

- **Preset ammo override**: `PrimaryAmmoTpl` / `SpareAmmoTpl` on a weapon preset skip `ammo.json5` for that build. Optional `AmmoLoad` / `SpareAmmoLoad` for mixed mag stacks (vanilla “Load from Preset”).
- **Mag load presets in `ammo.json5`**: an entry may be `{ weight, load: [{ id, count }, ...] }` instead of a solid `{ id, weight }`. Same weight scale as a single cartridge type. `count: -1` fills remaining capacity. Optional `spareId` / `spareLoad` for spare magazines.

### Changed

- Weapon generation resolves ammo via `ResolvedAmmo` (chamber + primary load + spare policy). Spare mags are rewritten when spare differs from chamber.

## [0.8.3] — 2026-07-20

### Fixed

- Staging T1 `ammo.json5` was still the legacy string-array format (no `weight`) — synced to weighted `{ id, weight }` like T2–T4.
- `presets/test/all/ammo.json5` updated to the same weighted format.
- T1–T3 weapon presets with legacy `Weight: 1` promoted to **1000** (same scale as ammo). Unheard SCAR T1 stays **500**.

### Changed

- Default weapon preset weight **1000** (was 1). Shotguns **3000** (~3×). M249 stays rare at **1**.
- Missing/`≤0` weapon `Weight` in C# now defaults to **1000**.

## [0.8.2] — 2026-07-20

### Added

- Weighted ammo pools in `presets/meta/{one,two,three,four}/ammo.json5`: each cartridge is `{ id, weight }`. Missing/≤0 weight → **1000**. Pick uses `WeightedRandomHelper` within the caliber.

### Changed

- T2/T3 `Caliber556x45NATO`: M855 weight `3000` / M856A1 `1000` (same ~75/25 as old duplicate-list trick).

## [0.8.1] — 2026-07-20

### Fixed

- T1/T2/T3 `ammo.json5`: added missing `Caliber9x19PARA` (Vityaz) and `Caliber1143x23ACP` (UMP) — empty ammo tpl was crashing PMC bot generation.
- Ammo lookup falls back to other tiers; GenerateWeapon aborts cleanly if still missing.

## [0.8.0] — 2026-07-20

### Fixed

- **T1/T2 overpowered loadouts**: endgame faction gear no longer upgrades bots below level 32 (was stacking plate carriers on soft armor and wiping vest mags/meds).
- Faction tactical-vest replace respects soft-rig vs plate-carrier path when ArmorVest is present.
- Faction gear runs **before** weapons/loot so vest swaps cannot delete magazines and medkits.
- `GenerateArmor` skips plate carriers when `armoredRigs` is empty.

### Changed

- **T1 gear**: soft armor only (Module-3M / PACA / Kora / rare Zhuk-Press / 6B23-1); no AVS/Trooper/6B13; no plate carriers.
- Removed T1 presets: G28, SCAR-H, ISO Hemlock (remain on T2+).
- Hunting .762 bias (G28/SCAR-H force) starts at level **15**, not 1.
- Backfilled `SpareMags` / `Weight` / `Locations` on T1–T3 weapon presets.

## [0.7.0] — 2026-07-20

### Added

- **Preset integrity check** (PostDB): if a weapon preset, gear entry, module alt, ammo tpl, or faction-pool item references a template that is not in the item DB (e.g. WTT/MSW removed), log a **red** console warning and **disable / prune** that entry so it cannot spawn.

## [0.6.0] — 2026-07-20

### Added

- Merged former **Raylee-AndernPmcPatch** into this mod (single DLL / no ModDependencies):
  - Map biases (Factory KS-23 / shotguns, Surgeon 1581, KATT AMR, SV-98, hunting .762, X95/Mk18 REAP)
  - Open-map shotgun limit, USEC/BEAR faction loadout filter
  - M249 ammo fix, spare-mag safety limiter
  - Close-quarters bolt sniper ban (Streets / Factory)
- Faction pool JSON (`faction_weapons.json`, `faction_gear.json`) ships with the mod

### Removed

- Need for a separate `Raylee-AndernPmcPatch` server mod (uninstall it after upgrading)

### Notes

- CQCM / mask % stays in `config.json5` (`MaskInsteadOfHelmetPercent*`) — Raylee’s old trim path is not used.

## [0.5.0] — 2026-07-19

### Added

- Per-tier **CQCM / mask-instead-of-helmet** chance in `config/config.json5` (SPT mod config):
  - `MaskInsteadOfHelmetPercentOne` … `Four`
  - Defaults: **0 / 0 / 3 / 6**
- Replaces the old hardcoded **30%** Andern roll. `gear.mask` (CQCM class-4) is used when the roll succeeds (and not NVG).

### Notes

- While live `BarlogM-Andern` is still installed, `Raylee-AndernPmcPatch` trims BarlogM’s fixed 30% path toward the same 0/0/3/6 via `cqcm_mask_bias.json`.
- After cutover to Reforged, Raylee skips that trim (`IsReforgedAndern`) so this config is the single source of truth.

## [0.4.0] — 2026-07-19

### Added

- Per-preset **`Weight`** (int) — higher weight = more frequent pick within the location-filtered tier pool.
- Weighted selection in `Data.GetRandomWeapon` via `WeightedRandomHelper` (missing/`≤0` Weight → 1).
- Report script: `mods-src/ander-presets/scripts/report_preset_chances.py` — per location: preset name, primary weapon, spawn %.

### Changed

- Builder always writes integer `Weight` (`apply_weight_metadata`). Default `1`; shotguns `3`; M249 `1`.
- Old shotgun `Weight: 80` retired (was unused dead metadata; would have flooded the pool once weights went live).

## [0.3.0] — 2026-07-19

### Added

- Per-preset **`SpareMags`** — exact spare magazine count for PMC loadouts.
- `SpareMagazineDefaults.ExactCountWeights(n)` — builds `GenerationData` so SPT adds **exactly N** spares instead of bot JSON weights (which flood vests with 3–5 mags).
- Capacity heuristic when `SpareMags` is omitted: drums ≥61 → 1, mid 50–60 → 2, smaller → 3.
- Builder `apply_spare_mags_metadata`: LMG/drums → 1, sniper/shotgun → 2, AR/SMG/DMR → 3 (overridable per preset).

### Changed

- `Data.GetRandomWeapon` returns `SelectedWeaponPreset` (items + spare count).
- `BotInventoryGeneratorEx` no longer passes `botJsonTemplate.BotGeneration.Items.Magazines` for PMC extras.

### Notes

- `SpareMags: 0` = mounted mag only (no loose spares).
- Until cutover, live `BarlogM-Andern` ignores this field; Raylee `SpareMagazineLimiter` may still cap counts if that patch runs on top of BarlogM.

## [0.2.0] — 2026-07-19

### Added

- Per-preset **`Locations`** allow-list on weapon JSON (map-aware PMC weapon picks).
- `LocationMatch` — matches raid location against preset lists; short aliases: `factory`, `streets`, `customs`, `reserve`, `groundzero` / `gz`.
- `RaidLocationContext` (`AsyncLocal`) — current raid map available to `Data.GetRandomWeapon` and post-processors that re-roll via `GenerateWeapon`.
- Empty filtered pool falls back to the full tier pool with a warning (bots never spawn without a primary).
- Builder (`mods-src/ander-presets/build_t4_presets.py`): `apply_locations_metadata` seeds:
  - open-map bolt / snipers (Mosin, M700, AXMC, Surgeon, SV-98, KATT) → woods / shoreline / lighthouse / customs / reserve / interchange / Ground Zero
  - CQ KS-23 → Factory day/night + Streets
  - all other T4 presets omit `Locations` (= every map)
- T4 deploy mirrors into this fork’s `presets/meta/four/` while live cutover is pending.

### Changed

- `WeaponPreset` model deserializes optional `Locations`.
- `BotInventoryGeneratorEx` sets `RaidLocationContext` from `RaidConfiguration.Location` for PMC inventory generation.

### Notes

- Missing / `null` / `[]` `Locations` = preset allowed on **all** maps.
- Live `BarlogM-Andern` DLL still ignores `Locations` until this mod is deployed and BarlogM is disabled.
- Not yet: faction filter on `Factions`, Raylee HintPath cutover, removal of redundant Raylee map-bias patches.

## [0.1.0] — 2026-07-19

### Added

- GitHub fork of [barlog-m/spt-andern](https://github.com/barlog-m/spt-andern) (MIT) as **Andern Builds Reforged**.
- Rebrand: namespace `AndernBuildsReforged`, assembly `Andern-Builds-Reforged`, ModGuid `com.raylee.andern-builds-reforged`.
- Incompatibility declared with upstream ModGuid `li.barlog.andern`.
- Staging layout under `dev/Andern-Builds-Reforged`, `deploy.ps1` → `SPT/user/mods/Andern-Builds-Reforged/`.
- Curated T4/T3/T2/T1 presets seeded from the live SPT install.

[0.5.0]: https://github.com/Rayllienstery/Andern-Builds-Reforged/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/Rayllienstery/Andern-Builds-Reforged/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/Rayllienstery/Andern-Builds-Reforged/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/Rayllienstery/Andern-Builds-Reforged/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/Rayllienstery/Andern-Builds-Reforged/releases/tag/v0.1.0
