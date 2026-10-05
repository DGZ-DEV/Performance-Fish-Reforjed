using HarmonyLib;
using PerformanceFishReforjed.Caching;
using RimWorld;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Parche de Harmony para <c>StoreUtility.TryFindBestBetterStoreCellForWorker</c>.
    ///
    /// Añade un atajo que verifica si el SlotGroup tiene capacidad libre usando la cache
    /// SlotGroupCapacityCache. Si no hay capacidad libre, se cortocircuita el metodo
    /// y se retorna inmediatamente, evitando recorrer todas las celdas del SlotGroup.
    ///
    /// Esto es similar en filosofia a C2 (atajo de bloqueantes): si el atajo falla
    /// (cacha desincronizada), el metodo original corre normalmente, asi que no hay
    /// riesgo de comportamiento incorrecto.
    /// </summary>
    internal static class StoreUtilityCapacityPatch
    {
        [HarmonyPatch(typeof(StoreUtility), "TryFindBestBetterStoreCellForWorker")]
        private static class TryFindBestBetterStoreCellForWorker_CapacityPatch
        {
            private static int _shortcutHits = 0;
            private static int _totalCalls = 0;

            [HarmonyPrefix]
            private static bool Prefix(ISlotGroup slotGroup, ref bool __runOriginal)
            {
                _totalCalls++;

                // Solo aplicar el atajo si es un SlotGroup (no null y tipo concreto)
                if (slotGroup is not SlotGroup sg)
                    return true; // Continuar con original

                // Verificar primero la cache del SlotGroup individual (C5)
                ref var sgCache = ref sg.ReforjedCapacityCache();
                if (!sgCache.IsValid)
                    sgCache.Recalculate(sg);

                if (sgCache.IsLikelyFull)
                {
                    _shortcutHits++;
                    __runOriginal = false;
                    return false;
                }

                // Si el SlotGroup pertenece a un StorageGroup, verificar tambien la cache del grupo (C6)
                if (sg.StorageGroup != null)
                {
                    ref var groupCache = ref sg.StorageGroup.ReforjedCapacityCache();
                    if (!groupCache.IsValid)
                        groupCache.Recalculate(sg.StorageGroup);

                    if (groupCache.FreeSlots <= 0)
                    {
                        _shortcutHits++;
                        __runOriginal = false;
                        return false;
                    }
                }

                return true; // Continuar con original
            }

            internal static (int hits, int total) GetStats()
            {
                return (_shortcutHits, _totalCalls);
            }

            internal static void ResetStats()
            {
                _shortcutHits = 0;
                _totalCalls = 0;
            }
        }
    }
}
