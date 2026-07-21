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

internal static class LowTierHunting762Bias
{
    public const string G28WeaponTpl = "6176aca650224f204c1da3fb";
    public const string ScarHWeaponTpl = "6183afd850224f204c1da514";
    public const int ForceChancePercent = 5;
    public const int MinLevel = 15;
    public const int MaxLevel = 31;
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

        if (!botGenerationDetails.Role.Equals("pmcUSEC", System.StringComparison.OrdinalIgnoreCase))
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

        var g28Id = new MongoId(G28WeaponTpl);
        var scarHId = new MongoId(ScarHWeaponTpl);
        if (weapon.Template == g28Id || weapon.Template == scarHId)
        {
            return;
        }

        if (randomUtil.GetChance100(ForceChancePercent))
        {
            RerollWeapon(inventory, botGenerationDetails, botJsonTemplate, profileActivityService,
                randomUtil, weaponGenerator, botWeaponGenerator, botGeneratorHelper, itemHelper, sessionId, botId, weapon, g28Id);
            return;
        }

        if (randomUtil.GetChance100(ForceChancePercent))
        {
            RerollWeapon(inventory, botGenerationDetails, botJsonTemplate, profileActivityService,
                randomUtil, weaponGenerator, botWeaponGenerator, botGeneratorHelper, itemHelper, sessionId, botId, weapon, scarHId);
        }
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
        MongoId targetWeaponTpl)
    {
        var isNightVision = profileActivityService
            .GetProfileActivityRaidData(sessionId)?.RaidConfiguration?.IsNightRaid == true;

        for (var attempt = 0; attempt < MaxRerollAttempts; attempt++)
        {
            var generated = weaponGenerator.GenerateWeapon(
                botGenerationDetails.BotLevel,
                inventory.Equipment,
                isNightVision);

            if (generated.WeaponTemplate.Id != targetWeaponTpl)
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

    static void RemoveItemTree(System.Collections.Generic.List<Item> items, MongoId rootId)
    {
        var toRemove = new System.Collections.Generic.HashSet<MongoId> { rootId };
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

    static void RemoveLooseMagazines(System.Collections.Generic.List<Item> items, ItemHelper itemHelper)
    {
        items.RemoveAll(item =>
            item.SlotId != "mod_magazine"
            && itemHelper.IsOfBaseclass(item.Template, BaseClasses.MAGAZINE));
    }
}
