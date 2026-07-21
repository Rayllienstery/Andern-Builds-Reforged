using System.Collections.Generic;
using System.Linq;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;

namespace AndernBuildsReforged;

/// <summary>
/// Last-pass safety net: refill empty mounted magazines and top up missing spares.
/// Catches cases where FillMagazine / MagGen silently wrote zero cartridges.
/// </summary>
internal static class MagAmmoSanity
{
    public static void Apply(
        MongoId botId,
        BotBaseInventory inventory,
        ItemHelper itemHelper,
        BotGeneratorHelper botGeneratorHelper,
        Action<string>? warn = null)
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
            ParentEquals(item.ParentId, weapon.Id) && item.SlotId == "mod_magazine");
        if (weaponMag == null)
        {
            return;
        }

        var chamber = inventory.Items.FirstOrDefault(item =>
            ParentEquals(item.ParentId, weapon.Id)
            && (item.SlotId == "patron_in_weapon"
                || item.SlotId?.StartsWith("patron_in_weapon_") == true
                || item.SlotId?.StartsWith("camora") == true));

        var ammoTpl = chamber?.Template.ToString() ?? "";
        if (string.IsNullOrEmpty(ammoTpl))
        {
            // Fall back to first cartridge already under any compatible mag, or mag filter.
            ammoTpl = inventory.Items
                .FirstOrDefault(item =>
                    ParentEquals(item.ParentId, weaponMag.Id)
                    && item.SlotId?.StartsWith("cartridges") == true)
                ?.Template.ToString() ?? "";
        }

        if (string.IsNullOrEmpty(ammoTpl))
        {
            ammoTpl = FirstCompatibleAmmo(itemHelper, weaponMag.Template);
        }

        if (string.IsNullOrEmpty(ammoTpl))
        {
            warn?.Invoke($"[Andern] MagAmmoSanity: no ammo tpl for weapon `{weapon.Template}`");
            return;
        }

        if (!MagazineHasCartridges(inventory.Items, weaponMag))
        {
            warn?.Invoke(
                $"[Andern] MagAmmoSanity: empty mounted mag `{weaponMag.Template}` — refilling with `{ammoTpl}`");
            ForceFillMagazine(inventory.Items, weaponMag, ammoTpl, itemHelper);
        }

        // Ensure at least SpareMagazineDefaults small/medium/drum count when vest was empty.
        var capacity = SpareMagazineDefaults.GetMagazineCapacity(itemHelper, weaponMag.Template.ToString());
        var want = SpareMagazineDefaults.FromCapacity(itemHelper, weaponMag.Template.ToString());
        SpareMagazineHelper.EnsureExactSpareCount(
            botId,
            inventory,
            weapon,
            weaponMag,
            want,
            ResolvedAmmo.FromSolid(ammoTpl, ammoTpl),
            ammoTpl,
            itemHelper,
            botGeneratorHelper,
            warn);
    }

    static bool MagazineHasCartridges(List<Item> items, Item magazine)
    {
        return items.Any(item =>
            ParentEquals(item.ParentId, magazine.Id)
            && item.SlotId?.StartsWith("cartridges") == true
            && (item.Upd?.StackObjectsCount ?? 0) > 0);
    }

    static void ForceFillMagazine(List<Item> items, Item magazine, string ammoTpl, ItemHelper itemHelper)
    {
        var magLookup = itemHelper.GetItem(magazine.Template);
        if (!magLookup.Key || magLookup.Value == null)
        {
            return;
        }

        items.RemoveAll(item =>
            ParentEquals(item.ParentId, magazine.Id)
            && item.SlotId?.StartsWith("cartridges") == true);

        var list = new List<Item> { magazine };
        MagazineLoadHelper.FillMagazineList(
            list,
            magLookup.Value,
            [new AmmoStackEntry { Id = ammoTpl, Count = -1 }],
            itemHelper);

        if (list.Count <= 1)
        {
            itemHelper.FillMagazineWithCartridge(list, magLookup.Value, ammoTpl, 1);
        }

        if (list.Count <= 1)
        {
            var cap = MagazineLoadHelper.GetCapacity(magLookup.Value, itemHelper);
            if (cap <= 0)
            {
                cap = 30;
            }

            list.Add(itemHelper.CreateCartridges(magazine.Id, new MongoId(ammoTpl), cap, 0));
            list[1].Location = null;
        }

        items.RemoveAll(item => item.Id == magazine.Id);
        items.AddRange(list);
    }

    static string FirstCompatibleAmmo(ItemHelper itemHelper, MongoId magTpl)
    {
        var lookup = itemHelper.GetItem(magTpl);
        var filter = lookup.Value?.Properties?.Cartridges?.FirstOrDefault()
            ?.Properties?.Filters?.FirstOrDefault()?.Filter;
        return filter?.FirstOrDefault().ToString() ?? "";
    }

    static bool ParentEquals(string? parentId, MongoId id)
    {
        if (string.IsNullOrEmpty(parentId))
        {
            return false;
        }

        return string.Equals(parentId, id.ToString(), StringComparison.Ordinal);
    }
}
