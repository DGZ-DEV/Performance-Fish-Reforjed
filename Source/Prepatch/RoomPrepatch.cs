// This file is a Modification (MPL-2.0, section 1.10) of the original
// Performance Fish by bradson (https://github.com/bbradson/Performance-Fish),
// which is covered by the Mozilla Public License 2.0.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Mono.Cecil;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Optimizacion de Room.ContainedBeds: usa ListerThings.listsByGroup[Bed] en lugar de
    /// recorrer todas las cosas de la room (que es lo que hace el vanilla).
    ///
    /// El vanilla recorre ContainedAndAdjacentThings filtrando por Building_Bed, lo cual es
    /// ineficiente. Este parche recorre las regiones de la room y extrae las camas directamente
    /// de ListerThings.listsByGroup[ThingRequestGroup.Bed], que es O(1) por region.
    /// </summary>
    internal static class RoomPrepatch
    {
        internal static int PatchesApplied;
        internal static int PatchesFailed;

        internal const int ExpectedPatches = 1;

        private const string RoomTypeName = "Verse.Room";

        private static readonly Type ReplacementType = typeof(RoomPrepatch);

        internal static void Apply(ModuleDefinition module)
        {
            PatchesApplied = 0;
            PatchesFailed = 0;

            // Room.ContainedBeds getter
            if (BodyRewriter.Rewrite(module, RoomTypeName, "get_ContainedBeds", 0, 0, false,
                    ReplacementType, nameof(ContainedBeds_Replacement)))
            {
                PatchesApplied++;
            }
            else
            {
                PatchesFailed++;
            }
        }

        /// <summary>
        /// Reemplazo de Room.ContainedBeds. Recorre las regiones de la room y extrae las camas
        /// de ListerThings.listsByGroup[ThingRequestGroup.Bed].
        /// </summary>
        public static IEnumerable<Building_Bed> ContainedBeds_Replacement(Room instance)
        {
            var regions = instance.Regions;
            var result = new List<Building_Bed>();
            // El vanilla (Room.ContainedAndAdjacentThings) deduplica con uniqueContainedThingsSet.
            // Una cama se registra en cada región que toca, así que sin este conjunto una cama que
            // cruza dos regiones de la misma habitación se listaría dos veces (Hallazgo M2).
            var seen = new HashSet<Building_Bed>();

            for (var i = regions.Count; i-- > 0;)
            {
                var region = regions[i];
                if (region == null)
                    continue;

                var listerThings = region.ListerThings;
                if (listerThings == null)
                    continue;

                var beds = listerThings.listsByGroup[(int)ThingRequestGroup.Bed];
                if (beds == null)
                    continue;

                for (var j = beds.Count; j-- > 0;)
                {
                    if (beds[j] is Building_Bed bed && seen.Add(bed))
                    {
                        result.Add(bed);
                    }
                }
            }

            return result;
        }
    }
}
