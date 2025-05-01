using I2.Loc;
using KSP.Game;
using KSP.Modules;
using KSP.Sim;
using KSP.Sim.Definitions;
using KSP.Sim.impl;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ISRUApi.Modules;

[DisallowMultipleComponent]
public class Module_ResourceScanner : PartBehaviourModule
{
    public override Type PartComponentModuleType => typeof(PartComponentModule_ResourceScanner);

    [SerializeField]
    protected Data_ResourceScanner _dataResourceScanner;

    public override void AddDataModules()
    {
        base.AddDataModules();
        _dataResourceScanner ??= new Data_ResourceScanner();
        DataModules.TryAddUnique(_dataResourceScanner, out _dataResourceScanner);
    }

    public override void OnInitialize()
    {
        base.OnInitialize();
        if (PartBackingMode == PartBackingModes.Flight)
        {
            _dataResourceScanner.EnabledToggle.OnChangedValue += new Action<bool>(OnToggleChangedValue);
        }
        _dataResourceScanner.SetLabel(_dataResourceScanner.EnabledToggle, LocalizationManager.GetTermTranslation(_dataResourceScanner.ToggleName));
        AddActionGroupAction(new Action(StartScanning), KSPActionGroup.None, LocalizationManager.GetTermTranslation(_dataResourceScanner.StartActionName));
        AddActionGroupAction(new Action(StartScanning), KSPActionGroup.Custom01, LocalizationManager.GetTranslation(_dataResourceScanner.StartActionName)); // TODO in Redux create a ScanResource action group
        UpdatePAMVisibility();
        _dataResourceScanner.statusTxt.SetValue(LocalizationManager.GetTranslation(ResourceScannerStatus.Idle.Description()));
        
    }
    public override void OnShutdown()
    {
        base.OnShutdown();
        _dataResourceScanner.EnabledToggle.OnChangedValue -= new Action<bool>(OnToggleChangedValue);
    }

    public override void OnModuleFixedUpdate(float fixedDeltaTime)
    {
        // nothing
    }

    private void UpdatePAMVisibility()
    {
        _dataResourceScanner.SetVisible(_dataResourceScanner.EnabledToggle, PartBackingMode == PartBackingModes.Flight);
        _dataResourceScanner.SetVisible(_dataResourceScanner.statusTxt, PartBackingMode == PartBackingModes.Flight);
    }


    private void OnToggleChangedValue(bool newValue)
    {
        if (PartBackingMode != PartBackingModes.Flight)
        {
            return;
        }
        //_dataResourceScanner.statusTxt.SetValue(LocalizationManager.GetTranslation(ResourceScannerStatus.Scanning.Description()));
        //_dataResourceScanner._startScanTimestamp = DateTime.Now;
        _dataResourceScanner._startScanTimestamp = Game.UniverseModel.Time.UniverseTime;
        //_notificationManager.ProcessNotification(this._experimentNotifications[ExperimentState.RUNNING]);
    } 

    private void StartScanning() => _dataResourceScanner.EnabledToggle.SetValue(true);


}