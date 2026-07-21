using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Utils;

namespace AndernBuildsReforged;

/// <summary>
/// After the item DB is final (PostDB), drop presets / gear / module alts / ammo
/// that reference missing templates (e.g. user removed WTT / MSW).
/// </summary>
[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostDBModLoader + 2500)]
public class PresetIntegrityValidator(
    ISptLogger<PresetIntegrityValidator> logger,
    ItemHelper itemHelper,
    Data data
) : IOnLoad
{
    public Task OnLoad()
    {
        data.ValidateAndDisableBrokenEntries(itemHelper);
        PmcFactionLoadoutFilter.PruneMissingTemplates(itemHelper, msg =>
            logger.LogWithColor(msg, SPTarkov.Server.Core.Models.Logging.LogTextColor.Red));
        return Task.CompletedTask;
    }
}
