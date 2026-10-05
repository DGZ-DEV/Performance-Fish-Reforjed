// This file is a Modification (MPL-2.0, section 1.10) of the original
// Performance Fish by bradson (https://github.com/bbradson/Performance-Fish),
// which is covered by the Mozilla Public License 2.0.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.Generic;
using System.Reflection;
using PerformanceFishReforjed.Caching;
using RimWorld.Planet;
using Verse;

namespace PerformanceFishReforjed.Prepatch
{
    internal static class WorldObjectsHolderPatches
    {
        // Cached MethodInfo for WorldObject.Tick to avoid reflection lookup every time
        private static readonly MethodInfo tickMethodInfo = typeof(WorldObject).GetMethod(
            "Tick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        /// <summary>
        /// Ticks the cached list of world objects that need ticking for a WorldObjectsHolder instance.
        /// Uses reflection to call the protected Tick method.
        /// </summary>
        public static void WorldObjectsHolderTickCached(WorldObjectsHolder instance)
        {
            if (instance == null)
                return;

            ref var cacheField = ref instance.ReforjedCachedWorldObjects();
            var cache = cacheField;
            if (cache == null)
            {
                cache = new WorldObjectsHolderCache();
                cacheField = cache;
            }

            var worldObjects = cache.GetCachedWorldObjects(instance);
            if (worldObjects == null)
                return;

            for (var i = worldObjects.Count; i-- > 0;)
            {
                // Invoke Tick via reflection
                tickMethodInfo.Invoke(worldObjects[i], null);
            }
        }
    }
}