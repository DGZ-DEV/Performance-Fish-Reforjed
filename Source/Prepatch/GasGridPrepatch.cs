using System;
using Mono.Cecil;
using PerformanceFishReforjed.Caching;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Reescritura de los métodos clave de <see cref="Verse.GasGrid"/> para que utilicen la
    /// arquitectura optimizada <see cref="ParallelGasGrid"/> con bitmaps de 64 bits.
    /// </summary>
    internal static class GasGridPrepatch
    {
        internal static int PatchesApplied;
        internal static int PatchesFailed;

        // Total esperado: 0. El grupo quedó desactivado: la optimización de gas introdujo dos
        // regresiones críticas (el humo nunca se disipaba y los datos vanilla quedaban
        // desactualizados), así que se revierte a comportamiento vanilla íntegro.
        // Ver Hallazgos C2 y H1 de 2026-10-05-findings-for-author.es.md.
        internal const int ExpectedPatches = 0;

        private const string GasGridTypeName = "Verse.GasGrid";
        private static readonly Type Caches = typeof(GasGridOptimization);

        internal static void Apply(ModuleDefinition module)
        {
            PatchesApplied = 0;
            PatchesFailed = 0;

            // 1. Tick() : void
            Rewrite(module, GasGridTypeName, "Tick", 0, 0, false,
                nameof(GasGridOptimization.Tick));

            // 2. AddGas(IntVec3, GasType, int, bool) : void
            Rewrite(module, GasGridTypeName, "AddGas", 0, 4, false,
                nameof(GasGridOptimization.AddGas));

            // 3. AnyGasAt(int) : bool
            Rewrite(module, GasGridTypeName, "AnyGasAt", 0, 1, false,
                nameof(GasGridOptimization.AnyGasAt), "Int32");

            // 4. DensityAt(int, GasType) : byte
            Rewrite(module, GasGridTypeName, "DensityAt", 0, 2, false,
                nameof(GasGridOptimization.DensityAt), "Int32");

            // 5. Debug_ClearAll() : void
            Rewrite(module, GasGridTypeName, "Debug_ClearAll", 0, 0, false,
                nameof(GasGridOptimization.Debug_ClearAll));

            // 6. Notify_ThingSpawned(Thing) : void
            Rewrite(module, GasGridTypeName, "Notify_ThingSpawned", 0, 1, false,
                nameof(GasGridOptimization.Notify_ThingSpawned));
        }

        private static void Rewrite(ModuleDefinition module, string typeName, string methodName,
            int genericArity, int parameterCount, bool isStatic, string replacementName,
            string? firstParameterTypeContains = null)
        {
            try
            {
                if (BodyRewriter.Rewrite(module, typeName, methodName, genericArity, parameterCount,
                        isStatic, Caches, replacementName, firstParameterTypeContains))
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
