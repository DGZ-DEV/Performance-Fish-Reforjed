// This file is a Modification (MPL-2.0, section 1.10) of the original
// Performance Fish by bradson (https://github.com/bbradson/Performance-Fish),
// which is covered by the Mozilla Public License 2.0.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
