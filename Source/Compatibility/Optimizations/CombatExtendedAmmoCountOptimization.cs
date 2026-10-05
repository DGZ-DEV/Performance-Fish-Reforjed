using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace PerformanceFishReforjed.Compatibility.Optimizations
{
    /// <summary>
    /// Optimización condicional nº1: <c>CombatExtended.CompInventory.AmmoCountOfDef</c>.
    ///
    /// La implementación original de CE es:
    /// <code>
    ///   public int AmmoCountOfDef(AmmoDef def)
    ///   {
    ///       return ammoListCached.Where((Thing t) => (object)t.def == def).Sum((Thing t) => t.stackCount);
    ///   }
    /// </code>
    ///
    /// Es un hot path real de CE (6+ call-sites: recarga por think, job givers y el gizmo leído por
    /// frame). El LINQ materializa enumeradores + closures en cada llamada.
    ///
    /// Lo sustituimos por un <c>Prefix</c> que devuelve <c>false</c> y escribe <c>__result</c> con un
    /// <c>for</c> manual sobre el mismo <c>ammoListCached</c>, preservando exactamente el resultado
    /// (misma comparación por referencia <c>(object)t.def == def</c> y misma acumulación de stackCount).
    ///
    /// Seguridad:
    ///  - Sin dependencia de compilación ni carga con CombatExtended: el tipo y el campo privado se
    ///    resuelven por reflexión (una sola vez). La firma del <c>Prefix</c> usa solo tipos del juego
    ///    (<c>ThingComp</c>, <c>object</c>); <c>Thing.def</c>/<c>stackCount</c> son campos públicos de
    ///    vanilla, así que el bucle los lee directamente, sin reflexión por elemento.
    ///  - Solo se registra si <c>CompatManager.IsActive(CombatExtended)</c> y el método/el campo existen.
    ///  - Si el campo o el tipo cambian, se resuelve a no-op sin romper la carga.
    /// </summary>
    internal static class CombatExtendedAmmoCountOptimization
    {
        /// <summary>Campo privado cachead del CE con la lista de municiones (resuelto una vez).</summary>
        private static FieldInfo? _ammoListCachedField;

        private static bool _applied;

        /// <summary>
        /// Registra el parche una sola vez si todo cuadra. Devuelve false si hay que informar
        /// y no romper la carga.
        /// </summary>
        internal static bool TryPatch()
        {
            if (_applied)
                return true;

            try
            {
                // 1. Tipo CompInventory de CE por reflexión (sin referencia de compilación).
                Type? compInventory = AccessTools.TypeByName("CombatExtended.CompInventory");
                if (compInventory == null)
                {
                    Log.Message("[PerformanceFishReforjed] CE CompInventory not found; " +
                                "CombatExtended optimization skipped (CE not loaded).");
                    return false;
                }

                // 2. Método objetivo, buscado por nombre (no puedo tipar su parámetro AmmoDef).
                MethodInfo? target = AccessTools.DeclaredMethod(compInventory, "AmmoCountOfDef");
                if (target == null)
                {
                    Log.Message("[PerformanceFishReforjed] CE CompInventory.AmmoCountOfDef not found; " +
                                "optimization skipped (signature changed).");
                    return false;
                }

                // 3. Campo privado ammoListCached (debe ser List<Thing>).
                FieldInfo? field = AccessTools.Field(compInventory, "ammoListCached");
                if (field == null || field.FieldType != typeof(List<Thing>))
                {
                    Log.Message("[PerformanceFishReforjed] CE CompInventory.ammoListCached field type " +
                                "unexpected; optimization skipped (field changed).");
                    return false;
                }
                _ammoListCachedField = field;

                // 4. Prefix (firma con solo tipos del juego).
                MethodInfo? prefix = AccessTools.Method(typeof(CombatExtendedAmmoCountOptimization), nameof(Prefix));
                if (prefix == null)
                {
                    Log.Error("[PerformanceFishReforjed] CE AmmoCountOfDef prefix not found.");
                    return false;
                }

                PerformanceFishReforjedMod.HarmonyInstance.Patch(target, prefix: new HarmonyMethod(prefix));
                _applied = true;

                if (PerformanceFishReforjedSettings.EnableInternalLogging)
                {
                    Log.Message("[PerformanceFishReforjed] CombatExtended optimization hooked: " +
                                "CompInventory.AmmoCountOfDef (LINQ -> manual loop).");
                }

                return true;
            }
            catch (Exception e)
            {
                Log.Warning("[PerformanceFishReforjed] Failed to apply CombatExtended " +
                            "AmmoCountOfDef optimization: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Reemplaza <c>AmmoCountOfDef</c> por un bucle sin LINQ.
        ///
        ///  - <c>__instance</c> es el <c>ThingComp</c> (CompInventory deriva de ThingComp).
        ///  - <c>__0</c> es el primer argumento del original (<c>def</c>), tipado <c>object</c> porque
        ///    CE (AmmoDef) no se referencia; la comparación es por referencia contra <c>Thing.def</c>,
        ///    exactamente lo que hace el original.
        /// </summary>
        private static bool Prefix(ThingComp __instance, object __0, ref int __result)
        {
            FieldInfo? fld = _ammoListCachedField;
            if (__instance == null || fld == null)
                return true; // defensivo: dejar correr el original

            if (fld.GetValue(__instance) is not List<Thing> ammo)
                return true; // el mod cambió el tipo: no optimizar

            int result = 0;
            for (int i = 0; i < ammo.Count; i++)
            {
                Thing t = ammo[i];
                // Misma comparación de referencia que el original: (object)t.def == def
                if (ReferenceEquals(t.def, __0))
                    result += t.stackCount;
            }

            __result = result;
            return false; // ya produjimos el resultado; saltar el cuerpo original
        }
    }
}