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
using static KSP.Api.UIDataPropertyStrings.View;

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

}

