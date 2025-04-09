using System.ComponentModel;
using KSP;
using KSP.Api;
using KSP.Modules;
using KSP.Sim;
using KSP.Sim.Definitions;
using Newtonsoft.Json;
using UnityEngine;

namespace ISRUApi.Modules;

[Serializable]
public class Data_Mining : ModuleData
{
    public override Type ModuleType => typeof(Module_Mining);

    
    [LocalizedField("PartModules/Mining/Enabled")]
    [KSPState]
    [HideInInspector]
    [PAMDisplayControl(SortIndex = 1)]
    public ModuleProperty<bool> EnabledToggle = new ModuleProperty<bool>(false);

    [LocalizedField("PartModules/Mining/Status")]
    [PAMDisplayControl(SortIndex = 2)]
    [JsonIgnore]
    [HideInInspector]
    public ModuleProperty<string> statusTxt = new ModuleProperty<string>(null, true, new ToStringDelegate(Data_Mining.GetConversionStatusString));

    [LocalizedField("PartModules/Mining/OreRate")]
    [PAMDisplayControl(SortIndex = 3)]
    [JsonIgnore]
    public ModuleProperty<double> OreRateTxt = new ModuleProperty<double>(0.0, true, new ToStringDelegate(Data_Mining.GetOreRateOutputString));

    [KSPDefinition]
    public ResourceConverterFormulaDefinition MiningFormulaDefinitions;

    [KSPDefinition]
    public string ToggleName = "PartModules/Mining/Enabled";
    [KSPDefinition]
    public string StartActionName = "PartModules/Mining/StartMining";
    [KSPDefinition]
    public string StopActionName = "PartModules/Mining/StopMining";
    [KSPDefinition]
    public string ToggleActionName = "PartModules/Mining/ToggleMining";

    [JsonIgnore]
    public PartComponentModule_Mining PartComponentModule;

    public static string GetOreRateOutputString(object valueObj) => string.Format("{0:F3} {1}/{2}", (object)Math.Abs((double)valueObj), (object)Units.SymbolTonne, (object)Units.SymbolSeconds);

    protected override void InitProperties()
    {
        base.InitProperties();

        //controlStatus.SetValue(deployed.GetValue() ? MiningState.Mining : MiningState.Stopped);
    }

    private static string GetConversionStatusString(object valueObj) => (string)valueObj;
}

public enum MiningState : byte
{
    None = 0,
    [Description("ISRUApi/Modules/Data_Mining/status/Mining")] Mining = 1,
    [Description("ISRUApi/Modules/Data_Mining/status/Stopped")] Stopped = 2,
}

[Serializable]
public enum ResourceConversionStateMinig : byte
{
    None,
    [Description("PartModules/ResourceConverter/TooHigh")] TooHigh,
}