using System.Collections.Generic;
using System.Linq;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;

namespace AndernBuildsReforged;

/// <summary>
/// Fill magazines with solid ammo or multi-stack load presets (vanilla "Load from Preset").
/// </summary>
internal static class MagazineLoadHelper
{
    public static int GetCapacity(TemplateItem magTemplate, ItemHelper itemHelper)
    {
        var props = magTemplate.Properties;
        if (props == null)
        {
            return 0;
        }

        if (itemHelper.IsOfBaseclass(magTemplate.Id, BaseClasses.SPRING_DRIVEN_CYLINDER))
        {
            return props.Slots?.Count() ?? 0;
        }

        foreach (var cartridgeSlot in props.Cartridges ?? [])
        {
            if (cartridgeSlot.MaxCount is > 0)
            {
                return (int)cartridgeSlot.MaxCount.Value;
            }
        }

        // Fallback some mod mags only expose capacity via Filters / Cartridges MaxCount variants.
        return SpareMagazineDefaults.GetMagazineCapacity(itemHelper, magTemplate.Id.ToString());
    }

    /// <summary>
    /// Expand load entries against mag capacity. First stack = top / feeds first.
    /// Count ≤0 = fill remaining (shared equally if multiple fill slots).
    /// If every count is &gt;0 and the pattern is shorter than capacity, the pattern is tiled
    /// (e.g. 1×M856 + 3×M855 repeats until the mag is full).
    /// </summary>
    public static List<(string Tpl, int Count)> ExpandLoad(
        IReadOnlyList<AmmoStackEntry> load,
        int capacity)
    {
        var result = new List<(string Tpl, int Count)>();
        if (load == null || load.Count == 0 || capacity <= 0)
        {
            return result;
        }

        var valid = load.Where(s => !string.IsNullOrEmpty(s.Id)).ToList();
        if (valid.Count == 0)
        {
            return result;
        }

        var hasFill = valid.Any(s => s.Count <= 0);
        var patternTotal = valid.Where(s => s.Count > 0).Sum(s => s.Count);

        // Tile fixed pattern (1 tracer + 3 ball, etc.) across the full magazine.
        if (!hasFill && patternTotal > 0 && patternTotal < capacity)
        {
            var filled = 0;
            var index = 0;
            while (filled < capacity)
            {
                var stack = valid[index % valid.Count];
                var take = Math.Min(stack.Count > 0 ? stack.Count : 1, capacity - filled);
                if (take <= 0)
                {
                    break;
                }

                AppendOrMerge(result, stack.Id, take);
                filled += take;
                index++;
            }

            return result;
        }

        var fixedTotal = valid.Where(s => s.Count > 0).Sum(s => s.Count);
        var fillCount = valid.Count(s => s.Count <= 0);
        var remaining = Math.Max(0, capacity - fixedTotal);

        var fillEach = fillCount > 0 ? remaining / fillCount : 0;
        var fillExtra = fillCount > 0 ? remaining % fillCount : 0;
        var fillIndex = 0;

        foreach (var stack in valid)
        {
            int count;
            if (stack.Count > 0)
            {
                count = stack.Count;
            }
            else
            {
                count = fillEach + (fillIndex < fillExtra ? 1 : 0);
                fillIndex++;
            }

            if (count <= 0)
            {
                continue;
            }

            AppendOrMerge(result, stack.Id, count);
        }

        // Cap total to capacity (trim from the end / bottom of mag).
        var total = result.Sum(x => x.Count);
        while (total > capacity && result.Count > 0)
        {
            var last = result.Count - 1;
            var (tpl, count) = result[last];
            var overflow = total - capacity;
            if (count <= overflow)
            {
                total -= count;
                result.RemoveAt(last);
            }
            else
            {
                result[last] = (tpl, count - overflow);
                total = capacity;
            }
        }

        return result;
    }

    static void AppendOrMerge(List<(string Tpl, int Count)> result, string tpl, int count)
    {
        if (result.Count > 0 && result[^1].Tpl == tpl)
        {
            var last = result[^1];
            result[^1] = (last.Tpl, last.Count + count);
        }
        else
        {
            result.Add((tpl, count));
        }
    }

    /// <summary>
    /// Replace cartridge children under <paramref name="magazine"/> with the given load.
    /// Mutates <paramref name="items"/> in place.
    /// </summary>
    public static void RefillMagazine(
        List<Item> items,
        Item magazine,
        IReadOnlyList<AmmoStackEntry> load,
        ItemHelper itemHelper)
    {
        var magLookup = itemHelper.GetItem(magazine.Template);
        if (!magLookup.Key || magLookup.Value == null)
        {
            return;
        }

        items.RemoveAll(item =>
            item.ParentId == magazine.Id
            && item.SlotId?.StartsWith("cartridges") == true);

        var magazineWithCartridges = new List<Item> { magazine };
        FillMagazineList(magazineWithCartridges, magLookup.Value, load, itemHelper);

        items.Remove(magazine);
        items.AddRange(magazineWithCartridges);
    }

    /// <summary>
    /// Fill a magazine list (mag is index 0) with load stacks via <see cref="ItemHelper.CreateCartridges"/>.
    /// </summary>
    public static void FillMagazineList(
        List<Item> magazineWithCartridges,
        TemplateItem magTemplate,
        IReadOnlyList<AmmoStackEntry> load,
        ItemHelper itemHelper)
    {
        if (magazineWithCartridges.Count == 0)
        {
            return;
        }

        if (itemHelper.IsOfBaseclass(magTemplate.Id, BaseClasses.LAUNCHER))
        {
            return;
        }

        // Drop any pre-existing cartridge children before rebuilding.
        magazineWithCartridges.RemoveAll(item =>
            item != magazineWithCartridges[0]
            && item.SlotId?.StartsWith("cartridges") == true);

        var capacity = GetCapacity(magTemplate, itemHelper);
        var stacks = ExpandLoad(load, capacity);
        if (stacks.Count == 0)
        {
            return;
        }

        var location = 0;
        foreach (var (tpl, count) in stacks)
        {
            var cartridgeDetails = itemHelper.GetItem(new MongoId(tpl));
            var maxStack = cartridgeDetails.Value?.Properties?.StackMaxSize ?? count;
            if (maxStack <= 0)
            {
                maxStack = count;
            }

            var left = count;
            while (left > 0)
            {
                var chunk = Math.Min(left, maxStack);
                magazineWithCartridges.Add(
                    itemHelper.CreateCartridges(
                        magazineWithCartridges[0].Id,
                        new MongoId(tpl),
                        chunk,
                        location));
                left -= chunk;
                location++;
            }
        }

        // Single stack → clear Location (SPT convention).
        if (location == 1 && magazineWithCartridges.Count > 1)
        {
            magazineWithCartridges[1].Location = null;
        }
    }

    public static void SetChamberAmmo(List<Item> items, MongoId weaponId, MongoId ammoTpl)
    {
        foreach (var item in items.Where(item =>
                     item.ParentId == weaponId
                     && (item.SlotId == "patron_in_weapon"
                         || item.SlotId?.StartsWith("patron_in_weapon_") == true
                         || item.SlotId?.StartsWith("camora") == true)))
        {
            item.Template = ammoTpl;
            item.Upd = new Upd { StackObjectsCount = 1 };
        }
    }

    /// <summary>
    /// After SPT adds spare mags (filled with chamber tpl), rewrite them to spare load / spare tpl.
    /// </summary>
    public static void ApplySpareAmmo(
        List<Item> items,
        Item weapon,
        Item? weaponMag,
        ResolvedAmmo resolved,
        ItemHelper itemHelper)
    {
        if (!resolved.SpareDiffersFromPrimary || weaponMag == null)
        {
            return;
        }

        var spareLoad = resolved.SpareLoad is { Count: > 0 }
            ? resolved.SpareLoad
            : [new AmmoStackEntry { Id = resolved.SpareAmmoTpl, Count = -1 }];

        foreach (var mag in items.Where(item =>
                     itemHelper.IsOfBaseclass(item.Template, BaseClasses.MAGAZINE)
                     && item.Id != weaponMag.Id
                     && item.SlotId != "mod_magazine"
                     && item.Template == weaponMag.Template))
        {
            RefillMagazine(items, mag, spareLoad, itemHelper);
        }
    }
}
