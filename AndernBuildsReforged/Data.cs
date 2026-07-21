using fastJSON5;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using Path = System.IO.Path;

namespace AndernBuildsReforged;

[Injectable]
public class Data
{
    readonly ModConfig _modConfig;
    readonly Dictionary<string, PresetData> data = new ();

    readonly ISptLogger<Data> logger;
    readonly ModHelper modHelper;
    readonly RandomUtil randomUtil;
    readonly WeightedRandomHelper weightedRandomHelper;
    readonly ICloner cloner;
    readonly ModData modData;

    public Data(
        ISptLogger<Data> logger,
        ModHelper modHelper,
        RandomUtil randomUtil,
        WeightedRandomHelper weightedRandomHelper,
        ICloner cloner,
        ModData modData)
    {
        this.logger = logger;
        this.modHelper = modHelper;
        this.randomUtil = randomUtil;
        this.weightedRandomHelper = weightedRandomHelper;
        this.modData = modData;
        this.cloner = cloner;
        this._modConfig = modData.ModConfig;

        LoadData();
    }

    private void LoadData()
    {
        var presetDir =
            Path.Join(modData.PathToMod, "presets", _modConfig.Preset);

        if (!Directory.Exists(presetDir))
        {
            logger.Error($"[Andern] preset directory {presetDir} does not exists");
            return;
        }

        foreach (var tierDir in Directory.EnumerateDirectories(presetDir))
        {
            var tierName = Path.GetFileNameWithoutExtension(tierDir);
            var tierData = LoadTierData(tierDir);
            data.Add(tierName, tierData);
        }
    }

    private PresetData LoadTierData(string path)
    {
        var data = new PresetData();
        data.PresetConfig = JSON5.ToObject<PresetConfig>(modHelper.GetRawFileData(path, "config.json5"));
        data.PresetGear = JSON5.ToObject<PresetGear>(modHelper.GetRawFileData(path, "gear.json5"));
        data.Ammo = JSON5.ToObject<Dictionary<string, List<AmmoEntry>>>(modHelper.GetRawFileData(path, "ammo.json5"))
            ?? new Dictionary<string, List<AmmoEntry>>();
        NormalizeAmmoWeights(data.Ammo);
        data.Modules = JSON5.ToObject<Dictionary<string, string[]>>(modHelper.GetRawFileData(path, "modules.json5"));

        LoadTierWeaponData(path, data);
        return data;
    }

    private void LoadTierWeaponData(string path, PresetData data)
    {
        foreach (var file in Directory.EnumerateFiles(path))
        {
            var fileName = Path.GetFileName(file);

            if (fileName is "ammo.json5" or "config.json5" or "gear.json5" or "modules.json5") continue;

            var weaponPreset = modHelper.GetJsonDataFromFile<WeaponPreset>(path, fileName);

            data.Weapon.Add(weaponPreset);
        }
    }

    public string TierByLevel(int level)
    {
        foreach (var tier in data.Keys)
        {
            if (level >= data[tier].PresetConfig.MinLevel &&
                level <= data[tier].PresetConfig.MaxLevel)
            {
                return tier;
            }
        }

        return data.First().Key;
    }

    public PresetGear GetGear(int level) {
        var tier = TierByLevel(level);
        return data[tier].PresetGear;
    }

    public string GetAlternativeModule(int level, string moduleTpl)
    {
        var tier = TierByLevel(level);

        if (!data[tier].Modules.ContainsKey(moduleTpl))
        {
            return moduleTpl;
        }

        var altModules = data[tier].Modules[moduleTpl];

        return randomUtil.GetArrayValue(altModules);
    }

    public SelectedWeaponPreset GetRandomWeapon(int level)
    {
        var tier = TierByLevel(level);
        var pool = data[tier].Weapon;
        var location = RaidLocationContext.Location;

        var filtered = pool.Where(p => LocationMatch.IsAllowed(p, location)).ToList();
        if (filtered.Count == 0)
        {
            logger.Warning(
                $"[Andern-Builds-Reforged] no presets in tier `{tier}` allowed on `{location ?? "(none)"}`; falling back to full tier pool");
            filtered = pool;
        }

        var weaponPreset = PickWeightedPreset(filtered);

        if (_modConfig.Debug)
        {
            logger.LogWithColor(
                $"[Andern] for bot level {level} loc `{location ?? "-"}` selected tier `{tier}` weapon '{weaponPreset.Name}' weight={weaponPreset.EffectiveWeight} spareMags={weaponPreset.SpareMags?.ToString() ?? "default"}",
                LogTextColor.Blue);
        }

        var weaponPresetClone = cloner.Clone(weaponPreset.Items).ReplaceIDs().ToList();
        weaponPresetClone.RemapRootItemId();

        return new SelectedWeaponPreset
        {
            Items = weaponPresetClone,
            Name = weaponPreset.Name,
            // Capacity-aware default applied later in WeaponGenerator once mag tpl is known;
            // negative sentinel means "resolve default".
            SpareMags = weaponPreset.SpareMags ?? -1,
            AmmoOverride = TryResolvePresetAmmo(weaponPreset),
        };
    }

    /// <summary>
    /// Preset-level ammo override. Null = use ammo.json5 for this caliber.
    /// </summary>
    static ResolvedAmmo? TryResolvePresetAmmo(WeaponPreset preset)
    {
        if (preset.AmmoLoad is { Count: > 0 })
        {
            return ResolvedAmmo.FromLoad(preset.AmmoLoad, preset.SpareAmmoTpl, preset.SpareAmmoLoad);
        }

        if (!string.IsNullOrEmpty(preset.PrimaryAmmoTpl))
        {
            if (preset.SpareAmmoLoad is { Count: > 0 })
            {
                return ResolvedAmmo.FromLoad(
                    [new AmmoStackEntry { Id = preset.PrimaryAmmoTpl, Count = -1 }],
                    preset.SpareAmmoTpl,
                    preset.SpareAmmoLoad);
            }

            return ResolvedAmmo.FromSolid(preset.PrimaryAmmoTpl, preset.SpareAmmoTpl);
        }

        return null;
    }

    /// <summary>
    /// Resolve ammo for a weapon: preset override wins; otherwise weighted pick from ammo.json5
    /// (solid tpl or mag load preset, same weight scale).
    /// </summary>
    public ResolvedAmmo? ResolveAmmo(int level, string caliber, ResolvedAmmo? presetOverride)
    {
        if (presetOverride != null && !string.IsNullOrEmpty(presetOverride.ChamberTpl))
        {
            return presetOverride;
        }

        var tier = TierByLevel(level);

        if (TryPickAmmoEntry(tier, caliber, out var entry))
        {
            return entry.ToResolved();
        }

        foreach (var otherTier in data.Keys)
        {
            if (otherTier == tier)
            {
                continue;
            }

            if (TryPickAmmoEntry(otherTier, caliber, out entry))
            {
                logger.Warning(
                    $"[Andern] tier `{tier}` missing ammo for `{caliber}`; using tier `{otherTier}`");
                return entry.ToResolved();
            }
        }

        logger.Error($"[Andern] no ammo record for tier '{tier}' with caliber '{caliber}'");
        return null;
    }

    /// <summary>Legacy helper — chamber tpl only.</summary>
    public string GetRandomAmmoByCaliber(int level, string caliber)
    {
        return ResolveAmmo(level, caliber, null)?.ChamberTpl ?? "";
    }

    WeaponPreset PickWeightedPreset(List<WeaponPreset> presets)
    {
        if (presets.Count == 1)
        {
            return presets[0];
        }

        // Index keys — preset Names/Ids are not guaranteed unique.
        var weights = new Dictionary<string, double>(presets.Count);
        for (var i = 0; i < presets.Count; i++)
        {
            weights[i.ToString()] = presets[i].EffectiveWeight;
        }

        var key = weightedRandomHelper.GetWeightedValue(weights);
        return presets[int.Parse(key)];
    }

    bool TryPickAmmoEntry(string tier, string caliber, out AmmoEntry entry)
    {
        entry = null!;
        if (!data.TryGetValue(tier, out var tierData)
            || !tierData.Ammo.TryGetValue(caliber, out var ammo)
            || ammo == null
            || ammo.Count == 0)
        {
            return false;
        }

        var valid = ammo.Where(e =>
                e.IsLoadPreset || !string.IsNullOrEmpty(e.Id))
            .Where(e => !string.IsNullOrEmpty(e.ChamberTpl))
            .ToList();
        if (valid.Count == 0)
        {
            return false;
        }

        if (valid.Count == 1)
        {
            entry = valid[0];
            return true;
        }

        var weights = new Dictionary<string, double>(valid.Count);
        for (var i = 0; i < valid.Count; i++)
        {
            weights[i.ToString()] = valid[i].EffectiveWeight;
        }

        var key = weightedRandomHelper.GetWeightedValue(weights);
        entry = valid[int.Parse(key)];
        return true;
    }

    static void NormalizeAmmoWeights(Dictionary<string, List<AmmoEntry>> ammo)
    {
        foreach (var entries in ammo.Values)
        {
            if (entries == null)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                if (entry.Weight is null or <= 0)
                {
                    entry.Weight = AmmoEntry.DefaultWeight;
                }
            }
        }
    }

    public PresetConfig GetConfig(int level)
    {
        var tier = TierByLevel(level);
        return data[tier].PresetConfig;
    }

    public int GetMaskInsteadOfHelmetPercent(int level)
    {
        return _modConfig.GetMaskInsteadOfHelmetPercent(TierByLevel(level));
    }

    /// <summary>
    /// Drop weapon presets / gear / module alts / ammo whose templates are not in the item DB.
    /// Call once after PostDB so custom item mods have registered.
    /// </summary>
    public void ValidateAndDisableBrokenEntries(ItemHelper itemHelper)
    {
        var disabledPresets = 0;
        var prunedGear = 0;
        var prunedModules = 0;
        var prunedAmmo = 0;

        foreach (var (tier, tierData) in data)
        {
            var keptWeapons = new List<WeaponPreset>(tierData.Weapon.Count);
            foreach (var preset in tierData.Weapon)
            {
                var missing = CollectMissingItemTpls(preset, itemHelper);
                if (missing.Count == 0)
                {
                    keptWeapons.Add(preset);
                    continue;
                }

                disabledPresets++;
                var shown = missing.Count <= 8
                    ? string.Join(", ", missing)
                    : string.Join(", ", missing.Take(8)) + $", … (+{missing.Count - 8} more)";
                logger.LogWithColor(
                    $"[Andern-Builds-Reforged] DISABLED weapon preset `{preset.Name}` (tier `{tier}`): missing tpl(s): {shown}",
                    LogTextColor.Red);
            }

            tierData.Weapon = keptWeapons;

            if (tierData.Weapon.Count == 0)
            {
                logger.LogWithColor(
                    $"[Andern-Builds-Reforged] CRITICAL: tier `{tier}` has ZERO valid weapon presets after integrity check",
                    LogTextColor.Red);
            }

            prunedGear += PruneGearLists(tier, tierData.PresetGear, itemHelper);
            prunedModules += PruneModuleMap(tier, tierData.Modules, itemHelper);
            prunedAmmo += PruneAmmoMap(tier, tierData.Ammo, itemHelper);
        }

        if (disabledPresets == 0 && prunedGear == 0 && prunedModules == 0 && prunedAmmo == 0)
        {
            logger.Info("[Andern-Builds-Reforged] preset integrity OK — all weapon/gear/module/ammo tpls present");
            return;
        }

        logger.LogWithColor(
            $"[Andern-Builds-Reforged] integrity summary: disabled {disabledPresets} weapon preset(s), pruned {prunedGear} gear / {prunedModules} module / {prunedAmmo} ammo entries",
            LogTextColor.Red);
    }

    static List<string> CollectMissingItemTpls(WeaponPreset preset, ItemHelper itemHelper)
    {
        var missing = new List<string>();
        if (preset.Items == null)
        {
            missing.Add("(preset has no Items)");
            return missing;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in preset.Items)
        {
            var tpl = item.Template.ToString();
            if (string.IsNullOrEmpty(tpl) || !seen.Add(tpl))
            {
                continue;
            }

            if (!ItemExists(itemHelper, tpl))
            {
                missing.Add(tpl);
            }
        }

        return missing;
    }

    int PruneGearLists(string tier, PresetGear gear, ItemHelper itemHelper)
    {
        if (gear == null)
        {
            return 0;
        }

        var removed = 0;
        removed += PruneGearList(tier, "Headsets", gear.Headsets, itemHelper);
        removed += PruneGearList(tier, "Helmets", gear.Helmets, itemHelper);
        removed += PruneGearList(tier, "ArmoredRigs", gear.ArmoredRigs, itemHelper);
        removed += PruneGearList(tier, "Armor", gear.Armor, itemHelper);
        removed += PruneGearList(tier, "Rigs", gear.Rigs, itemHelper);
        removed += PruneGearList(tier, "Backpacks", gear.Backpacks, itemHelper);
        removed += PruneGearList(tier, "Face", gear.Face, itemHelper);
        removed += PruneGearList(tier, "Eyewear", gear.Eyewear, itemHelper);
        removed += PruneGearList(tier, "Sheath", gear.Sheath, itemHelper);
        removed += PruneGearList(tier, "Mask", gear.Mask, itemHelper);
        return removed;
    }

    int PruneGearList(string tier, string slotName, List<GearItem>? list, ItemHelper itemHelper)
    {
        if (list == null || list.Count == 0)
        {
            return 0;
        }

        var removed = 0;
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var id = list[i].Id;
            if (string.IsNullOrEmpty(id) || ItemExists(itemHelper, id))
            {
                continue;
            }

            logger.LogWithColor(
                $"[Andern-Builds-Reforged] REMOVED gear `{slotName}` tpl `{id}` (tier `{tier}`): not in item DB",
                LogTextColor.Red);
            list.RemoveAt(i);
            removed++;
        }

        if (list.Count == 0)
        {
            logger.LogWithColor(
                $"[Andern-Builds-Reforged] WARNING: gear slot `{slotName}` empty in tier `{tier}` after integrity check",
                LogTextColor.Red);
        }

        return removed;
    }

    int PruneModuleMap(string tier, Dictionary<string, string[]> modules, ItemHelper itemHelper)
    {
        if (modules == null || modules.Count == 0)
        {
            return 0;
        }

        var removed = 0;
        foreach (var key in modules.Keys.ToList())
        {
            if (!ItemExists(itemHelper, key))
            {
                logger.LogWithColor(
                    $"[Andern-Builds-Reforged] REMOVED modules key `{key}` (tier `{tier}`): not in item DB",
                    LogTextColor.Red);
                modules.Remove(key);
                removed++;
                continue;
            }

            var alts = modules[key];
            if (alts == null || alts.Length == 0)
            {
                continue;
            }

            var kept = alts.Where(tpl =>
            {
                if (ItemExists(itemHelper, tpl))
                {
                    return true;
                }

                logger.LogWithColor(
                    $"[Andern-Builds-Reforged] REMOVED module alt `{tpl}` for key `{key}` (tier `{tier}`): not in item DB",
                    LogTextColor.Red);
                removed++;
                return false;
            }).ToArray();

            if (kept.Length == 0)
            {
                modules.Remove(key);
            }
            else
            {
                modules[key] = kept;
            }
        }

        return removed;
    }

    int PruneAmmoMap(string tier, Dictionary<string, List<AmmoEntry>> ammo, ItemHelper itemHelper)
    {
        if (ammo == null || ammo.Count == 0)
        {
            return 0;
        }

        var removed = 0;
        foreach (var caliber in ammo.Keys.ToList())
        {
            var entries = ammo[caliber];
            if (entries == null || entries.Count == 0)
            {
                continue;
            }

            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                var tpls = CollectAmmoTpls(entry);
                if (tpls.Count == 0)
                {
                    logger.LogWithColor(
                        $"[Andern-Builds-Reforged] REMOVED empty ammo entry for `{caliber}` (tier `{tier}`)",
                        LogTextColor.Red);
                    entries.RemoveAt(i);
                    removed++;
                    continue;
                }

                var bad = tpls.FirstOrDefault(tpl => !ItemExists(itemHelper, tpl));
                if (bad == null)
                {
                    continue;
                }

                logger.LogWithColor(
                    $"[Andern-Builds-Reforged] REMOVED ammo `{bad}` for `{caliber}` (tier `{tier}`): not in item DB",
                    LogTextColor.Red);
                entries.RemoveAt(i);
                removed++;
            }

            if (entries.Count == 0)
            {
                logger.LogWithColor(
                    $"[Andern-Builds-Reforged] WARNING: caliber `{caliber}` has no valid ammo in tier `{tier}`",
                    LogTextColor.Red);
                ammo.Remove(caliber);
            }
        }

        return removed;
    }

    static List<string> CollectAmmoTpls(AmmoEntry entry)
    {
        var tpls = new List<string>();
        if (entry.IsLoadPreset)
        {
            foreach (var stack in entry.Load!)
            {
                if (!string.IsNullOrEmpty(stack.Id))
                {
                    tpls.Add(stack.Id);
                }
            }
        }
        else if (!string.IsNullOrEmpty(entry.Id))
        {
            tpls.Add(entry.Id);
        }

        if (!string.IsNullOrEmpty(entry.SpareId))
        {
            tpls.Add(entry.SpareId);
        }

        if (entry.SpareLoad != null)
        {
            foreach (var stack in entry.SpareLoad)
            {
                if (!string.IsNullOrEmpty(stack.Id))
                {
                    tpls.Add(stack.Id);
                }
            }
        }

        return tpls;
    }

    static bool ItemExists(ItemHelper itemHelper, string tpl)
    {
        try
        {
            return itemHelper.GetItem(new MongoId(tpl)).Key;
        }
        catch
        {
            return false;
        }
    }
}
