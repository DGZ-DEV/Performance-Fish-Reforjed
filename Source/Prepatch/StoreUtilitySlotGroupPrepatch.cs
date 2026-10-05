// This file is a Modification (MPL-2.0, section 1.10) of the original
// Performance Fish by bradson (https://github.com/bbradson/Performance-Fish),
// which is covered by the Mozilla Public License 2.0.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFishReforjed.Prepatch;
using RimWorld;
using Verse;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Prepatch para <c>StoreUtility.GetSlotGroup(Thing)</c>, <c>StoreUtility.GetSlotGroup(IntVec3, Map)</c>
    /// y <c>StoreUtility.CurrentHaulDestinationOf(Thing)</c>.
    ///
    /// En 1.6 estos métodos son triviales (10-14 instrucciones) pero se llaman masivamente durante
    /// la búsqueda de destinos de acarreo. El prepatch los sustituye por llamadas directas a
    /// <c>HaulDestinationManager.SlotGroupAt</c> / <c>SlotGroupParentAt</c> (acceso O(1) a array 3D),
    /// eliminando la cadena de llamadas virtuales y comprobaciones de nulo redundantes.
    ///
    /// Ganancia real: ~5-8 instrucciones eliminadas por llamada × miles de llamadas/tick.
    /// </summary>
    internal static class StoreUtilitySlotGroupPrepatch
    {
        internal static int PatchedCount = 0;
        internal static int FailedCount = 0;

        /// <summary>Punto de entrada llamado desde FreePatchEntry.</summary>
        internal static void Start(ModuleDefinition module)
        {
            // StoreUtility.GetSlotGroup(Thing)
            if (BodyRewriter.Rewrite(
                    module,
                    "RimWorld.StoreUtility",
                    "GetSlotGroup",
                    genericArity: 0,
                    parameterCount: 1,
                    isStatic: true,
                    typeof(StoreUtilitySlotGroupPrepatch),
                    nameof(GetSlotGroup_Thing),
                    firstParameterTypeContains: "Verse.Thing"))
            {
                PatchedCount++;
            }
            else
            {
                FailedCount++;
            }

            // StoreUtility.GetSlotGroup(IntVec3, Map)
            if (BodyRewriter.Rewrite(
                    module,
                    "RimWorld.StoreUtility",
                    "GetSlotGroup",
                    genericArity: 0,
                    parameterCount: 2,
                    isStatic: true,
                    typeof(StoreUtilitySlotGroupPrepatch),
                    nameof(GetSlotGroup_IntVec3_Map),
                    firstParameterTypeContains: "Verse.IntVec3"))
            {
                PatchedCount++;
            }
            else
            {
                FailedCount++;
            }

            // StoreUtility.CurrentHaulDestinationOf(Thing)
            if (BodyRewriter.Rewrite(
                    module,
                    "RimWorld.StoreUtility",
                    "CurrentHaulDestinationOf",
                    genericArity: 0,
                    parameterCount: 1,
                    isStatic: true,
                    typeof(StoreUtilitySlotGroupPrepatch),
                    nameof(CurrentHaulDestinationOf_Thing),
                    firstParameterTypeContains: "Verse.Thing"))
            {
                PatchedCount++;
            }
            else
            {
                FailedCount++;
            }
        }

        /// <summary>
        /// Reemplazo de <c>StoreUtility.GetSlotGroup(Thing)</c>.
        /// Firma: (Thing thing) -> SlotGroup
        /// </summary>
        public static SlotGroup GetSlotGroup_Thing(Thing thing)
        {
            if (thing?.Spawned != true)
                return null;

            Map map = thing.Map;
            if (map?.haulDestinationManager == null)
                return null;

            return map.haulDestinationManager.SlotGroupAt(thing.Position);
        }

        /// <summary>
        /// Reemplazo de <c>StoreUtility.GetSlotGroup(IntVec3, Map)</c>.
        /// Firma: (IntVec3 c, Map map) -> SlotGroup
        /// </summary>
        public static SlotGroup GetSlotGroup_IntVec3_Map(IntVec3 c, Map map)
        {
            if (map?.haulDestinationManager == null)
                return null;

            return map.haulDestinationManager.SlotGroupAt(c);
        }

        /// <summary>
        /// Reemplazo de <c>StoreUtility.CurrentHaulDestinationOf(Thing)</c>.
        /// Firma: (Thing t) -> IHaulDestination
        /// </summary>
        public static IHaulDestination CurrentHaulDestinationOf_Thing(Thing t)
        {
            if (t?.Spawned == true)
            {
                Map map = t.Map;
                if (map?.haulDestinationManager != null)
                    return map.haulDestinationManager.SlotGroupParentAt(t.Position);
            }

            // Fallback: si no está spawneada, mirar ParentHolder (igual que el vanilla)
            return t?.ParentHolder as IHaulDestination;
        }
    }
}
