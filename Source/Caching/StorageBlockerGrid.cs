using System;
using PerformanceFishReforjed.Prepatch;
using RimWorld;
using Verse;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>Datos del mapa para el atajo de bloqueantes de almacenamiento.</summary>
    internal sealed class StorageBlockerGridData
    {
        /// <summary>Por celda: ¿hay algo que el vanilla considere bloqueante?</summary>
        internal readonly bool[] Blockers;

        /// <summary>Celdas marcadas. Se lleva al dia para poder mostrarlo sin recorrer el mapa entero.</summary>
        internal int MarkedCells;

        internal int LastRebuildTick;

        internal StorageBlockerGridData(int cellCount) => Blockers = new bool[cellCount];
    }

    /// <summary>
    /// Atajo para <c>StoreUtility.NoStorageBlockersIn(IntVec3, Map, Thing)</c>.
    ///
    /// El vanilla recorre la lista de cosas de la celda y devuelve <c>false</c> en cuanto encuentra un
    /// bloqueante, con DOS condiciones (leidas de su IL, 85 instrucciones):
    ///
    /// <list type="number">
    /// <item><c>def.entityDefToBuild != null &amp;&amp; entityDefToBuild.passability != Standable</c></item>
    /// <item><c>def.surfaceType == SurfaceType.None &amp;&amp; def.passability != Standable
    /// &amp;&amp; GridsUtility.GetMaxItemsAllowedInCell(c, map) &lt;= 1 &amp;&amp; def.category != Item</c></item>
    /// </list>
    ///
    /// **La segunda condicion depende del ESTADO de la celda**, no solo de la def: incluye la capacidad
    /// de la celda. El mod original se queda solo con la parte de def (<c>IsStorageBlocker(def)</c>) y
    /// por tanto su atajo puede declarar inutilizable una celda que el vanilla aceptaria (por ejemplo
    /// con una estanteria, donde la capacidad es mayor que 1). Aqui se evalua la condicion COMPLETA.
    ///
    /// Por que se puede mantener de forma exacta: <c>GetMaxItemsAllowedInCell</c> es
    /// <c>GetEdifice(c, map)?.MaxItemsInCell ?? 1</c> (11 instrucciones, verificado), o sea que solo
    /// depende del edificio de la celda, y los edificios estan en la misma lista de cosas de la celda.
    /// Como esa lista solo cambia en <c>ThingGrid.RegisterInCell</c>/<c>DeregisterInCell</c> (los
    /// mismos dos enganches que usa el contador de objetos), recalcular el bit de la celda ahi es
    /// exacto.
    ///
    /// Uso: un PREFIJO de Harmony sobre <c>NoStorageBlockersIn</c> que responde <c>false</c> cuando la
    /// celda tiene bloqueante. No se sustituye el cuerpo: si el bit dice que no hay bloqueante, corre
    /// el cuerpo entero del vanilla (con su logica de apilado y de conteo), asi que la parte delicada
    /// sigue siendo del juego.
    /// </summary>
    internal static class StorageBlockerGrid
    {
        // Reconstruccion completa cada 200 ticks (~3.3 s) para minimizar falsos negativos y positivos:
        // cambios indirectos de capacidad (edificio construido/destruido sin add/remove de cosas
        // en la celda) no dejan bits rancios. El coste es bajo: barrido lineal de AllThings.
        private const int RebuildIntervalTicks = 200;

        internal static StorageBlockerGridData Grid(Map map)
        {
            ref StorageBlockerGridData slot = ref map.ReforjedStorageBlockerGrid();

            if (slot == null)
            {
                slot = new StorageBlockerGridData(map.cellIndices.NumGridCells);
                Rebuild(map, slot);
                slot.LastRebuildTick = Ticks;
            }

            return slot;
        }

        /// <summary>Adaptador del postfijo de <c>ThingGrid.RegisterInCell(Thing, IntVec3)</c>.</summary>
        public static void RegisterPostfix(ThingGrid __instance, Thing __0, IntVec3 __1)
            => Refresh(__instance, __1);

        /// <summary>Adaptador del postfijo de <c>ThingGrid.DeregisterInCell(Thing, IntVec3)</c>.</summary>
        public static void DeregisterPostfix(ThingGrid __instance, Thing __0, IntVec3 __1)
            => Refresh(__instance, __1);

        /// <summary>Postfix para <c>Building.SpawnSetup</c>: cuando un edificio aparece, recalcular
        /// el bit de bloqueante en su celda (puede cambiar MaxItemsInCell).</summary>
        public static void BuildingSpawnedPostfix(Building __instance)
        {
            Map map = __instance.Map;
            if (map == null)
                return;
            RefreshCell(map, __instance.Position);
        }

        /// <summary>
        /// Prefix para <c>Building.DeSpawn</c>: captura el mapa y posición antes de que el edificio sea removido.
        /// </summary>
        public static bool BuildingDespawnedPrefix(Building __instance, DestroyMode mode, out Map __state)
        {
            __state = __instance.Map;
            return true; // continue to original method
        }

        /// <summary>
        /// Postfix para <c>Building.DeSpawn</c>: recalcula la celda DESPUÉS de que el edificio sea removido.
        /// Usa el mapa capturado en el prefix.
        /// </summary>
        public static void BuildingDeSpawnedPostfix(Map __state, Building __instance)
        {
            if (__state != null)
            {
                RefreshCell(__state, __instance.Position);
            }
        }

        /// <summary>Recalcula el bit de una sola celda y actualiza MarkedCells si cambió.</summary>
        private static void RefreshCell(Map map, IntVec3 cell)
        {
            StorageBlockerGridData data = Grid(map);
            int index = map.cellIndices.CellToIndex(cell);
            if ((uint)index >= (uint)data.Blockers.Length)
                return;

            bool previous = data.Blockers[index];
            bool current = AnyBlocker(map, cell);

            if (previous != current)
            {
                data.Blockers[index] = current;
                data.MarkedCells += current ? 1 : -1;
            }
        }

        /// <summary>
        /// Prefijo de <c>StoreUtility.NoStorageBlockersIn(IntVec3, Map, Thing)</c>: si la celda tiene
        /// bloqueante, el vanilla devolveria false de todas formas, asi que se responde sin recorrer.
        /// Si no lo tiene, se deja correr el cuerpo original.
        /// </summary>
        public static bool NoStorageBlockersInPrefix(IntVec3 __0, Map __1, Thing __2, ref bool __result)
        {
            if (HasBlocker(__1, __0))
            {
                __result = false;
                return false;
            }

            return true;
        }

        /// <summary>¿El contador dice que esa celda tiene algo bloqueante?</summary>
        internal static bool HasBlocker(Map map, IntVec3 cell)
        {
            StorageBlockerGridData data = Grid(map);
            int index = map.cellIndices.CellToIndex(cell);

            return (uint)index < (uint)data.Blockers.Length && data.Blockers[index];
        }

        /// <summary>Recalcula el bit de la celda desde cero (la capacidad puede haber cambiado).</summary>
        private static void Refresh(ThingGrid grid, IntVec3 cell)
        {
            Map? map = grid.map;
            if (map == null)
                return;

            StorageBlockerGridData data = Grid(map);

            int ticks = Ticks;
            if (ticks - data.LastRebuildTick > RebuildIntervalTicks)
            {
                Rebuild(map, data);
                data.LastRebuildTick = ticks;
                return;   // la reconstruccion ya ha recalculado esta celda
            }

            int index = map.cellIndices.CellToIndex(cell);
            if ((uint)index >= (uint)data.Blockers.Length)
                return;

            bool previous = data.Blockers[index];
            bool current = AnyBlocker(map, cell);

            if (previous == current)
                return;

            data.Blockers[index] = current;
            data.MarkedCells += current ? 1 : -1;
        }

        /// <summary>Recorrido directo de la celda con la condicion completa del vanilla.</summary>
        internal static bool AnyBlocker(Map? map, IntVec3 cell)
        {
            if (map?.thingGrid == null)
                return false;

            var things = map.thingGrid.ThingsListAt(cell);
            if (things == null)
                return false;

            for (int i = 0; i < things.Count; i++)
            {
                Thing? t = things[i];
                if (t?.def != null && IsBlocker(t.def, map, cell))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Las DOS condiciones del vanilla, exactas. La segunda incluye la capacidad de la celda, que es
        /// lo que el original omitia.
        /// </summary>
        internal static bool IsBlocker(ThingDef def, Map map, IntVec3 cell)
        {
            if (def.entityDefToBuild != null
                && def.entityDefToBuild.passability != Traversability.Standable)
            {
                return true;
            }

            return def.surfaceType == SurfaceType.None
                   && def.passability != Traversability.Standable
                   && GridsUtility.GetMaxItemsAllowedInCell(cell, map) <= 1
                   && def.category != ThingCategory.Item;
        }

        /// <summary>Reconstruye todos los bits desde cero (auto-reparacion y arranque).</summary>
        internal static void Rebuild(Map map, StorageBlockerGridData data)
        {
            Array.Clear(data.Blockers, 0, data.Blockers.Length);
            data.MarkedCells = 0;

            var allThings = map.listerThings?.AllThings;
            if (allThings == null)
                return;

            // Se recorre la lista de cosas del mapa y se marca la celda de cada bloqueante. Basta con
            // marcar: el bit es "hay alguno", no un recuento.
            for (int i = 0; i < allThings.Count; i++)
            {
                Thing t = allThings[i];
                if (t?.def == null || !t.Spawned || !IsBlocker(t.def, map, t.Position))
                    continue;

                int index = map.cellIndices.CellToIndex(t.Position);
                if ((uint)index >= (uint)data.Blockers.Length || data.Blockers[index])
                    continue;

                data.Blockers[index] = true;
                data.MarkedCells++;
            }
        }

        private static int Ticks => Find.TickManager?.TicksGame ?? 0;
    }
}
