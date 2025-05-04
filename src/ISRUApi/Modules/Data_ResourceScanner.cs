using System.ComponentModel;
using I2.Loc;
using KSP;
using KSP.Api;
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
    public ModuleProperty<string> statusTxt = new(null, true, new ToStringDelegate(GetConversionStatusString));

    [KSPDefinition]
    public List<PartModuleResourceSetting> RequiredResources;
    [KSPDefinition]
    public List<PartModuleResourceSetting> ScannableResources;
    [KSPDefinition]
    public float TimeToComplete;
    [KSPDefinition]
    public double minAltitude;
    [KSPDefinition]
    public double maxAltitude;
    [KSPDefinition]
    public double minInclination;
    [KSPDefinition]
    public double maxInclination;

    private List<OABPartData.PartInfoModuleEntry> _cachedPartInfoEntries;

    [KSPDefinition]
    public string ToggleName = "PartModules/ResourceScanner/Enabled";
    [KSPDefinition]
    public string StartActionName = "PartModules/ResourceScanner/StartScanning";

    public double _startScanTimestamp = 0;

    public override List<OABPartData.PartInfoModuleEntry> GetPartInfoEntries(Type partBehaviourModuleType, List<OABPartData.PartInfoModuleEntry> delegateList)
    {
        CheckInclinationValues();
        if (partBehaviourModuleType == ModuleType)
        {
            if (_cachedPartInfoEntries == null || _cachedPartInfoEntries.Count == 0)
            {
                _cachedPartInfoEntries =
                [
                    new OABPartData.PartInfoModuleEntry(LocalizationManager.GetTranslation("PartModules/Generic/Tooltip/Resources", true, 0, true, false, null, null, true), new OABPartData.PartInfoModuleMultipleEntryValueDelegate(GetRequiredResourceStrings)),
                    new OABPartData.PartInfoModuleEntry(LocalizationManager.GetTranslation("PartModules/ResourceScanner/Tooltip/ScannableResources", true, 0, true, false, null, null, true), new OABPartData.PartInfoModuleMultipleEntryValueDelegate(GetScannableResourceStrings)),
                    new OABPartData.PartInfoModuleEntry(LocalizationManager.GetTranslation("PartModules/ResourceScanner/Tooltip/ScanningRunTime", Units.FormatTimeString(TimeToComplete)), new OABPartData.PartInfoModuleEntryValueDelegate(GetEmptyString)),
                    new OABPartData.PartInfoModuleEntry(LocalizationManager.GetTranslation("PartModules/ResourceScanner/Tooltip/AltitudeRange"), new OABPartData.PartInfoModuleEntryValueDelegate(GetAltitudeRangeString)),
                    new OABPartData.PartInfoModuleEntry(LocalizationManager.GetTranslation("PartModules/ResourceScanner/Tooltip/InclinationRange"), new OABPartData.PartInfoModuleEntryValueDelegate(GetInclinationRangeString)),
                ];
            }
            delegateList.AddRange(_cachedPartInfoEntries);

        }
        return delegateList;
    }

    private string GetEmptyString(OABPartData.OABSituationStats oabSituationStats) => string.Empty;

    private string GetAltitudeRangeString(OABPartData.OABSituationStats oabSituationStats)
    {
        string minAltitudeString = Units.PrintSI(minAltitude, Units.SymbolMeters);
        string maxAltitudeString = Units.PrintSI(maxAltitude, Units.SymbolMeters);
        if (minAltitude == -1 && maxAltitude == -1) return LocalizationManager.GetTranslation("PartModules/ResourceScanner/Tooltip/NoCondition");
        if (minAltitude == -1) return "< " + maxAltitudeString;
        if (maxAltitude == -1) return "> " + minAltitudeString;
        return minAltitudeString + ".." + maxAltitudeString;
    }

    private string GetInclinationRangeString(OABPartData.OABSituationStats oabSituationStats)
    {
        return Units.PrintSI(minInclination, Units.SymbolDegree) + ".." + Units.PrintSI(maxInclination, Units.SymbolDegree);
    }

    private List<OABPartData.PartInfoModuleSubEntry> GetRequiredResourceStrings(OABPartData.OABSituationStats oabSituationStats)
    {
        List<OABPartData.PartInfoModuleSubEntry> resourceStrings = [];
        for (int index = 0; index < RequiredResources.Count; ++index)
        {
            ResourceDefinitionData definitionData = Game.ResourceDefinitionDatabase.GetDefinitionData(Game.ResourceDefinitionDatabase.GetResourceIDFromName(RequiredResources[index].ResourceName));
            resourceStrings.Add(new OABPartData.PartInfoModuleSubEntry(string.Format(LocalizationManager.GetTranslation("PartModules/Generic/Tooltip/ResourceRateMax", true, 0, true, false, null, null, true), definitionData.DisplayName, PartModuleTooltipLocalization.FormatResourceRate(RequiredResources[index].Rate, PartModuleTooltipLocalization.GetTooltipResourceUnits(RequiredResources[index].ResourceName)))));
        }
        return resourceStrings;
    }

    private List<OABPartData.PartInfoModuleSubEntry> GetScannableResourceStrings(OABPartData.OABSituationStats oabSituationStats)
    {
        List<OABPartData.PartInfoModuleSubEntry> resourceStrings = [];
        for (int index = 0; index < ScannableResources.Count; ++index)
        {
            ResourceDefinitionData definitionData = Game.ResourceDefinitionDatabase.GetDefinitionData(Game.ResourceDefinitionDatabase.GetResourceIDFromName(ScannableResources[index].ResourceName));
            resourceStrings.Add(new OABPartData.PartInfoModuleSubEntry(LocalizationManager.GetTranslation(definitionData.displayNameKey, true, 0, true, false, null, null, true)));
        }
        return resourceStrings;
    }

    private static string GetConversionStatusString(object valueObj) => (string)valueObj;

    // Modifies the value to get it in the range [-180; 180]
    private double GetRangedDegreeValue(double value)
    {
        //value %= 180.0;
        //if (value > 90) value -= 180.0;
        if (value < -180) return -180;
        if (value > 180) return 180;
        return value;
    }

    public void CheckInclinationValues()
    {
        minInclination = GetRangedDegreeValue(minInclination);
        maxInclination = GetRangedDegreeValue(maxInclination);
    }
}

[Serializable]
public enum ResourceScannerStatus : byte
{
    None,
    [Description("PartModules/ResourceScanner/Idle")] Idle,
    [Description("PartModules/ResourceScanner/Scanning")] Scanning,
    [Description("PartModules/ResourceScanner/OutOfResource")] OutOfResource,
    [Description("PartModules/ResourceScanner/IncorrectAltitude")] IncorrectAltitude,
    [Description("PartModules/ResourceScanner/IncorrectInclination")] IncorrectInclination,
}

