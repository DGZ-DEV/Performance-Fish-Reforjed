using System;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFishReforjed.Caching;
using PerformanceFishReforjed.Prepatch;
using RimWorld;
using Verse;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Prepatch para <c>StorageSettings.AllowedToAccept(Thing)</c> y
    /// <c>StorageSettings.AllowedToAccept(ThingDef)</c>.
    ///
    /// Sustituye los 24 instrucciones de cada sobrecarga por una llamada a nuestra cache.
    /// La cache vive en un campo inyectado en <c>StorageSettings</c>
    /// (<c>ReforjedAllowedToAcceptCache</c>), y el mantenimiento de invalidación se hace
    /// con postfijos de Harmony sobre <c>TryNotifyChanged</c> y <c>set_Priority</c>.
    ///
    /// NOTA: El reemplazo es un método público estático que recibe el StorageSettings como
    /// primer argumento (receptor), porque el motor de prepatching pasa los argumentos en
    /// orden: receptor + parámetros del objetivo. La firma del reemplazo DEBE coincidir
    /// exactamente con (StorageSettings, Thing/ThingDef).
    /// </summary>
    internal static class StorageSettingsPrepatch
    {
        internal static int PatchedCount = 0;
        internal static int FailedCount = 0;

        /// <summary>Punto de entrada llamado desde FreePatchEntry.</summary>
        internal static void Start(ModuleDefinition module)
        {
            // StorageSettings.AllowedToAccept(Thing)
            if (BodyRewriter.Rewrite(
                    module,
                    "RimWorld.StorageSettings",
                    "AllowedToAccept",
                    genericArity: 0,
                    parameterCount: 1,
                    isStatic: false,
                    typeof(StorageSettingsPrepatch),
                    nameof(AllowedToAccept_Thing),
                    firstParameterTypeContains: "Verse.Thing"))
            {
                PatchedCount++;
            }
            else
            {
                FailedCount++;
            }

            // StorageSettings.AllowedToAccept(ThingDef)
            if (BodyRewriter.Rewrite(
                    module,
                    "RimWorld.StorageSettings",
                    "AllowedToAccept",
                    genericArity: 0,
                    parameterCount: 1,
                    isStatic: false,
                    typeof(StorageSettingsPrepatch),
                    nameof(AllowedToAccept_ThingDef),
                    firstParameterTypeContains: "Verse.ThingDef"))
            {
                PatchedCount++;
            }
            else
            {
                FailedCount++;
            }
        }

        /// <summary>
        /// Reemplazo de <c>StorageSettings.AllowedToAccept(Thing)</c>.
        /// Firma: (StorageSettings __instance, Thing t) -> bool
        /// </summary>
        public static bool AllowedToAccept_Thing(StorageSettings __instance, Thing t)
        {
            // Obtener/crear la cache de esta instancia de StorageSettings
            // El campo inyectado puede ser null en instancias recién creadas (p. ej. durante generación de mapa),
            // así que lo inicializamos si hace falta.
            ref AllowedToAcceptCache cache = ref __instance.ReforjedAllowedToAcceptCache();
            if (cache == null)
            {
                cache = new AllowedToAcceptCache();
            }

            // Intentar lectura cacheada
            if (cache.TryGet(t, out bool result))
                return result;

            // Miss: evaluar vanilla y actualizar cache
            // El vanilla hace: filter.Allows(t) && (owner == null || owner.GetParentStoreSettings()?.AllowedToAccept(t) ?? true)
            // Como estamos en un prepatch, llamamos al método original... pero el cuerpo original ya fue reemplazado.
            // Por eso NO podemos llamar a __instance.AllowedToAccept(t) aquí (sería recursión).
            // En su lugar, replicamos la lógica vanilla de los 24 instrucciones:
            //   1. filter.Allows(t)
            //   2. Si falla, false
            //   3. Si owner == null, true
            //   4. Si parentStoreSettings == null, true
            //   5. parentStoreSettings.AllowedToAccept(t) (recursivo hacia el padre)

            // Como el filtro y owner no cambian sin invalidar, podemos llamar al método
            // ThingFilter.Allows que SÍ sigue siendo vanilla, y luego hacer la recursión
            // hacia el padre usando ESTE MISMO método (que tiene cache).

            if (t == null || __instance.filter == null)
                return false;

            if (!__instance.filter.Allows(t))
                return false;

            IStoreSettingsParent owner = __instance.owner;
            if (owner == null)
                return true;

            StorageSettings parent = owner.GetParentStoreSettings();
            if (parent == null)
                return true;

            // Recursión hacia el padre (con cache)
            result = AllowedToAccept_Thing(parent, t);
            cache.Update(isThing: true, t.thingIDNumber, result);
            return result;
        }

        /// <summary>
        /// Reemplazo de <c>StorageSettings.AllowedToAccept(ThingDef)</c>.
        /// Firma: (StorageSettings __instance, ThingDef def) -> bool
        /// </summary>
        public static bool AllowedToAccept_ThingDef(StorageSettings __instance, ThingDef def)
        {
            // Obtener/crear la cache de esta instancia de StorageSettings
            ref AllowedToAcceptCache cache = ref __instance.ReforjedAllowedToAcceptCache();
            if (cache == null)
            {
                cache = new AllowedToAcceptCache();
            }

            if (cache.TryGet(def, out bool result))
                return result;

            if (def == null || __instance.filter == null)
                return false;

            if (!__instance.filter.Allows(def))
                return false;

            IStoreSettingsParent owner = __instance.owner;
            if (owner == null)
                return true;

            StorageSettings parent = owner.GetParentStoreSettings();
            if (parent == null)
                return true;

            result = AllowedToAccept_ThingDef(parent, def);
            cache.Update(isThing: false, def.shortHash, result);
            return result;
        }
    }
}
