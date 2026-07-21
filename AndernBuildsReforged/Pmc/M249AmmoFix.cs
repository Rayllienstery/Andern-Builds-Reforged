using System.Collections.Generic;
using System.Linq;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;

namespace AndernBuildsReforged;

internal static class M249AmmoFix
{
    public const string M249WeaponTpl = "66e718dc498d978477e0ba75";
    public const string RpdWeaponTpl = "6513ef33e06849f06c0957ca";
    public const string RpdNWeaponTpl = "65268d8ecb944ff1e90ea385";
    public const string Ks23WeaponTpl = "5e848cc2988a8701445df1e8";
    public const string Surgeon1581WeaponTpl = "1bf618e47cce6d69bec01e9f";
    public const string G28WeaponTpl = "6176aca650224f204c1da3fb";
    public const string ScarHWeaponTpl = "6183afd850224f204c1da514";
    public const string KattAmrWeaponTpl = "020020AB50AB500000000000";
    public const string Mk18WeaponTpl = "5fc22d7c187fea44d52eda44";

    public const string M249PrimaryAmmoTpl = "54527ac44bdc2d36668b4567"; // M855A1
    public const string M249SpareAmmoTpl = "59e6906286f7746c9f75e847"; // M856A1
    public const string RpdPrimaryAmmoTpl = "5656d7c34bdc2d9d198b4587"; // PS
    public const string RpdSpareAmmoTpl = "64b7af434b75259c590fa893"; // PP (never BP / MAI AP)
    public const string Ks23AmmoTpl = "5e85a9a6eacf8c039e4e2ac1"; // Shrapnel-10
    public const string Surgeon1581PrimaryAmmoTpl = "c556f40e464c7f09d4c757fc"; // Norma Golden Target
    public const string Surgeon1581SpareAmmoTpl = "ea4cdb6fc3dd4f799eeef33e"; // Norma SMK
    public const string Hunting762AmmoTpl = "5e023e88277cce2b522ff2b1"; // TCW SP
    public const string AmrPrimaryAmmoTpl = "67dc2648ba5b79876906a166"; // M903 AP
    public const string AmrSpareAmmoTpl = "67dc255ee3028a8b120efc48"; // M33 Ball
    public const string Mk18ApAmmoTpl = "5fc382a9d724d907e2077dab"; // .338 LM AP

    static readonly Dictionary<MongoId, (MongoId Primary, MongoId Spare)> WeaponAmmo = new()
    {
        [new MongoId(M249WeaponTpl)] = (new MongoId(M249PrimaryAmmoTpl), new MongoId(M249SpareAmmoTpl)),
        [new MongoId(RpdWeaponTpl)] = (new MongoId(RpdPrimaryAmmoTpl), new MongoId(RpdSpareAmmoTpl)),
        [new MongoId(RpdNWeaponTpl)] = (new MongoId(RpdPrimaryAmmoTpl), new MongoId(RpdSpareAmmoTpl)),
        [new MongoId(Ks23WeaponTpl)] = (new MongoId(Ks23AmmoTpl), new MongoId(Ks23AmmoTpl)),
        [new MongoId(Surgeon1581WeaponTpl)] = (new MongoId(Surgeon1581PrimaryAmmoTpl), new MongoId(Surgeon1581SpareAmmoTpl)),
        [new MongoId(G28WeaponTpl)] = (new MongoId(Hunting762AmmoTpl), new MongoId(Hunting762AmmoTpl)),
        [new MongoId(ScarHWeaponTpl)] = (new MongoId(Hunting762AmmoTpl), new MongoId(Hunting762AmmoTpl)),
        [new MongoId(KattAmrWeaponTpl)] = (new MongoId(AmrPrimaryAmmoTpl), new MongoId(AmrSpareAmmoTpl)),
        [new MongoId(Mk18WeaponTpl)] = (new MongoId(Mk18ApAmmoTpl), new MongoId(Mk18ApAmmoTpl)),
    };

    public static void Apply(BotBaseInventory inventory, ItemHelper itemHelper)
    {
        if (inventory?.Items == null || inventory.Items.Count == 0)
        {
            return;
        }

        var weapon = inventory.Items.FirstOrDefault(item =>
            item.SlotId == EquipmentSlots.FirstPrimaryWeapon.ToString()
            && WeaponAmmo.ContainsKey(item.Template));

        if (weapon == null)
        {
            return;
        }

        var (primaryAmmo, spareAmmo) = WeaponAmmo[weapon.Template];
        ApplyWeaponAmmo(inventory.Items, weapon, primaryAmmo, spareAmmo, itemHelper);
    }

    static void ApplyWeaponAmmo(
        List<Item> items,
        Item weapon,
        MongoId primaryAmmo,
        MongoId spareAmmo,
        ItemHelper itemHelper)
    {
        var weaponMag = items.FirstOrDefault(item =>
            item.ParentId == weapon.Id && item.SlotId == "mod_magazine");

        if (weaponMag != null)
        {
            RefillMagazine(items, weaponMag, primaryAmmo, itemHelper);
        }

        SetChamberAmmo(items, weapon.Id, primaryAmmo);

        if (weaponMag == null)
        {
            return;
        }

        var compatibleMags = IsRpdWeapon(weapon.Template)
            ? GetCompatibleMagazineTemplates(itemHelper, weapon.Template)
            : null;
        compatibleMags?.Add(weaponMag.Template);

        foreach (var mag in items.Where(item =>
                     itemHelper.IsOfBaseclass(item.Template, BaseClasses.MAGAZINE)
                     && item.Id != weaponMag.Id
                     && item.SlotId != "mod_magazine"
                     && (compatibleMags == null
                         ? item.Template == weaponMag.Template
                         : compatibleMags.Contains(item.Template))))
        {
            RefillMagazine(items, mag, spareAmmo, itemHelper);
        }
    }

    static bool IsRpdWeapon(MongoId weaponTpl)
    {
        return weaponTpl == new MongoId(RpdWeaponTpl) || weaponTpl == new MongoId(RpdNWeaponTpl);
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

    static void RefillMagazine(List<Item> items, Item magazine, MongoId ammoTpl, ItemHelper itemHelper)
    {
        var magTemplate = itemHelper.GetItem(magazine.Template).Value;
        if (magTemplate == null)
        {
            return;
        }

        items.RemoveAll(item => item.ParentId == magazine.Id && item.SlotId?.StartsWith("cartridges") == true);

        var magazineWithCartridges = new List<Item> { magazine };
        itemHelper.FillMagazineWithCartridge(
            magazineWithCartridges,
            magTemplate,
            ammoTpl,
            1);

        items.Remove(magazine);
        items.AddRange(magazineWithCartridges);
    }

    static void SetChamberAmmo(List<Item> items, MongoId weaponId, MongoId ammoTpl)
    {
        foreach (var item in items.Where(item =>
                     item.ParentId == weaponId
                     && (item.SlotId == "patron_in_weapon"
                         || item.SlotId?.StartsWith("patron_in_weapon_") == true)))
        {
            item.Template = ammoTpl;
            item.Upd = new Upd { StackObjectsCount = 1 };
        }
    }
}
