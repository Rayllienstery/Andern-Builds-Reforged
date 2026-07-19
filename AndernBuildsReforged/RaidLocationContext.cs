namespace AndernBuildsReforged;

/// <summary>
/// Holds the current raid map ID for the duration of PMC inventory generation
/// so Data.GetRandomWeapon (and Raylee re-rolls via GenerateWeapon) can filter presets.
/// </summary>
public static class RaidLocationContext
{
    static readonly AsyncLocal<string?> Current = new();

    public static string? Location
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}
