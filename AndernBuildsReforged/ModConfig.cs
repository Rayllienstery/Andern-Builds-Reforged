using SPTarkov.Server.Core.Models.Enums;

namespace AndernBuildsReforged;

public class ModConfig
{
    public string Preset { get; set; } = "meta";
    public int PmcBotLevelDownDelta { get; set; }
    public int PmcBotLevelUpDelta { get; set; }
    public bool UseFixedPmcBotLevelRange { get; set; }
    public int PmcBotMinLevel { get; set; }
    public int PmcBotMaxLevel { get; set; }
    public bool Trader { get; set; }
    public bool TraderInsurance { get; set; }
    public bool TraderRepair { get; set; }
    public bool InsuranceOnLab { get; set; }
    public bool InsuranceReturnsNothing { get; set; }
    public int FleaMinUserLevel { get; set; }
    public bool MapBotSettings { get; set; }
    public int MapBossChanceAdjustment { get; set; }
    public bool MapBossPartisanDisable { get; set; }
    public bool MapBossGoonsDisable { get; set; }
    public bool MapMakePmcAlwaysHostile { get; set; }
    public bool MapPmcBrainsAsLive { get; set; }
    public bool MapScavsAlwaysHasBackpack { get; set; }
    public bool MapScavsAlwaysHasArmor { get; set; }
    public bool MapScavsAlwaysHasHeadwear { get; set; }
    public bool MapPlayerScavsBossBrainsOff { get; set; }
    public bool PmcBackpackWeaponDisable { get; set; }
    public bool EmissaryPmcBotsDisable { get; set; }
    public bool SeasonalEventsDisable { get; set; }
    public bool InsuranceDecreaseReturnTime { get; set; }
    public bool InsuranceIncreaseStorageTime { get; set; }
    public bool FleaBlacklistDisable { get; set; }
    public Season[] RandomizeSeason { get; set; }
    public bool PlayerScavAlwaysHasBackpack { get; set; }
    public bool GpCoinsOnPmcAndScavs { get; set; }
    public bool LegaMedalOnBosses { get; set; }
    public bool RemoveAllTradersItemsFromFlea { get; set; }
    public bool WeeklyBossEventDisable { get; set; }
    public bool CheeseQuests { get; set; }
    public int ScavCaseLootValueMultiplier { get; set; }
    public bool ReducePenaltiesFromLargeMagazines { get; set; }
    public bool IncreaseLabLoot { get; set; }
    public bool Debug { get; set; }

    /// <summary>
    /// Chance (%) to equip FaceCover from gear.mask (CQCM) instead of a helmet, per Andern tier.
    /// Defaults: T1=0, T2=0, T3=3, T4=6. Edit config.json5 (SPT mod config).
    /// </summary>
    public int MaskInsteadOfHelmetPercentOne { get; set; } = 0;
    public int MaskInsteadOfHelmetPercentTwo { get; set; } = 0;
    public int MaskInsteadOfHelmetPercentThree { get; set; } = 3;
    public int MaskInsteadOfHelmetPercentFour { get; set; } = 6;

    public int GetMaskInsteadOfHelmetPercent(string tierName)
    {
        var percent = tierName switch
        {
            "one" => MaskInsteadOfHelmetPercentOne,
            "two" => MaskInsteadOfHelmetPercentTwo,
            "three" => MaskInsteadOfHelmetPercentThree,
            "four" => MaskInsteadOfHelmetPercentFour,
            _ => 0,
        };
        return Math.Clamp(percent, 0, 100);
    }
}
