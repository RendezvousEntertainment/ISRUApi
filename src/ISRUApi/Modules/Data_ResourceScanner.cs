using System.ComponentModel;
using I2.Loc;
using KSP;
using KSP.Sim;
using KSP.Sim.Definitions;
using KSP.Sim.ResourceSystem;
using Newtonsoft.Json;
using UnityEngine;

namespace ISRUApi.Modules;

[Serializable]
public class Data_ResourceScanner : ModuleData
{
    public override Type ModuleType => typeof(Module_ResourceScanner);

    [LocalizedField("PartModules/ResourceScanner/Enabled")]
    [KSPState]
    [HideInInspector]
    [PAMDisplayControl(SortIndex = 1)]
    public ModuleProperty<bool> EnabledToggle = new(false);

    [LocalizedField("PartModules/ResourceScanner/Status")]
    [PAMDisplayControl(SortIndex = 2)]
    [JsonIgnore]
    [HideInInspector]
    public ModuleProperty<string> statusTxt = new(null, true, GetConversionStatusString);

    [KSPDefinition]
    public List<PartModuleResourceSetting> RequiredResources;
    [KSPDefinition]
    public List<PartModuleResourceSetting> ScannableResources;
    [KSPDefinition]
    public float TimeToComplete;

    private List<OABPartData.PartInfoModuleEntry> _cachedPartInfoEntries;

    [KSPDefinition]
    public string ToggleName = "PartModules/ResourceScanner/Enabled";
    [KSPDefinition]
    public string StartActionName = "PartModules/ResourceScanner/StartScanning";

    public double _startScanTimestamp;

    public override List<OABPartData.PartInfoModuleEntry> GetPartInfoEntries(
        Type partBehaviourModuleType,
        List<OABPartData.PartInfoModuleEntry> delegateList
    )
    {
        if (partBehaviourModuleType != ModuleType)
        {
            return delegateList;
        }
        
        if (_cachedPartInfoEntries == null || _cachedPartInfoEntries.Count == 0)
        {
            _cachedPartInfoEntries =
            [
                new OABPartData.PartInfoModuleEntry(
                    LocalizationManager.GetTranslation("PartModules/Generic/Tooltip/Resources"),
                    GetRequiredResourceStrings
                ),
                new OABPartData.PartInfoModuleEntry(
                    LocalizationManager.GetTranslation("PartModules/ResourceScanner/Tooltip/ScannableResources"),
                    GetScannableResourceStrings
                ),
                new OABPartData.PartInfoModuleEntry(
                    LocalizationManager.GetTranslation(
                        "PartModules/ResourceScanner/Tooltip/ScanningRunTime",
                        Units.FormatTimeString(TimeToComplete)
                    ),
                    GetTimeToCompleteString
                )
            ];
        }
        delegateList.AddRange(_cachedPartInfoEntries);
        return delegateList;
    }

    private string GetTimeToCompleteString(OABPartData.OABSituationStats oabSituationStats) => string.Empty;

    private List<OABPartData.PartInfoModuleSubEntry> GetRequiredResourceStrings(
        OABPartData.OABSituationStats oabSituationStats
    )
    {
        List<OABPartData.PartInfoModuleSubEntry> resourceStrings = [];
        for (int index = 0; index < RequiredResources.Count; ++index)
        {
            ResourceDefinitionData definitionData = Game.ResourceDefinitionDatabase.GetDefinitionData(
                Game.ResourceDefinitionDatabase.GetResourceIDFromName(RequiredResources[index].ResourceName)
            );
            resourceStrings.Add(new OABPartData.PartInfoModuleSubEntry(string.Format(
                LocalizationManager.GetTranslation("PartModules/Generic/Tooltip/ResourceRateMax"),
                definitionData.DisplayName,
                PartModuleTooltipLocalization.FormatResourceRate(
                    RequiredResources[index].Rate,
                    PartModuleTooltipLocalization.GetTooltipResourceUnits(RequiredResources[index].ResourceName))
                )
            ));
        }
        return resourceStrings;
    }

    private List<OABPartData.PartInfoModuleSubEntry> GetScannableResourceStrings(
        OABPartData.OABSituationStats oabSituationStats
    )
    {
        List<OABPartData.PartInfoModuleSubEntry> resourceStrings = [];
        for (int index = 0; index < ScannableResources.Count; ++index)
        {
            ResourceDefinitionData definitionData = Game.ResourceDefinitionDatabase.GetDefinitionData(
                Game.ResourceDefinitionDatabase.GetResourceIDFromName(ScannableResources[index].ResourceName)
            );
            resourceStrings.Add(new OABPartData.PartInfoModuleSubEntry(
                LocalizationManager.GetTranslation(definitionData.displayNameKey)
            ));
        }
        return resourceStrings;
    }

    private static string GetConversionStatusString(object valueObj) => (string)valueObj;
}

[Serializable]
public enum ResourceScannerStatus : byte
{
    None,
    [Description("PartModules/ResourceScanner/Idle")] Idle,
    [Description("PartModules/ResourceScanner/Scanning")] Scanning,
    [Description("PartModules/ResourceScanner/OutOfResource")] OutOfResource,
}

