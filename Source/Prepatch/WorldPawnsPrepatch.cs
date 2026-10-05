using System;
using Mono.Cecil;
using PerformanceFishReforjed.Prepatch;
using RimWorld.Planet;
using Verse;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Prepatch para WorldPawns.AllPawnsAlive y WorldPawns.AllPawnsAliveOrDead.
    /// Sustituye el cuerpo de estos métodos por llamadas a nuestra caché.
    ///
    /// NOTA: DefPreventingMothball NO se reescribe. MissileGirl le aplica un transpiler que espera
    /// el IL original de RimWorld; si lo sustituimos por nuestro cuerpo, el patrón del transpiler
    /// deja de coincidir y MissileGirl falla al parchearlo ("Failed to patch WorldPawns.
    /// DefPreventingMothball"). Dejamos ese método intacto para no romper el mod ajeno.
    /// </summary>
    internal static class WorldPawnsPrepatch
    {
        internal static int PatchesApplied;
        internal static int PatchesFailed;

        internal const int ExpectedPatches = 2;

        internal static void Apply(ModuleDefinition module)
        {
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
        }
    }
}