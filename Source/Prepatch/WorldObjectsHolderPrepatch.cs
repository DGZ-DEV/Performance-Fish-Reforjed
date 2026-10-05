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

        // Reescritura desactivada (0 aplicadas). La sustitución de cuerpo llamaba solo a
        // WorldObject.Tick y nunca a TickInterval, por lo que caravanas, cápsulas de
        // transporte, lanzaderas y gravships nunca avanzaban (Hallazgo C1). Se revierte a
        // vanilla. Aplicar() no reescribe nada.
        internal const int ExpectedPatches = 0;

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