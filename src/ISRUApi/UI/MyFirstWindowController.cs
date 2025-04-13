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
    private List<string> _options = ["O2", "H2", "Carbon", "Aluminium", "Nitrogen", "Iron"];


    // The backing field for the IsWindowOpen property
    private bool _isWindowOpen;

    // Overlay
    Texture _originalTexture;
    private const int OverlaySideSize = 500;
    readonly Texture2D _newTexture = new(OverlaySideSize, OverlaySideSize);

    private static float _densityValue;

    /// <summary>
    /// The state of the window. Setting this value will open or close the window.
    /// </summary>
    public bool IsWindowOpen
    {
        get => _isWindowOpen;
        set
        {
            _isWindowOpen = value;

            LoadResourceImage();

            // Set the display style of the root element to show or hide the window
            _rootElement.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            // Alternatively, you can deactivate the window game object to close the window and stop it from updating,
            // which is useful if you perform expensive operations in the window update loop. However, this will also
            // mean you will have to re-register any event handlers on the window elements when re-enabled in OnEnable.
            // gameObject.SetActive(value);

            // Update the Flight AppBar button state
            GameObject.Find(ISRUApiPlugin.ToolbarFlightButtonID)
                ?.GetComponent<UIValue_WriteBool_Toggle>()
                ?.SetValue(value);
        }
    }

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

        // Get the text field from the window
        //_densityValueField = _rootElement.Q<TextField>("density-label");
        // Get the toggle from the window
        _overlayToggle = _rootElement.Q<Toggle>("overlay-toggle");
        // Get the greeting label from the window
        _densityValueField = _rootElement.Q<Label>("density-value");
        // Get the dropdown list from the window
        _resourceDropdown = _rootElement.Q<DropdownField>("resource-dropdown");
        _resourceDropdown.choices = _options;
        _resourceDropdown.RegisterValueChangedCallback(evt => OnSelectResource(evt.newValue));


        // Center the window by default
        _rootElement.CenterByDefault();

        // Get the close button from the window
        var closeButton = _rootElement.Q<Button>("close-button");
        // Add a click event handler to the close button
        closeButton.clicked += () => IsWindowOpen = false;

        // Get the "Say hello!" button from the window
        //var sayHelloButton = _rootElement.Q<Button>("say-hello-button");
        // Add a click event handler to the button
        //_overlayToggle.clicked += SayHelloButtonClicked;
        _overlayToggle.RegisterValueChangedCallback(evt => DisplayResourceShader(evt.newValue));
    }

    private void Update()
    {
        if (_isWindowOpen)
        {
            SetDensity();
            _densityValueField.text = _densityValue.ToString("F2"); // display value with 2 decimals
        }
            
    }

    private void SetDensity()
    {
        if (_newTexture == null) return;
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
        string filePath = "./BepInEx/plugins/ISRU/assets/images/noise.png";
        if (!File.Exists(filePath))
        {
            System.Diagnostics.Debug.Write("ISRU ERROR File not found: " + filePath);
            return;
        }
        _newTexture.LoadImage(File.ReadAllBytes(filePath));
    }

    /// <summary>
    /// Returns the path of the current celestial body game object where the material is applied.
    /// </summary>
    private string GetCelestialBodyPath()
    {
        VesselComponent vessel = Game.ViewController.GetActiveSimVessel();
        return "Map3D(Clone)/Map-" + vessel.mainBody.Name + "/Celestial." + vessel.mainBody.Name + ".Scaled(Clone)";
    }

    private void DisplayResourceShader(bool state)
    {
        GameObject gameObject = GameObject.Find(GetCelestialBodyPath());
        if (gameObject == null)
        {
            System.Diagnostics.Debug.Write("ISRU ERROR Celestial Body Map not found");
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

            if (_newTexture == null)
            {
                System.Diagnostics.Debug.Write("ISRU ERROR newTexture not found");
                return;
            }
            material.mainTexture = _newTexture;
        }
        else // displays back the original texture
        {
            material.mainTexture = _originalTexture;
        }
        System.Diagnostics.Debug.Write("ISRU end DisplayResourceShader");
    }

    /// <summary>
    /// Returns the density at the current vessel location.
    /// </summary>
    public static double GetDensity()
    {
        return _densityValue;
    }

    // Called when the player select a resource on the dropdown list.
    private void OnSelectResource(string newResource)
    {
        if (newResource == "Option B")
        {
            Debug.Log("Option B !");

        }
    }
}
