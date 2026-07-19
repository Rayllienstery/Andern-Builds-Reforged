namespace AndernBuildsReforged;

/// <summary>
/// Matches raid location IDs against WeaponPreset.Locations allow-lists
/// (canonical SPT IDs plus short aliases).
/// </summary>
public static class LocationMatch
{
    static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["factory"] = ["factory4_day", "factory4_night"],
        ["streets"] = ["tarkovstreets"],
        ["customs"] = ["bigmap"],
        ["reserve"] = ["rezervbase"],
        ["groundzero"] = ["sandbox", "sandbox_high"],
        ["gz"] = ["sandbox", "sandbox_high"],
    };

    /// <summary>
    /// Expand one Locations entry to one or more canonical map IDs.
    /// </summary>
    public static IEnumerable<string> ExpandEntry(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            yield break;
        }

        var key = entry.Trim();
        if (Aliases.TryGetValue(key, out var expanded))
        {
            foreach (var id in expanded)
            {
                yield return id;
            }

            yield break;
        }

        yield return key.ToLowerInvariant();
    }

    /// <summary>
    /// True if the preset may spawn on <paramref name="location"/>.
    /// Missing / empty Locations = allowed everywhere.
    /// Null/empty location = allowed (offline / unknown raid edge).
    /// </summary>
    public static bool IsAllowed(WeaponPreset preset, string? location)
    {
        if (preset.Locations == null || preset.Locations.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            return true;
        }

        var raid = location.Trim().ToLowerInvariant();
        foreach (var entry in preset.Locations)
        {
            foreach (var canonical in ExpandEntry(entry))
            {
                if (string.Equals(canonical, raid, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
