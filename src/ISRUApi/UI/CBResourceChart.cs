using UnityEngine;

namespace ISRUApi.UI
{
    public class CBResourceChart(string resource, Texture2D map = null, bool isScanned = false)
    {
        public string ResourceName { get; } = resource;
        public Texture2D LevelMap { get; set; } = map;
        public bool IsScanned { get; set; } = isScanned;
    }
}
