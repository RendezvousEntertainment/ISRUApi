using KSP.UI.Binding;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;
using KSP.Sim.impl;
using KSP.Game;
using static Texture2DArrayConfig;
using static KSP.Api.UIDataPropertyStrings.View;

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
    private DropdownField _resourceDropdown;
    private Toggle _overlayToggle;
    private Label _densityValueField;
    private Label _messageField;
    //private readonly List<string> _options = ["Aluminium", "Carbon", "H2", "Iron", "Nitrogen", "O2", "Regolith"];
    private readonly List<string> _options = ["Nickel", "Regolith"];
    //private string _selectedResource;
    private UIResourceWindowStatus _uiWindowStatus;


    // The backing field for the IsWindowOpen property
    private bool _isWindowOpen;

    // Overlay
    Texture _originalTexture;
    private const int OverlaySideSize = 500;
    readonly Texture2D _newTexture = new(OverlaySideSize, OverlaySideSize);
    readonly Texture2D _newLevels = new(OverlaySideSize, OverlaySideSize);

    private static float _densityValue;
    private string _celestialBodyName;

    /// <summary>
    /// Runs when the window is first created, and every time the window is re-enabled.
    /// </summary>
    private void OnEnable()
    {
        // Get the UIDocument component from the game object
        _window = GetComponent<UIDocument>();

        // Get the root element of the window.
        // Since we're cloning the UXML tree from a VisualTreeAsset, the actual root element is a TemplateContainer,
        // so we need to get the first child of the TemplateContainer to get our actual root VisualElement.
        _rootElement = _window.rootVisualElement[0];

        // Hide the window
        _rootElement.style.display = DisplayStyle.None;

        // Get the toggle from the window
        _overlayToggle = _rootElement.Q<Toggle>("overlay-toggle");
        // Add a click event handler to the toggle
        _overlayToggle.RegisterValueChangedCallback(evt => DisplayResourceShader(evt.newValue));

        // Get the greeting label from the window
        _densityValueField = _rootElement.Q<Label>("density-value");
        _messageField = _rootElement.Q<Label>("label-message");
        SetUserMessage("", false);
        // Get the dropdown list from the window
        _resourceDropdown = _rootElement.Q<DropdownField>("resource-dropdown");
        _resourceDropdown.choices = _options; // Populate the dropdown elements
        _resourceDropdown.value = _options[0]; // Select the first element by default
        _resourceDropdown.RegisterValueChangedCallback(evt => OnSelectResource());

        // Center the window by default
        _rootElement.CenterByDefault();

        // Get the close button from the window
        var closeButton = _rootElement.Q<Button>("close-button");
        // Add a click event handler to the close button
        closeButton.clicked += () => IsWindowOpen = false;
    }

    // Save the celestial body original texture
    private void SaveOriginalTexture()
    {
        if (_originalTexture != null) return;
        Material material = GetCelestialBodyMaterial();
        if (material != null) _originalTexture = material.mainTexture;
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

            SaveOriginalTexture();

            // Set the Resource Gathering UI window status
            GameState gameState = Game.GlobalGameState.GetState();
            if (gameState != GameState.Map3DView)
            {
                _uiWindowStatus = UIResourceWindowStatus.NotInMapView;
            } else
            {
                _uiWindowStatus = UIResourceWindowStatus.DisplayingResources;
                LoadResourceImage();
            }            

            // Set the display style of the root element to show or hide the window
            _rootElement.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;

            // Update the Flight AppBar button state
            GameObject.Find(ISRUApiPlugin.ToolbarFlightButtonID)
                ?.GetComponent<UIValue_WriteBool_Toggle>()
                ?.SetValue(value);
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

    private void Update()
    {
        if (_isWindowOpen)
        {
            SetDensity();
            _densityValueField.text = _densityValue.ToString("F2"); // display value with 2 decimals
            switch (_uiWindowStatus)
            {
                case UIResourceWindowStatus.DisplayingResources:
                    SetUserMessage("Hmm, " + _resourceDropdown.value + "!", false);
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
            
    }

    private void SetDensity()
    {
        if (_uiWindowStatus == UIResourceWindowStatus.NoSuchResource)
        {
            _densityValue = 0;
            return;
        }
        VesselComponent vessel = Game.ViewController.GetActiveSimVessel();
        double longitude = vessel.Longitude;
        double latitude = vessel.Latitude;
        double long_norm = (180 + longitude) / 360;
        double lat_norm = (90 - latitude) / 180;
        int x = (int)Math.Round(long_norm * OverlaySideSize);
        int y = (int)Math.Round(lat_norm * OverlaySideSize);
        if (x < 0 || x > _newLevels.width || y < 0 || y > _newLevels.height)
        {
            System.Diagnostics.Debug.Write("ISRU ERROR coordinates out of bound: x=" + x + "; y=" + y);
            _densityValue = 0.0f;
            return;
        }
        Color pixelColor = _newLevels.GetPixel(x, y);
        _densityValue = pixelColor.r; // we assume the texture is grayscale and only use the red value
    }

    private static float GetLocalDensity(VesselComponent vessel, Texture2D levelTex)
    {
        double longitude = vessel.Longitude;
        double latitude = vessel.Latitude;
        double long_norm = (180 + longitude) / 360;
        double lat_norm = (90 - latitude) / 180;
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

    private void LoadResourceImage()
    {

        System.Diagnostics.Debug.Write("ISRU Attempting to load " + _celestialBodyName + "_" + _resourceDropdown.value + "_Tex.png");
        string filePathStart = "./BepInEx/plugins/ISRU/assets/images/" + _celestialBodyName + "_" + _resourceDropdown.value;
        string filePathTexture = String.Concat(filePathStart, "_Tex.png");
        string filePathLevels = String.Concat(filePathStart, "_Lev.png");
        //filePath = "./BepInEx/plugins/ISRU/assets/images/gradient.png"; // for testing
        if (!File.Exists(filePathTexture))
        {
            System.Diagnostics.Debug.Write("ISRU File not found: " + filePathTexture + ", switching to black texture");
            _uiWindowStatus = UIResourceWindowStatus.NoSuchResource;
            string filePathBlack = "./BepInEx/plugins/ISRU/assets/images/black.png";
            filePathTexture = filePathBlack;
            filePathLevels = filePathBlack;
        }
        else
        {
            _uiWindowStatus = UIResourceWindowStatus.DisplayingResources;
        }
        _newTexture.LoadImage(File.ReadAllBytes(filePathTexture));
        _newLevels.LoadImage(File.ReadAllBytes(filePathLevels));
    }

    private static Texture2D GetLevelsImage(string celestialBodyName, string resourceName)
    {

        //System.Diagnostics.Debug.Write("ISRU Attempting to load " + celestialBodyName + "_" + resourceName + "_Lev.png");
        string filePathStart = "./BepInEx/plugins/ISRU/assets/images/" + celestialBodyName + "_" + resourceName;
        //string filePathTexture = String.Concat(filePathStart, "_Tex.png");
        string filePathLevels = String.Concat(filePathStart, "_Lev.png");
        if (!File.Exists(filePathLevels))
        {
            System.Diagnostics.Debug.Write("ISRU File not found: " + filePathLevels + ", switching to black texture");
            return null;
        }
        Texture2D texture = new(OverlaySideSize, OverlaySideSize);
        texture.LoadImage(File.ReadAllBytes(filePathLevels));
        return texture;
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
        SetCelestialBodyName();
        GameObject gameObject = GameObject.Find(GetCelestialBodyPath());
        if (gameObject == null)
        {
            System.Diagnostics.Debug.Write("ISRU ERROR Celestial Body Map not found");
            _uiWindowStatus = UIResourceWindowStatus.NotInMapView;
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
        material.mainTexture = _newTexture;
        //string[] list = material.GetTexturePropertyNames();
        //for (int i = 0; i < list.Length; i++)
        //{
        //    System.Diagnostics.Debug.Write("ISRU list texture property name." + i + "=" + list[i]);
        //}
        
        // doesn't work
        material.SetTexture("_EmissionTex", _newTexture);
        material.SetColor("_EmissionColor", Color.white);
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

    // Called when the player select a resource on the dropdown list.
    private void OnSelectResource()
    {
        SaveOriginalTexture();
        LoadResourceImage();
        DisplayResourceShader(_overlayToggle.value);
    }
}
