using HarmonyLib;
using KSP.Modules;

namespace ISRUApi
{
    internal class Data_ResourceConverterPatches
    {
        [HarmonyPatch(typeof(Data_ResourceConverter), nameof(Data_ResourceConverter.SetupResourceRequest))]
        [HarmonyPrefix]
        public static void OnInitializePreFix(Data_ResourceConverter __instance)
        {
            if (__instance.SelectedFormula == -1)
            {
                __instance.SelectedFormula = 0;
            }
        }
    }
}
