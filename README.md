# Andern Builds Reforged

Personal fork of [BarlogM Andern](https://github.com/barlog-m/spt-andern) (MIT) for SPT **4.0.13**.

| | |
|--|--|
| Version | **0.3.0** |
| GitHub | https://github.com/Rayllienstery/Andern-Builds-Reforged |
| Changelog | [CHANGELOG.md](CHANGELOG.md) |
| Upstream | `upstream` → `barlog-m/spt-andern` |
| Local staging | `c:\Games\SPT\dev\Andern-Builds-Reforged` |
| Live deploy (later) | `SPT/user/mods/Andern-Builds-Reforged/` |

Keep live `BarlogM-Andern` until this fork is verified in-raid.

## Identity

| Field | Value |
|-------|-------|
| ModGuid | `com.raylee.andern-builds-reforged` |
| Assembly | `Andern-Builds-Reforged.dll` |
| Author | Raylee |
| SPT | `~4.0.0` (tested 4.0.13) |
| Incompatible with | `li.barlog.andern` |

## Features (vs upstream)

### Map-aware presets (`Locations`) — since 0.2.0

Each weapon preset JSON may include an allow-list of SPT location IDs:

```json
{
  "Name": "KS-23 T4",
  "Factions": ["bear"],
  "Locations": ["factory4_day", "factory4_night", "tarkovstreets"],
  "SpareMags": 2,
  "Items": [ ... ]
}
```

| Rule | Behavior |
|------|----------|
| No field / `null` / `[]` | Allowed on **all** maps |
| Non-empty list | Raid map must match (after alias expand) |
| Empty pool after filter | Fall back to full tier + server warning |

**Aliases** (written in JSON, expanded at runtime):

| Alias | Canonical IDs |
|-------|----------------|
| `factory` | `factory4_day`, `factory4_night` |
| `streets` | `tarkovstreets` |
| `customs` | `bigmap` |
| `reserve` | `rezervbase` |
| `groundzero` / `gz` | `sandbox`, `sandbox_high` |

### Spare magazines (`SpareMags`) — since 0.3.0

Exact number of **loose** magazines (same tpl as the one on the gun) placed in vest/pockets. Does **not** use PMC bot JSON magazine weights (those used to flood inventories).

| Value | Meaning |
|-------|---------|
| `3` | three spare mags |
| `0` | gun mag only |
| omitted | capacity heuristic (drum 1 / mid 2 / small 3) |

Builder defaults: LMG/drums `1`, sniper/shotgun `2`, AR/SMG/DMR `3`. Override per preset in `build_t4_presets.py` / JSON.

Preset generation lives in the SPT workspace builder:

`mods-src/ander-presets/build_t4_presets.py` → `apply_locations_metadata()` / `apply_spare_mags_metadata()`.

Do **not** hand-edit deployed `presets/meta/four/*.json` — change the builder, regenerate, run optic audits.

## Build

```powershell
dotnet build AndernBuildsReforged\AndernBuildsReforged.csproj -c Release
```

## Deploy to SPT

Stop **SPT Server**, then:

```powershell
powershell -File deploy.ps1
```

Disable `BarlogM-Andern` only after verifying this mod loads (they are incompatible).

## Sync upstream

```powershell
git fetch upstream
git merge upstream/main
```

## Roadmap

- [x] `Locations` allow-list on presets
- [x] `SpareMags` exact spare count (no inventory flood)
- [ ] Retarget `Raylee-AndernPmcPatch` HintPath / namespaces to this assembly
- [ ] Cut over: deploy here, remove live `BarlogM-Andern`
- [ ] Optional: filter on `Factions` (field already emitted by builder)
- [ ] Slim redundant Raylee map-bias / SpareMagazineLimiter once Reforged covers the same cases

## License

MIT — see [LICENSE](LICENSE). Upstream copyright Barlog_M; this fork adds Raylee changes documented in [CHANGELOG.md](CHANGELOG.md).
