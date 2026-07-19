using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;

namespace AndernBuildsReforged;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PreSptModLoader + 1)]
public class EtcPreSpt(
    ISptLogger<EtcPreSpt> logger,
    ConfigServer configServer,
    ModData modData
)
    : IOnLoad
{
    private readonly ModConfig _modConfig = modData.ModConfig;
    
    private readonly SeasonalEventConfig _seasonalEventConfig = configServer.GetConfig<SeasonalEventConfig>();
    private readonly BotConfig _botConfig = configServer.GetConfig<BotConfig>();

    public Task OnLoad()
    {
        if (_modConfig.SeasonalEventsDisable)
        {
            SeasonalEventsDisable();
        }

        if (_modConfig.WeeklyBossEventDisable)
        {
            WeeklyBossEventDisable();
        }

        return Task.CompletedTask;
    }

    private void SeasonalEventsDisable()
    {
        _seasonalEventConfig.EnableSeasonalEventDetection = false;
    }

    private void WeeklyBossEventDisable()
    {
        _botConfig.WeeklyBoss.Enabled = false;
    }
}
