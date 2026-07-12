using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;

namespace BarlogM_Andern;

[Injectable(InjectionType.Singleton,
    TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class MapBotTuning(
    ISptLogger<MapBotTuning> logger,
    DatabaseService databaseService,
    ConfigServer configServer,
    BotHelper botHelper,
    ModData modData
)
    : IOnLoad
{
    private readonly ModConfig _modConfig = modData.ModConfig;
    private readonly BotConfig _botConfig = configServer.GetConfig<BotConfig>();
    private readonly PmcConfig _pmcConfig = configServer.GetConfig<PmcConfig>();

    public Task OnLoad()
    {
        if (_modConfig.MapBotSettings)
        {
            TunePmc();
            TuneScavs();
        }

        return Task.CompletedTask;
    }

    private void TunePmc()
    {
        if (_modConfig.MapMakePmcAlwaysHostile)
        {
            MakePmcAlwaysHostile();
        }

        if (_modConfig.MapPmcBrainsAsLive)
        {
            SetPmcBrainsAsLive();
        }

        MapBossChanceAdjustment();

        TunePmcGear();
    }

    private void MapBossChanceAdjustment()
    {
        foreach (var locationId in ModData.ALL_MAPS)
        {
            var location = databaseService.GetLocation(locationId);
            foreach (var bossLocationSpawn in location.Base.BossLocationSpawn)
            {
                if (locationId == "labyrinth") continue;
                var bossName = bossLocationSpawn.BossName.ToLower();
                if (
                    bossName is "pmcusec" or "pmcbear" or "pmcbot"
                    or "crazyassaultevent" or "exusec" or "arenafighterevent"
                )
                {
                    continue;
                }

                if (bossLocationSpawn.BossChance is >= 100 or <= 0)
                {
                    continue;
                }

                var newChance = bossLocationSpawn.BossChance +
                                modData.ModConfig.MapBossChanceAdjustment;

                var chance = Math.Clamp(Math.Round(newChance.Value), 0, 100);

                if (_modConfig.MapBossPartisanDisable &&
                    bossName == "bosspartisan")
                {
                    chance = 0;
                }

                if (bossName == "bossknight")
                {
                    if (_modConfig.MapBossGoonsDisable)
                    {
                        chance = 0;
                        _botConfig.GoonSpawnSystem.SpawnChance = 0;
                    }
                    else if (chance >= 100)
                    {
                        _botConfig.GoonSpawnSystem.Enabled = false;
                        _botConfig.GoonSpawnSystem.SpawnChance = chance;
                    }
                    else
                    {
                        _botConfig.GoonSpawnSystem.SpawnChance = chance;
                    } 
                } 

                bossLocationSpawn.BossChance = chance;

                if (_modConfig.Debug)
                {
                    logger.LogWithColor(
                        $"[Andern] '{location.Base.Name}' boss '{bossLocationSpawn.BossName}' chance {bossLocationSpawn.BossChance}",
                        LogTextColor.Blue);
                }
            }
        }

        if (_modConfig.Debug)
        {
            logger.LogWithColor(
                $"[Andern] BotConfig.GoonSpawnSystem.Enabled = {_botConfig.GoonSpawnSystem.Enabled}",
                LogTextColor.Blue);
            logger.LogWithColor(
                $"[Andern] BotConfig.GoonSpawnSystem.SpawnChance = {_botConfig.GoonSpawnSystem.SpawnChance}",
                LogTextColor.Blue);
        }
    }

    private void SetPmcBrainsAsLive()
    {
        foreach (var locationName in ModData.ALL_MAPS)
        {
            var usecType = _pmcConfig.PmcType["pmcusec"][locationName];
            usecType.Clear();
            usecType.Add("pmcUSEC", 1);

            var bearType = _pmcConfig.PmcType["pmcbear"][locationName];
            bearType.Clear();
            bearType.Add("pmcBEAR", 1);
        }
    }

    private void MakePmcAlwaysHostile()
    {
        PmcHostilitySettings(_pmcConfig.HostilitySettings["pmcusec"]);
        PmcHostilitySettings(_pmcConfig.HostilitySettings["pmcbear"]);
    }

    private void PmcHostilitySettings(
        HostilitySettings hostilitySetting)
    {
        hostilitySetting.BearEnemyChance = 100;
        hostilitySetting.UsecEnemyChance = 100;
        hostilitySetting.SavageEnemyChance = 100;
        hostilitySetting.SavagePlayerBehaviour = "AlwaysEnemies";
        foreach (var hostilitySettingChancedEnemy in hostilitySetting
                     .ChancedEnemies!)
        {
            hostilitySettingChancedEnemy.EnemyChance = 100;
        }
    }

    private void TuneScavs()
    {
        var assaultJson = botHelper.GetBotTemplate("assault")!;
        var equipmentChances = assaultJson.BotChances.EquipmentChances;

        var modConfig = modData.ModConfig;

        if (modConfig.MapScavsAlwaysHasArmor)
        {
            _botConfig.Equipment["assault"]!.ForceOnlyArmoredRigWhenNoArmor =
                true;
            equipmentChances["ArmorVest"] = 100;
        }

        if (modConfig.MapScavsAlwaysHasBackpack)
        {
            equipmentChances["Backpack"] = 100;
        }

        if (modConfig.MapScavsAlwaysHasHeadwear)
        {
            equipmentChances["Headwear"] = 100;
        }

        if (modConfig.MapPlayerScavsBossBrainsOff)
        {
            foreach (var map in _botConfig.PlayerScavBrainType.Keys)
            {
                _botConfig.PlayerScavBrainType[map] = [];
                _botConfig.PlayerScavBrainType[map].Add("pmcBot", 1);
            }
        }
    }

    private void TunePmcGear()
    {
        _botConfig.Equipment["pmc"]!.ForceOnlyArmoredRigWhenNoArmor = true;

        foreach (var randomisationDetailse in _botConfig.Equipment["pmc"]!
                     .Randomisation!)
        {
            randomisationDetailse.Equipment["Backpack"] = 100;
            randomisationDetailse.Equipment["Earpiece"] = 100;
            randomisationDetailse.Equipment["Eyewear"] = 100;
            randomisationDetailse.Equipment["FaceCover"] = 100;
            randomisationDetailse.Equipment["FirstPrimaryWeapon"] = 100;
            randomisationDetailse.Equipment["Holster"] = 80;
            randomisationDetailse.Equipment["SecondPrimaryWeapon"] = 40;

            randomisationDetailse.EquipmentMods["back_plate"] = 100;
            randomisationDetailse.EquipmentMods["left_side_plate"] = 100;
            randomisationDetailse.EquipmentMods["right_side_plate"] = 100;
        }
    }
}
