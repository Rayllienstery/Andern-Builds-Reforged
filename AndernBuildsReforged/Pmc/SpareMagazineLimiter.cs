using System.Collections.Generic;
using System.Linq;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;

namespace AndernBuildsReforged;

internal static class SpareMagazineLimiter
{
    public const int DrumMagMinCapacity = 61;
    public const int MediumMagMinCapacity = 50;
    public const int MediumMagMaxCapacity = 60;
    public const int SmallMagMaxCapacity = 49;

    public const int DrumSpareMagazines = 1;
    public const int MediumSpareMagazines = 2;
    public const int SmallSpareMagazines = 3;

    public static void Apply(BotBaseInventory inventory, ItemHelper itemHelper)
    {
        if (inventory?.Items == null || inventory.Items.Count == 0)
        {
            return;
        }

        var weapon = inventory.Items.FirstOrDefault(item =>
            item.SlotId == EquipmentSlots.FirstPrimaryWeapon.ToString());
        if (weapon == null)
        {
            return;
        }

        var weaponMag = inventory.Items.FirstOrDefault(item =>
            item.ParentId == weapon.Id && item.SlotId == "mod_magazine");
        if (weaponMag == null)
        {
            return;
        }

        var compatibleMags = GetCompatibleMagazineTemplates(itemHelper, weapon.Template);
        compatibleMags.Add(weaponMag.Template);

        var looseMags = inventory.Items
            .Where(item =>
                itemHelper.IsOfBaseclass(item.Template, BaseClasses.MAGAZINE)
                && item.Id != weaponMag.Id
                && item.SlotId != "mod_magazine"
                && compatibleMags.Contains(item.Template))
            .OrderBy(item => item.ParentId?.ToString())
            .ThenBy(item => item.SlotId)
            .ToList();

        var capacity = GetMagazineCapacity(itemHelper, weaponMag.Template);
        var allowed = GetAllowedSpareMagazines(capacity);
        if (looseMags.Count <= allowed)
        {
            return;
        }

        foreach (var mag in looseMags.Skip(allowed))
        {
            RemoveItemTree(inventory.Items, mag.Id);
        }
    }

    static int GetAllowedSpareMagazines(int capacity)
    {
        if (capacity >= DrumMagMinCapacity)
        {
            return DrumSpareMagazines;
        }

        if (capacity is >= MediumMagMinCapacity and <= MediumMagMaxCapacity)
        {
            return MediumSpareMagazines;
        }

        return SmallSpareMagazines;
    }

    static HashSet<MongoId> GetCompatibleMagazineTemplates(ItemHelper itemHelper, MongoId weaponTpl)
    {
        var compatible = new HashSet<MongoId>();
        var lookup = itemHelper.GetItem(weaponTpl);
        if (!lookup.Key || lookup.Value == null)
        {
            return compatible;
        }

        foreach (var slot in lookup.Value.Properties?.Slots ?? [])
        {
            if (slot.Name != "mod_magazine")
            {
                continue;
            }

            foreach (var filter in slot.Properties?.Filters ?? [])
            {
                foreach (var tpl in filter.Filter ?? [])
                {
                    compatible.Add(tpl);
                }
            }
        }

        return compatible;
    }

    static int GetMagazineCapacity(ItemHelper itemHelper, MongoId magTpl)
    {
        var lookup = itemHelper.GetItem(magTpl);
        if (!lookup.Key || lookup.Value == null)
        {
            return 0;
        }

        var cartridgeSlot = lookup.Value.Properties?.Cartridges?.FirstOrDefault();
        return cartridgeSlot?.MaxCount is > 0 ? (int)cartridgeSlot.MaxCount : 0;
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
}
