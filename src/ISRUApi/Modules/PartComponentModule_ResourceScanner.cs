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

    private NotificationManager _notificationManager;

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
        System.Diagnostics.Debug.Write("ISRU PartComponentModule_ResourceScanner.OnStart success");
        _notificationManager = Game.Notifications;
    }

    public override void OnUpdate(double universalTime, double deltaUniversalTime)
    {
        if (!_dataResourceScanner.EnabledToggle.GetValue()) return;
        if (_dataResourceScanner._startScanTimestamp == 0) // no start date
        {
            return;
        }

        double difference = Game.UniverseModel.Time.UniverseTime - _dataResourceScanner._startScanTimestamp;
        _dataResourceScanner.statusTxt.SetValue(LocalizationManager.GetTranslation(ResourceScannerStatus.Scanning.Description(), Math.Ceiling(difference)));
        if (difference >= _dataResourceScanner.TimeToComplete)
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
            _dataResourceScanner.statusTxt.SetValue(LocalizationManager.GetTranslation(ResourceScannerStatus.Idle.Description()));
            _dataResourceScanner._startScanTimestamp = 0;
            _dataResourceScanner.EnabledToggle.SetValue(false);
        }
    }
}