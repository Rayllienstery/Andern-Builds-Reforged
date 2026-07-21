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

internal static class FactoryKs23Bias
{
    public const string Ks23WeaponTpl = "5e848cc2988a8701445df1e8";
    public const int ForceKs23ChancePercent = 35;
    public const int MaxRerollAttempts = 50;

    static readonly HashSet<string> FactoryMaps = ["factory4_day", "factory4_night"];

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
        if (location == null || !FactoryMaps.Contains(location))
        {
            return;
        }

        if (!randomUtil.GetChance100(ForceKs23ChancePercent))
        {
            return;
        }

        var weapon = inventory.Items.FirstOrDefault(item =>
            item.SlotId == EquipmentSlots.FirstPrimaryWeapon.ToString());
        if (weapon == null || weapon.Template == new MongoId(Ks23WeaponTpl))
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

            if (generated.WeaponTemplate.Id != new MongoId(Ks23WeaponTpl))
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
