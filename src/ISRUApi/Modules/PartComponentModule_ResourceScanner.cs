using I2.Loc;
using ISRUApi.UI;
using KSP.Game;
using KSP.Modules;
using KSP.Sim;
using KSP.Sim.impl;
using KSP.Sim.ResourceSystem;

namespace ISRUApi.Modules;

public class PartComponentModule_ResourceScanner : PartComponentModule
{
    public override Type PartBehaviourModuleType => typeof(Module_ResourceScanner);

    // Module data
    private Data_ResourceScanner _dataResourceScanner;

    public override void OnStart(double universalTime)
    {
        if (!DataModules.TryGetByType(out _dataResourceScanner))
        {
            System.Diagnostics.Debug.Write("Unable to find a Data_Mining in the PartComponentModule for " + Part.PartName);
        }
        else if (GameManager.Instance.Game == null || GameManager.Instance.Game.ResourceDefinitionDatabase == null)
        {
            System.Diagnostics.Debug.Write("Unable to find a valid game with a resource definition database");
        }
        else
        {
            // TODO
            System.Diagnostics.Debug.Write("ISRU PartComponentModule_ResourceScanner.OnStart success");
        }
    }

    public override void OnUpdate(double universalTime, double deltaUniversalTime)
    {
        if (_dataResourceScanner.EnabledToggle.GetValue())
        {
            // TODO
        }
        SetStatusTxt();
    }

    /**
    * Compute the status for every frame.
    **/
    public void SetStatusTxt()
    {
        // TODO
    }
}