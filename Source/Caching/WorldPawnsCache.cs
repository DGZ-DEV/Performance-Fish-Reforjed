using System;
using System.Collections.Generic;
using System.Reflection;
using PerformanceFishReforjed.Caching;
using RimWorld.Planet;
using Verse;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Cache for WorldPawns.AllPawnsAlive and AllPawnsAliveOrDead results.
    /// Stores cached lists and version numbers to detect when cache is dirty.
    /// Uses reflection to access the private _version field of HashSet<Pawn> and List<Pawn>.
    /// </summary>
    public class WorldPawnsCache
    {
        // Version tracking for cache invalidation
        public int PawnsAliveVersion = -2;
        public int PawnsMothballedVersion = -2;
        public int AllPawnsAliveVersion = -2;
        public int AllPawnsDeadVersion = -2;

        // Cached results
        public readonly List<Pawn> allPawnsAliveResult = new List<Pawn>();
        public readonly List<Pawn> allPawnsAliveOrDeadResult = new List<Pawn>();

        // Reflection fields to access _version of HashSet<Pawn> and List<Pawn>
        private static readonly FieldInfo pawnsAliveVersionField = typeof(HashSet<Pawn>).GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo pawnsMothballedVersionField = typeof(HashSet<Pawn>).GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo pawnsDeadVersionField = typeof(HashSet<Pawn>).GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic);
        // Note: List<Pawn>._version is also non-public instance
        private static readonly FieldInfo listVersionField = typeof(List<Pawn>).GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Updates the cache version numbers from the current state.
        /// </summary>
        public void UpdateCache(WorldPawns instance)
        {
            PawnsAliveVersion = (int)pawnsAliveVersionField.GetValue(instance.pawnsAlive);
            PawnsMothballedVersion = (int)pawnsMothballedVersionField.GetValue(instance.pawnsMothballed);
            AllPawnsAliveVersion = (int)listVersionField.GetValue(instance.allPawnsAliveResult);
            AllPawnsDeadVersion = (int)pawnsDeadVersionField.GetValue(instance.pawnsDead);
        }

        /// <summary>
        /// Checks if the cache is dirty based on version numbers.
        /// </summary>
        public bool IsCacheDirty(WorldPawns instance)
        {
            return (int)pawnsAliveVersionField.GetValue(instance.pawnsAlive) != PawnsAliveVersion ||
                   (int)pawnsMothballedVersionField.GetValue(instance.pawnsMothballed) != PawnsMothballedVersion ||
                   (int)listVersionField.GetValue(instance.allPawnsAliveResult) != AllPawnsAliveVersion ||
                   (int)pawnsDeadVersionField.GetValue(instance.pawnsDead) != AllPawnsDeadVersion;
        }

        /// <summary>
        /// Gets the cached AllPawnsAlive list, updating if dirty.
        /// </summary>
        public List<Pawn> GetAllPawnsAlive(WorldPawns instance)
        {
            if (IsCacheDirty(instance))
            {
                allPawnsAliveResult.Clear();
                allPawnsAliveResult.AddRange(instance.pawnsAlive);
                allPawnsAliveResult.AddRange(instance.pawnsMothballed);
                UpdateCache(instance);
            }
            return allPawnsAliveResult;
        }

        /// <summary>
        /// Gets the cached AllPawnsAliveOrDead list, updating if dirty.
        /// </summary>
        public List<Pawn> GetAllPawnsAliveOrDead(WorldPawns instance)
        {
            if (IsCacheDirty(instance))
            {
                allPawnsAliveOrDeadResult.Clear();
                allPawnsAliveOrDeadResult.AddRange(instance.AllPawnsAlive); // This will use cached version
                allPawnsAliveOrDeadResult.AddRange(instance.pawnsDead);
                UpdateCache(instance);
            }
            return allPawnsAliveOrDeadResult;
        }
    }
}