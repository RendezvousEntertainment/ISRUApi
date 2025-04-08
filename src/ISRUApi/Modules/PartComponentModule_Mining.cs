using BepInEx;
using I2.Loc;
using KSP.Game;
using KSP.Messages.PropertyWatchers;
using KSP.Modules;
using KSP.Sim;
using KSP.Sim.impl;
using KSP.Sim.ResourceSystem;
using KSP.Tools;

namespace ISRUApi.Modules;

public class PartComponentModule_Mining : PartComponentModule
{
    public override Type PartBehaviourModuleType => typeof(Module_Mining);

    // Module data
    private Data_Mining _dataMining;

    // Container group for the vessel
    private ResourceContainerGroup _containerGroup;

    // Ingredient & products units
    private ResourceUnitsPair[] _currentIngredientUnits;
    private ResourceUnitsPair[] _currentProductUnits;

    // Useful game objects
    private ResourceDefinitionDatabase _resourceDB;

    private bool outOfStorage;
    private string outOfStorageProduct;
    private bool outOfIngredient;
    private string missingIngredient;
    private bool isTooHigh;

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
            //_notificationManager = GameManager.Instance.Game.Notifications;
            _resourceDB = GameManager.Instance.Game.ResourceDefinitionDatabase;
            //_rosterManager = GameManager.Instance.Game.SessionManager.KerbalRosterManager;

            // Set up resource request
            SetupIngredientDataStructures();
            System.Diagnostics.Debug.Write("ISRU OnStart success");
        }
    }

    public override void OnUpdate(double universalTime, double deltaUniversalTime)
    {
        UpdateIngredients();
        SendResourceRequest(deltaUniversalTime);
        SetStatus();
    }

    /**
     * Compute the status for every frame.
     **/
    public void SetStatus()
    {
        if (_dataMining.EnabledToggle.GetValue() && isTooHigh)
        {
            _dataMining.statusTxt.SetValue(LocalizationManager.GetTranslation(ResourceConversionStateMinig.TooHigh.Description())); // too far from ground
        }
        else if (_dataMining.EnabledToggle.GetValue())
        {
            _dataMining.statusTxt.SetValue(LocalizationManager.GetTranslation(ResourceConversionState.Operational.Description())); // active
        }
        else if (outOfStorage)
        {
            _dataMining.statusTxt.SetValue(LocalizationManager.GetTranslation(ResourceConversionState.InsufficientContainment.Description(), outOfStorageProduct)); // out of storage
        }
        else if (outOfIngredient)
        {
            _dataMining.statusTxt.SetValue(LocalizationManager.GetTranslation(ResourceConversionState.InsufficientResource.Description(), missingIngredient)); // out of input resource
        } else
        {
            _dataMining.statusTxt.SetValue(LocalizationManager.GetTranslation(ResourceConversionState.Inactive.Description())); // inactive
        }
    }

    /**
     * Update ingredient and product data structures
     **/
    private void UpdateIngredients()
    {
        // Ingredients
        outOfIngredient = false;
        missingIngredient = null;
        for (var i = 0; i < _currentIngredientUnits.Length; ++i)
        {
            var inputName = _dataMining.MiningFormulaDefinitions.InputResources[i].ResourceName;
            //System.Diagnostics.Debug.Write("ISRU " + inputName + " Remaining Capacity: " + _containerGroup.GetResourceCapacityUnits(_currentIngredientUnits[i].resourceID));
            //System.Diagnostics.Debug.Write("ISRU Stored " + inputName + ": " + _containerGroup.GetResourceStoredUnits(_currentIngredientUnits[i].resourceID));

            _currentIngredientUnits[i].units = _dataMining.MiningFormulaDefinitions.InputResources[i].Rate;

            // Remove ingredient from request if container empty
            if (_containerGroup.GetResourceStoredUnits(_currentIngredientUnits[i].resourceID) < _dataMining.MiningFormulaDefinitions.AcceptanceThreshold)
            {
                outOfIngredient = true;
                missingIngredient = inputName;
                _dataMining.EnabledToggle.SetValue(false);
                _currentIngredientUnits[i].units = 0.0;
            }
        }

        // Products
        outOfStorage = false;
        outOfStorageProduct = null;
        for (var i = 0; i < _currentProductUnits.Length; ++i)
        {
            var outputName = _dataMining.MiningFormulaDefinitions.OutputResources[i].ResourceName;
            double productCapacity = _containerGroup.GetResourceCapacityUnits(_currentProductUnits[i].resourceID);
            double storedProduct = _containerGroup.GetResourceStoredUnits(_currentProductUnits[i].resourceID);
            //System.Diagnostics.Debug.Write("ISRU " + outputName + " Remaining Capacity: " + _containerGroup.GetResourceCapacityUnits(_currentProductUnits[i].resourceID));
            //System.Diagnostics.Debug.Write("ISRU Stored " + outputName + ": " + storedProduct);

            _currentProductUnits[i].units = _dataMining.MiningFormulaDefinitions.OutputResources[i].Rate;

            // Remove product from request if container full
            if (productCapacity - storedProduct < _dataMining.MiningFormulaDefinitions.AcceptanceThreshold)
            {
                outOfStorage = true;
                outOfStorageProduct = outputName;
                _dataMining.EnabledToggle.SetValue(false);
                _currentProductUnits[i].units = 0.0;
            }
            
        }
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
        for (var i = 0; i < inputCount; ++i)
            _containerGroup.RemoveResourceUnits(_currentIngredientUnits[i].resourceID, _currentIngredientUnits[i].units,
                deltaTime);

        // Products
        double altitude = 0.0;
        VesselSituations situation = new();
        VesselComponent ActiveVessel = GameManager.Instance?.Game?.ViewController?.GetActiveVehicle(true)?.GetSimVessel(true);
        if (ActiveVessel != null) {
            altitude = ActiveVessel.AltitudeFromScenery;
            situation = ActiveVessel.Situation;
        } else
        {
            System.Diagnostics.Debug.Write("ISRU Ground Altitude not computable");
        }
        System.Diagnostics.Debug.Write("ISRU Ground Altitude = " + altitude);
        //System.Diagnostics.Debug.Write("ISRU Ground AltitudeFromScenery = " + ActiveVessel.AltitudeFromScenery);
        //System.Diagnostics.Debug.Write("ISRU Ground AltitudeFromSurface = " + ActiveVessel.AltitudeFromSurface);
        //System.Diagnostics.Debug.Write("ISRU Ground AltitudeFromRadius = " + ActiveVessel.AltitudeFromRadius);
        //System.Diagnostics.Debug.Write("ISRU Ground AltitudeFromSeaLevel = " + ActiveVessel.AltitudeFromSeaLevel);
        //System.Diagnostics.Debug.Write("ISRU Situation = " + situation);
        if (altitude > 5.0 || !VesselSituations.Landed.Equals(situation)) { // if drill is not on the ground, do nothing
            isTooHigh = true;
        } else
        {
            isTooHigh = false;
            for (var i = 0; i < outputCount; ++i)
                _containerGroup.AddResourceUnits(_currentProductUnits[i].resourceID, _currentProductUnits[i].units,
                    deltaTime);
        }
    }

    /**
     * Setup the data structures storing the ingredients and products for mining on the part
     **/
    private void SetupIngredientDataStructures()
    {
        System.Diagnostics.Debug.Write("ISRU 1");
        if (_dataMining.MiningFormulaDefinitions == null)
        {
            System.Diagnostics.Debug.Write("[ISRU] ERROR Unable to find MiningFormulaDefinitions.");
            return;
        }
        var inputCount = _dataMining.MiningFormulaDefinitions.InputResources.Count;
        //System.Diagnostics.Debug.Write("ISRU 2");
        var outputCount = _dataMining.MiningFormulaDefinitions.OutputResources.Count;
        //System.Diagnostics.Debug.Write("ISRU 3");
        _currentIngredientUnits = new ResourceUnitsPair[inputCount];
        _currentProductUnits = new ResourceUnitsPair[outputCount];
        
        var resourceUnitsPair = new ResourceUnitsPair();
        //System.Diagnostics.Debug.Write("ISRU 4");
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
        //System.Diagnostics.Debug.Write("ISRU 5");
        // Initializing the products data
        for (var i = 0; i < outputCount; ++i)
        {
            // Resource name
            var outputName = _dataMining.MiningFormulaDefinitions.OutputResources[i].ResourceName;

            // Rate
            var rate = _dataMining.MiningFormulaDefinitions.OutputResources[i].Rate;
            _dataMining.OreRateTxt.SetValue(rate); // TODO only works when there is one product

            // Setup resource
            resourceUnitsPair.resourceID = _resourceDB.GetResourceIDFromName(outputName);
            resourceUnitsPair.units = rate;
            _currentProductUnits[i] = resourceUnitsPair;
        }
    }
}