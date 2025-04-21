using I2.Loc;
using ISRUApi.UI;
using KSP.Game;
using KSP.Modules;
using KSP.Sim;
using KSP.Sim.impl;
using KSP.Sim.ResourceSystem;

namespace ISRUApi.Modules;

public struct OutputResource
{
    public double Rate { get; }
    public float Density { get; }

    public OutputResource(double rate, float density)
    {
        Rate = rate;
        Density = density;
    }
}

public class PartComponentModule_Mining : PartComponentModule
{
    public override Type PartBehaviourModuleType => typeof(Module_Mining);

    // Module data
    private Data_Mining _dataMining;
    private VesselComponent _activeVessel;

    // Container group for the vessel
    private ResourceContainerGroup _containerGroup;

    // Ingredient & products units
    private ResourceUnitsPair[] _currentIngredientUnits;
    private ResourceUnitsPair[] _currentProductUnits;
    //private double _oreStandardRate;

    // Useful game objects
    private ResourceDefinitionDatabase _resourceDB;

    //private string outOfStorageProduct;
    private string missingIngredient;

    //private float _localDensity = -1;
    Dictionary<string, OutputResource> _localDensities = new Dictionary<string, OutputResource>();

    protected Data_Deployable dataDeployable;

    public override void OnStart(double universalTime)
    {
        if (!DataModules.TryGetByType(out _dataMining))
        {
            System.Diagnostics.Debug.Write("Unable to find a Data_Mining in the PartComponentModule for " + this.Part.PartName);
        }
        else if (GameManager.Instance.Game == null || GameManager.Instance.Game.ResourceDefinitionDatabase == null)
        {
            System.Diagnostics.Debug.Write("Unable to find a valid game with a resource definition database");
        }
        else
        {
            // Initialize useful objects
            _containerGroup = Part.PartOwner.ContainerGroup;
            _resourceDB = GameManager.Instance.Game.ResourceDefinitionDatabase;

            // Set up resource request
            SetupIngredientDataStructures();
            System.Diagnostics.Debug.Write("ISRU OnStart success");
        }
    }

    public override void OnUpdate(double universalTime, double deltaUniversalTime)
    {
        if (_activeVessel == null) _activeVessel = GameManager.Instance?.Game?.ViewController?.GetActiveVehicle(true)?.GetSimVessel(true);
        if (_dataMining.EnabledToggle.GetValue())
        {
            UpdateIngredients();
            SendResourceRequest(deltaUniversalTime);

            for (var i = 0; i < _currentProductUnits.Length; ++i)
            {
                var outputName = _dataMining.MiningFormulaDefinitions.OutputResources[i].ResourceName;
                if (!_localDensities.ContainsKey(outputName) || !IsVesselLanded())
                {
                    double rate = _dataMining.MiningFormulaDefinitions.OutputResources[i].Rate;
                    float density = MyFirstWindowController.GetDensity(outputName, Game.ViewController.GetActiveSimVessel());
                    _localDensities[outputName] = new OutputResource(rate, density);
                }
            }

            if (_localDensities.ContainsKey("Nickel")) {
                _dataMining.NickelRateTxt.SetValue(_localDensities["Nickel"].Rate * _localDensities["Nickel"].Density);
            }
            if (_localDensities.ContainsKey("Regolith"))
            {
                _dataMining.RegolithRateTxt.SetValue(_localDensities["Regolith"].Rate * _localDensities["Regolith"].Density);
            }
        }
        SetStatusTxt();
    }

    /**
    * Compute the status for every frame.
    **/
    public void SetStatusTxt()
    {
        if (_dataMining.status == ResourceConversionStateMinig.InsufficientContainment.Description())
        {
            _dataMining.statusTxt.SetValue(LocalizationManager.GetTranslation(_dataMining.status)); // out of storage
        }
        else if (_dataMining.status == ResourceConversionState.InsufficientResource.Description())
        {
            _dataMining.statusTxt.SetValue(LocalizationManager.GetTranslation(_dataMining.status, missingIngredient)); // out of input resource
        } else if (_dataMining.EnabledToggle.GetValue())
        {
            if (!_dataMining.PartIsDeployed)
            {
                _dataMining.status = ResourceConversionStateMinig.NotDeployed.Description(); // not deployed
                _dataMining.statusTxt.SetValue(LocalizationManager.GetTranslation(_dataMining.status));
            } else if (_dataMining.status == ResourceConversionStateMinig.TooHigh.Description())
            {
                _dataMining.statusTxt.SetValue(LocalizationManager.GetTranslation(_dataMining.status)); // too high
            }
            else {
                _dataMining.status = ResourceConversionState.Operational.Description(); // active
                _dataMining.statusTxt.SetValue(LocalizationManager.GetTranslation(_dataMining.status));
            }

        } else
        {
            _dataMining.status = ResourceConversionState.Inactive.Description(); // inactive
            _dataMining.statusTxt.SetValue(LocalizationManager.GetTranslation(_dataMining.status));
        }
    }

    /**
     * Update ingredient and product data structures
     **/
    private void UpdateIngredients()
    {
        // Ingredients
        missingIngredient = null;
        for (var i = 0; i < _currentIngredientUnits.Length; ++i)
        {
            var inputName = _dataMining.MiningFormulaDefinitions.InputResources[i].ResourceName;

            _currentIngredientUnits[i].units = _dataMining.MiningFormulaDefinitions.InputResources[i].Rate;

            // Remove ingredient from request if container empty
            if (_containerGroup.GetResourceStoredUnits(_currentIngredientUnits[i].resourceID) < _dataMining.MiningFormulaDefinitions.AcceptanceThreshold)
            {
                missingIngredient = inputName;
                _dataMining.EnabledToggle.SetValue(false);
                _currentIngredientUnits[i].units = 0.0;
                _dataMining.status = ResourceConversionState.InsufficientResource.Description();
            }
        }

        // Products
        //outOfStorageProduct = null;
        bool isEachProductOutOfStorage = true;
        for (var i = 0; i < _currentProductUnits.Length; ++i)
        {
            //var outputName = _dataMining.MiningFormulaDefinitions.OutputResources[i].ResourceName;
            double productCapacity = _containerGroup.GetResourceCapacityUnits(_currentProductUnits[i].resourceID);
            double storedProduct = _containerGroup.GetResourceStoredUnits(_currentProductUnits[i].resourceID);

            _currentProductUnits[i].units = _dataMining.MiningFormulaDefinitions.OutputResources[i].Rate;

            // Remove product from request if container full
            if (productCapacity - storedProduct < _dataMining.MiningFormulaDefinitions.AcceptanceThreshold)
            {
                //outOfStorageProduct = outputName;
                _currentProductUnits[i].units = 0.0;
            }
            else
            {
                isEachProductOutOfStorage = false;
            }
        }
        if (isEachProductOutOfStorage)
        {
            _dataMining.status = ResourceConversionStateMinig.InsufficientContainment.Description();
            _dataMining.EnabledToggle.SetValue(false);
        }
    }

    private bool IsVesselLanded()
    {
        if (_activeVessel == null) return false;
        return VesselSituations.Landed.Equals(_activeVessel.Situation);
    }

    /**
     * Consume ingredients and produce products based on the current consumption data structures and elapsed time
     **/
    private void SendResourceRequest(double deltaTime)
    {
        if (!this._dataMining.EnabledToggle.GetValue()) return; // if drill is inactive, do nothing

        var inputCount = _dataMining.MiningFormulaDefinitions.InputResources.Count;
        var outputCount = _dataMining.MiningFormulaDefinitions.OutputResources.Count;

        // Ingredients
        if (_dataMining.status == ResourceConversionState.Operational.Description())
        {
            for (var i = 0; i < inputCount; ++i)
                _containerGroup.RemoveResourceUnits(_currentIngredientUnits[i].resourceID, _currentIngredientUnits[i].units,
                    deltaTime);
        }

        // Products
        double altitude = 0.0;
        if (_activeVessel != null) {
            altitude = _activeVessel.AltitudeFromScenery;
        } else
        {
            System.Diagnostics.Debug.Write("ISRU Ground Altitude not computable");
        }
        if (altitude > 5.0 || !IsVesselLanded()) { // if drill is not on the ground, do nothing
            _dataMining.status = ResourceConversionStateMinig.TooHigh.Description();
        }
        if (_dataMining.status == ResourceConversionState.Operational.Description()) {
            for (var i = 0; i < outputCount; ++i)
            {
                var outputName = _dataMining.MiningFormulaDefinitions.OutputResources[i].ResourceName;
                double standardRate = _currentProductUnits[i].units;
                float localDensity = _localDensities[outputName].Density;
                _containerGroup.AddResourceUnits(_currentProductUnits[i].resourceID, standardRate * localDensity, deltaTime);
            }
        }
    }

    /**
     * Setup the data structures storing the ingredients and products for mining on the part
     **/
    private void SetupIngredientDataStructures()
    {
        if (_dataMining.MiningFormulaDefinitions == null)
        {
            System.Diagnostics.Debug.Write("[ISRU] ERROR Unable to find MiningFormulaDefinitions.");
            return;
        }
        var inputCount = _dataMining.MiningFormulaDefinitions.InputResources.Count;
        var outputCount = _dataMining.MiningFormulaDefinitions.OutputResources.Count;
        _currentIngredientUnits = new ResourceUnitsPair[inputCount];
        _currentProductUnits = new ResourceUnitsPair[outputCount];
        
        var resourceUnitsPair = new ResourceUnitsPair();
        // Initializing the ingredients data
        for (var i = 0; i < inputCount; ++i)
        {
            // Resource name
            var inputName = _dataMining.MiningFormulaDefinitions.InputResources[i].ResourceName;

            // Rate
            var rate = _dataMining.MiningFormulaDefinitions.InputResources[i].Rate;
            
            // Setup the resource
            resourceUnitsPair.resourceID = _resourceDB.GetResourceIDFromName(inputName);
            resourceUnitsPair.units = rate;
            _currentIngredientUnits[i] = resourceUnitsPair;
        }
        // Initializing the products data
        for (var i = 0; i < outputCount; ++i)
        {
            // Resource name
            var outputName = _dataMining.MiningFormulaDefinitions.OutputResources[i].ResourceName;

            // Rate
            var rate = _dataMining.MiningFormulaDefinitions.OutputResources[i].Rate;
            //_oreStandardRate = rate; // TODO only works when there is one product

            // Setup resource
            resourceUnitsPair.resourceID = _resourceDB.GetResourceIDFromName(outputName);
            resourceUnitsPair.units = rate;
            _currentProductUnits[i] = resourceUnitsPair;
        }
    }
}