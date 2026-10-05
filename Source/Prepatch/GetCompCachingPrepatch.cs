using System;
using Mono.Cecil;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Reescritura de los 11 metodos de comps de 1.6 para que pasen por el indice de
    /// <see cref="Caching.CompCaches"/>.
    ///
    /// La maquinaria de sustitucion esta en <see cref="BodyRewriter"/>; aqui solo se declara que
    /// objetivos se sustituyen por que reemplazo. Todas las firmas, incluida la aridad generica y
    /// el numero de parametros, estan verificadas contra Assembly-CSharp 1.6.9655.19392 con Cecil.
    /// </summary>
    internal static class GetCompCachingPrepatch
    {
        /// <summary>Reescrituras aplicadas.</summary>
        internal static int PatchesApplied;

        /// <summary>Objetivos que no se encontraron (se anotan en la marca de prepatching).</summary>
        internal static int PatchesFailed;

        /// <summary>Total esperado: los 11 objetivos de GetCompCaching.</summary>
        internal const int ExpectedPatches = 11;

        private static readonly Type Caches = typeof(Caching.CompCaches);

        internal static void Apply(ModuleDefinition module)
        {
            PatchesApplied = 0;
            PatchesFailed = 0;

            // ─── ThingWithComps / ThingDef ───────────────────────────────────────────
            Rewrite(module, "Verse.ThingWithComps", "GetComp", 1, 0, false,
                nameof(Caching.CompCaches.ThingComp));

            Rewrite(module, "Verse.ThingDef", "GetCompProperties", 1, 0, false,
                nameof(Caching.CompCaches.ThingCompProperties));

            Rewrite(module, "Verse.ThingDef", "HasComp", 0, 1, false,
                nameof(Caching.CompCaches.HasCompByType));

            Rewrite(module, "Verse.ThingDef", "HasComp", 1, 0, false,
                nameof(Caching.CompCaches.HasComp));

            // ─── Hediffs ─────────────────────────────────────────────────────────────
            // TryGetComp<T>(Hediff) es estatico; la otra sobrecarga es TryGetComp<T>(Hediff, T&),
            // por eso se exige exactamente 1 parametro.
            Rewrite(module, "Verse.HediffUtility", "TryGetComp", 1, 1, true,
                nameof(Caching.CompCaches.HediffComp));

            Rewrite(module, "Verse.HediffDef", "CompProps", 1, 0, false,
                nameof(Caching.CompCaches.HediffCompProperties));

            // ─── Habilidades ─────────────────────────────────────────────────────────
            Rewrite(module, "RimWorld.Ability", "CompOfType", 1, 0, false,
                nameof(Caching.CompCaches.AbilityComp));

            // ─── Objetos de mundo y componentes ──────────────────────────────────────
            Rewrite(module, "RimWorld.Planet.WorldObject", "GetComponent", 1, 0, false,
                nameof(Caching.CompCaches.WorldObjectComp));

            Rewrite(module, "Verse.Map", "GetComponent", 1, 0, false,
                nameof(Caching.CompCaches.MapComponent));

            Rewrite(module, "RimWorld.Planet.World", "GetComponent", 1, 0, false,
                nameof(Caching.CompCaches.WorldComponent));

            Rewrite(module, "Verse.Game", "GetComponent", 1, 0, false,
                nameof(Caching.CompCaches.GameComponent));
        }

        /// <summary>
        /// Cuenta el resultado en lugar de lanzar: el prepatch corre antes de que exista el log del
        /// juego, asi que la visibilidad de un fallo son los contadores de la marca. Un objetivo que
        /// reviente no debe impedir que se apliquen los demas.
        /// </summary>
        private static void Rewrite(ModuleDefinition module, string typeName, string methodName,
            int genericArity, int parameterCount, bool isStatic, string replacementName)
        {
            try
            {
                if (BodyRewriter.Rewrite(module, typeName, methodName, genericArity, parameterCount,
                        isStatic, Caches, replacementName))
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
