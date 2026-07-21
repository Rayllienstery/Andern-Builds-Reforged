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

internal static class X95ReapThermalBias
{
    public const string WeaponTpl = "66a47e98c486ec9d1af3a4da";
    public const string ReapIrTpl = "5a1eaa87fcdbcb001865f75e";
    public const int ForceChancePercent = 5;
    public const int MinLevel = 32;
    public const int MaxLevel = 99;
    public const int MaxRerollAttempts = 50;

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

        var level = botGenerationDetails.BotLevel;
        if (level is < MinLevel or > MaxLevel)
        {
            return;
        }

        var weapon = inventory.Items.FirstOrDefault(item =>
            item.SlotId == EquipmentSlots.FirstPrimaryWeapon.ToString());
        if (weapon == null)
        {
            return;
        }

        var x95Id = new MongoId(WeaponTpl);
        var reapId = new MongoId(ReapIrTpl);
        if (weapon.Template == x95Id && WeaponTreeContains(inventory.Items, weapon.Id, reapId))
        {
            return;
        }

        if (!randomUtil.GetChance100(ForceChancePercent))
        {
            return;
        }

        RerollWeapon(inventory, botGenerationDetails, botJsonTemplate, profileActivityService,
            randomUtil, weaponGenerator, botWeaponGenerator, botGeneratorHelper, itemHelper, sessionId, botId, weapon, x95Id, reapId);
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
        MongoId x95Id,
        MongoId reapId)
    {
        var isNightVision = profileActivityService
            .GetProfileActivityRaidData(sessionId)?.RaidConfiguration?.IsNightRaid == true;

        for (var attempt = 0; attempt < MaxRerollAttempts; attempt++)
        {
            var generated = weaponGenerator.GenerateWeapon(
                botGenerationDetails.BotLevel,
                inventory.Equipment,
                isNightVision);

            if (generated.WeaponTemplate.Id != x95Id)
            {
                continue;
            }

            var generatedWeapon = generated.WeaponWithMods.FirstOrDefault(item =>
                item.SlotId == EquipmentSlots.FirstPrimaryWeapon.ToString());
            if (generatedWeapon == null || !WeaponTreeContains(generated.WeaponWithMods, generatedWeapon.Id, reapId))
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

    static bool WeaponTreeContains(List<Item> items, MongoId rootId, MongoId targetTpl)
    {
        var toVisit = new Queue<MongoId>();
        toVisit.Enqueue(rootId);
        while (toVisit.Count > 0)
        {
            var current = toVisit.Dequeue();
            foreach (var item in items)
            {
                if (item.ParentId != current)
                {
                    continue;
                }

                if (item.Template == targetTpl)
                {
                    return true;
                }

                toVisit.Enqueue(item.Id);
            }
        }

        return false;
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
