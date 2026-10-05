using System;
using Mono.Cecil;
using PerformanceFishReforjed.Prepatch;
using RimWorld.Planet;
using Verse;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Prepatch para WorldPawns.AllPawnsAlive, WorldPawns.AllPawnsAliveOrDead y WorldPawns.DefPreventingMothball.
    /// Sustituye el cuerpo de estos métodos por llamadas a nuestra caché.
    /// </summary>
    internal static class WorldPawnsPrepatch
    {
        internal static int PatchesApplied;
        internal static int PatchesFailed;

        internal const int ExpectedPatches = 3;

        internal static void Apply(ModuleDefinition module)
        {
            Console.WriteLine("WorldPawnsPrepatch.Apply called");
            PatchesApplied = 0;
            PatchesFailed = 0;

            // Patch get_AllPawnsAlive
            try
            {
                if (BodyRewriter.Rewrite(
                        module,
                        "RimWorld.Planet.WorldPawns",
                        "get_AllPawnsAlive",
                        genericArity: 0,
                        parameterCount: 0,
                        isStatic: false,
                        typeof(WorldPawnsPatches),
                        nameof(WorldPawnsPatches.GetAllPawnsAliveCached)))
                {
                    PatchesApplied++;
                }
                else
                {
                    PatchesFailed++;
                }
            }
            catch (Exception)
            {
                PatchesFailed++;
            }

            // Patch get_AllPawnsAliveOrDead
            try
            {
                if (BodyRewriter.Rewrite(
                        module,
                        "RimWorld.Planet.WorldPawns",
                        "get_AllPawnsAliveOrDead",
                        genericArity: 0,
                        parameterCount: 0,
                        isStatic: false,
                        typeof(WorldPawnsPatches),
                        nameof(WorldPawnsPatches.GetAllPawnsAliveOrDeadCached)))
                {
                    PatchesApplied++;
                }
                else
                {
                    PatchesFailed++;
                }
            }
            catch (Exception)
            {
                PatchesFailed++;
            }

            // Patch DefPreventingMothball
            try
            {
                if (BodyRewriter.Rewrite(
                        module,
                        "RimWorld.Planet.WorldPawns",
                        "DefPreventingMothball",
                        genericArity: 0,
                        parameterCount: 1,
                        isStatic: false,
                        typeof(WorldPawnsPatches),
                        nameof(WorldPawnsPatches.GetDefPreventingMothballCached),
                        firstParameterTypeContains: "Verse.Pawn"))
                {
                    PatchesApplied++;
                }
                else
                {
                    PatchesFailed++;
                }
            }
            catch (Exception)
            {
                PatchesFailed++;
            }
        }
    }
}