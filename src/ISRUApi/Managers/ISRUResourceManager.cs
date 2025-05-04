using ISRUApi.Modules;
using ISRUApi.UI;
using KSP.Sim.Definitions;
using UnityEngine;

namespace ISRUApi.Managers
{
    public static class ISRUResourceManager
    {
        public static readonly Dictionary<string, List<CBResourceChart>> CbResourceList = new()
        {
            {
                "Kerbin",
                [
                    new CBResourceChart("Methane"),
                    new CBResourceChart("Carbon"),
                    new CBResourceChart("Copper"),
                    new CBResourceChart("Iron"),
                    new CBResourceChart("Lithium")
                ]
            },
            {
                "Mun", [
                    new CBResourceChart("Nickel"),
                    new CBResourceChart("Regolith"),
                    new CBResourceChart("Water")
                ]
            },
            {
                "Minmus", [
                    new CBResourceChart("Iron"),
                    new CBResourceChart("Nickel"),
                    new CBResourceChart("Quartz")
                ]
            },
        };

        public static readonly Dictionary<string, Color> ColorMap = new()
        {
            {"Carbon", new Color(1, 0, 255, 1) }, // deep blue
            {"Iron", new Color(0 ,255, 121, 1) }, // light blue
            {"Nickel", new Color(204, 14, 0, 1) }, // orange
            {"Quartz", new Color(255, 173, 0, 1) }, // gold
            {"Regolith", new Color(55, 0, 204, 1) }, // violet
            {"Water", new Color(0, 156, 204, 1) }, // blue
        };

        public static void MarkedCelestialBodyResourcesAsScanned(
            string celestialBodyName,
            List<PartComponentModule_ResourceScanner> partComponentResourceScannerList
        )
        {
            List<string> scannableResourceList = [];
            List<CBResourceChart> availableResourceList = CbResourceList[celestialBodyName];

            // Loop through all current resource scanner parts
            foreach (PartComponentModule_ResourceScanner partComponent in partComponentResourceScannerList)
            {
                List<PartModuleResourceSetting> scannedResources = partComponent._dataResourceScanner.ScannableResources;
                // Loop through all resources they can scan
                foreach (PartModuleResourceSetting resourceSetting in scannedResources)
                {
                    // Add the resource to the list
                    if (!scannableResourceList.Contains(resourceSetting.ResourceName))
                    {
                        scannableResourceList.Add(resourceSetting.ResourceName);
                    }
                }
            }

            // Loop through all available resources on current celestial body
            foreach (CBResourceChart availableResource in availableResourceList)
            {
                // If the resource is scannable, it is marked as scanned
                if (scannableResourceList.Contains(availableResource.ResourceName))
                {
                    availableResource.IsScanned = true;
                }
            }
        }

        public static bool IsResourceScanned(String resourceName, String celestialBodyName)
        {
            if (resourceName == null || celestialBodyName == null) return false;
            List<CBResourceChart> availableResourceList = CbResourceList[celestialBodyName];

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

        public static bool NoResourceWasScanned(string celestialBodyName)
        {
            List<CBResourceChart> availableResourceList = CbResourceList[celestialBodyName];
            // Loop through all available resources on current celestial body
            foreach (CBResourceChart availableResource in availableResourceList)
            {
                if (availableResource.IsScanned)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
