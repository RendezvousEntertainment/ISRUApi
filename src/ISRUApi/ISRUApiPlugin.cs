/* Copyright 2024 Verox;NexusHelium;The Space Peacock
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.Diagnostics;
using BepInEx;
using JetBrains.Annotations;
using KSP.Game;
using KSP.Game.Science;
using KSP.Messages.PropertyWatchers;
using KSP.OAB;
using KSP.Rendering.Planets;
using KSP.Sim.impl;
using KSP.UI.Binding;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
using SpaceWarp.API.UI;
using SpaceWarp.API.UI.Appbar;
using UnityEngine;
using UnityEngine.Assertions;

namespace ISRUApi;

/// <summary>
/// Main plugin class for the mod.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class ISRUApiPlugin : BaseSpaceWarpPlugin
{
    /// <summary>
    /// The GUID of the mod.
    /// </summary>
    [PublicAPI] public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    /// <summary>
    /// The name of the mod.
    /// </summary>
    [PublicAPI] public const string ModName = MyPluginInfo.PLUGIN_NAME;
    /// <summary>
    /// The version of the mod.
    /// </summary>
    [PublicAPI] public const string ModVer = MyPluginInfo.PLUGIN_VERSION;

    /// <summary>
    /// Singleton instance of the mod.
    /// </summary>
    [PublicAPI] public static ISRUApiPlugin Instance { get; set; }

    // AppBar button IDs
    public const string ToolbarFlightButtonID = "BTN-ISRUFlight";
    
    private string iconLabel = "Resource Gathering";

    // UI window state
    private bool _isWindowOpen;
    private Rect _windowRect;
    private bool _resourceOverlayToggle = false;
    private const int Height = 60;
    private const int Width = 350;

    // Overlay
    private PQSScienceOverlay _scienceOverlay;
    private Material _regionBoundsMaterial;
    private Transform _celestialBody;

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

        // Register Flight AppBar button
        System.Diagnostics.Debug.Write("ISRU " + $"{Info.Metadata.GUID}/images/icon.png");
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



    /// <summary>
    /// Draws a simple UI window when <code>this._isWindowOpen</code> is set to <code>true</code>.
    /// </summary>
    private void OnGUI()
    {
        // Set the UI
        GUI.skin = Skins.ConsoleSkin;

        if (_isWindowOpen)
        {
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
            //UpdateResourceOverlay();
            DisplayResourceShader();
        }

        GUILayout.EndHorizontal();

        // Close button (X)
        if (GUI.Button(new Rect(_windowRect.width - 25, 5, 20, 20), "X"))
        {
            _isWindowOpen = false;
            GameObject.Find(ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(false);
        }

        GUI.DragWindow(new Rect(0, 0, Width, 40)); // dragable part of the window
    }

    Texture2D LoadTextureFromFile(string filePath)
    {
        Texture2D texture = null;

        if (File.Exists(filePath))
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            texture = new Texture2D(2, 2); // Crée une texture vide. La taille sera redimensionnée par LoadImage.
            if (texture.LoadImage(fileData)) // Charge les données de l'image dans la texture.
            {
                return texture;
            }
        }
        return null;
    }

    private void DisplayResourceShader()
    {
        GameObject gameObject = GameObject.Find("Map3D(Clone)/Map-Kerbin/Celestial.Kerbin.Scaled(Clone)");
        if (gameObject == null)
        {
            System.Diagnostics.Debug.Write("ISRU ERROR Kerbin Map nowhere to be found");
            return;
        }
        System.Diagnostics.Debug.Write("ISRU Kerbin Map found");

        Renderer renderer = gameObject.GetComponent<Renderer>();
        if (renderer == null)
        {
            System.Diagnostics.Debug.Write("ISRU ERROR Renderer not found");
            return;
        }
        System.Diagnostics.Debug.Write("ISRU Renderer found");

        Material material = renderer.material;       
        if (material == null)
        {
            System.Diagnostics.Debug.Write("ISRU material not found");
            return;
        }
        System.Diagnostics.Debug.Write("ISRU material found: " + material.name);

        Texture texture = material.mainTexture;
        System.Diagnostics.Debug.Write("ISRU mainTexture=" + material.mainTexture.name);

        Texture2D newTexture = LoadTextureFromFile("./BepInEx/plugins/ISRU/assets/images/texture.png");
        if (newTexture == null)
        {
            System.Diagnostics.Debug.Write("ISRU newTexture not found: " + material.name);
            return;
        }

        material.mainTexture = newTexture;
        System.Diagnostics.Debug.Write("ISRU end DisplayResourceShader");
    }

    /// <summary>
    /// Adds an overlay to the current celestial body.
    /// </summary>
    private void UpdateResourceOverlay()
    {
        System.Diagnostics.Debug.Write("ISRU UpdateResourceOverlay begin");
        string pathToAssets = "Assets/";

        GameManager.Instance.Assets.Load<Material>(pathToAssets + "PhysXBubbleMat.mat", mat => _regionBoundsMaterial = mat); // TODO : move to Awake()?
        if (_regionBoundsMaterial == null)
        {
            System.Diagnostics.Debug.Write("ISRU ERROR no _regionBoundsMaterial");
        }

        GameManager.Instance.Assets.Load<PQSScienceOverlay>(pathToAssets + "PqsOverlayPrefab.prefab", overlay =>  // TODO : move to Awake()?
        {
            _scienceOverlay = Instantiate(overlay, transform, false);
            _scienceOverlay.enabled = false;
        });
        if (_scienceOverlay == null)
        {
            System.Diagnostics.Debug.Write("ISRU ERROR no PqsOverlayPrefab");
        }

        if (_scienceOverlay == null || _regionBoundsMaterial == null)
        {
            return;
        }
        System.Diagnostics.Debug.Write("ISRU overlay loaded");

        if (Game?.ScienceManager?.ScienceRegionsDataProvider != null)
        {
            _scienceOverlay.SetScienceRegionsDataProvider(Game.ScienceManager.ScienceRegionsDataProvider);
            VesselComponent vessel = Game.ViewController.GetActiveSimVessel();
            var celestialBodyBehavior = vessel!.mainBody.SurfaceProvider as CelestialBodyBehavior;
            var celestialBody = (celestialBodyBehavior != null) ? celestialBodyBehavior.PqsController : null;
            
            if (celestialBody != null)
            {
                System.Diagnostics.Debug.Write("ISRU CelestialBodyName=" + celestialBody.name);
                _scienceOverlay.SetCelestialBody(celestialBody);
            }
            _scienceOverlay.Strength = 1.0F;
        } else
        {
            System.Diagnostics.Debug.Write("ISRU ERROR ScienceRegionsDataProvider is null");
        }
        PQSObject.RegionBoundingSphereMaterial = _regionBoundsMaterial;
        PQSObject.ShowScienceRegionInfo = true;
        System.Diagnostics.Debug.Write("ISRU UpdateResourceOverlay end");
    }

}

