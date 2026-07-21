using System;
using System.Collections.Generic;
using System.Linq;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;

namespace AndernBuildsReforged;

internal static class Sv98MapBias
{
    public const string WeaponTpl = "55801eed4bdc2d89578b4588";
    public const int ForceChancePercent = 25;
    public const int MinT2Level = 15;
    public const int MaxT2Level = 31;
    public const int MaxRerollAttempts = 50;

    static readonly HashSet<string> OpenMaps = new(StringComparer.OrdinalIgnoreCase)
    {
        "Woods",
        "Lighthouse",
    };

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
        MongoId sessionId,
        MongoId botId)
    {
        if (inventory?.Items == null || inventory.Items.Count == 0 || !botGenerationDetails.IsPmc)
        {
            return;
        }

        var location = profileActivityService
            .GetProfileActivityRaidData(sessionId)?.RaidConfiguration?.Location;
        if (location == null)
        {
            return;
        }

        var weapon = inventory.Items.FirstOrDefault(item =>
            item.SlotId == EquipmentSlots.FirstPrimaryWeapon.ToString());
        if (weapon == null)
        {
            return;
        }

        var isOpenMap = OpenMaps.Contains(location);
        var isSv98 = weapon.Template == new MongoId(WeaponTpl);
        var inT2Band = botGenerationDetails.BotLevel is >= MinT2Level and <= MaxT2Level;

        if (!isOpenMap)
        {
            if (isSv98)
            {
                RerollWeapon(inventory, botGenerationDetails, botJsonTemplate, profileActivityService,
                    randomUtil, weaponGenerator, botWeaponGenerator, botGeneratorHelper, itemHelper, sessionId, botId, weapon,
                    wantWeapon: false);
            }

            return;
        }

        if (!inT2Band || isSv98)
        {
            return;
        }

        if (!randomUtil.GetChance100(ForceChancePercent))
        {
            return;
        }

        RerollWeapon(inventory, botGenerationDetails, botJsonTemplate, profileActivityService,
            randomUtil, weaponGenerator, botWeaponGenerator, botGeneratorHelper, itemHelper, sessionId, botId, weapon,
            wantWeapon: true);
    }

    static void RerollWeapon(
        BotBaseInventory inventory,
        BotGenerationDetails botGenerationDetails,
        BotType botJsonTemplate,
        ProfileActivityService profileActivityService,
        RandomUtil randomUtil,
        WeaponGenerator weaponGenerator,
        BotWeaponGenerator botWeaponGenerator,
        BotGeneratorHelper botGeneratorHelper,
        ItemHelper itemHelper,
        MongoId sessionId,
        MongoId botId,
        Item weapon,
        bool wantWeapon)
    {
        var isNightVision = profileActivityService
            .GetProfileActivityRaidData(sessionId)?.RaidConfiguration?.IsNightRaid == true;
        var sv98Id = new MongoId(WeaponTpl);

        for (var attempt = 0; attempt < MaxRerollAttempts; attempt++)
        {
            var generated = weaponGenerator.GenerateWeapon(
                botGenerationDetails.BotLevel,
                inventory.Equipment,
                isNightVision);

            var gotSv98 = generated.WeaponTemplate.Id == sv98Id;
            if (gotSv98 != wantWeapon)
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

            return;
        }
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
