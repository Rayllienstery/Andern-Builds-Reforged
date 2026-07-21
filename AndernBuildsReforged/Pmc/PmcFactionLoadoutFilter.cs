using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using IoPath = System.IO.Path;

namespace AndernBuildsReforged;

internal sealed class FactionPools
{
    public HashSet<string> UsecWeapons { get; } = new();
    public HashSet<string> BearWeapons { get; } = new();
    public Dictionary<string, List<GearPoolEntry>> UsecGear { get; } = new();
    public Dictionary<string, List<GearPoolEntry>> BearGear { get; } = new();

    public static FactionPools Load(Action<string>? warn = null)
    {
        var pools = new FactionPools();
        var modDir = IoPath.Combine(Directory.GetCurrentDirectory(), "user", "mods", "Andern-Builds-Reforged");
        var weaponPath = IoPath.Combine(modDir, "faction_weapons.json");
        var gearPath = IoPath.Combine(modDir, "faction_gear.json");

        if (File.Exists(weaponPath))
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var weaponJson = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(weaponPath), options);
            if (weaponJson != null)
            {
                if (weaponJson.TryGetValue("usec", out var usecWeapons))
                {
                    pools.UsecWeapons.UnionWith(usecWeapons);
                }

                if (weaponJson.TryGetValue("bear", out var bearWeapons))
                {
                    pools.BearWeapons.UnionWith(bearWeapons);
                }
            }
        }
        else
        {
            warn?.Invoke("[Andern] faction_weapons.json missing; faction weapon filter disabled");
        }

        if (File.Exists(gearPath))
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var gearJson = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<GearPoolEntry>>>>(File.ReadAllText(gearPath), options);
            if (gearJson != null)
            {
                if (gearJson.TryGetValue("usec", out var usecGear))
                {
                    foreach (var entry in usecGear)
                    {
                        pools.UsecGear[entry.Key] = entry.Value;
                    }
                }

                if (gearJson.TryGetValue("bear", out var bearGear))
                {
                    foreach (var entry in bearGear)
                    {
                        pools.BearGear[entry.Key] = entry.Value;
                    }
                }
            }
        }
        else
        {
            warn?.Invoke("[Andern] faction_gear.json missing; faction gear filter disabled");
        }

        return pools;
    }
}

internal sealed class GearPoolEntry
{
    public double Weight { get; set; } = 1;
    public string Id { get; set; }
}

internal static class PmcFactionLoadoutFilter
{
    public const int MaxRerollAttempts = 50;

    static FactionPools _pools;
    static bool _loadAttempted;

    public static void EnsureLoaded(ISptLogger<BotInventoryGenerator> logger)
    {
        if (_loadAttempted)
        {
            return;
        }

        _loadAttempted = true;
        _pools = FactionPools.Load(msg => logger.Warning(msg));
    }

    /// <summary>
    /// Drop faction pool entries whose templates are missing (weapon mod uninstalled).
    /// Safe to call before any PMC spawn; loads pools if needed.
    /// </summary>
    public static void PruneMissingTemplates(ItemHelper itemHelper, Action<string> logRed)
    {
        if (!_loadAttempted)
        {
            _loadAttempted = true;
            _pools = FactionPools.Load(msg => logRed(msg));
        }

        if (_pools == null)
        {
            return;
        }

        PruneWeaponSet(_pools.UsecWeapons, "usec", itemHelper, logRed);
        PruneWeaponSet(_pools.BearWeapons, "bear", itemHelper, logRed);
        PruneGearPool(_pools.UsecGear, "usec", itemHelper, logRed);
        PruneGearPool(_pools.BearGear, "bear", itemHelper, logRed);
    }

    static void PruneWeaponSet(HashSet<string> weapons, string faction, ItemHelper itemHelper, Action<string> logRed)
    {
        foreach (var tpl in weapons.ToList())
        {
            if (ItemExists(itemHelper, tpl))
            {
                continue;
            }

            weapons.Remove(tpl);
            logRed($"[Andern-Builds-Reforged] REMOVED faction weapon `{tpl}` ({faction}): not in item DB");
        }
    }

    static void PruneGearPool(
        Dictionary<string, List<GearPoolEntry>> gear,
        string faction,
        ItemHelper itemHelper,
        Action<string> logRed)
    {
        foreach (var slot in gear.Keys.ToList())
        {
            var list = gear[slot];
            if (list == null)
            {
                continue;
            }

            var kept = list.Where(entry =>
            {
                if (string.IsNullOrEmpty(entry.Id) || ItemExists(itemHelper, entry.Id))
                {
                    return true;
                }

                logRed(
                    $"[Andern-Builds-Reforged] REMOVED faction gear `{slot}` `{entry.Id}` ({faction}): not in item DB");
                return false;
            }).ToList();

            if (kept.Count == 0)
            {
                gear.Remove(slot);
            }
            else
            {
                gear[slot] = kept;
            }
        }
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

    public static void Apply(
        BotBaseInventory inventory,
        BotGenerationDetails botGenerationDetails,
        BotType botJsonTemplate,
        ProfileActivityService profileActivityService,
        RandomUtil randomUtil,
        WeaponGenerator weaponGenerator,
        BotWeaponGenerator botWeaponGenerator,
        BotGeneratorHelper botGeneratorHelper,
        ItemHelper itemHelper,
        GearGeneratorHelper gearGeneratorHelper,
        HelmetGenerator helmetGenerator,
        Data data,
        WeightedRandomHelper weightedRandomHelper,
        ISptLogger<BotInventoryGenerator> logger,
        MongoId sessionId,
        MongoId botId)
    {
        if (inventory?.Items == null || inventory.Items.Count == 0 || !botGenerationDetails.IsPmc)
        {
            return;
        }

        var role = botGenerationDetails.Role;
        if (!IsLivePmcRole(role))
        {
            return;
        }

        EnsureLoaded(logger);
        if (_pools == null)
        {
            return;
        }

        var faction = GetFaction(role);
        EnforceFactionWeapon(
            inventory,
            botGenerationDetails,
            botJsonTemplate,
            profileActivityService,
            randomUtil,
            weaponGenerator,
            botWeaponGenerator,
            botGeneratorHelper,
            itemHelper,
            logger,
            sessionId,
            botId,
            faction);
        // Gear is applied earlier via ApplyGearEarly (before weapons/loot).
    }

    /// <summary>
    /// Faction gear only — call after equipment, before weapons/loot so vest swaps do not wipe mags/meds.
    /// No-op for bot levels below 32 (Andern tiers one/two).
    /// </summary>
    public static void ApplyGearEarly(
        BotBaseInventory inventory,
        BotGenerationDetails botGenerationDetails,
        ProfileActivityService profileActivityService,
        RandomUtil randomUtil,
        GearGeneratorHelper gearGeneratorHelper,
        HelmetGenerator helmetGenerator,
        Data data,
        WeightedRandomHelper weightedRandomHelper,
        ISptLogger<BotInventoryGenerator> logger,
        MongoId sessionId)
    {
        if (inventory?.Items == null || !botGenerationDetails.IsPmc)
        {
            return;
        }

        if (!IsLivePmcRole(botGenerationDetails.Role) || botGenerationDetails.BotLevel < 32)
        {
            return;
        }

        EnsureLoaded(logger);
        if (_pools == null)
        {
            return;
        }

        var faction = GetFaction(botGenerationDetails.Role);
        var isNightVision = profileActivityService
            .GetProfileActivityRaidData(sessionId)?.RaidConfiguration?.IsNightRaid == true;
        var isKittedHelmet = randomUtil.GetChance100(data.GetConfig(botGenerationDetails.BotLevel).KittedHelmetPercent);

        EnforceFactionGear(
            inventory,
            botGenerationDetails,
            randomUtil,
            gearGeneratorHelper,
            helmetGenerator,
            weightedRandomHelper,
            isNightVision,
            isKittedHelmet,
            faction);
    }

    static bool IsLivePmcRole(string role)
    {
        return role.Equals("pmcUSEC", System.StringComparison.OrdinalIgnoreCase)
            || role.Equals("pmcBEAR", System.StringComparison.OrdinalIgnoreCase);
    }

    static string GetFaction(string role)
    {
        return role.Equals("pmcUSEC", System.StringComparison.OrdinalIgnoreCase) ? "usec" : "bear";
    }

    static HashSet<string> AllowedWeapons(string faction)
    {
        return faction == "usec" ? _pools.UsecWeapons : _pools.BearWeapons;
    }

    static Dictionary<string, List<GearPoolEntry>> AllowedGear(string faction)
    {
        return faction == "usec" ? _pools.UsecGear : _pools.BearGear;
    }

    static void EnforceFactionWeapon(
        BotBaseInventory inventory,
        BotGenerationDetails botGenerationDetails,
        BotType botJsonTemplate,
        ProfileActivityService profileActivityService,
        RandomUtil randomUtil,
        WeaponGenerator weaponGenerator,
        BotWeaponGenerator botWeaponGenerator,
        BotGeneratorHelper botGeneratorHelper,
        ItemHelper itemHelper,
        ISptLogger<BotInventoryGenerator> logger,
        MongoId sessionId,
        MongoId botId,
        string faction)
    {
        var allowed = AllowedWeapons(faction);
        if (allowed.Count == 0)
        {
            return;
        }

        var weapon = inventory.Items.FirstOrDefault(item =>
            item.SlotId == EquipmentSlots.FirstPrimaryWeapon.ToString());
        if (weapon == null || allowed.Contains(weapon.Template.ToString()))
        {
            return;
        }

        var isNightVision = profileActivityService
            .GetProfileActivityRaidData(sessionId)?.RaidConfiguration?.IsNightRaid == true;

        for (var attempt = 0; attempt < MaxRerollAttempts; attempt++)
        {
            var generated = weaponGenerator.GenerateWeapon(
                botGenerationDetails.BotLevel,
                inventory.Equipment,
                isNightVision);

            if (!allowed.Contains(generated.WeaponTemplate.Id.ToString()))
            {
                continue;
            }

            RemoveItemTree(inventory.Items, weapon.Id);
            RemoveLooseMagazines(inventory.Items, itemHelper);
            inventory.Items.AddRange(generated.WeaponWithMods);

            // After loot the vest is often full — helper tops up via backpack.
            SpareMagazineHelper.AddSparesForGeneratedWeapon(
                botId,
                inventory,
                botGenerationDetails.Role,
                generated,
                botJsonTemplate,
                botWeaponGenerator,
                itemHelper,
                botGeneratorHelper,
                msg => logger.Warning(msg));

            return;
        }
    }

    static void EnforceFactionGear(
        BotBaseInventory inventory,
        BotGenerationDetails botGenerationDetails,
        RandomUtil randomUtil,
        GearGeneratorHelper gearGeneratorHelper,
        HelmetGenerator helmetGenerator,
        WeightedRandomHelper weightedRandomHelper,
        bool isNightVision,
        bool isKittedHelmet,
        string faction)
    {
        var gearPools = AllowedGear(faction);
        if (gearPools.Count == 0)
        {
            return;
        }

        ReplaceHeadwearIfNeeded(
            inventory,
            botGenerationDetails,
            gearGeneratorHelper,
            helmetGenerator,
            weightedRandomHelper,
            gearPools,
            isNightVision,
            isKittedHelmet);

        ReplaceSimpleSlotIfNeeded(
            inventory,
            botGenerationDetails.Role,
            gearPools,
            weightedRandomHelper,
            gearGeneratorHelper,
            EquipmentSlots.Earpiece,
            "headsets");

        ReplaceSimpleSlotIfNeeded(
            inventory,
            botGenerationDetails.Role,
            gearPools,
            weightedRandomHelper,
            gearGeneratorHelper,
            EquipmentSlots.Eyewear,
            "eyewear");

        ReplaceComplexSlotIfNeeded(
            inventory,
            botGenerationDetails.Role,
            gearPools,
            weightedRandomHelper,
            gearGeneratorHelper,
            EquipmentSlots.ArmorVest,
            "armor");

        ReplaceTacticalVestIfNeeded(
            inventory,
            botGenerationDetails.Role,
            gearPools,
            weightedRandomHelper,
            gearGeneratorHelper);

        ReplaceComplexSlotIfNeeded(
            inventory,
            botGenerationDetails.Role,
            gearPools,
            weightedRandomHelper,
            gearGeneratorHelper,
            EquipmentSlots.Backpack,
            "backpacks");
    }

    /// <summary>
    /// Soft rig + armor vest must stay soft; plate-carrier path must not stack on ArmorVest.
    /// </summary>
    static void ReplaceTacticalVestIfNeeded(
        BotBaseInventory inventory,
        string botRole,
        Dictionary<string, List<GearPoolEntry>> gearPools,
        WeightedRandomHelper weightedRandomHelper,
        GearGeneratorHelper gearGeneratorHelper)
    {
        var hasArmorVest = inventory.Items.Any(item =>
            item.SlotId == EquipmentSlots.ArmorVest.ToString());

        if (hasArmorVest)
        {
            ReplaceComplexSlotIfNeeded(
                inventory,
                botRole,
                gearPools,
                weightedRandomHelper,
                gearGeneratorHelper,
                EquipmentSlots.TacticalVest,
                "rigs");
            return;
        }

        ReplaceComplexSlotIfNeeded(
            inventory,
            botRole,
            gearPools,
            weightedRandomHelper,
            gearGeneratorHelper,
            EquipmentSlots.TacticalVest,
            "armoredRigs",
            "rigs");
    }

    static void ReplaceHeadwearIfNeeded(
        BotBaseInventory inventory,
        BotGenerationDetails botGenerationDetails,
        GearGeneratorHelper gearGeneratorHelper,
        HelmetGenerator helmetGenerator,
        WeightedRandomHelper weightedRandomHelper,
        Dictionary<string, List<GearPoolEntry>> gearPools,
        bool isNightVision,
        bool isKittedHelmet)
    {
        if (!gearPools.TryGetValue("helmets", out var helmets) || helmets.Count == 0)
        {
            return;
        }

        var allowed = new HashSet<string>(helmets.Select(h => h.Id));
        var headwear = inventory.Items.FirstOrDefault(item =>
            item.SlotId == EquipmentSlots.Headwear.ToString());
        if (headwear == null || allowed.Contains(headwear.Template.ToString()))
        {
            return;
        }

        RemoveItemTree(inventory.Items, headwear.Id);
        var tpl = PickGearTpl(helmets, weightedRandomHelper);
        helmetGenerator.GenerateHelmet(
            botGenerationDetails.BotLevel,
            botGenerationDetails.Role,
            inventory,
            tpl,
            isNightVision,
            isKittedHelmet);
    }

    static void ReplaceSimpleSlotIfNeeded(
        BotBaseInventory inventory,
        string botRole,
        Dictionary<string, List<GearPoolEntry>> gearPools,
        WeightedRandomHelper weightedRandomHelper,
        GearGeneratorHelper gearGeneratorHelper,
        EquipmentSlots slot,
        string poolKey)
    {
        if (!gearPools.TryGetValue(poolKey, out var entries) || entries.Count == 0)
        {
            return;
        }

        var allowed = new HashSet<string>(entries.Select(e => e.Id));
        var existing = inventory.Items.FirstOrDefault(item => item.SlotId == slot.ToString());
        if (existing == null || allowed.Contains(existing.Template.ToString()))
        {
            return;
        }

        RemoveItemTree(inventory.Items, existing.Id);
        gearGeneratorHelper.PutGearItemToInventory(
            slot,
            botRole,
            inventory,
            PickGearTpl(entries, weightedRandomHelper));
    }

    static void ReplaceComplexSlotIfNeeded(
        BotBaseInventory inventory,
        string botRole,
        Dictionary<string, List<GearPoolEntry>> gearPools,
        WeightedRandomHelper weightedRandomHelper,
        GearGeneratorHelper gearGeneratorHelper,
        EquipmentSlots slot,
        params string[] poolKeys)
    {
        List<GearPoolEntry>? pickFrom = null;
        var allowed = new HashSet<string>();
        foreach (var key in poolKeys)
        {
            if (!gearPools.TryGetValue(key, out var found) || found.Count == 0)
            {
                continue;
            }

            pickFrom ??= found;
            foreach (var entry in found)
            {
                if (!string.IsNullOrEmpty(entry.Id))
                {
                    allowed.Add(entry.Id);
                }
            }
        }

        if (pickFrom == null || pickFrom.Count == 0 || allowed.Count == 0)
        {
            return;
        }

        var existing = inventory.Items.FirstOrDefault(item => item.SlotId == slot.ToString());
        if (existing == null)
        {
            return;
        }

        if (allowed.Contains(existing.Template.ToString()))
        {
            return;
        }

        // Preset-based armor/rigs may use a child root; check any tree tpl.
        var treeIds = CollectTreeIds(inventory.Items, existing.Id);
        var treeTemplates = inventory.Items
            .Where(item => treeIds.Contains(item.Id))
            .Select(item => item.Template.ToString())
            .ToHashSet();

        if (treeTemplates.Any(allowed.Contains))
        {
            return;
        }

        RemoveItemTree(inventory.Items, existing.Id);
        gearGeneratorHelper.PutGearItemToInventory(
            slot,
            botRole,
            inventory,
            PickGearTpl(pickFrom, weightedRandomHelper));
    }

    static string PickGearTpl(List<GearPoolEntry> entries, WeightedRandomHelper weightedRandomHelper)
    {
        var weights = entries.ToDictionary(e => e.Id, e => e.Weight <= 0 ? 1 : e.Weight);
        return weightedRandomHelper.GetWeightedValue(weights);
    }

    static HashSet<MongoId> CollectTreeIds(List<Item> items, MongoId rootId)
    {
        var result = new HashSet<MongoId> { rootId };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var item in items)
            {
                if (item.ParentId == null || !result.Contains(item.ParentId))
                {
                    continue;
                }

                if (result.Add(item.Id))
                {
                    changed = true;
                }
            }
        }

        return result;
    }

    static void RemoveItemTree(List<Item> items, MongoId rootId)
    {
        var toRemove = CollectTreeIds(items, rootId);
        items.RemoveAll(item => toRemove.Contains(item.Id));
    }

    static void RemoveLooseMagazines(List<Item> items, ItemHelper itemHelper)
    {
        items.RemoveAll(item =>
            item.SlotId != "mod_magazine"
            && itemHelper.IsOfBaseclass(item.Template, BaseClasses.MAGAZINE));
    }
}
