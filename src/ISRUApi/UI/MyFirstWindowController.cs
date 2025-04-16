using KSP.UI.Binding;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;
using KSP.Sim.impl;
using KSP.Game;

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
    private readonly List<string> _options = ["Aluminium", "Carbon", "H2", "Iron", "Nitrogen", "O2"];
    //private string _selectedResource;
    private UIResourceWindowStatus _uiWindowStatus;


    // The backing field for the IsWindowOpen property
    private bool _isWindowOpen;

    // Overlay
    Texture _originalTexture;
    private const int OverlaySideSize = 500;
    readonly Texture2D _newTexture = new(OverlaySideSize, OverlaySideSize);

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
        // Get the greeting label from the window
        _densityValueField = _rootElement.Q<Label>("density-value");
        _messageField = _rootElement.Q<Label>("label-message");
        SetUserMessage("", false);
        // Get the dropdown list from the window
        _resourceDropdown = _rootElement.Q<DropdownField>("resource-dropdown");
        _resourceDropdown.choices = _options; // Populate the dropdown elements
        _resourceDropdown.value = _options[0]; // Select the first element by default
        //_selectedResource = _resourceDropdown.value;
        _resourceDropdown.RegisterValueChangedCallback(evt => OnSelectResource());


        // Center the window by default
        _rootElement.CenterByDefault();

        // Get the close button from the window
        var closeButton = _rootElement.Q<Button>("close-button");
        // Add a click event handler to the close button
        closeButton.clicked += () => IsWindowOpen = false;
        // Add a click event handler to the toggle
        _overlayToggle.RegisterValueChangedCallback(evt => DisplayResourceShader(evt.newValue));
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

            SetCelestialBodyName();
            GameState gameState = Game.GlobalGameState.GetState();
            if (gameState != GameState.Map3DView)
            {
                _uiWindowStatus = UIResourceWindowStatus.NotInMapView;
                //SetUserMessage("Switch to map view.", true);
            } else
            {
                _uiWindowStatus = UIResourceWindowStatus.DisplayingResources;
                LoadResourceImage();
            }
            //System.Diagnostics.Debug.Write("ISRU gameState=" + gameState);
            // TODO check if map view and if so, load the image
            

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
        //if (_newTexture == null) return;
        if (_uiWindowStatus == UIResourceWindowStatus.NoSuchResource)
        {
            _densityValue = 0;
            return;
        }
        VesselComponent vessel = Game.ViewController.GetActiveSimVessel();
        double longitude = vessel.Longitude;
        double latitude = vessel.Latitude;
        double long_norm = (180 - longitude) / 360;
        double lat_norm = (90 - latitude) / 180;
        int x = (int)Math.Round(long_norm * OverlaySideSize);
        int y = (int)Math.Round(lat_norm * OverlaySideSize);
        if (x < 0 || x > _newTexture.width || y < 0 || y > _newTexture.height)
        {
            System.Diagnostics.Debug.Write("ISRU ERROR coordinates out of bound: x=" + x + "; y=" + y);
            _densityValue = 0.0f;
            return;
        }
        Color pixelColor = _newTexture.GetPixel(x, y);
        _densityValue = pixelColor.r; // we assume the texture is grayscale and only use the red value
    }

    private void LoadResourceImage()
    {
        System.Diagnostics.Debug.Write("ISRU Attempting to load " + _celestialBodyName + "_" + _resourceDropdown.value + ".png");
        string filePath = "./BepInEx/plugins/ISRU/assets/images/" + _celestialBodyName + "_" + _resourceDropdown.value + ".png";
        if (!File.Exists(filePath))
        {
            //SetUserMessage("This body is bare of this resource.", true);
            System.Diagnostics.Debug.Write("ISRU ERROR File not found: " + filePath);
            _newTexture.Reinitialize(OverlaySideSize, OverlaySideSize);
            System.Diagnostics.Debug.Write("ISRU Texture reinitialized. isReadable=" + _newTexture.isReadable);
            //_overlayToggle.focusable = false; // does not work?
            _overlayToggle.SetEnabled(false);
            _uiWindowStatus = UIResourceWindowStatus.NoSuchResource;
            return;
        }
        _overlayToggle.SetEnabled(true);
        _newTexture.LoadImage(File.ReadAllBytes(filePath));
        _uiWindowStatus = UIResourceWindowStatus.DisplayingResources;
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
        //VesselComponent vessel = Game.ViewController.GetActiveSimVessel();
        return "Map3D(Clone)/Map-" + _celestialBodyName + "/Celestial." + _celestialBodyName + ".Scaled(Clone)";
    }



    private void DisplayResourceShader(bool state)
    {
        // if there's either nothing to display or the overlay toggle is not enabled
        if (_uiWindowStatus != UIResourceWindowStatus.DisplayingResources || _overlayToggle.value == false)
        {
            return;
        }
        SetCelestialBodyName();
        GameObject gameObject = GameObject.Find(GetCelestialBodyPath());
        if (gameObject == null)
        {
            System.Diagnostics.Debug.Write("ISRU ERROR Celestial Body Map not found");
            _uiWindowStatus = UIResourceWindowStatus.NotInMapView;
            return;
        }

        Material material = gameObject.GetComponent<Renderer>()?.material;
        if (material == null)
        {
            System.Diagnostics.Debug.Write("ISRU ERROR material not found");
            return;
        }

        if (state) // displays the resource overlay
        {
            _originalTexture = material.mainTexture;

            if (!_newTexture.isReadable)
            {
                //SetUserMessage("This body is bare of this resource.", true);
                System.Diagnostics.Debug.Write("ISRU ERROR newTexture not found");
                return;
            }
            material.mainTexture = _newTexture;
            //string[] list = material.GetTexturePropertyNames();
            //for (int i = 0; i < list.Length; i++)
           // {
             //   System.Diagnostics.Debug.Write("ISRU list texture property name." + i + "=" + list[i]);
            //}
            
            material.SetTexture("_EmissionTex", _newTexture);
            //material.SetColor("_EmissionColor", Color.black);
            //SetUserMessage("Hmm, " + _resourceDropdown.value + "!", false);
        }
        else // displays back the original texture
        {
            material.mainTexture = _originalTexture;
            //SetUserMessage("", false);
        }
        //System.Diagnostics.Debug.Write("ISRU end DisplayResourceShader");
    }

    /// <summary>
    /// Returns the density at the current vessel location.
    /// </summary>
    public static double GetDensity()
    {
        return _densityValue;
    }

    // Called when the player select a resource on the dropdown list.
    private void OnSelectResource()
    {
        //_selectedResource = selectedResource;
        LoadResourceImage();
        DisplayResourceShader(_overlayToggle.value);
    }
}
