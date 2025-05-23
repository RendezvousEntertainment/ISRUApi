using I2.Loc;
using ISRUApi.Managers;
using ISRUApi.Modules;
using KSP.Game;
using KSP.Messages;
using KSP.Sim;
using KSP.Sim.Definitions;
using KSP.Sim.impl;
using KSP.Sim.State;
using KSP.UI.Binding;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;
using Debug = System.Diagnostics.Debug;
using Random = System.Random;

namespace ISRUApi.UI;

/// <summary>
/// Controller for the MyFirstWindow UI.
/// </summary>
public class MyFirstWindowController : KerbalMonoBehaviour
{
    // The UIDocument component of the window game object
    private UIDocument _window;

    // The elements of the window that we need to access
    private VisualElement _rootElement;
    private Label _messageField;
    private List<RadioButton> _radioGroup;
    Button _buttonScan;
    Button _buttonDisplayOverlay;

    private UIResourceWindowStatus _uiWindowStatus;
    double _maxRemainingTime;

    // The backing field for the IsWindowOpen property
    private bool _isWindowOpen;

    // Scan
    private bool _isScanning;

    // Overlay
    Texture _originalTexture;
    private const int OverlaySideSize = 500;
    private Material _originalMaterial;
    private Material _cbMaterial;
    private Material _cbScanningMaterial;
    private bool _displayOverlay;

    // Useful game objects
    private string _celestialBodyName;
    private VesselComponent _vessel;
    private List<PartComponentModule_ResourceScanner> _partComponentResourceScannerList;

    private Dictionary<string, List<CBResourceChart>> _cbResourceList;
    private Dictionary<string, Color> _colorMap;

    /// <summary>
    /// Runs when the window is first created, and every time the window is re-enabled.
    /// </summary>
#pragma warning disable IDE0051 // Remove unused private members
    private void OnEnable()
#pragma warning restore IDE0051 // Remove unused private members
    {
        // Get the UIDocument component from the game object
        _window = GetComponent<UIDocument>();

        // Get the root element of the window.
        // Since we're cloning the UXML tree from a VisualTreeAsset, the actual root element is a TemplateContainer,
        // so we need to get the first child of the TemplateContainer to get our actual root VisualElement.
        _rootElement = _window.rootVisualElement[0];

        // Hide the window
        _rootElement.style.display = DisplayStyle.None;

        // Plug the buttons
        _buttonScan = _rootElement.Q<Button>("button-scan");
        _buttonDisplayOverlay = _rootElement.Q<Button>("button-display-overlay");
        _buttonScan.clicked += OnClickButtonScan;
        _buttonDisplayOverlay.clicked += OnClickButtonResourceOverlay;

        // Hide the available resource fields
        HideResourceFields();

        // Radio buttons
        _radioGroup =
        [
            _rootElement.Q<RadioButton>("available-resource-1-radio"),
            _rootElement.Q<RadioButton>("available-resource-2-radio"),
            _rootElement.Q<RadioButton>("available-resource-3-radio"),
            _rootElement.Q<RadioButton>("available-resource-4-radio"),
            _rootElement.Q<RadioButton>("available-resource-5-radio")
        ];

        foreach (var button in _radioGroup)
        {
            button.RegisterValueChangedCallback(evt => ToggleRadioButton(button, evt.newValue));
        }

        // Message
        _messageField = _rootElement.Q<Label>("label-message");
        SetUserMessage("");

        // Center the window by default
        _rootElement.CenterByDefault();

        // Close Button
        var closeButton = _rootElement.Q<Button>("close-button");
        closeButton.clicked += () => IsWindowOpen = false;
    }

    private void HideResourceFields()
    {
        _rootElement.Q<VisualElement>("available-resource-1").style.display = DisplayStyle.None;
        _rootElement.Q<VisualElement>("available-resource-2").style.display = DisplayStyle.None;
        _rootElement.Q<VisualElement>("available-resource-3").style.display = DisplayStyle.None;
        _rootElement.Q<VisualElement>("available-resource-4").style.display = DisplayStyle.None;
        _rootElement.Q<VisualElement>("available-resource-5").style.display = DisplayStyle.None;
    }

    // Save the celestial body original texture
    private void SaveOriginalTexture(bool onSOIEntered = false)
    {
        if (_originalMaterial != null && !onSOIEntered) return;
        _originalMaterial = GetCelestialBodyMaterial();
        if (_originalMaterial != null) _originalTexture = _originalMaterial.mainTexture;
    }

    private static Texture2D GetLevelsImage(string celestialBodyName, string resourceName)
    {
        string filePath = "./BepInEx/plugins/ISRU/assets/images/" + celestialBodyName + "_" + resourceName + ".png";
        //string filePath = "./BepInEx/plugins/ISRU/assets/images/gradient.png";
        if (!File.Exists(filePath))
        {
            Debug.Write("ISRU File not found: " + filePath + ", switching to black texture");
            return null;
        }
        Texture2D texture = new(OverlaySideSize, OverlaySideSize);
        texture.LoadImage(File.ReadAllBytes(filePath));
        return texture;
    }

    private bool IsResourceScanned(string resourceName)
    {
        if (resourceName == null) return false;
        List<CBResourceChart> availableResourceList = _cbResourceList[_celestialBodyName];

        // Loop through all available resources on current celestial body
        foreach (CBResourceChart availableResource in availableResourceList)
        {
            if (availableResource.ResourceName == resourceName)
            {
                return availableResource.IsScanned;
            }
        }
        return false;
    }

    private void InitializeFields()
    {
        // identity card
        _rootElement.Q<Label>("identity-card-title").text = _celestialBodyName;
        _rootElement.Q<Label>("identity-card-description").text = LocalizationManager.GetTranslation(
            "ISRU/UI/IdentityCard/" + _celestialBodyName
        );

        // select the first scanned radio button by default
        for (int i = 0; i < _cbResourceList[_celestialBodyName].Count; i++)
        {
            CBResourceChart cbResource = _cbResourceList[_celestialBodyName][i];
            if (ISRUResourceManager.IsResourceScanned(cbResource.ResourceName, _celestialBodyName))
            {
                _radioGroup[i].value = true; // the first radio button is checked by default
                return;
            }
        }
    }

    private void UpdateAvailableResourceFields()
    {
        for (int i = 0; i < _cbResourceList[_celestialBodyName].Count; i++)
        {
            CBResourceChart cbResource = _cbResourceList[_celestialBodyName][i];

            // available resources list
            _rootElement.Q<VisualElement>("available-resource-" + (i + 1)).style.display = DisplayStyle.Flex;
            string label = LocalizationManager.GetTranslation("ISRU/UI/AvailableResources/Unknown");
            if (ISRUResourceManager.IsResourceScanned(cbResource.ResourceName, _celestialBodyName))
            {
                label = cbResource.ResourceName;
                // loading texture level maps
                if (_cbResourceList[_celestialBodyName][i].LevelMap == null)
                {
                    _cbResourceList[_celestialBodyName][i].LevelMap = GetLevelsImage(
                        _celestialBodyName,
                        cbResource.ResourceName
                    );
                }
                _radioGroup[i].SetEnabled(true); // enable the radio button
            }
            else
            {
                _radioGroup[i].SetEnabled(false); // disable the radio button
            }
            _rootElement.Q<Label>("available-resource-" + (i + 1) + "-name").text = label;
        }
    }

    private bool IsMapView()
    {
        return Game.GlobalGameState.GetState() == GameState.Map3DView;
    }

    /// <summary>
    /// The state of the window. Setting this value will open or close the window.
    /// </summary>
    public bool IsWindowOpen
    {
        get => _isWindowOpen;
        set
        {
            _isWindowOpen = value;

            // Game objects
            _vessel = Game?.ViewController?.GetActiveSimVessel();

            //Resource manager
            _cbResourceList = ISRUResourceManager.CbResourceList;
            _colorMap = ISRUResourceManager.ColorMap;

            SetCelestialBodyName();
            LoadCbMaterialAsync(); // asynchronous
            LoadCbScanningMaterialAsync(); // asynchronous
            SaveOriginalTexture();
            InitializeFields();

            // Set the Resource Gathering UI window status
            UpdateWindowStatus();    

            // Set the display style of the root element to show or hide the window
            _rootElement.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;

            // Update the Flight AppBar button state
            GameObject.Find(ISRUApiPlugin.ToolbarFlightButtonID)?
                .GetComponent<UIValue_WriteBool_Toggle>()?
                .SetValue(value);

            Game.Messages.Subscribe<SOIEnteredMessage>(OnSOIEntered);
        }
    }

    private void SetUserMessage(string message, bool isWarning = false)
    {
        _messageField.text = message;
        if (isWarning)
        {
            _messageField.style.color = new Color(0.89f, 0.56f, 0.31f);
        }
        else
        {
            _messageField.style.color = Color.white;
        }
    }

    private void UpdateWindowStatus()
    {
        if (_displayOverlay && ISRUResourceManager.NoResourceWasScanned(_celestialBodyName))
        {
            _uiWindowStatus = UIResourceWindowStatus.NoResourceScanned;
            return;
        }
        if (!IsMapView())
        {
            _uiWindowStatus = UIResourceWindowStatus.NotInMapView;
            _messageField.text = "";
            return;
        }
        GameObject gameObject = GameObject.Find(GetCelestialBodyPath());
        if (gameObject == null)
        {
            Debug.Write("ISRU ERROR Celestial Body Map not found");
            _uiWindowStatus = UIResourceWindowStatus.NotInMapView;
            _messageField.text = "";
            return;
        }
        if (_uiWindowStatus != UIResourceWindowStatus.DisplayingResources) // to display a new user message
        {
            _messageField.text = "";
        }
        _uiWindowStatus = UIResourceWindowStatus.DisplayingResources;
    }

    private void UpdateUserMessage()
    {
        if (_uiWindowStatus == UIResourceWindowStatus.NotInMapView && IsMapView())
        {
            _uiWindowStatus = UIResourceWindowStatus.TurnedOff;
        }
        switch (_uiWindowStatus)
        {
            case UIResourceWindowStatus.DisplayingResources:
                if (_messageField.text != "")
                {
                    break;
                }
                
                if (!_displayOverlay)
                {
                    break;
                }
                
                string resourceName = GetResourceNameSelectedRadioButton();
                string[] options;
                if (ISRUResourceManager.IsResourceScanned(resourceName, _celestialBodyName))
                {
                    options =
                    [
                        "Hmm, {0}"!,
                        "{0}! {0} everywhere!",
                        "{0} spotted!",
                        "What a wonderful resource!",
                        "Let's mine it all!",
                        "For science! And the mining industry."
                    ];
                }
                else
                {
                    options =
                    [
                        "I wonder what this resource could be.",
                        "Ooh, what treasures are hiding beneath the surface?",
                        "Fingers crossed for something good!",
                        "Time to see what this planet's made of!",
                        "Let the resource hunt begin!",
                        "I've got a good feeling about this one!",
                        "Hope it's not just more dirt.",
                        "Let's just hope the scanner doesn't pick up any angry space kraken.",
                        "Let's see if this one's got any tasty rocks!"
                    ];
                }
                Random random = new();
                int randomIndex = random.Next(options.Length);
                SetUserMessage(string.Format(options[randomIndex], resourceName));
                break;
            case UIResourceWindowStatus.TurnedOff:
                SetUserMessage("");
                break;
            case UIResourceWindowStatus.NotInMapView:
                SetUserMessage("Switch to map view to see the overlay.", true);
                break;
            case UIResourceWindowStatus.NoResourceScanned:
                SetUserMessage("No resource was scanned.", true);
                break;
            case UIResourceWindowStatus.ScanComplete:
                SetUserMessage("Scan complete.");
                break;
            case UIResourceWindowStatus.NoVesselControl:
                SetUserMessage("Insufficient vessel control.", true);
                break;
            case UIResourceWindowStatus.Scanning:
                SetUserMessage(LocalizationManager.GetTranslation("PartModules/ResourceScanner/Scanning", _maxRemainingTime));
                break;
            case UIResourceWindowStatus.NoScanner:
                SetUserMessage("No resource scanner on board.", true);
                break;
        }
    }

    private void UpdateDensityValues()
    {
        if (!_cbResourceList.ContainsKey(_celestialBodyName))
        {
            Debug.Write("ISRU _cbResourceList does not contain key " + _celestialBodyName);
            return;
        }
        int i = 1;
        foreach (CBResourceChart ressourceChart in _cbResourceList[_celestialBodyName])
        {
            float localDensity = GetLocalDensity(_vessel, ressourceChart.LevelMap);
            string density = localDensity == -1.0f ? "?" : Math.Round(100 * localDensity).ToString();
            _rootElement.Q<Label>("available-resource-" + i + "-density").text = density;
            i++;
        }
    }

#pragma warning disable IDE0051 // Remove unused private members
    private void Update()
#pragma warning restore IDE0051 // Remove unused private members
    {
        if (!_isWindowOpen)
        {
            return;
        }

        UpdateDensityValues();
        UpdateScanningData();
        UpdateAvailableResourceFields();
        UpdateUserMessage();
    }

    private void UpdateScanningData()
    {
        bool isScanOngoing = _isScanning && _partComponentResourceScannerList is { Count: > 0 };

        // Not scanning
        if (!isScanOngoing) {
            UnclickButtonScan();
            return;
        }

        // Scanning
        bool isAtLeastOneScannerActive = false;
        _maxRemainingTime = 0;
        foreach (PartComponentModule_ResourceScanner partComponent in _partComponentResourceScannerList)
        {
            _maxRemainingTime = Math.Max(partComponent.GetRemainingTime(), _maxRemainingTime);
            if (partComponent._dataResourceScanner._startScanTimestamp != 0) // scan is not complete
            {
                isAtLeastOneScannerActive = true;
            }
        }
        if (!isAtLeastOneScannerActive) // scan complete
        {
            UnclickButtonScan();
            _maxRemainingTime = 0;
            ISRUResourceManager.MarkedCelestialBodyResourcesAsScanned(
                _celestialBodyName,
                _partComponentResourceScannerList
            );
            InitializeFields();
            Renderer renderer = GameObject.Find(GetCelestialBodyPath()).GetComponent<Renderer>();
            if (_originalMaterial != null) // displays back the original texture
            {
                renderer.material = _originalMaterial;
            }
            _uiWindowStatus = UIResourceWindowStatus.ScanComplete;
        }
        else
        {
            _uiWindowStatus = UIResourceWindowStatus.Scanning;
        }
    }

    private static float GetLocalDensity(VesselComponent vessel, Texture2D levelTex)
    {
        if (vessel == null)
        {
            Debug.Write("ISRU vessel is null");
            return 0.0f;
        }
        if (levelTex == null) // level map is unknown when the resource has not been scanned
        {
            return -1.0f;
        }
        double longitude = vessel.Longitude;
        double latitude = vessel.Latitude;
        double longNorm = (180 + longitude) / 360;
        double latNorm = (90 + latitude) / 180;
        int x = (int)Math.Round(longNorm * OverlaySideSize);
        int y = (int)Math.Round(latNorm * OverlaySideSize);
        if (x < 0 || x > levelTex.width || y < 0 || y > levelTex.height)
        {
            Debug.Write("ISRU ERROR coordinates out of bound: x=" + x + "; y=" + y);
            return 0.0f;
        }
        Color pixelColor = levelTex.GetPixel(x, y);
        return pixelColor.r; // we assume the texture is grayscale and only use the red value
    }

    /// <summary>
    /// Returns the density at the current vessel location.
    /// </summary>
    public static float GetDensity(string resourceName, VesselComponent vessel)
    {
        if (vessel == null)
        {
            Debug.Write("ISRU ERROR GetDensity vessel is null");
            return 0.0f;
        }
        if (resourceName == null)
        {
            Debug.Write("ISRU ERROR GetDensity resourceName is null");
            return 0.0f;
        }
        Texture2D texture = GetLevelsImage(vessel.mainBody.Name, resourceName);
        return GetLocalDensity(vessel, texture);
    }

    private void SetCelestialBodyName()
    {
        _celestialBodyName = Game.ViewController.GetActiveSimVessel().mainBody.Name;
    }

    /// <summary>
    /// Returns the path of the current celestial body game object where the material is applied.
    /// </summary>
    private string GetCelestialBodyPath()
    {
        return "Map3D(Clone)/Map-" + _celestialBodyName + "/Celestial." + _celestialBodyName + ".Scaled(Clone)";
    }

    private Material GetCelestialBodyMaterial()
    {
        GameObject gameObj = GameObject.Find(GetCelestialBodyPath());
        if (gameObj == null)
        {
            Debug.Write("ISRU ERROR Celestial Body Map not found");
            UpdateWindowStatus();
            return null;
        }

        Material material = gameObj.GetComponent<Renderer>()?.material;
        if (material == null)
        {
            Debug.Write("ISRU ERROR material not found");
            return null;
        }
        return material;
    }

    private string GetResourceNameSelectedRadioButton()
    {
        foreach (var radioButton in _radioGroup)
        {
            if (radioButton.value)
            {
                return _cbResourceList[_celestialBodyName][radioButton.tabIndex-1].ResourceName;
            }
        }
        return "";
    }

    private void DisplayResourceShader()
    {
        GameObject gameObj = GameObject.Find(GetCelestialBodyPath());
        Renderer renderer = gameObj.GetComponent<Renderer>();

        if (!_displayOverlay && _originalMaterial != null) // displays back the original texture
        {
            Debug.Write("ISRU DisplayResourceShader displaying back original texture");
            renderer.material = _originalMaterial;
            return;
        }

        string resourceName = GetResourceNameSelectedRadioButton();

        if (!ISRUResourceManager.IsResourceScanned(resourceName, _celestialBodyName))
        {
            Debug.Write("ISRU DisplayResourceShader " + resourceName + " is not scanned");
            return;
        }

        SaveOriginalTexture();

        // if the overlay toggle is not enabled
        if (_displayOverlay == false) return;
        if (_cbMaterial == null)
        {
            Debug.Write("ISRU ERROR material not loaded :(");
            return;
        }
        if (gameObj == null)
        {
            Debug.Write("ISRU ERROR DisplayResourceShader: Celestial Body Map not found");
            return;
        }
        
        renderer.material = _cbMaterial;
        renderer.material.SetTexture("_DensityMap", GetLevelsImage(_celestialBodyName, resourceName));
        renderer.material.SetTexture("_CbAlbedo", _originalTexture);
        Color color = _colorMap.GetValueOrDefault(resourceName, new Color(237, 31, 255, 1));
        renderer.material.SetColor("_Color", color);
        Debug.Write("ISRU material has been replaced");
    }

    private void DisplayScanningShader()
    {
        if (!_isScanning) return;

        SaveOriginalTexture();

        if (_cbScanningMaterial == null)
        {
            Debug.Write("ISRU ERROR DisplayScanningShader: material not loaded :(");
            return;
        }

        GameObject gameObj = GameObject.Find(GetCelestialBodyPath());
        Renderer renderer = gameObj.GetComponent<Renderer>();

        if (gameObj == null)
        {
            Debug.Write("ISRU ERROR DisplayScanningShader: Celestial Body Map not found");
            return;
        }

        renderer.material = _cbScanningMaterial;
        renderer.material.SetTexture("_MainTex", _originalTexture);
        renderer.material.SetColor("_Color", new Color(0, 1, 55)); // TODO make custom colors per CB
    }

    private void ToggleRadioButton(RadioButton button, bool newValue)
    {
        if (!newValue) return;
        UpdateWindowStatus();
        _messageField.text = "";
        UpdateUserMessage();
        if (button == null) return;
        if (!_displayOverlay) return;
        DisplayResourceShader();
    }

    private void OnSOIEntered(MessageCenterMessage message)
    {
        Debug.Write("ISRU OnSOIEntered");
        SetCelestialBodyName();
        HideResourceFields();
        InitializeFields();
        SaveOriginalTexture(true);
        UnclickDisplayOverlayButton();
    }

    private async void LoadCbMaterialAsync()
    {
        AsyncOperationHandle<Material> handle = Addressables.LoadAssetAsync<Material>("isruCbMaterial.mat");
        await handle.Task;
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            _cbMaterial = handle.Result;
            Debug.Write("ISRU LoadCbMateralAsync material isruCbMaterial.mat loaded");
        }
        else
        {
            Debug.Write("ISRU LoadCbMateralAsync failed to load material: isruCbMaterial.mat " + handle.Status);
        }
    }

    private async void LoadCbScanningMaterialAsync()
    {
        AsyncOperationHandle<Material> opHandle = Addressables.LoadAssetAsync<Material>("isruCbScanning.mat");
        await opHandle.Task;
        if (opHandle.Status == AsyncOperationStatus.Succeeded)
        {
            _cbScanningMaterial = opHandle.Result;
            Debug.Write("ISRU LoadCbScanningMateralAsync material isruCbScanning.mat loaded");
        }
        else
        {
            Debug.Write("ISRU LoadCbScanningMateralAsync failed to load material: isruCbScanning.mat " + opHandle.Status);
        }
    }

#pragma warning disable IDE0051 // Remove unused private members
    private void OnDestroy()
#pragma warning restore IDE0051 // Remove unused private members
    {
        _originalMaterial = null;
    }

    private void UnclickButtonScan()
    {
        _buttonScan.RemoveFromClassList("tinted"); // remove color change
        _isScanning = false;
    }

    private void UnclickDisplayOverlayButton()
    {
        _buttonDisplayOverlay.RemoveFromClassList("tinted"); // remove color change
        _displayOverlay = false;
    }

    private void OnClickButtonScan()
    {
        _isScanning = !_isScanning;

        // Scanning complete
        if (!_isScanning)
        {
            UnclickButtonScan();
            return;
        }

        // If no control, we don't scan
        VesselState state = (VesselState)_vessel.GetState();
        if (state.ControlState.HasValue && state.ControlState.Value != VesselControlState.FullControl)
        {
            _vessel.NotifyInsufficientVesselControl();
            _uiWindowStatus = UIResourceWindowStatus.NoVesselControl;
            UnclickButtonScan();
            return;
        }

        // If no scanner onboard, we don't scan
        _partComponentResourceScannerList = _vessel.SimulationObject.PartOwner
            .GetPartModules<PartComponentModule_ResourceScanner>();
        if (!_partComponentResourceScannerList.Any())
        {
            _uiWindowStatus = UIResourceWindowStatus.NoScanner;
            UnclickButtonScan();
            return;
        }

        // Button color change
        _buttonScan.AddToClassList("tinted");

        // Disable the other button
        UnclickDisplayOverlayButton();

        // Run scanning through action group
        _vessel.TriggerActionGroup(KSPActionGroup.Custom01); // start scanning with all scan parts // TODO in Redux change to custom Scan Resource action group
        DisplayScanningShader();
    }

    private void OnClickButtonResourceOverlay()
    {
        _displayOverlay = !_displayOverlay;
        if (_displayOverlay)
        {
            _buttonDisplayOverlay.AddToClassList("tinted");
        }
        else
        {
            UnclickDisplayOverlayButton();
            SetUserMessage("");
        }
        UpdateWindowStatus();
        UpdateUserMessage();
        DisplayResourceShader();
        if (_uiWindowStatus == UIResourceWindowStatus.NoResourceScanned)
        {
            UnclickDisplayOverlayButton();
        }
    }
}
