using I2.Loc;
using KSP.Game;
using KSP.Sim;
using KSP.Sim.Definitions;
using UnityEngine;

namespace ISRUApi.Modules;

[DisallowMultipleComponent]
public class Module_Mining : PartBehaviourModule
{
    public override Type PartComponentModuleType => typeof(PartComponentModule_Mining);

    [SerializeField]
    protected Data_Mining _dataMining;

    public Animator Animator;

    public override void AddDataModules()
    {
        base.AddDataModules();
        _dataMining ??= new Data_Mining();
        DataModules.TryAddUnique(_dataMining, out _dataMining);
    }

    public override void OnInitialize()
    {
        base.OnInitialize();
        
        if (PartBackingMode == PartBackingModes.Flight)
        {
            _dataMining.EnabledToggle.OnChangedValue += OnToggleChangedValue;
        }
        
        _dataMining.SetLabel(_dataMining.EnabledToggle, LocalizationManager.GetTermTranslation(_dataMining.ToggleName));
        
        AddActionGroupAction(
            StartMining,
            KSPActionGroup.None,
            LocalizationManager.GetTermTranslation(_dataMining.StartActionName)
        );
        AddActionGroupAction(
            StopMining,
            KSPActionGroup.None,
            LocalizationManager.GetTermTranslation(_dataMining.StopActionName)
        );
        AddActionGroupAction(
            ToggleMining,
            KSPActionGroup.None,
            LocalizationManager.GetTermTranslation(_dataMining.ToggleActionName)
        );
        
        UpdatePAMVisibility(_dataMining.EnabledToggle.GetValue());

        //get the animator
        if (part != null)
        {
            PartUtil.TryGetComponentInPart(part.transform, out Animator);
        }
    }
    public override void OnShutdown()
    {
        base.OnShutdown();
        _dataMining.EnabledToggle.OnChangedValue -= OnToggleChangedValue;
    }

    public override void OnModuleFixedUpdate(float fixedDeltaTime)
    {
        if (Animator.GetCurrentAnimatorStateInfo(0).IsName("isru_drill_1v_deployed"))
        {
            _dataMining.PartIsDeployed = true;
        }
    }

    private void UpdatePAMVisibility(bool state)
    {
        _dataMining.SetVisible(_dataMining.EnabledToggle, PartBackingMode == PartBackingModes.Flight);
        _dataMining.SetVisible(_dataMining.NickelRateTxt, PartBackingMode == PartBackingModes.Flight & state);
        _dataMining.SetVisible(_dataMining.RegolithRateTxt, PartBackingMode == PartBackingModes.Flight & state);
        _dataMining.SetVisible(_dataMining.statusTxt, PartBackingMode == PartBackingModes.Flight);
    }

    private void OnToggleChangedValue(bool newValue)
    {
        if (newValue && !CheckCollisions())
        {
            _dataMining.EnabledToggle.SetValue(false);
            Game.Notifications.ProcessNotification(new NotificationData
            {
                Tier = NotificationTier.Alert,
                Importance = NotificationImportance.High,
                AlertTitle =
                {
                    LocKey = "PartModules/Mining/TooHigh"
                },
            });
        }
        
        UpdatePAMVisibility(newValue);
    }

    private bool CheckCollisions()
    {
        if (!((PartComponentModule_Mining)ComponentModule).IsVesselLanded())
        {
            return false;
        }

        var cb = part.SimObjectComponent.PartOwner.SimulationObject.Vessel.mainBody;
        // var drillExtension = gameObject.transform.Find(...)
        return true;
    }

    private void StartMining() => _dataMining.EnabledToggle.SetValue(true);

    private void StopMining() => _dataMining.EnabledToggle.SetValue(false);

    private void ToggleMining() => _dataMining.EnabledToggle.SetValue(!_dataMining.EnabledToggle.GetValue());
}