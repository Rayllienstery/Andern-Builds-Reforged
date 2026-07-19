using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace AndernBuildsReforged;

/// <summary>
/// Resolves SpareMags when a preset omits the field (capacity heuristic).
/// Builds GenerationData weights so SPT adds exactly N spares — no vest flooding.
/// </summary>
public static class SpareMagazineDefaults
{
    public const int DefaultSpareMags = 2;
    public const int DrumMagMinCapacity = 61;
    public const int MediumMagMinCapacity = 50;
    public const int DrumSpareMags = 1;
    public const int MediumSpareMags = 2;
    public const int SmallSpareMags = 3;

    public static int FromCapacity(ItemHelper itemHelper, string? magazineTpl)
    {
        if (string.IsNullOrEmpty(magazineTpl))
        {
            return DefaultSpareMags;
        }

        var capacity = GetMagazineCapacity(itemHelper, magazineTpl);
        if (capacity >= DrumMagMinCapacity)
        {
            return DrumSpareMags;
        }

        if (capacity >= MediumMagMinCapacity)
        {
            return MediumSpareMags;
        }

        if (capacity > 0)
        {
            return SmallSpareMags;
        }

        return DefaultSpareMags;
    }

    public static int GetMagazineCapacity(ItemHelper itemHelper, string magTpl)
    {
        var lookup = itemHelper.GetItem(magTpl);
        if (!lookup.Key || lookup.Value == null)
        {
            return 0;
        }

        var cartridgeSlot = lookup.Value.Properties?.Cartridges?.FirstOrDefault();
        return cartridgeSlot?.MaxCount is > 0 ? (int)cartridgeSlot.MaxCount : 0;
    }

    /// <summary>
    /// GenerationData that always picks exactly <paramref name="spareCount"/> magazines.
    /// </summary>
    public static GenerationData ExactCountWeights(int spareCount)
    {
        var count = Math.Max(0, spareCount);
        return new GenerationData
        {
            Weights = new Dictionary<double, double>
            {
                [count] = 1,
            },
            Whitelist = new Dictionary<MongoId, double>(),
        };
    }
}
