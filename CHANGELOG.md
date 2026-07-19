# Changelog

All notable changes to **Andern Builds Reforged** are documented in this file.

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versioning follows [SemVer](https://semver.org/).

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

[0.4.0]: https://github.com/Rayllienstery/Andern-Builds-Reforged/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/Rayllienstery/Andern-Builds-Reforged/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/Rayllienstery/Andern-Builds-Reforged/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/Rayllienstery/Andern-Builds-Reforged/releases/tag/v0.1.0
