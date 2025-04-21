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
        if (this.PartBackingMode == PartBehaviourModule.PartBackingModes.Flight)
        {
            this._dataMining.EnabledToggle.OnChangedValue += new Action<bool>(this.OnToggleChangedValue);
        }
        this._dataMining.SetLabel((IModuleProperty)this._dataMining.EnabledToggle, LocalizationManager.GetTermTranslation(this._dataMining.ToggleName));
        this.AddActionGroupAction(new Action(this.StartMining), KSPActionGroup.None, LocalizationManager.GetTermTranslation(this._dataMining.StartActionName));
        this.AddActionGroupAction(new Action(this.StopMining), KSPActionGroup.None, LocalizationManager.GetTermTranslation(this._dataMining.StopActionName));
        this.AddActionGroupAction(new Action(this.ToggleMining), KSPActionGroup.None, LocalizationManager.GetTermTranslation(this._dataMining.ToggleActionName));
        this.UpdatePAMVisibility(this._dataMining.EnabledToggle.GetValue());

        //get the animator
        if (part != null)
        {
            PartUtil.TryGetComponentInPart<Animator>(part.transform, out Animator);
        }
    }
    public override void OnShutdown()
    {
        base.OnShutdown();
        this._dataMining.EnabledToggle.OnChangedValue -= new Action<bool>(this.OnToggleChangedValue);
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
        this.UpdatePAMVisibility(newValue);
    } 

    private void StartMining() => this._dataMining.EnabledToggle.SetValue(true);

    private void StopMining() => this._dataMining.EnabledToggle.SetValue(false);

    private void ToggleMining() => this._dataMining.EnabledToggle.SetValue(!this._dataMining.EnabledToggle.GetValue());
}