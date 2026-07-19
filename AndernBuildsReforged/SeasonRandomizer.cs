using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;

namespace AndernBuildsReforged;

[Injectable(InjectionType.Singleton)]
public class SeasonRandomizer(
    ISptLogger<SeasonRandomizer> logger,
    ConfigServer configServer,
    RandomUtil randomUtil,
    ModData modData
)
{
    private readonly ModConfig _modConfig = modData.ModConfig;
    private readonly WeatherConfig _weatherConfig = configServer.GetConfig<WeatherConfig>();

    public void RandimizeSeason()
    {
        _weatherConfig.OverrideSeason = randomUtil.GetArrayValue(_modConfig.RandomizeSeason);

        if (_modConfig.Debug)
        {
            logger.LogWithColor($"[Andern] Next raid season is: {_weatherConfig.OverrideSeason.ToString()}", LogTextColor.Blue);
        }
    }
}
