using HarmonyLib;
using PerformanceFishReforjed.Caching;
using RimWorld;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Parches de Harmony para mantener la cache de capacidad de StorageGroup.
    /// Se actualiza cuando se añaden/quitan miembros o cambian los settings.
    /// </summary>
    internal static class StorageGroupCapacityPatches
    {
        [HarmonyPatch(typeof(StorageGroup), nameof(StorageGroup.RemoveMember))]
        private static class RemoveMember_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(StorageGroup __instance)
            {
                ref var cache = ref __instance.ReforjedCapacityCache();
                cache.Recalculate(__instance);
            }
        }

        [HarmonyPatch(typeof(StorageGroup), nameof(StorageGroup.Notify_SettingsChanged))]
        private static class Notify_SettingsChanged_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(StorageGroup __instance)
            {
                ref var cache = ref __instance.ReforjedCapacityCache();
                cache.Recalculate(__instance);
            }
        }
    }
}
