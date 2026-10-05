using HarmonyLib;
using PerformanceFishReforjed.Caching;
using Prepatcher;
using RimWorld;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Parches de Harmony para mantener la cache de capacidad de SlotGroup.
    /// Se actualiza cuando se añaden o quitan celdas del SlotGroup.
    /// </summary>
    internal static class SlotGroupCapacityPatches
    {
        [HarmonyPatch(typeof(SlotGroup), nameof(SlotGroup.Notify_AddedCell))]
        private static class Notify_AddedCell_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(SlotGroup __instance)
            {
                ref var cache = ref __instance.ReforjedCapacityCache();
                cache.Recalculate(__instance);
            }
        }

        [HarmonyPatch(typeof(SlotGroup), nameof(SlotGroup.Notify_LostCell))]
        private static class Notify_LostCell_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(SlotGroup __instance)
            {
                ref var cache = ref __instance.ReforjedCapacityCache();
                cache.Recalculate(__instance);
            }
        }
    }
}
