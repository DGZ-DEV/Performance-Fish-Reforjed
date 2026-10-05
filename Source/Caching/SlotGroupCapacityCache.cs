using RimWorld;
using Verse;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Cache de capacidad de un SlotGroup.
    /// Permite evitar recorrer celdas de SlotGroups llenos en TryFindBestBetterStoreCellForWorker.
    ///
    /// En lugar de calcular la capacidad total (costoso en 1.6), cachear HeldThingsCount
    /// y usaremos un heuristico simple: si HeldThingsCount > X * CellCount, asumimos lleno.
    /// </summary>
    public struct SlotGroupCapacityCache
    {
        public int HeldThingsCount;
        public int CellCount;
        public int LastUpdatedTick;

        /// <summary>
        /// Heuristico: si hay mas de 3 cosas por celda en promedio, asumimos lleno.
        /// Esto es conservador pero evita el coste de calcular capacidad real celda por celda.
        /// </summary>
        public bool IsLikelyFull => CellCount > 0 && HeldThingsCount > CellCount * 3;

        /// <summary>
        /// La cache es valida si se actualizo en el ultimo dia de juego (~60.000 ticks).
        /// Si expira, se recalcula lazy.
        /// </summary>
        public bool IsValid => Current.Game.tickManager.TicksGame - LastUpdatedTick < 60000;

        /// <summary>
        /// Recalcula la cache del SlotGroup.
        /// </summary>
        public void Recalculate(SlotGroup slotGroup)
        {
            HeldThingsCount = slotGroup.HeldThingsCount;
            CellCount = slotGroup.CellsList.Count;
            LastUpdatedTick = Current.Game.tickManager.TicksGame;
        }
    }
}
