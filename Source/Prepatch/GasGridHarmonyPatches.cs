using HarmonyLib;
using PerformanceFishReforjed.Caching;
using Verse;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Parches Harmony complementarios para <see cref="Verse.GasGrid"/> que no son sustituciones
    /// directas de cuerpo, como la persistencia en <c>ExposeData</c>.
    /// </summary>
    public static class GasGridHarmonyPatches
    {
        [HarmonyPatch(typeof(GasGrid), nameof(GasGrid.ExposeData))]
        public static class GasGrid_ExposeData_Patch
        {
            [HarmonyPrefix]
            public static void Prefix(GasGrid __instance)
            {
                if (Scribe.mode != LoadSaveMode.Saving)
                    return;

                ParallelGasGrid[] grids = GasGridOptimization.GetGrids(__instance);
                ParallelGasGrid smokeGrid = grids[0];
                ParallelGasGrid toxGrid = grids[1];
                ParallelGasGrid rotStinkGrid = grids[2];
                ParallelGasGrid deadlifeGrid = grids[3];

                uint[] densityArray = __instance.gasDensity;
                if (densityArray == null)
                    return;

                for (int i = smokeGrid.CellCount; i-- > 0;)
                {
                    densityArray[i] =
                        ((uint)deadlifeGrid.DensityAt(i) << 24)
                        | ((uint)rotStinkGrid.DensityAt(i) << 16)
                        | ((uint)toxGrid.DensityAt(i) << 8)
                        | smokeGrid.DensityAt(i);
                }
            }

            [HarmonyPostfix]
            public static void Postfix(GasGrid __instance)
            {
                if (Scribe.mode != LoadSaveMode.LoadingVars)
                    return;

                ParallelGasGrid[] grids = GasGridOptimization.GetGrids(__instance);
                ParallelGasGrid smokeGrid = grids[0];
                ParallelGasGrid toxGrid = grids[1];
                ParallelGasGrid rotStinkGrid = grids[2];
                ParallelGasGrid deadlifeGrid = grids[3];

                uint[] densityArray = __instance.gasDensity;
                if (densityArray == null)
                    return;

                for (int i = smokeGrid.CellCount; i-- > 0;)
                {
                    uint val = densityArray[i];
                    smokeGrid.SetDirect(i, (byte)val);
                    toxGrid.SetDirect(i, (byte)(val >> 8));
                    rotStinkGrid.SetDirect(i, (byte)(val >> 16));
                    deadlifeGrid.SetDirect(i, (byte)(val >> 24));
                }

                for (int i = grids.Length; i-- > 0;)
                {
                    grids[i].CycleIndexDiffusion = __instance.cycleIndexDiffusion;
                    grids[i].CycleIndexDissipation = __instance.cycleIndexDissipation;
                }
            }
        }
    }
}
