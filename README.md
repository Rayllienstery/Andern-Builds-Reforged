# Andern Builds Reforged

Personal fork of [BarlogM Andern](https://github.com/barlog-m/spt-andern) (MIT) for SPT **4.0.13**.

- GitHub: https://github.com/Rayllienstery/Andern-Builds-Reforged
- Upstream: `upstream` → `barlog-m/spt-andern`
- Local staging (this clone): `c:\Games\SPT\dev\Andern-Builds-Reforged`
- Live deploy target (later): `SPT/user/mods/Andern-Builds-Reforged/`

While developing here, keep live `BarlogM-Andern` until the fork is verified in-raid.

## Identity

| Field | Value |
|-------|-------|
| ModGuid | `com.raylee.andern-builds-reforged` |
| Assembly | `Andern-Builds-Reforged.dll` |
| Incompatible with | `li.barlog.andern` |

## Build

```powershell
dotnet build AndernBuildsReforged\AndernBuildsReforged.csproj -c Release
```

## Deploy to SPT

Stop **SPT Server**, then:

```powershell
powershell -File deploy.ps1
```

## Sync upstream

```powershell
git fetch upstream
git merge upstream/main
```

## Planned

- Location / MapTags filtering in `WeaponGenerator` — **done** (`Locations` allow-list on presets)
- Retarget Raylee AndernPmcPatch to this assembly
- Cut over from live `BarlogM-Andern`

## Preset `Locations` field

Allow-list of SPT map IDs on each weapon JSON. Missing / empty = all maps.

```json
"Locations": ["woods", "shoreline", "factory4_day"]
```

Aliases supported by the filter: `factory`, `streets`, `customs`, `reserve`, `groundzero` / `gz`.
Builder rules seed open-map bolt snipers and CQ `KS-23`; see `apply_locations_metadata` in `build_t4_presets.py`.

