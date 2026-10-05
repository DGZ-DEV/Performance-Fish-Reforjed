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
        [HarmonyPrefix]
        public static bool Remove_Prefix<T>(ThingOwner<T> __instance, Thing item, ref bool __result) where T : Thing
        {
            if (__instance == null || item == null)
                return true;

            ref var indexMap = ref __instance.ReforjedThingOwnerIndexMap();
            if (indexMap == null)
                indexMap = new IntCache<int>();

            var innerList = __instance.innerList;
            if (innerList == null)
                return true;

            int thingID = item.thingIDNumber;
            int index = -1;

            if (indexMap.TryGet(thingID, out int knownIndex) && knownIndex >= 0 && knownIndex < innerList.Count && innerList[knownIndex] == item)
            {
                index = knownIndex;
            }
            else
            {
                index = innerList.LastIndexOf((T)item);
            }

            if (index >= 0)
            {
                // Remove fast
                innerList.RemoveAt(index);
                indexMap.Remove(thingID);

                // Update moved element's index if necessary
                if (index < innerList.Count && innerList[index] != null)
                {
                    indexMap.GetOrAdd(innerList[index].thingIDNumber) = index;
                }

                __result = true;
                return false; // Skip original method
            }

            __result = false;
            return false;
        }
    }
}
