using RimWorld;
using Verse;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Cache de masa de un pawn (gear e inventory).
    /// Permite evitar recalcular la masa recorriendo el equipo e inventario.
    /// </summary>
    public struct PawnMassCache
    {
        public float GearMass;
        public float InventoryMass;
        public int LastUpdatedTick;
        public int ApparelListVersion;
        public int EquipmentListVersion;
        public int InventoryListVersion;

        /// <summary>
        /// TTL para gear: ~51 segundos (3072 ticks).
        /// Se usa un TTL más largo porque el equipo cambia menos frecuentemente.
        /// </summary>
        private const int GearTTL = 3072;

        /// <summary>
        /// TTL para inventory: ~17 segundos (1024 ticks).
        /// Se usa un TTL más corto porque el inventario cambia más frecuentemente.
        /// </summary>
        private const int InventoryTTL = 1024;

        public bool IsGearDirty(Pawn p)
        {
            // Invalidar si TTL expira o cambió la lista de equipo
            if (Current.Game.tickManager.TicksGame - LastUpdatedTick > GearTTL)
                return true;

            int currentApparelVersion = p.apparel?.WornApparel._version ?? -1;
            int currentEquipmentVersion = p.equipment?.AllEquipmentListForReading._version ?? -1;

            return currentApparelVersion != ApparelListVersion
                || currentEquipmentVersion != EquipmentListVersion;
        }

        public bool IsInventoryDirty(Pawn p)
        {
            // Invalidar si TTL expira o cambió el inventario
            if (Current.Game.tickManager.TicksGame - LastUpdatedTick > InventoryTTL)
                return true;

            int currentInventoryVersion = p.inventory?.innerContainer?.innerList._version ?? -1;

            return currentInventoryVersion != InventoryListVersion;
        }

        public void UpdateGear(Pawn p, float mass)
        {
            GearMass = mass;
            LastUpdatedTick = Current.Game.tickManager.TicksGame;
            ApparelListVersion = p.apparel?.WornApparel._version ?? -1;
            EquipmentListVersion = p.equipment?.AllEquipmentListForReading._version ?? -1;
        }

        public void UpdateInventory(Pawn p, float mass)
        {
            InventoryMass = mass;
            LastUpdatedTick = Current.Game.tickManager.TicksGame;
            InventoryListVersion = p.inventory?.innerContainer?.innerList._version ?? -1;
        }
    }
}
