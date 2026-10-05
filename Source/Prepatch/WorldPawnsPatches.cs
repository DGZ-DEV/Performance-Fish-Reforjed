using System.Collections.Generic;
using PerformanceFishReforjed.Caching;
using RimWorld.Planet;
using Verse;

namespace PerformanceFishReforjed.Prepatch
{
    internal static class WorldPawnsPatches
    {
        /// <summary>
        /// Gets the cached AllPawnsAlive list for a WorldPawns instance.
        /// Initializes the cache on first use.
        /// </summary>
        public static List<Pawn> GetAllPawnsAliveCached(WorldPawns instance)
        {
            if (instance == null)
                return new List<Pawn>();

            ref var cacheField = ref instance.ReforjedWorldPawnsCache();
            var cache = cacheField;
            if (cache == null)
            {
                cache = new WorldPawnsCache();
                cacheField = cache;
            }
            return cache.GetAllPawnsAlive(instance);
        }

        /// <summary>
        /// Gets the cached AllPawnsAliveOrDead list for a WorldPawns instance.
        /// Initializes the cache on first use.
        /// </summary>
        public static List<Pawn> GetAllPawnsAliveOrDeadCached(WorldPawns instance)
        {
            if (instance == null)
                return new List<Pawn>();

            ref var cacheField = ref instance.ReforjedWorldPawnsCache();
            var cache = cacheField;
            if (cache == null)
            {
                cache = new WorldPawnsCache();
                cacheField = cache;
            }
            return cache.GetAllPawnsAliveOrDead(instance);
        }

        /// <summary>
        /// Gets the cached DefPreventingMothball for a pawn.
        /// For now, we just call the original method as caching this is complex.
        /// </summary>
        public static HediffDef GetDefPreventingMothballCached(WorldPawns instance, Pawn pawn)
        {
            if (instance == null)
                return null;

            try
            {
                return instance.DefPreventingMothball(pawn);
            }
            catch
            {
                return null;
            }
        }
    }
}