using System;
using I2.Loc;
using ISRUApi.UI;
using KSP.Game;
using KSP.Messages.PropertyWatchers;
using KSP.Modules;
using KSP.Sim;
using KSP.Sim.Definitions;
using KSP.Sim.impl;
using KSP.Sim.ResourceSystem;

namespace ISRUApi.Modules;

public class PartComponentModule_ResourceScanner : PartComponentModule
{
    public override Type PartBehaviourModuleType => typeof(Module_ResourceScanner);

    // Module data
    private Data_ResourceScanner _dataResourceScanner;

    // Game objects
    private NotificationManager _notificationManager;

    // Container group for the vessel
    private ResourceContainerGroup _containerGroup;

    // Scanner attributes
    //private ResourceUnitsPair[] _currentRequiredResourcetUnits;
    //private ResourceDefinitionDatabase _resourceDB;
    private bool _hasEnoughResources = true;


    public override void OnStart(double universalTime)
    {
        if (!DataModules.TryGetByType(out _dataResourceScanner))
        {
            System.Diagnostics.Debug.Write("Unable to find a Data_Mining in the PartComponentModule for " + Part.PartName);
            return;
        }
        else if (GameManager.Instance.Game == null || GameManager.Instance.Game.ResourceDefinitionDatabase == null)
        {
            System.Diagnostics.Debug.Write("Unable to find a valid game with a resource definition database");
            return;
        }
        //System.Diagnostics.Debug.Write("ISRU PartComponentModule_ResourceScanner.OnStart success");
        _notificationManager = Game.Notifications;
        //_resourceDB = GameManager.Instance.Game.ResourceDefinitionDatabase;
        _containerGroup = Part.PartOwner.ContainerGroup;
        //SetupIngredientDataStructures();
    }

    /**
     * Setup the data structures storing the required resources.
     **/
    //private void SetupIngredientDataStructures()
    //{
    //    if (_dataResourceScanner.RequiredResources == null)
    //    {
    //        System.Diagnostics.Debug.Write("[ISRU] ERROR Unable to find RequiredResources.");
    //        return;
    //    }

    //    var count = _dataResourceScanner.RequiredResources.Count;
    //    _currentRequiredResourcetUnits = new ResourceUnitsPair[count];
    //    var resourceUnitsPair = new ResourceUnitsPair();

    //    // Initializing the data
    //    for (var i = 0; i < count; ++i)
    //    {
    //        string inputName = _dataResourceScanner.RequiredResources[i].ResourceName;
    //        double rate = _dataResourceScanner.RequiredResources[i].Rate;
    //        resourceUnitsPair.resourceID = _resourceDB.GetResourceIDFromName(inputName);
    //        resourceUnitsPair.units = rate;
    //        _currentRequiredResourcetUnits[i] = resourceUnitsPair;
    //    }
    //}

    private void RequiredResourcesConsumptionUpdate(double deltaTime)
    {
        if (Game.SessionManager.IsDifficultyOptionEnabled("InfinitePower"))
        {
            _hasEnoughResources = true;
            return;
        }

        if (!_dataResourceScanner.EnabledToggle.GetValue()) return; // if scanner is idle, do nothing

        for (var i = 0; i < _dataResourceScanner.RequiredResources.Count; ++i)
        {
            PartModuleResourceSetting moduleResourceSetting = _dataResourceScanner.RequiredResources[i];
            ResourceDefinitionID resourceId = Game.ResourceDefinitionDatabase.GetResourceIDFromName(moduleResourceSetting.ResourceName);

            // Update the current consumption
            //_currentRequiredResourcetUnits[i].units = moduleResourceSetting.Rate;

            // Remove resource from request if container empty
            if (_containerGroup.GetResourceStoredUnits(resourceId) < moduleResourceSetting.AcceptanceThreshold)
            {
                _dataResourceScanner.EnabledToggle.SetValue(false);
                //_currentRequiredResourcetUnits[i].units = 0.0;
                _hasEnoughResources = false;
                ResourceDefinitionData definitionData = Game.ResourceDefinitionDatabase.GetDefinitionData(resourceId);
                string localizedResourceName = LocalizationManager.GetTranslation(definitionData.displayNameKey, true, 0, true, false, null, null, true);
                SetStatus(ResourceScannerStatus.OutOfResource, localizedResourceName);
            } else
            // Send request otherwise
            {
                _containerGroup.RemoveResourceUnits(resourceId, moduleResourceSetting.Rate, deltaTime);
            }
        }
    }

    private void SendNotification()
    {
        NotificationData notificationData = new()
        {
            Tier = NotificationTier.Alert,
            Importance = NotificationImportance.Low,
            TimeStamp = Game.UniverseModel.Time.UniverseTime
        };
        notificationData.AlertTitle.LocKey = "Resource/Notifications/ScanningDone";
        notificationData.FirstLine.LocKey = "Resource/Notifications/ScanningDoneMessage";
        _notificationManager.ProcessNotification(notificationData);
    }

    public override void OnUpdate(double universalTime, double deltaUniversalTime)
    {
        // Scanner not enabled
        if (!_dataResourceScanner.EnabledToggle.GetValue()) return;
        // No start date (shouldn't be possible)
        if (_dataResourceScanner._startScanTimestamp == 0) return;

        // Update resource consumption
        RequiredResourcesConsumptionUpdate(deltaUniversalTime);
        //SendResourceRequest(deltaUniversalTime);

        if (!_hasEnoughResources) return;

        double difference = Game.UniverseModel.Time.UniverseTime - _dataResourceScanner._startScanTimestamp;
        SetStatus(ResourceScannerStatus.Scanning, Math.Ceiling(_dataResourceScanner.TimeToComplete - difference));
        if (difference >= _dataResourceScanner.TimeToComplete)
        {
            SendNotification();
            SetStatus(ResourceScannerStatus.Idle);
            _dataResourceScanner._startScanTimestamp = 0;
            _dataResourceScanner.EnabledToggle.SetValue(false);
        }
    }

    private void SetStatus(ResourceScannerStatus status, object param = null)
    {
        _dataResourceScanner.statusTxt.SetValue(LocalizationManager.GetTranslation(status.Description(), param));
    }
}