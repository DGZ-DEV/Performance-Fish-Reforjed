// This file is a Modification (MPL-2.0, section 1.10) of the original
// Performance Fish by bradson (https://github.com/bbradson/Performance-Fish),
// which is covered by the Mozilla Public License 2.0.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using HarmonyLib;
using PerformanceFishReforjed.Caching;
using PerformanceFishReforjed.Prepatch;
using Verse;

namespace PerformanceFishReforjed.Listers
{
    /// <summary>
    /// Harmony patches and helper methods for ThingOwner<T> index map caching.
    /// Keeps index map in sync during ExposeData, TryAdd, and Remove.
    /// </summary>
    [HarmonyPatch]
    public static class ThingOwnerOptimization
    {
        [HarmonyPatch(typeof(ThingOwner<Thing>), nameof(ThingOwner<Thing>.ExposeData))]
        [HarmonyPostfix]
        public static void ExposeData_Postfix<T>(ThingOwner<T> __instance) where T : Thing
        {
            if (Scribe.mode != LoadSaveMode.PostLoadInit || __instance == null)
                return;

            ref var indexMap = ref __instance.ReforjedThingOwnerIndexMap();
            if (indexMap == null)
                indexMap = new IntCache<int>();

            var innerList = __instance.innerList;
            if (innerList == null)
                return;

            innerList.RemoveAll(t => t == null);
            indexMap.Clear();

            for (int i = innerList.Count; i-- > 0;)
            {
                var thing = innerList[i];
                if (thing != null)
                    indexMap.GetOrAdd(thing.thingIDNumber) = i;
            }
        }

        [HarmonyPatch(typeof(ThingOwner<Thing>), nameof(ThingOwner<Thing>.TryAdd), new[] { typeof(Thing), typeof(bool) })]
        [HarmonyPostfix]
        public static void TryAdd_Postfix<T>(ThingOwner<T> __instance, Thing item, bool canMergeWithExistingStacks, bool __result) where T : Thing
        {
            if (!__result || item == null || __instance == null)
                return;

            if (canMergeWithExistingStacks && (item.Destroyed || item.stackCount == 0))
                return;

            ref var indexMap = ref __instance.ReforjedThingOwnerIndexMap();
            if (indexMap == null)
                indexMap = new IntCache<int>();

            var innerList = __instance.innerList;
            if (innerList != null && innerList.Count > 0)
            {
                indexMap.GetOrAdd(item.thingIDNumber) = innerList.Count - 1;
            }
        }

        [HarmonyPatch(typeof(ThingOwner<Thing>), nameof(ThingOwner<Thing>.Remove))]
        [HarmonyPostfix]
        public static void Remove_Postfix<T>(ThingOwner<T> __instance) where T : Thing
        {
            if (__instance == null)
                return;

            ref var indexMap = ref __instance.ReforjedThingOwnerIndexMap();
            if (indexMap == null)
                indexMap = new IntCache<int>();

            var innerList = __instance.innerList;
            if (innerList == null)
            {
                indexMap.Clear();
                return;
            }

            // Reconstruir el índice tras el borrado del vanilla. Un prefix que borraba él mismo
            // dejaba el ítem sin `holdingOwner = null` y sin `NotifyRemoved` (el objeto quedaba
            // "atrapado": re-añadirlo saltaba "already in another container"). Ejecutamos DESPUÉS
            // del vanilla, que ya hizo esos efectos secundarios.
            indexMap.Clear();
            for (int i = 0; i < innerList.Count; i++)
            {
                Thing item = innerList[i];
                if (item != null)
                    indexMap.GetOrAdd(item.thingIDNumber) = i;
            }
        }
    }
}
