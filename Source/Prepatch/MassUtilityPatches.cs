using HarmonyLib;
using PerformanceFishReforjed.Caching;
using RimWorld;
using Verse;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Parches de Harmony para caché de masa de pawns.
    /// Prefijos en MassUtility.GearMass e InventoryMass que usan caché
    /// con invalidación lazy por TTL y versión de lista.
    /// </summary>
    internal static class MassUtilityPatches
    {
        [HarmonyPatch(typeof(MassUtility), nameof(MassUtility.GearMass))]
        private static class GearMass_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(Pawn p, ref float __result, ref bool __state)
            {
                if (p.Map == null)
                    return true; // Sin mapa, no hay caché

                ref var cache = ref p.Map.ReforjedMassCache().GetOrAdd(p.thingIDNumber);

                if (!cache.IsGearDirty(p))
                {
                    __result = cache.GearMass;
                    __state = false;
                    return false;
                }

                __state = true;
                return true;
            }

            [HarmonyPostfix]
            private static void Postfix(Pawn p, float __result, bool __state)
            {
                if (!__state || p.Map == null)
                    return;

                ref var cache = ref p.Map.ReforjedMassCache().GetOrAdd(p.thingIDNumber);
                cache.UpdateGear(p, __result);
            }
        }

        [HarmonyPatch(typeof(MassUtility), nameof(MassUtility.InventoryMass))]
        private static class InventoryMass_Patch
        {
            [HarmonyPrefix]
            private static bool Prefix(Pawn p, ref float __result, ref bool __state)
            {
                if (p.Map == null)
                    return true; // Sin mapa, no hay caché

                ref var cache = ref p.Map.ReforjedMassCache().GetOrAdd(p.thingIDNumber);

                if (!cache.IsInventoryDirty(p))
                {
                    __result = cache.InventoryMass;
                    __state = false;
                    return false;
                }

                __state = true;
                return true;
            }

            [HarmonyPostfix]
            private static void Postfix(Pawn p, float __result, bool __state)
            {
                if (!__state || p.Map == null)
                    return;

                ref var cache = ref p.Map.ReforjedMassCache().GetOrAdd(p.thingIDNumber);
                cache.UpdateInventory(p, __result);
            }
        }
    }
}
