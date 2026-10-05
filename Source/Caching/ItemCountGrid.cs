using System;
using PerformanceFishReforjed.Prepatch;
using Verse;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>Datos del contador por celda de un mapa (cada mapa tiene los suyos).</summary>
    internal sealed class ItemCountGridData
    {
        /// <summary>Objetos (categoria Item) por celda, indexado con <c>CellIndices.CellToIndex</c>.</summary>
        internal readonly int[] Counts;

        /// <summary>Ultima reconstruccion completa, para la auto-reparacion perezosa.</summary>
        internal int LastRebuildTick;

        internal ItemCountGridData(int cellCount) => Counts = new int[cellCount];
    }

    /// <summary>
    /// Contador de objetos por celda, que sustituye al recorrido de <c>GridsUtility.GetItemCount</c>.
    ///
    /// Por que esta pieza: el vanilla hace, por CADA llamada, un recorrido de la lista de cosas de la
    /// celda contando las de categoria <c>Item</c> (verificado leyendo su IL: 31 instrucciones, un
    /// incremento por cosa, sin sumar <c>stackCount</c>). Y se llama desde
    /// <c>StoreUtility.IsGoodStoreCell</c>, o sea una vez por celda candidata al buscar sitio para
    /// acarrear. Es el metodo mas llamado del bloque de acarreo.
    ///
    /// Como se mantiene: con dos postfijos de Harmony sobre <c>ThingGrid.RegisterInCell</c> y
    /// <c>DeregisterInCell</c>, que son (verificado en el IL) los DOS UNICOS sitios que anaden y quitan
    /// de esas listas, y que ademas ya calculan el indice de celda con <c>CellIndices.CellToIndex</c>
    /// (el mismo que usa este contador). No se usa el anclaje por IL que emplea el mod original
    /// (insertar tras el primer <c>callvirt</c> a <c>Add</c>/<c>Remove</c>): un postfijo de Harmony
    /// depende del METODO, no de una instruccion concreta, y un anclaje por instruccion falla en
    /// silencio cuando el IL cambia.
    ///
    /// Auto-reparacion: si algo ajeno al ThingGrid tocara esas listas, el contador se desviaria sin
    /// avisar. Para que eso no dure indefinidamente, una vez por dia de juego se reconstruye entero
    /// (una pasada sobre las cosas del mapa, no sobre las 62.500 celdas). Asi el peor caso es "un dia
    /// de juego con el contador desviado" en lugar de "para siempre", y la autocomprobacion manual
    /// dice si habia desviacion.
    /// </summary>
    internal static class ItemCountGrid
    {
        /// <summary>Reconstrucción cada 50 ticks (~0.8 s) para evitar deriva. El vanilla no tiene
        /// auto-reparación, pero nosotros sí. Un intervalo menor mantiene el contador sincronizado
        /// con el ThingGrid real.</summary>
        private const int RebuildIntervalTicks = 50;

        /// <summary>Datos del mapa, creandolos y sembrandolos en el primer uso.</summary>
        internal static ItemCountGridData Grid(Map map)
        {
            ref ItemCountGridData slot = ref map.ReforjedItemCountGrid();

            if (slot == null)
            {
                slot = new ItemCountGridData(map.cellIndices.NumGridCells);

                // Sembrar desde cero: si el mapa ya tenia cosas antes del primer uso, contarlas.
                Rebuild(map, slot);
                slot.LastRebuildTick = Ticks;
            }

            return slot;
        }

        /// <summary>Adaptador del postfijo de <c>ThingGrid.RegisterInCell(Thing, IntVec3)</c>.</summary>
        public static void RegisterPostfix(ThingGrid __instance, Thing __0, IntVec3 __1)
            => Apply(__instance, __0, __1, 1);

        /// <summary>Adaptador del postfijo de <c>ThingGrid.DeregisterInCell(Thing, IntVec3)</c>.</summary>
        public static void DeregisterPostfix(ThingGrid __instance, Thing __0, IntVec3 __1)
            => Apply(__instance, __0, __1, -1);

        private static void Apply(ThingGrid grid, Thing t, IntVec3 cell, int delta)
        {
            if (t?.def == null || t.def.category != ThingCategory.Item)
                return;

            Map? map = grid.map;
            if (map == null)
                return;

            ItemCountGridData data = Grid(map);

            // Auto-reparacion perezosa tambien aqui: el hook de registro es un sitio natural para
            // comprobarlo, porque se ejecuta con cada aparicion y no en el bucle de busqueda.
            int ticks = Ticks;
            if (ticks - data.LastRebuildTick > RebuildIntervalTicks)
            {
                Rebuild(map, data);
                data.LastRebuildTick = ticks;
            }

            int index = map.cellIndices.CellToIndex(cell);
            if ((uint)index >= (uint)data.Counts.Length)
                return;

            data.Counts[index] += delta;
        }

        /// <summary>
        /// Numero de objetos que el vanilla contaria en esa celda.
        ///
        /// El orden de parametros es el del vanilla (<c>IntVec3</c> y luego <c>Map</c>) A PROPOSITO:
        /// el motor de prepatching pasa los argumentos en orden, asi que la firma del reemplazo tiene
        /// que coincidir exactamente con la del objetivo.
        /// </summary>
        public static int Get(IntVec3 cell, Map map)
        {
            ItemCountGridData data = Grid(map);
            int index = map.cellIndices.CellToIndex(cell);

            if ((uint)index < (uint)data.Counts.Length)
                return data.Counts[index];

            // Fuera de la rejilla: se responde exactamente como el vanilla (recorriendo su lista), para
            // no inventarse un 0 si algun dia esa situacion significara otra cosa.
            return ScanVanilla(map, cell);
        }

        /// <summary>Recuento del vanilla: es el que hay que reproducir.</summary>
        internal static int ScanVanilla(Map map, IntVec3 cell)
        {
            if (map?.thingGrid == null)
                return 0;

            var things = map.thingGrid.ThingsListAt(cell);
            if (things == null)
                return 0;

            int count = 0;
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i]?.def != null && things[i].def.category == ThingCategory.Item)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Reconstruye el contar desde cero. Se recorre la lista de cosas de cada celda con la API
        /// publica <c>ThingGrid.ThingsListAtFast(Int32)</c>, la misma que usa el vanilla en
        /// <c>GridsUtility.GetItemCount</c>, así que el resultado coincide con el vanilla por
        /// construcción (no por suerte).
        /// </summary>
        internal static void Rebuild(Map map, ItemCountGridData data)
        {
            Array.Clear(data.Counts, 0, data.Counts.Length);

            var thingGrid = map.thingGrid;
            if (thingGrid == null)
                return;

            int cellCount = data.Counts.Length;
            for (int i = 0; i < cellCount; i++)
            {
                var things = thingGrid.ThingsListAtFast(i);
                if (things == null)
                    continue;

                int count = 0;
                for (int j = 0; j < things.Count; j++)
                {
                    Thing t = things[j];
                    if (t?.def != null && t.def.category == ThingCategory.Item)
                        count++;
                }

                data.Counts[i] = count;
            }
        }

        /// <summary>Suma de todos los contadores (se usa en la autocomprobacion).</summary>
        internal static long TotalCounted(ItemCountGridData data)
        {
            long total = 0;
            for (int i = 0; i < data.Counts.Length; i++)
                total += data.Counts[i];

            return total;
        }

        private static int Ticks => Find.TickManager?.TicksGame ?? 0;
    }
}
