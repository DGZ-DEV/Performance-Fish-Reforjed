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
    /// Reescritura de <c>GridsUtility.GetItemCount(IntVec3, Map)</c> para que lea el contador por celda
    /// de <see cref="Caching.ItemCountGrid"/>.
    ///
    /// Es la primera pieza del bloque de acarreo (la de mejor relacion valor/riesgo segun el informe de
    /// diseno del original): el vanilla recorre la lista de cosas de la celda en CADA llamada, y se
    /// llama una vez por celda candidata desde <c>StoreUtility.IsGoodStoreCell</c>.
    ///
    /// Verificado contra 1.6 con Cecil: hay UNA sola sobrecarga (`(IntVec3, Map) -> int`) y su cuerpo
    /// cuenta 1 por cosa con <c>def.category == Item</c>, sin sumar <c>stackCount</c>, que es
    /// exactamente lo que replica el contador.
    /// </summary>
    internal static class GridsUtilityPrepatch
    {
        internal static int PatchesApplied;
        internal static int PatchesFailed;

        internal const int ExpectedPatches = 1;

        private const string UtilityTypeName = "Verse.GridsUtility";

        private static readonly Type Caches = typeof(Caching.ItemCountGrid);

        internal static void Apply(ModuleDefinition module)
        {
            PatchesApplied = 0;
            PatchesFailed = 0;

            try
            {
                // El reemplazo declara los parametros en el MISMO orden que el objetivo ((IntVec3, Map)):
                // el motor pasa los argumentos en orden, asi que una firma con el orden cambiado
                // generaria IL invalido.
                if (BodyRewriter.Rewrite(module, UtilityTypeName, "GetItemCount", 0, 2, true,
                        Caches, nameof(Caching.ItemCountGrid.Get)))
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
