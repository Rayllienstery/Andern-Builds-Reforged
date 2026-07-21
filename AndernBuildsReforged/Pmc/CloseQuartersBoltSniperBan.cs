using System;
using System.Collections.Generic;
using System.Linq;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;

namespace AndernBuildsReforged;

/// <summary>
/// PMC bots must not spawn with bolt-action sniper rifles on close-quarters maps.
/// Runs last in the PMC inventory pipeline so map biases cannot reintroduce bolts.
/// </summary>
internal static class CloseQuartersBoltSniperBan
{
    public const int MaxRerollAttempts = 120;
    public const int MaxFallbackAttempts = 80;

    static readonly HashSet<string> RestrictedMaps = new(StringComparer.OrdinalIgnoreCase)
    {
        "TarkovStreets",
        "Streets",
        "factory4_day",
        "factory4_night",
        "Factory",
    };

    /// <summary>
    /// Explicit bolts (WTT/vanilla) — WTT clones sometimes fail IsOfBaseclass until late DB merge.
    /// </summary>
    static readonly HashSet<MongoId> ExplicitBoltWeapons =
    [
        new MongoId("1bf618e47cce6d69bec01e9f"), // Surgeon 1581 .338 Norma
        new MongoId("627e14b21713922ded6f2c15"), // AXMC .338
        new MongoId("68a3836826dffa87b5767c04"), // AXMC .300
        new MongoId("5bfea6e90db834001b7347f3"), // M700
        new MongoId("5ae08f0a5acfc408fb1398a1"), // Mosin Sniper
        new MongoId("5bfd297f0db834001a669119"), // Mosin Infantry
        new MongoId("55801eed4bdc2d89578b4588"), // SV-98
        new MongoId("69236a0b2d1260dbca41ef92"), // SV-98M (WTT)
        new MongoId("5a67ae0ea2750c00125ea7ed"), // T-5000
        new MongoId("5df24cf80dee1b22f862e9bc"), // DVL-10 M2
        new MongoId("588892092459774ac91d4b11"), // Remington M700 (base clone host)
    ];

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
        ISptLogger<BotInventoryGenerator> logger,
        MongoId sessionId,
        MongoId botId)
    {
        if (inventory?.Items == null || inventory.Items.Count == 0 || !botGenerationDetails.IsPmc)
        {
            return;
        }

        var location = profileActivityService
            .GetProfileActivityRaidData(sessionId)?.RaidConfiguration?.Location;
        if (!IsRestrictedMap(location))
        {
            return;
        }

        var weapon = inventory.Items.FirstOrDefault(item =>
            item.SlotId == EquipmentSlots.FirstPrimaryWeapon.ToString());
        if (weapon == null || !IsBoltSniper(weapon.Template, itemHelper))
        {
            return;
        }

        var bannedTpl = weapon.Template.ToString();
        var isNightVision = profileActivityService
            .GetProfileActivityRaidData(sessionId)?.RaidConfiguration?.IsNightRaid == true;

        // Phase 1: any non-bolt from Andern pool.
        if (TryReplacePrimary(
                inventory,
                botGenerationDetails,
                botJsonTemplate,
                weaponGenerator,
                botWeaponGenerator,
                botGeneratorHelper,
                itemHelper,
                botId,
                weapon,
                isNightVision,
                MaxRerollAttempts,
                candidate => !IsBoltSniper(candidate, itemHelper)))
        {
            logger?.Info($"[Andern] CQ bolt ban: replaced {bannedTpl} on {location}");
            return;
        }

        // Phase 2: Andern pool may be bolt-heavy — force shotgun/SMG/AR baseclass.
        if (TryReplacePrimary(
                inventory,
                botGenerationDetails,
                botJsonTemplate,
                weaponGenerator,
                botWeaponGenerator,
                botGeneratorHelper,
                itemHelper,
                botId,
                weapon,
                isNightVision,
                MaxFallbackAttempts,
                candidate => IsCloseQuartersSafe(candidate, itemHelper)))
        {
            logger?.Info($"[Andern] CQ bolt ban fallback: replaced {bannedTpl} on {location}");
            return;
        }

        // Phase 3: never leave a bolt — strip primary (pistol-only PMC beats Factory Mosin).
        RemoveItemTree(inventory.Items, weapon.Id);
        RemoveLooseMagazines(inventory.Items, itemHelper);
        logger?.Warning(
            $"[Andern] CQ bolt ban: stripped {bannedTpl} on {location} (no non-bolt roll)");
    }

    static bool IsRestrictedMap(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return false;
        }

        if (RestrictedMaps.Contains(location))
        {
            return true;
        }

        // Be tolerant of aliases / odd client strings.
        var key = location.Trim();
        return key.Contains("factory", StringComparison.OrdinalIgnoreCase)
               || key.Contains("streets", StringComparison.OrdinalIgnoreCase);
    }

    static bool TryReplacePrimary(
        BotBaseInventory inventory,
        BotGenerationDetails botGenerationDetails,
        BotType botJsonTemplate,
        WeaponGenerator weaponGenerator,
        BotWeaponGenerator botWeaponGenerator,
        BotGeneratorHelper botGeneratorHelper,
        ItemHelper itemHelper,
        MongoId botId,
        Item weapon,
        bool isNightVision,
        int attempts,
        Func<MongoId, bool> accept)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var generated = weaponGenerator.GenerateWeapon(
                botGenerationDetails.BotLevel,
                inventory.Equipment,
                isNightVision);

            if (!accept(generated.WeaponTemplate.Id))
            {
                continue;
            }

            RemoveItemTree(inventory.Items, weapon.Id);
            RemoveLooseMagazines(inventory.Items, itemHelper);
            inventory.Items.AddRange(generated.WeaponWithMods);

            SpareMagazineHelper.AddSparesForGeneratedWeapon(
                botId,
                inventory,
                botGenerationDetails.Role,
                generated,
                botJsonTemplate,
                botWeaponGenerator,
                itemHelper,
                botGeneratorHelper);

            return true;
        }

        return false;
    }

    static bool IsCloseQuartersSafe(MongoId weaponTpl, ItemHelper itemHelper)
    {
        if (IsBoltSniper(weaponTpl, itemHelper))
        {
            return false;
        }

        return itemHelper.IsOfBaseclass(weaponTpl, BaseClasses.SHOTGUN)
               || itemHelper.IsOfBaseclass(weaponTpl, BaseClasses.SMG)
               || itemHelper.IsOfBaseclass(weaponTpl, BaseClasses.ASSAULT_RIFLE)
               || itemHelper.IsOfBaseclass(weaponTpl, BaseClasses.ASSAULT_CARBINE)
               || itemHelper.IsOfBaseclass(weaponTpl, BaseClasses.MACHINE_GUN);
    }

    static bool IsBoltSniper(MongoId weaponTpl, ItemHelper itemHelper)
    {
        if (ExplicitBoltWeapons.Contains(weaponTpl))
        {
            return true;
        }

        return itemHelper.IsOfBaseclass(weaponTpl, BaseClasses.SNIPER_RIFLE);
    }

    static void RemoveItemTree(List<Item> items, MongoId rootId)
    {
        var toRemove = new HashSet<MongoId> { rootId };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var item in items)
            {
                if (item.ParentId == null || !toRemove.Contains(item.ParentId))
                {
                    continue;
                }

                if (toRemove.Add(item.Id))
                {
                    changed = true;
                }
            }
        }

        items.RemoveAll(item => toRemove.Contains(item.Id));
    }

    static void RemoveLooseMagazines(List<Item> items, ItemHelper itemHelper)
    {
        items.RemoveAll(item =>
            item.SlotId != "mod_magazine"
            && itemHelper.IsOfBaseclass(item.Template, BaseClasses.MAGAZINE));
    }
}
