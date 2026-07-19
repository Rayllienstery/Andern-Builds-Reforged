using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace AndernBuildsReforged;

public class PresetData
{
    public PresetConfig PresetConfig { get; set; } = new();
    public PresetGear PresetGear { get; set; } = new();
    public List<WeaponPreset> Weapon { get; set; } = [];
    public Dictionary<string, string[]> Ammo { get; set; } = new();
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
    /// Relative spawn weight (int). Higher = more often. Null/≤0 treated as 1.
    /// </summary>
    public int? Weight { get; set; }

    public int EffectiveWeight => Weight is > 0 ? Weight.Value : 1;
}

public class SelectedWeaponPreset
{
    public List<Item> Items { get; set; } = [];
    public string Name { get; set; } = "";
    public int SpareMags { get; set; }
}

public class GeneratedWeapon
{
    public List<Item> WeaponWithMods { get; set; }
    public TemplateItem WeaponTemplate { get; set; }
    public string AmmoTpl { get; set; }
    public string MagazineTpl { get; set; }

    /// <summary>Spare magazine count taken from the chosen preset.</summary>
    public int SpareMags { get; set; }
}

public class GearItem
{
    public double Weight { get; set; }
    public string Id { get; set; }
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
