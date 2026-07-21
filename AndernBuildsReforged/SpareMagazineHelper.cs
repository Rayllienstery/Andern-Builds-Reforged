using System.Collections.Generic;
using System.Linq;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Bots;

namespace AndernBuildsReforged;

/// <summary>
/// Ensures exact spare magazine counts. SPT ExternalInventoryMagGen only tries
/// vest + pockets; after loot / faction re-rolls those are often full. We top up
/// via vest → pockets → backpack.
/// </summary>
internal static class SpareMagazineHelper
{
    static readonly EquipmentSlots[] SlotPriority =
    [
        EquipmentSlots.TacticalVest,
        EquipmentSlots.Pockets,
        EquipmentSlots.Backpack,
    ];

    /// <summary>
    /// SPT MagGen (secure ammo + vest/pockets) then top-up missing spares including backpack.
    /// </summary>
    public static void AddSparesForGeneratedWeapon(
        MongoId botId,
        BotBaseInventory inventory,
        string botRole,
        GeneratedWeapon generated,
        BotType botJsonTemplate,
        BotWeaponGenerator botWeaponGenerator,
        ItemHelper itemHelper,
        BotGeneratorHelper botGeneratorHelper,
        Action<string>? warn = null)
    {
        if (generated.WeaponWithMods == null || generated.WeaponWithMods.Count == 0)
        {
            return;
        }

        var generatedWeaponResult = new GenerateWeaponResult
        {
            Weapon = generated.WeaponWithMods,
            ChosenAmmoTemplate = generated.AmmoTpl,
            ChosenUbglAmmoTemplate = null,
            WeaponMods = botJsonTemplate.BotInventory.Mods,
            WeaponTemplate = generated.WeaponTemplate,
        };

        botWeaponGenerator.AddExtraMagazinesToInventory(
            botId,
            generatedWeaponResult,
            SpareMagazineDefaults.ExactCountWeights(generated.SpareMags),
            inventory,
            botRole);

        var weapon = generated.WeaponWithMods[0];
        var weaponMag = generated.WeaponWithMods.FirstOrDefault(item =>
            item.ParentId == weapon.Id && item.SlotId == "mod_magazine");

        EnsureExactSpareCount(
            botId,
            inventory,
            weapon,
            weaponMag,
            generated.SpareMags,
            generated.ResolvedAmmo,
            generated.AmmoTpl,
            itemHelper,
            botGeneratorHelper,
            warn);

        if (generated.ResolvedAmmo != null)
        {
            MagazineLoadHelper.ApplySpareAmmo(
                inventory.Items,
                weapon,
                weaponMag,
                generated.ResolvedAmmo,
                itemHelper);
        }
    }

    /// <summary>
    /// After SPT <c>AddExtraMagazinesToInventory</c>, add any missing spares (backpack fallback).
    /// </summary>
    public static void EnsureExactSpareCount(
        MongoId botId,
        BotBaseInventory inventory,
        Item weapon,
        Item? weaponMag,
        int spareCount,
        ResolvedAmmo? resolved,
        string chamberAmmoTpl,
        ItemHelper itemHelper,
        BotGeneratorHelper botGeneratorHelper,
        Action<string>? warn = null)
    {
        if (spareCount <= 0 || weaponMag == null || inventory.Items == null)
        {
            return;
        }

        var magTpl = weaponMag.Template;
        var magLookup = itemHelper.GetItem(magTpl);
        if (!magLookup.Key || magLookup.Value == null)
        {
            warn?.Invoke($"[Andern] EnsureExactSpareCount: mag tpl `{magTpl}` not in DB");
            return;
        }

        var existing = CountLooseMags(inventory.Items, weaponMag, itemHelper);
        var needed = spareCount - existing;
        if (needed <= 0)
        {
            return;
        }

        var spareLoad = ResolveSpareLoad(resolved, chamberAmmoTpl);
        var added = 0;

        for (var i = 0; i < needed; i++)
        {
            var magWithAmmo = CreateFilledMagazine(magTpl, magLookup.Value, spareLoad, itemHelper);
            var result = TryAddMagazine(botId, inventory, magTpl, magWithAmmo, botGeneratorHelper);
            if (result == ItemAddedResult.SUCCESS)
            {
                added++;
                continue;
            }

            warn?.Invoke(
                $"[Andern] Spare mag fit failed for `{magTpl}` ({result}); placed {existing + added}/{spareCount}");
            break;
        }
    }

    static ItemAddedResult TryAddMagazine(
        MongoId botId,
        BotBaseInventory inventory,
        MongoId magTpl,
        List<Item> magWithAmmo,
        BotGeneratorHelper botGeneratorHelper)
    {
        var last = ItemAddedResult.NO_SPACE;
        foreach (var slot in SlotPriority)
        {
            last = botGeneratorHelper.AddItemWithChildrenToEquipmentSlot(
                botId,
                [slot],
                magWithAmmo[0].Id,
                magTpl,
                magWithAmmo,
                inventory);

            if (last == ItemAddedResult.SUCCESS)
            {
                return last;
            }
        }

        return last;
    }

    static int CountLooseMags(List<Item> items, Item weaponMag, ItemHelper itemHelper)
    {
        return items.Count(item =>
            itemHelper.IsOfBaseclass(item.Template, BaseClasses.MAGAZINE)
            && item.Id != weaponMag.Id
            && item.SlotId != "mod_magazine"
            && item.Template == weaponMag.Template);
    }

    static List<AmmoStackEntry> ResolveSpareLoad(ResolvedAmmo? resolved, string chamberAmmoTpl)
    {
        if (resolved?.SpareLoad is { Count: > 0 })
        {
            return resolved.SpareLoad;
        }

        if (resolved != null && !string.IsNullOrEmpty(resolved.SpareAmmoTpl))
        {
            return [new AmmoStackEntry { Id = resolved.SpareAmmoTpl, Count = -1 }];
        }

        var tpl = !string.IsNullOrEmpty(chamberAmmoTpl) ? chamberAmmoTpl : "";
        return [new AmmoStackEntry { Id = tpl, Count = -1 }];
    }

    static List<Item> CreateFilledMagazine(
        MongoId magTpl,
        TemplateItem magTemplate,
        IReadOnlyList<AmmoStackEntry> load,
        ItemHelper itemHelper)
    {
        var magazine = new Item { Id = new MongoId(), Template = magTpl };
        var list = new List<Item> { magazine };

        var useCustom = load.Count > 1 || (load.Count == 1 && load[0].Count > 0);
        if (useCustom)
        {
            MagazineLoadHelper.FillMagazineList(list, magTemplate, load, itemHelper);
            if (list.Count > 1)
            {
                return list;
            }

            list.Clear();
            list.Add(magazine);
        }

        var ammoTpl = load.FirstOrDefault(s => !string.IsNullOrEmpty(s.Id))?.Id;
        if (!string.IsNullOrEmpty(ammoTpl))
        {
            itemHelper.FillMagazineWithCartridge(list, magTemplate, ammoTpl, 1);
        }

        return list;
    }
}
