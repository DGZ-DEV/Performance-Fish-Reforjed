using System;
using Mono.Cecil;
using PerformanceFishReforjed.Prepatch;
using RimWorld.Planet;
using Verse;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Prepatch para WorldObjectsHolder.WorldObjectsHolderTick.
    /// Sustituye el cuerpo del método por una llamada a nuestra caché para evitar recorrer toda la lista cada tick.
    /// </summary>
    internal static class WorldObjectsHolderPrepatch
    {
        internal static int PatchesApplied;
        internal static int PatchesFailed;

        internal const int ExpectedPatches = 1;

        internal static void Apply(ModuleDefinition module)
        {
            PatchesApplied = 0;
            PatchesFailed = 0;

            // Patch WorldObjectsHolderTick
            try
            {
                if (BodyRewriter.Rewrite(
                        module,
                        "RimWorld.Planet.WorldObjectsHolder",
                        "WorldObjectsHolderTick",
                        genericArity: 0,
                        parameterCount: 0,
                        isStatic: false,
                        typeof(WorldObjectsHolderPatches),
                        nameof(WorldObjectsHolderPatches.WorldObjectsHolderTickCached)))
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