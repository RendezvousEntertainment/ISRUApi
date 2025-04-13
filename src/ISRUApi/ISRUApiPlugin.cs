using System.Reflection;
using BepInEx;
using JetBrains.Annotations;
using KSP.Sim.impl;
using KSP.UI.Binding;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
using SpaceWarp.API.UI;
using SpaceWarp.API.UI.Appbar;
using ISRUApi.UI;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;

namespace ISRUApi;

/// <summary>
/// Main plugin class for the mod.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class ISRUApiPlugin : BaseSpaceWarpPlugin
{
    // Useful in case some other mod wants to use this mod a dependency
    [PublicAPI] public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    [PublicAPI] public const string ModName = MyPluginInfo.PLUGIN_NAME;
    [PublicAPI] public const string ModVer = MyPluginInfo.PLUGIN_VERSION;

    /// Singleton instance of the plugin class
    [PublicAPI] public static ISRUApiPlugin Instance { get; set; }

    /// <summary>
    /// AppBar button IDs
    /// </summary>
    public const string ToolbarFlightButtonID = "BTN-ISRUFlight";
    
    private string iconLabel = "Resource Gathering";

    // UI window state
    private bool _isWindowOpen;
    private Rect _windowRect;
    private bool _resourceOverlayToggle = false;
    private const int Height = 60; // height of window
    private const int Width = 350; // width of window
    private static float _densityValue;

    // Overlay
    Texture _originalTexture;
    private const int OverlaySideSize = 500;
    readonly Texture2D _newTexture = new(OverlaySideSize, OverlaySideSize);

    void Awake()
    {      
        // Set initial position for window
        _windowRect = new Rect((Screen.width * 0.7f) - (Width / 2), (Screen.height / 2) - (Height / 2), 0, 0);
    }

    /// <summary>
    /// Runs when the mod is first initialized.
    /// </summary>
    public override void OnInitialized()
    {
        base.OnInitialized();

        Instance = this;

        // Load all the other assemblies used by this mod
        LoadAssemblies();

        // Load the UI from the asset bundle
        var myFirstWindowUxml = AssetManager.GetAsset<VisualTreeAsset>(
            // The case-insensitive path to the asset in the bundle is composed of:
            // - The mod GUID:
            $"{ModGuid}/" +
            // - The name of the asset bundle:
            "ISRUApi_ui/" +
            // - The path to the asset in your Unity project (without the "Assets/" part)
            "ui/myfirstwindow/myfirstwindow.uxml"
        );

        // Create the window options object
        var windowOptions = new WindowOptions
        {
            // The ID of the window. It should be unique to your mod.
            WindowId = "ISRUApi_MyFirstWindow",
            // The transform of parent game object of the window.
            // If null, it will be created under the main canvas.
            Parent = null,
            // Whether or not the window can be hidden with F2.
            IsHidingEnabled = true,
            // Whether to disable game input when typing into text fields.
            DisableGameInputForTextFields = true,
            MoveOptions = new MoveOptions
            {
                // Whether or not the window can be moved by dragging.
                IsMovingEnabled = true,
                // Whether or not the window can only be moved within the screen bounds.
                CheckScreenBounds = true
            }
        };

        // Create the window
        var myFirstWindow = Window.Create(windowOptions, myFirstWindowUxml);
        // Add a controller for the UI to the window's game object
        var myFirstWindowController = myFirstWindow.gameObject.AddComponent<MyFirstWindowController>();

        // Register Flight AppBar button
        Appbar.RegisterAppButton(
            ModName,
            ToolbarFlightButtonID,
            AssetManager.GetAsset<Texture2D>($"{ModGuid}/images/icon.png"),
            isOpen => myFirstWindowController.IsWindowOpen = isOpen
        );

        // Register Flight AppBar button
        //System.Diagnostics.Debug.Write("ISRU " + $"{Info.Metadata.GUID}/images/icon.png");
        Appbar.RegisterAppButton(
            iconLabel,
            ToolbarFlightButtonID,
            AssetManager.GetAsset<Texture2D>($"{Info.Metadata.GUID}/images/icon.png"),
            isOpen =>
            {
                _isWindowOpen = isOpen;
                GameObject.Find(ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(isOpen);
            }
        );
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
    /// Draws a simple UI window when <code>this._isWindowOpen</code> is set to <code>true</code>.
    /// </summary>
    private void OnGUI()
    {
        // Set the UI
        GUI.skin = Skins.ConsoleSkin;

        if (_isWindowOpen)
        {
            LoadResourceImage();
            _windowRect = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                _windowRect,
                FillWindow,
                "RESOURCE GATHERING--------",
                GUILayout.Height(Height),
                GUILayout.Width(Width)
            );
        }
    }

    /// <summary>
    /// Defines the content of the UI window drawn in the <code>OnGui</code> method.
    /// </summary>
    /// <param name="windowID"></param>
    private void FillWindow(int windowID)
    {
        GUILayout.BeginHorizontal();

        bool newToggleState = GUILayout.Toggle(_resourceOverlayToggle, "Resource Overlay");
        if (newToggleState != _resourceOverlayToggle)
        {
            _resourceOverlayToggle = newToggleState;
            System.Diagnostics.Debug.Write("ISRU newToggleState=" + newToggleState);
            DisplayResourceShader(newToggleState);
        }

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Density: " + _densityValue.ToString("F2")); // display value with 2 decimals
        //GUILayout.TextField(); // display value with 2 decimals
        GUILayout.EndHorizontal();

        // Close button (X)
        if (GUI.Button(new Rect(_windowRect.width - 25, 5, 20, 20), "X"))
        {
            _isWindowOpen = false;
            GameObject.Find(ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(false);
        }

        GUI.DragWindow(new Rect(0, 0, Width, 40)); // dragable part of the window
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

    private void Update()
    {
        if (_isWindowOpen)
            SetDensity();
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
        } else // displays back the original texture
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

    /// <summary>
    /// Loads all the assemblies for the mod.
    /// </summary>
    private static void LoadAssemblies()
    {
        
        // Load the Unity project assembly
        var currentFolder = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory!.FullName;
        var unityAssembly = Assembly.LoadFrom(Path.Combine(currentFolder, "ISRUApi.Unity.dll"));
        // Register any custom UI controls from the loaded assembly
        CustomControls.RegisterFromAssembly(unityAssembly);
    }
}
