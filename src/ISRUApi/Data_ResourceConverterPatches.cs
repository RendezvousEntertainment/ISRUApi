using HarmonyLib;
using KSP.Modules;
using KSP.Sim.ResourceSystem;

namespace ISRUApi
{
    internal class Data_ResourceConverterPatches
    {
        [HarmonyPatch(typeof(Data_ResourceConverter), nameof(Data_ResourceConverter.SetupResourceRequest))]
        [HarmonyPrefix]
        static public void OnInitializePreFix(ResourceFlowRequestBroker resourceFlowRequestBroker, Data_ResourceConverter __instance)
        {
            if (__instance.SelectedFormula == -1)
                {
                    __instance.SelectedFormula = 0;
                }
        }
    }
}
