using I2.Loc;
using ISRUApi.Managers;
using KSP.Game;
using KSP.Sim.Definitions;
using KSP.Sim.impl;
using KSP.Sim.ResourceSystem;

namespace ISRUApi.Modules;

public class PartComponentModule_ResourceScanner : PartComponentModule
{
    public override Type PartBehaviourModuleType => typeof(Module_ResourceScanner);

    // Module data
    public Data_ResourceScanner _dataResourceScanner;

    // Game objects
    private NotificationManager _notificationManager;

    // Container group for the vessel
    private ResourceContainerGroup _containerGroup;

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
        _notificationManager = Game.Notifications;
        _containerGroup = Part.PartOwner.ContainerGroup;
    }

    private bool HasEnoughResources()
    {
        if (Game.SessionManager.IsDifficultyOptionEnabled("InfinitePower")) // TODO works only if EC is the only resource
        {
            return true;
        }
        for (var i = 0; i < _dataResourceScanner.RequiredResources.Count; ++i)
        {
            PartModuleResourceSetting moduleResourceSetting = _dataResourceScanner.RequiredResources[i];
            ResourceDefinitionID resourceId = Game.ResourceDefinitionDatabase.GetResourceIDFromName(moduleResourceSetting.ResourceName);

            // Remove resource from request if container empty
            if (_containerGroup.GetResourceStoredUnits(resourceId) < moduleResourceSetting.AcceptanceThreshold)
            {
                return false;
            }
        }
        return true;
    }

    private bool AreConditionsMet()
    {
        VesselComponent vessel = Game?.ViewController?.GetActiveSimVessel(); // warning, Part.SimulationObject.Vessel is null!

        // Altitude condition
        bool altitudeCondition = true;
        if (_dataResourceScanner.minAltitude != -1 || _dataResourceScanner.maxAltitude != -1)
        {
;           double altitude = vessel.AltitudeFromSeaLevel;
            if (_dataResourceScanner.minAltitude == -1)
            {
                altitudeCondition = (altitude <= _dataResourceScanner.maxAltitude);
            }
            else if (_dataResourceScanner.maxAltitude == -1)
            {
                altitudeCondition = (altitude >= _dataResourceScanner.minAltitude);
            } else
            {
                altitudeCondition = (altitude <= _dataResourceScanner.maxAltitude) && (altitude >= _dataResourceScanner.minAltitude);
            }
        }

        if (!altitudeCondition) SetStatus(ResourceScannerStatus.IncorrectAltitude);

        // Inclination condition
        double inclination = vessel.Orbit.inclination;
        bool inclinationCondition = (_dataResourceScanner.minInclination <= inclination) && (_dataResourceScanner.maxInclination >= inclination);

        if (!inclinationCondition) SetStatus(ResourceScannerStatus.IncorrectInclination);

        return altitudeCondition && inclinationCondition;
    }

    private void RequiredResourcesConsumptionUpdate(double deltaTime)
    {
        if (Game.SessionManager.IsDifficultyOptionEnabled("InfinitePower")) // TODO works only if EC is the only resource
        {
            return;
        }

        if (!_dataResourceScanner.EnabledToggle.GetValue()) return; // if scanner is idle, do nothing

        for (var i = 0; i < _dataResourceScanner.RequiredResources.Count; ++i)
        {
            PartModuleResourceSetting moduleResourceSetting = _dataResourceScanner.RequiredResources[i];
            ResourceDefinitionID resourceId = Game.ResourceDefinitionDatabase.GetResourceIDFromName(moduleResourceSetting.ResourceName);

            // Remove resource from request if container empty
            if (_containerGroup.GetResourceStoredUnits(resourceId) < moduleResourceSetting.AcceptanceThreshold)
            {
                _dataResourceScanner.EnabledToggle.SetValue(false);
                ResourceDefinitionData definitionData = Game.ResourceDefinitionDatabase.GetDefinitionData(resourceId);
                string localizedResourceName = LocalizationManager.GetTranslation(definitionData.displayNameKey);
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
        notificationData.AlertTitle.LocKey = "Parts/Title/" + Part.Name;
        notificationData.FirstLine.LocKey = "Resource/Notifications/ScanComplete";
        notificationData.FirstLine.ObjectParams = [
            Part.PartCelestialBody.Name
        ];
        _notificationManager.ProcessNotification(notificationData);
    }

    public double GetRemainingTime()
    {
        double difference = Game.UniverseModel.Time.UniverseTime - _dataResourceScanner._startScanTimestamp;
        return Math.Ceiling(_dataResourceScanner.TimeToComplete - difference);
    }

    public override void OnUpdate(double universalTime, double deltaUniversalTime)
    {
        // Scanner not enabled
        if (!_dataResourceScanner.EnabledToggle.GetValue()) return;
        // No start date (shouldn't be possible)
        if (_dataResourceScanner._startScanTimestamp == 0) return;

        // Update resource consumption
        RequiredResourcesConsumptionUpdate(deltaUniversalTime);

        if (!HasEnoughResources()) return;

        if (!AreConditionsMet())
        {
            _dataResourceScanner.EnabledToggle.SetValue(false);
            return;
        };

        double difference = Game.UniverseModel.Time.UniverseTime - _dataResourceScanner._startScanTimestamp;
        SetStatus(ResourceScannerStatus.Scanning, GetRemainingTime());

        // Scan complete
        if (difference >= _dataResourceScanner.TimeToComplete)
        {
            SendNotification();
            SetStatus(ResourceScannerStatus.Idle);
            _dataResourceScanner._startScanTimestamp = 0;
            _dataResourceScanner.EnabledToggle.SetValue(false);
            ISRUResourceManager.MarkedCelestialBodyResourcesAsScanned(Part.PartCelestialBody.Name, [ this ]);
        }
    }

    private void SetStatus(ResourceScannerStatus status, object param = null)
    {
        _dataResourceScanner.statusTxt.SetValue(LocalizationManager.GetTranslation(status.Description(), param));
    }
}