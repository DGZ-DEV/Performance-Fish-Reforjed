using RimWorld;
using Verse;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Cache de capacidad de un StorageGroup (grupo de SlotGroups vinculados).
    /// Permite evitar recalcular la capacidad sumando la de cada SlotGroup individual.
    /// </summary>
    public struct StorageGroupCapacityCache
    {
        public int TotalSlots;
        public int UsedSlots;
        public int LastUpdatedTick;

        public int FreeSlots => TotalSlots - UsedSlots;

        /// <summary>
        /// La cache es valida si se actualizo en el ultimo dia de juego (~60.000 ticks).
        /// Si expira, se recalcula lazy.
        /// </summary>
        public bool IsValid => Current.Game.tickManager.TicksGame - LastUpdatedTick < 60000;

        /// <summary>
        /// Recalcula la capacidad del StorageGroup sumando la de todos sus miembros.
        /// En 1.6, GetTotalSlots no existe, así que usamos el mismo heuristico que C5.
        /// </summary>
        public void Recalculate(StorageGroup storageGroup)
        {
            var members = storageGroup.members;
            TotalSlots = 0;
            UsedSlots = 0;

            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] is SlotGroup slotGroup)
                {
                    // Usamos el mismo heuristico que C5: 3 cosas por celda como umbral
                    int cellCount = slotGroup.CellsList.Count;
                    int heldThings = slotGroup.HeldThingsCount;
                    int estimatedSlots = cellCount * 3;

                    TotalSlots += estimatedSlots;
                    UsedSlots += heldThings;
                }
            }

            LastUpdatedTick = Current.Game.tickManager.TicksGame;
        }
    }
}
