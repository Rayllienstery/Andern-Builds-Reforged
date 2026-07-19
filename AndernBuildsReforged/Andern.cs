using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;

namespace AndernBuildsReforged;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.raylee.andern-builds-reforged";
    public override string Name { get; init; } = "Andern Builds Reforged";
    public override string Author { get; init; } = "Raylee";
    public override List<string>? Contributors { get; init; } = ["fork of Barlog_M Andern (MIT)"];
    public override SemanticVersioning.Version Version { get; init; } = new("0.1.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; } = ["li.barlog.andern"];
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; } = "https://github.com/Rayllienstery/Andern-Builds-Reforged";
    public override bool? IsBundleMod { get; init; } = false;
    public override string? License { get; init; } = "MIT";
}

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PreSptModLoader + 1)]
public class Andern(ISptLogger<Andern> logger, ModData modData) : IOnLoad
{
    public Task OnLoad()
    {
        logger.Info("[Andern-Builds-Reforged] loaded");
        return Task.CompletedTask;
    }
}
