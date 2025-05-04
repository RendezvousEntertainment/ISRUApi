using System.ComponentModel;

namespace ISRUApi.UI
{
    public enum UIResourceWindowStatus : byte
    {
        None,
        [Description("Displaying resources")] DisplayingResources,
        [Description("Turned off")] TurnedOff,
        [Description("Not in Map View")] NotInMapView,
        //[Description("The resource is not available")] NoSuchResource,
        [Description("No resource was scanned")] NoResourceScanned,
        [Description("Scan complete")] ScanComplete,
        [Description("No vessel control")] NoVesselControl,
        [Description("Scanning")] Scanning,
        [Description("No Scanner")] NoScanner,
    }
}
