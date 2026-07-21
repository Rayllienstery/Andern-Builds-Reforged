using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace AndernBuildsReforged;

public class PresetData
{
    public PresetConfig PresetConfig { get; set; } = new();
    public PresetGear PresetGear { get; set; } = new();
    public List<WeaponPreset> Weapon { get; set; } = [];
    public Dictionary<string, List<AmmoEntry>> Ammo { get; set; } = new();
    public Dictionary<string, string[]> Modules { get; set; } = new();
}

public class PresetConfig
{
    public int MinLevel { get; set; }
    public int MaxLevel { get; set; }
    public int KittedHelmetPercent { get; set; }
    public int NightVisionPercent { get; set; }

    public MinMax<int> GetMinMax()
    {
        return new MinMax<int>
        {
            Min = MinLevel,
            Max = MaxLevel
        };
    }
}

public class WeaponPreset
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Root { get; set; }
    public List<Item> Items { get; set; }
    public string Parent { get; set; }

    /// <summary>
    /// Allow-list of map IDs (canonical or short aliases). Null/empty = all maps.
    /// </summary>
    public List<string>? Locations { get; set; }

    /// <summary>
    /// How many spare magazines (same tpl as mounted) to put in pockets/vest.
    /// Null = capacity heuristic via <see cref="SpareMagazineDefaults.FromCapacity"/>.
    /// </summary>
    public int? SpareMags { get; set; }

    /// <summary>
    /// Relative spawn weight (int). Higher = more often. Null/≤0 treated as 1000.
    /// </summary>
    public int? Weight { get; set; }

    public int EffectiveWeight => Weight is > 0 ? Weight.Value : 1000;

    /// <summary>
    /// Solid ammo tpl for chamber + mounted magazine. When set (and <see cref="AmmoLoad"/> is null),
    /// skips <c>ammo.json5</c> entirely.
    /// </summary>
    public string? PrimaryAmmoTpl { get; set; }

    /// <summary>
    /// Solid ammo for spare magazines. Null → same as primary / chamber.
    /// </summary>
    public string? SpareAmmoTpl { get; set; }

    /// <summary>
    /// Mag load preset (vanilla "Load from Preset" style). When set, skips <c>ammo.json5</c>.
    /// Stacks are ordered top→bottom (first entry feeds first). <c>count</c> ≤0 fills remaining capacity.
    /// </summary>
    public List<AmmoStackEntry>? AmmoLoad { get; set; }

    /// <summary>
    /// Optional mixed load for spare magazines. Null → <see cref="SpareAmmoTpl"/> or solid chamber tpl.
    /// </summary>
    public List<AmmoStackEntry>? SpareAmmoLoad { get; set; }
}

/// <summary>One stack in a magazine load preset (vanilla Load from Preset).</summary>
public class AmmoStackEntry
{
    public string Id { get; set; } = "";

    /// <summary>Rounds in this stack. ≤0 means "fill remaining magazine capacity".</summary>
    public int Count { get; set; } = -1;
}

public class SelectedWeaponPreset
{
    public List<Item> Items { get; set; } = [];
    public string Name { get; set; } = "";
    public int SpareMags { get; set; }

    /// <summary>Preset-level ammo override; null → pick from ammo.json5.</summary>
    public ResolvedAmmo? AmmoOverride { get; set; }
}

public class GeneratedWeapon
{
    public List<Item> WeaponWithMods { get; set; }
    public TemplateItem WeaponTemplate { get; set; }

    /// <summary>Chamber / chosen ammo tpl (also used by SPT spare-mag filler as default).</summary>
    public string AmmoTpl { get; set; }

    public string MagazineTpl { get; set; }

    /// <summary>Spare magazine count taken from the chosen preset.</summary>
    public int SpareMags { get; set; }

    /// <summary>Full ammo resolution (primary load + spare). Used to refill spares after SPT adds them.</summary>
    public ResolvedAmmo? ResolvedAmmo { get; set; }
}

/// <summary>
/// Resolved ammo for one weapon generation: chamber + primary mag load + spare policy.
/// </summary>
public class ResolvedAmmo
{
    public string ChamberTpl { get; set; } = "";

    public List<AmmoStackEntry> PrimaryLoad { get; set; } = [];

    /// <summary>When null, spare mags are filled solid with <see cref="SpareAmmoTpl"/>.</summary>
    public List<AmmoStackEntry>? SpareLoad { get; set; }

    public string SpareAmmoTpl { get; set; } = "";

    public bool SpareDiffersFromPrimary
    {
        get
        {
            if (SpareLoad is { Count: > 0 })
            {
                return true;
            }

            return !string.IsNullOrEmpty(SpareAmmoTpl)
                   && !string.Equals(SpareAmmoTpl, ChamberTpl, StringComparison.Ordinal);
        }
    }

    public static ResolvedAmmo FromSolid(string primaryTpl, string? spareTpl = null)
    {
        var primary = primaryTpl ?? "";
        var spare = string.IsNullOrEmpty(spareTpl) ? primary : spareTpl;
        return new ResolvedAmmo
        {
            ChamberTpl = primary,
            PrimaryLoad = [new AmmoStackEntry { Id = primary, Count = -1 }],
            SpareAmmoTpl = spare,
            SpareLoad = null,
        };
    }

    public static ResolvedAmmo FromLoad(
        List<AmmoStackEntry> primaryLoad,
        string? spareTpl = null,
        List<AmmoStackEntry>? spareLoad = null)
    {
        var chamber = primaryLoad.FirstOrDefault(s => !string.IsNullOrEmpty(s.Id))?.Id ?? "";
        var spare = string.IsNullOrEmpty(spareTpl) ? chamber : spareTpl;
        return new ResolvedAmmo
        {
            ChamberTpl = chamber,
            PrimaryLoad = primaryLoad,
            SpareAmmoTpl = spare,
            SpareLoad = spareLoad,
        };
    }
}

public class GearItem
{
    public double Weight { get; set; }
    public string Id { get; set; }
}

/// <summary>
/// One ammo choice for a caliber pool. Missing/≤0 Weight → 1000.
/// Either solid <see cref="Id"/> or a mag <see cref="Load"/> preset (same weight scale).
/// </summary>
public class AmmoEntry
{
    public const int DefaultWeight = 1000;

    /// <summary>Solid cartridge tpl. Ignored when <see cref="Load"/> is set.</summary>
    public string Id { get; set; } = "";

    /// <summary>Relative pick weight. Null/≤0 treated as <see cref="DefaultWeight"/>.</summary>
    public int? Weight { get; set; }

    public int EffectiveWeight => Weight is > 0 ? Weight.Value : DefaultWeight;

    /// <summary>
    /// Mag load preset (weighted like a single solid <see cref="Id"/> entry).
    /// </summary>
    public List<AmmoStackEntry>? Load { get; set; }

    /// <summary>Optional solid spare-mag ammo when this entry is picked.</summary>
    public string? SpareId { get; set; }

    /// <summary>Optional mixed spare-mag load when this entry is picked.</summary>
    public List<AmmoStackEntry>? SpareLoad { get; set; }

    public bool IsLoadPreset => Load is { Count: > 0 };

    public string ChamberTpl =>
        IsLoadPreset
            ? Load!.FirstOrDefault(s => !string.IsNullOrEmpty(s.Id))?.Id ?? ""
            : Id;

    public ResolvedAmmo ToResolved()
    {
        if (IsLoadPreset)
        {
            return ResolvedAmmo.FromLoad(Load!, SpareId, SpareLoad);
        }

        return ResolvedAmmo.FromSolid(Id, SpareId);
    }
}

public class PresetGear
{
    public List<GearItem> Headsets { get; set; }
    public List<GearItem> Helmets { get; set; }
    public List<GearItem> ArmoredRigs { get; set; }
    public List<GearItem> Armor { get; set; }
    public List<GearItem> Rigs { get; set; }
    public List<GearItem> Backpacks { get; set; }
    public List<GearItem> Face { get; set; }
    public List<GearItem> Eyewear { get; set; }
    public List<GearItem> Sheath { get; set; }
    public List<GearItem> Mask { get; set; }
}
