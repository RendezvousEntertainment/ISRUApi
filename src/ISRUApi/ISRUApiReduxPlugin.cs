#if Redux

using System.Reflection;
using HarmonyLib;
using ISRUApi.UI;
using JetBrains.Annotations;
using SpaceWarp.API.Mods;
using SpaceWarp.UI.API.Appbar;
using UitkForKsp2.API;
using UnityEngine;
using UnityEngine.UIElements;

namespace ISRUApi;

/// <summary>
/// Main plugin class for the mod.
/// </summary>
public class ISRUApiPlugin : GeneralMod
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
    
    private readonly string iconLabel = "Resource Gathering";

    private VisualTreeAsset _uiWindowAsset;
    private Dictionary<string, Texture2D> _graphics = new();
    
    public ISRUApiPlugin()
    {
        SpaceWarp.API.Loading.Loading.AddAddressablesLoadingAction<VisualTreeAsset>(
            "Loading ISRU UI",
            "isru_ui",
            true,
            OnUIAssetsLoaded
        );
        
        SpaceWarp.API.Loading.Loading.AddAddressablesLoadingAction<Texture2D>(
            "Loading ISRU Assets",
            "isru_graphics",
            true,
            OnGraphicAssetsLoaded
        );
    }

    private void OnUIAssetsLoaded(VisualTreeAsset asset)
    {
        _uiWindowAsset = asset;
    }
    
    private void OnGraphicAssetsLoaded(Texture2D asset)
    {
        _graphics.Add(asset.name, asset);
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
        var myFirstWindow = Window.Create(windowOptions, _uiWindowAsset);
        // Add a controller for the UI to the window's game object
        var myFirstWindowController = myFirstWindow.gameObject.AddComponent<MyFirstWindowController>();

        // Register Flight AppBar button
        Appbar.RegisterAppButton(
            iconLabel,
            ToolbarFlightButtonID,
            _graphics["icon.png"],
            isOpen => myFirstWindowController.IsWindowOpen = isOpen
        );

        Harmony.CreateAndPatchAll(typeof(Data_ResourceConverterPatches));
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

#endif // Redux