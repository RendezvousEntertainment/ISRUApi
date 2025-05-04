using System.ComponentModel;
using I2.Loc;
using KSP;
using KSP.Modules;
using KSP.Sim;
using KSP.Sim.Definitions;
using KSP.Sim.ResourceSystem;
using Newtonsoft.Json;
using UnityEngine;

namespace ISRUApi.Modules;

/// <summary>
/// This class defines the properties of the mining module.
/// </summary>
[Serializable]
public class Data_Mining : ModuleData
{
    /// <summary>
    /// The module type.
    /// </summary>
    public override Type ModuleType => typeof(Module_Mining);

    /// <summary>
    /// Boolean toggle describing on the PAM if the mining device is enabled.
    /// </summary>
    [LocalizedField("PartModules/Mining/Enabled")]
    [KSPState]
    [HideInInspector]
    [PAMDisplayControl(SortIndex = 1)]
    public ModuleProperty<bool> EnabledToggle = new(false);

    /// <summary>
    /// The status of the mining device, displayed on the PAM
    /// </summary>
    [LocalizedField("PartModules/Mining/Status")]
    [PAMDisplayControl(SortIndex = 2)]
    [JsonIgnore]
    [HideInInspector]
    public ModuleProperty<string> statusTxt = new(null, true, GetConversionStatusString);

    [LocalizedField("PartModules/Mining/NickelRate")]
    [PAMDisplayControl(SortIndex = 3)]
    [JsonIgnore]
    public ModuleProperty<double> NickelRateTxt = new(0.0, true, GetOreRateOutputString);

    [LocalizedField("PartModules/Mining/RegolithRate")]
    [PAMDisplayControl(SortIndex = 4)]
    [JsonIgnore]
    public ModuleProperty<double> RegolithRateTxt = new(0.0, true, GetOreRateOutputString);

    [KSPDefinition]
    public ResourceConverterFormulaDefinition MiningFormulaDefinitions; // TODO turn into list like in Data_ResourceConverter

    [KSPDefinition]
    public string ToggleName = "PartModules/Mining/Enabled";
    [KSPDefinition]
    public string StartActionName = "PartModules/Mining/StartMining";
    [KSPDefinition]
    public string StopActionName = "PartModules/Mining/StopMining";
    [KSPDefinition]
    public string ToggleActionName = "PartModules/Mining/ToggleMining";

    [KSPDefinition]
    public string status;

    [KSPDefinition]
    [HideInInspector]
    [JsonIgnore]
    public bool PartIsDeployed;

    [JsonIgnore]
    public PartComponentModule_Mining PartComponentModule;

    private List<OABPartData.PartInfoModuleSubEntry> GetInputStrings(ResourceConverterFormulaDefinition formula)
    {
        List<OABPartData.PartInfoModuleSubEntry> inputStrings = [];
        for (int index = 0; index < formula.InputResources.Count(); ++index)
        {
            ResourceDefinitionData definitionData = Game.ResourceDefinitionDatabase.GetDefinitionData(
                Game.ResourceDefinitionDatabase.GetResourceIDFromName(formula.InputResources[index].ResourceName)
            );
            
            inputStrings.Add(new OABPartData.PartInfoModuleSubEntry(string.Format(
                LocalizationManager.GetTranslation("PartModules/Generic/Tooltip/ResourceRate"),
                definitionData.DisplayName,
                Units.PrintFormattedRate(
                    formula.InputResources[index].Rate,
                    PartModuleTooltipLocalization.GetTooltipResourceUnits(definitionData.name),
                    numDigits: 6
                )
            )));
        }
        return inputStrings;
    }

    private List<OABPartData.PartInfoModuleSubEntry> GetOutputStrings(ResourceConverterFormulaDefinition formula)
    {
        List<OABPartData.PartInfoModuleSubEntry> outputStrings = [];
        for (int index = 0; index < formula.OutputResources.Count(); ++index)
        {
            ResourceDefinitionData definitionData = Game.ResourceDefinitionDatabase.GetDefinitionData(
                Game.ResourceDefinitionDatabase.GetResourceIDFromName(formula.OutputResources[index].ResourceName)
            );
            
            outputStrings.Add(new OABPartData.PartInfoModuleSubEntry(string.Format(
                LocalizationManager.GetTranslation("PartModules/Generic/Tooltip/ResourceRate"),
                definitionData.DisplayName,
                Units.PrintFormattedRate(
                    formula.OutputResources[index].Rate,
                    PartModuleTooltipLocalization.GetTooltipResourceUnits(definitionData.name),
                    numDigits: 1
                )
            )));
        }
        return outputStrings;
    }

    private List<OABPartData.PartInfoModuleSubEntry> GetConverterFormulas(OABPartData.OABSituationStats oabSituationStats)
    {
        List<OABPartData.PartInfoModuleSubEntry> converterFormulas = [];
        //for (int index = 0; index < this.FormulaDefinitions.Count<ResourceConverterFormulaDefinition>(); ++index)
            converterFormulas.Add(new OABPartData.PartInfoModuleSubEntry(
                LocalizationManager.GetTranslation(MiningFormulaDefinitions.FormulaLocalizationKey),
                GetConverterFormulaEntry(oabSituationStats, MiningFormulaDefinitions)
            ));
        return converterFormulas;
    }

#pragma warning disable IDE0060 // Remove unused parameter
    private List<OABPartData.PartInfoModuleSubEntry> GetConverterFormulaEntry(
        OABPartData.OABSituationStats oabSituationStats,
        ResourceConverterFormulaDefinition formula
    )
#pragma warning restore IDE0060 // Remove unused parameter
    {
        List<OABPartData.PartInfoModuleSubEntry> converterFormulaEntry =
        [
            new OABPartData.PartInfoModuleSubEntry(
                LocalizationManager.GetTranslation("PartModules/Generic/Tooltip/Inputs"),
                GetInputStrings(formula)
            ),
            new OABPartData.PartInfoModuleSubEntry(
                LocalizationManager.GetTranslation("PartModules/Generic/Tooltip/Outputs"),
                GetOutputStrings(formula)
            ),
        ];
        return converterFormulaEntry;
    }

    public override List<OABPartData.PartInfoModuleEntry> GetPartInfoEntries(Type partBehaviourModuleType, List<OABPartData.PartInfoModuleEntry> delegateList)
    {
        if (partBehaviourModuleType == ModuleType)
            delegateList.Add(new OABPartData.PartInfoModuleEntry(
                LocalizationManager.GetTranslation("PartModules/ResourceConverter/Tooltip/Modes"),
                GetConverterFormulas
            ));
        return delegateList;
    }

    public static string GetOreRateOutputString(object valueObj) =>
        $"{Math.Abs((double)valueObj):F3} {Units.SymbolTonne}/{Units.SymbolSeconds}";

    private static string GetConversionStatusString(object valueObj) => (string)valueObj;

}

[Serializable]
public enum ResourceConversionStateMining : byte
{
    None,
    [Description("PartModules/Mining/TooHigh")] TooHigh,
    [Description("PartModules/Mining/NotDeployed")] NotDeployed,
    [Description("PartModules/Mining/InsufficientContainment")] InsufficientContainment,
}

