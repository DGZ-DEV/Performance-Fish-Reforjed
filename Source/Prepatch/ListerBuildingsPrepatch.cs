using System;
using Mono.Cecil;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Reescritura de las CONSULTAS de <see cref="Verse.ListerBuildings"/> para que lean de los indices
    /// de <see cref="Caching.ListerBuildingsCaches"/>.
    ///
    /// Aqui no se tocan <c>Add</c> ni <c>Remove</c>: sus cuerpos (95 y 67 instrucciones) incluyen en 1.6
    /// el sistema <c>TrackingScope</c>, que no existia cuando se escribio el parche del mod original, y
    /// <c>Track</c> es publico y lo usa el generador de mapas. El mantenimiento del indice va en un
    /// postfijo de Harmony (ver CacheLifecycle).
    ///
    /// Objetivos verificados contra Assembly-CSharp 1.6.9655.19392 con Cecil. Ojo con las dos
    /// <c>ColonistsHaveBuilding</c>: tienen el mismo nombre y el mismo numero de parametros, y solo se
    /// distinguen por el tipo del parametro, por eso se filtra por tipo.
    /// </summary>
    internal static class ListerBuildingsPrepatch
    {
        internal static int PatchesApplied;
        internal static int PatchesFailed;

        internal const int ExpectedPatches = 7;

        private const string ListerTypeName = "Verse.ListerBuildings";

        private static readonly Type Caches = typeof(Caching.ListerBuildingsCaches);

        internal static void Apply(ModuleDefinition module)
        {
            PatchesApplied = 0;
            PatchesFailed = 0;

            const string thingDefParameter = "ThingDef";

            // La mas valiosa: 21 call sites y recorria todos los edificios coloniales en cada llamada.
            Rewrite(module, "AllBuildingsColonistOfDef", 0, 1, nameof(Caching.ListerBuildingsCaches.AllBuildingsColonistOfDef), thingDefParameter);

            // 7 call sites.
            Rewrite(module, "ColonistsHaveBuilding", 0, 1, nameof(Caching.ListerBuildingsCaches.ColonistsHaveBuildingByDef), thingDefParameter);

            // 4 call sites. En el vanilla es AllBuildingsColonistOfClass<Building_ResearchBench>().Any().
            Rewrite(module, "ColonistsHaveResearchBench", 0, 0, nameof(Caching.ListerBuildingsCaches.ColonistsHaveResearchBench), null);

            Rewrite(module, "ColonistsHaveBuildingWithPowerOn", 0, 1, nameof(Caching.ListerBuildingsCaches.ColonistsHaveBuildingWithPowerOn), thingDefParameter);

            // 16 call sites. Tiene restriccion T : Building.
            Rewrite(module, "AllBuildingsColonistOfClass", 1, 0, nameof(Caching.ListerBuildingsCaches.AllBuildingsColonistOfClass), null);

            // 2 call sites. SIN restriccion: el reemplazo comprueba en tiempo de ejecucion si T es de la
            // jerarquia de Building y, si no (por ejemplo IHaulSource), recorre como el vanilla.
            Rewrite(module, "AllColonistBuildingsOfType", 1, 0, nameof(Caching.ListerBuildingsCaches.AllColonistBuildingsOfType), null);

            // 8 call sites.
            Rewrite(module, "AllBuildingsNonColonistOfDef", 0, 1, nameof(Caching.ListerBuildingsCaches.AllBuildingsNonColonistOfDef), thingDefParameter);
        }

        private static void Rewrite(ModuleDefinition module, string methodName, int genericArity,
            int parameterCount, string replacementName, string? firstParameterTypeContains)
        {
            try
            {
                if (BodyRewriter.Rewrite(module, ListerTypeName, methodName, genericArity, parameterCount,
                        false, Caches, replacementName, firstParameterTypeContains))
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
