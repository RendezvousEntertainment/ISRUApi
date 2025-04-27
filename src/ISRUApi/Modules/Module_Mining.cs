using I2.Loc;
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
            _dataMining.EnabledToggle.OnChangedValue += new Action<bool>(OnToggleChangedValue);
        }
        _dataMining.SetLabel(_dataMining.EnabledToggle, LocalizationManager.GetTermTranslation(_dataMining.ToggleName));
        AddActionGroupAction(new Action(StartMining), KSPActionGroup.None, LocalizationManager.GetTermTranslation(_dataMining.StartActionName));
        AddActionGroupAction(new Action(StopMining), KSPActionGroup.None, LocalizationManager.GetTermTranslation(_dataMining.StopActionName));
        AddActionGroupAction(new Action(ToggleMining), KSPActionGroup.None, LocalizationManager.GetTermTranslation(_dataMining.ToggleActionName));
        UpdatePAMVisibility(_dataMining.EnabledToggle.GetValue());

        //get the animator
        if (part != null)
        {
            PartUtil.TryGetComponentInPart<Animator>(part.transform, out Animator);
        }
    }
    public override void OnShutdown()
    {
        base.OnShutdown();
        _dataMining.EnabledToggle.OnChangedValue -= new Action<bool>(OnToggleChangedValue);
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
        _dataMining.SetVisible(_dataMining.EnabledToggle, PartBackingMode == PartBehaviourModule.PartBackingModes.Flight);
        _dataMining.SetVisible(_dataMining.NickelRateTxt, PartBackingMode == PartBehaviourModule.PartBackingModes.Flight & state);
        _dataMining.SetVisible(_dataMining.RegolithRateTxt, PartBackingMode == PartBehaviourModule.PartBackingModes.Flight & state);
        _dataMining.SetVisible(_dataMining.statusTxt, PartBackingMode == PartBehaviourModule.PartBackingModes.Flight);
    }

    private void OnToggleChangedValue(bool newValue)
    {
        UpdatePAMVisibility(newValue);
    } 

    private void StartMining() => _dataMining.EnabledToggle.SetValue(true);

    private void StopMining() => _dataMining.EnabledToggle.SetValue(false);

    private void ToggleMining() => _dataMining.EnabledToggle.SetValue(!_dataMining.EnabledToggle.GetValue());
}