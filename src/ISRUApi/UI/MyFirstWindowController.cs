using KSP.UI.Binding;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;
using KSP.Sim.impl;
using KSP.Game;
using I2.Loc;
using KSP.Messages;

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
    private Toggle _overlayToggle;
    private Label _messageField;
    private List<RadioButton> _radioGroup;

    private UIResourceWindowStatus _uiWindowStatus;

    // The backing field for the IsWindowOpen property
    private bool _isWindowOpen;

    // Overlay
    Texture _originalTexture;
    private const int OverlaySideSize = 500;

    // Useful game objects
    private string _celestialBodyName;
    private VesselComponent _vessel;

    private readonly Dictionary<string, List<CBResourceChart>> _cbResourceList = new()
        {
            { "Mun", new List<CBResourceChart>([new CBResourceChart("Nickel"), new CBResourceChart("Regolith"), new CBResourceChart("Water")])},
            { "Minmus", new List<CBResourceChart>([new CBResourceChart("Iron"), new CBResourceChart("Nickel"), new CBResourceChart("Quartz")])},
        };

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

        // Hide the available resource fields
        _rootElement.Q<VisualElement>("available-resource-1").style.display = DisplayStyle.None;
        _rootElement.Q<VisualElement>("available-resource-2").style.display = DisplayStyle.None;
        _rootElement.Q<VisualElement>("available-resource-3").style.display = DisplayStyle.None;

        // Radio buttons
        _radioGroup =
        [
            _rootElement.Q<RadioButton>("available-resource-1-radio"),
            _rootElement.Q<RadioButton>("available-resource-2-radio"),
            _rootElement.Q<RadioButton>("available-resource-3-radio")
        ];

        _radioGroup[0].value = true; // the first radio button is checked by default

        foreach (var button in _radioGroup)
        {
            button.RegisterValueChangedCallback(evt => ToggleRadioButton(button, evt.newValue));
        }

        // Overlay Toggle
        _overlayToggle = _rootElement.Q<Toggle>("overlay-toggle");
        _overlayToggle.RegisterValueChangedCallback(evt => OnToggleOverlay(evt.newValue));

        // Message
        _messageField = _rootElement.Q<Label>("label-message");
        SetUserMessage("", false);

        // Center the window by default
        _rootElement.CenterByDefault();

        // Close Button
        var closeButton = _rootElement.Q<Button>("close-button");
        closeButton.clicked += () => IsWindowOpen = false;
    }

    // Save the celestial body original texture
    private void SaveOriginalTexture()
    {
        if (_originalTexture != null) return;
        Material material = GetCelestialBodyMaterial();
        if (material != null) _originalTexture = material.mainTexture;
    }

    private static Texture2D GetImage(string celestialBodyName, string resourceName, string type)
    {
        string filePath = "./BepInEx/plugins/ISRU/assets/images/" + celestialBodyName + "_" + resourceName + "_" + type + ".png";
        //string filePath = "./BepInEx/plugins/ISRU/assets/images/gradient.png";
        if (!File.Exists(filePath))
        {
            System.Diagnostics.Debug.Write("ISRU File not found: " + filePath + ", switching to black texture");
            return null;
        }
        Texture2D texture = new(OverlaySideSize, OverlaySideSize);
        texture.LoadImage(File.ReadAllBytes(filePath));
        return texture;
    }

    private static Texture2D GetLevelsImage(string celestialBodyName, string resourceName)
    {
        return GetImage(celestialBodyName, resourceName, "Lev");
    }

    private static Texture2D GetTextureImage(string celestialBodyName, string resourceName)
    {
        return GetImage(celestialBodyName, resourceName, "Tex");
    }

    private void InitializeFields()
    {
        // identity card
        _rootElement.Q<Label>("identity-card-title").text = _celestialBodyName;
        _rootElement.Q<Label>("identity-card-description").text = LocalizationManager.GetTranslation("ISRU/UI/IdentityCard/" + _celestialBodyName);

        for (int i = 0; i < _cbResourceList[_celestialBodyName].Count; i++)
        {
            CBResourceChart cbResource = _cbResourceList[_celestialBodyName][i];

            // available resources list
            _rootElement.Q<VisualElement>("available-resource-" + (i+1)).style.display = DisplayStyle.Flex;
            _rootElement.Q<Label>("available-resource-" + (i+1) + "-name").text = cbResource.ResourceName;

            // loading texture level maps
            _cbResourceList[_celestialBodyName][i].LevelMap = GetLevelsImage(_celestialBodyName, cbResource.ResourceName);
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

            SetCelestialBodyName();
            SaveOriginalTexture();
            InitializeFields();

            // Set the Resource Gathering UI window status
            UpdateWindowStatus();    

            // Set the display style of the root element to show or hide the window
            _rootElement.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;

            // Update the Flight AppBar button state
            GameObject.Find(ISRUApiPlugin.ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(value);

            Game.Messages.Subscribe<SOIEnteredMessage>(new Action<MessageCenterMessage>(OnSOIEntered));
        }
    }

    private void SetUserMessage(string message, bool isWarning)
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
        if (!IsMapView())
        {
            _uiWindowStatus = UIResourceWindowStatus.NotInMapView;
            _messageField.text = "";
            return;
        }
        GameObject gameObject = GameObject.Find(GetCelestialBodyPath());
        if (gameObject == null)
        {
            System.Diagnostics.Debug.Write("ISRU ERROR Celestial Body Map not found");
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
        switch (_uiWindowStatus)
        {
            case UIResourceWindowStatus.DisplayingResources:
                if (_messageField.text != "") break;
                string resourceName = GetResourceNameSelectedRadioButton();
                string[] options = ["Hmm, {0}"!, "{0}! {0} everywhere!", "{0} spotted!", "What a wonderful resource!", "Let's mine it all!", "For science! And the mining industry."];
                System.Random random = new();
                int randomIndex = random.Next(options.Length);
                SetUserMessage(string.Format(options[randomIndex], resourceName), false);
                break;
            case UIResourceWindowStatus.TurnedOff:
                SetUserMessage("", false);
                break;
            case UIResourceWindowStatus.NotInMapView:
                SetUserMessage("Switch to map view.", true);
                break;
            case UIResourceWindowStatus.NoSuchResource:
                SetUserMessage("This body is bare of this resource.", true);
                break;
        }
    }

    private void SetDensityValues()
    {
        if (!_cbResourceList.ContainsKey(_celestialBodyName))
        {
            System.Diagnostics.Debug.Write("ISRU _cbResourceList does not contain key " + _celestialBodyName);
            return;
        }
        int i = 1;
        foreach (CBResourceChart ressourceChart in _cbResourceList[_celestialBodyName])
        {
            float density = 100 * GetLocalDensity(_vessel, ressourceChart.LevelMap);
            _rootElement.Q<Label>("available-resource-" + i + "-density").text = Math.Round(density).ToString();
            i++;
        }
    }

#pragma warning disable IDE0051 // Remove unused private members
    private void Update()
#pragma warning restore IDE0051 // Remove unused private members
    {
        if (_isWindowOpen)
        {
            SetDensityValues();
            UpdateUserMessage();
        }
            
    }

    private static float GetLocalDensity(VesselComponent vessel, Texture2D levelTex)
    {
        if (vessel == null)
        {
            System.Diagnostics.Debug.Write("ISRU vessel is null");
            return 0.0f;
        }
        if (levelTex == null)
        {
            System.Diagnostics.Debug.Write("ISRU levelTex is null");
            return 0.0f;
        }
        double longitude = vessel.Longitude;
        double latitude = vessel.Latitude;
        double long_norm = (180 + longitude) / 360;
        double lat_norm = (90 + latitude) / 180;
        int x = (int)Math.Round(long_norm * OverlaySideSize);
        int y = (int)Math.Round(lat_norm * OverlaySideSize);
        if (x < 0 || x > levelTex.width || y < 0 || y > levelTex.height)
        {
            System.Diagnostics.Debug.Write("ISRU ERROR coordinates out of bound: x=" + x + "; y=" + y);
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
            System.Diagnostics.Debug.Write("ISRU ERROR GetDensity vessel is null");
            return 0.0f;
        }
        if (resourceName == null)
        {
            System.Diagnostics.Debug.Write("ISRU ERROR GetDensity resourceName is null");
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
        GameObject gameObject = GameObject.Find(GetCelestialBodyPath());
        if (gameObject == null)
        {
            System.Diagnostics.Debug.Write("ISRU ERROR Celestial Body Map not found");
            //_uiWindowStatus = UIResourceWindowStatus.NotInMapView;
            UpdateWindowStatus();
            return null;
        }

        Material material = gameObject.GetComponent<Renderer>()?.material;
        if (material == null)
        {
            System.Diagnostics.Debug.Write("ISRU ERROR material not found");
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

    private void DisplayResourceShader(bool state)
    {
        if (!state) // displays back the original texture
        {
            Material materialCB = GetCelestialBodyMaterial();
            materialCB.mainTexture = _originalTexture;
            return;
        }

        SaveOriginalTexture();

        // if the overlay toggle is not enabled
        if (_overlayToggle.value == false) return;

        Material material = GetCelestialBodyMaterial();
        string[] list = material.GetTexturePropertyNames();
        for (int i = 0; i < list.Length; i++)
        {
            System.Diagnostics.Debug.Write("ISRU list texture property name." + i + "=" + list[i]);
        }
        System.Diagnostics.Debug.Write("ISRU GetTextureImage called");
        material.mainTexture = GetTextureImage(_celestialBodyName, GetResourceNameSelectedRadioButton());
    }

    private void OnToggleOverlay(bool state)
    {
        UpdateWindowStatus();
        UpdateUserMessage();
        DisplayResourceShader(state);
    }

    private void ToggleRadioButton(RadioButton button, bool newValue)
    {
        if (!newValue) return;
        //System.Diagnostics.Debug.Write("ISRU ToggleRadioButton newValue=" + newValue);
        UpdateWindowStatus();
        _messageField.text = "";
        UpdateUserMessage();
        if (button == null) return;
        if (!_overlayToggle.value) return;
        DisplayResourceShader(_overlayToggle.value);
    }

    private void OnSOIEntered(MessageCenterMessage message)
    {
        System.Diagnostics.Debug.Write("ISRU OnSOIEntered");
    }
}
