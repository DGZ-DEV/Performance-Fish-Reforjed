// This file is a Modification (MPL-2.0, section 1.10) of the original
// Performance Fish by bradson (https://github.com/bbradson/Performance-Fish),
// which is covered by the Mozilla Public License 2.0.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System;
using Mono.Cecil;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Reescritura de los metodos de comps de 1.6 que pasan por el indice de
    /// <see cref="Caching.CompCaches"/>.
    ///
    /// La maquinaria de sustitucion esta en <see cref="BodyRewriter"/>; aqui solo se declara que
    /// objetivos se sustituyen por que reemplazo. Todas las firmas, incluida la aridad generica y
    /// el numero de parametros, estan verificadas contra Assembly-CSharp 1.6.9655.19392 con Cecil.
    ///
    /// Dos objetivos se quitaron de la lista (Hallazgos L2 y H3 de
    /// 2026-10-05-findings-for-author.es.md):
    /// <list type="bullet">
    /// <item><c>ThingWithComps.GetComp&lt;T&gt;</c> (L2): el vanilla de 1.6 ya indexa los comps por
    /// tipo (<c>compsByType</c>) y devuelve null de inmediato para los tipos sellados ausentes; con
    /// menos de 3 comps hace dos comprobaciones de tipo. El reemplazo hacia una consulta a la tabla
    /// y un incremento atomico: no ahorraba nada y bloqueaba transpilers ajenos.</item>
    /// <item><c>HediffUtility.TryGetComp&lt;T&gt;(Hediff)</c> (H3): es el unico metodo del grupo que
    /// se ejecuta desde los hilos de trabajo del renderizado (pre-dibujado de pawns →
    /// <c>Pawn.IsHiddenFromPlayer</c> → <c>InvisibilityUtility</c>), y las tablas estaticas
    /// compartidas de <c>IntCaches&lt;TValue&gt;</c> se escriben sin lock desde esos hilos. Al
    /// devolver el metodo a vanilla, ese camino vuelve a ser de solo lectura sobre la lista del
    /// hediff y la carrera desaparece.</item>
    /// </list>
    /// </summary>
    internal static class GetCompCachingPrepatch
    {
        /// <summary>Reescrituras aplicadas.</summary>
        internal static int PatchesApplied;

        /// <summary>Objetivos que no se encontraron (se anotan en la marca de prepatching).</summary>
        internal static int PatchesFailed;

        /// <summary>Total esperado: los 9 objetivos de GetCompCaching.</summary>
        internal const int ExpectedPatches = 9;

        private static readonly Type Caches = typeof(Caching.CompCaches);

        internal static void Apply(ModuleDefinition module)
        {
            PatchesApplied = 0;
            PatchesFailed = 0;

            // ─── ThingDef ────────────────────────────────────────────────────────────
            Rewrite(module, "Verse.ThingDef", "GetCompProperties", 1, 0, false,
                nameof(Caching.CompCaches.ThingCompProperties));

            Rewrite(module, "Verse.ThingDef", "HasComp", 0, 1, false,
                nameof(Caching.CompCaches.HasCompByType));

            Rewrite(module, "Verse.ThingDef", "HasComp", 1, 0, false,
                nameof(Caching.CompCaches.HasComp));

            // ─── Hediffs ─────────────────────────────────────────────────────────────
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
