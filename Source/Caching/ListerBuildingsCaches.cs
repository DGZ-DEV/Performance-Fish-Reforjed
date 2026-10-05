using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using PerformanceFishReforjed.Prepatch;
using RimWorld;
using Verse;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Caches por instancia de un <see cref="ListerBuildings"/>.
    ///
    /// Los contadores no son diagnostico: son la **comprobacion de confianza**. La correccion de las
    /// consultas depende de que los postfijos de Add/Remove esten vivos y de que nadie mas toque las
    /// listas del lister, asi que antes de responder desde el indice se compara el numero de edificios
    /// registrados con el tamano real de la lista del juego. Si no cuadra, se responde con el recorrido
    /// del vanilla. Nunca una respuesta equivocada en silencio: eso romperia el juego (por ejemplo,
    /// <c>ColonistsHaveBuilding</c> devolviendo siempre false).
    /// </summary>
    internal sealed class ListerBuildingsCache
    {
        internal readonly Dictionary<ThingDef, List<Building>> ColonistByDef = new Dictionary<ThingDef, List<Building>>();
        internal readonly Dictionary<ThingDef, List<Building>> NonColonistByDef = new Dictionary<ThingDef, List<Building>>();
        internal readonly Dictionary<Type, IList> ColonistByType = new Dictionary<Type, IList>();

        /// <summary>Edificios coloniales registrados (tiene que coincidir con allBuildingsColonist.Count).</summary>
        internal int ColonistRegistered;

        /// <summary>Edificios no coloniales registrados (tiene que coincidir con allBuildingsNonColonist.Count).</summary>
        internal int NonColonistRegistered;
    }

    /// <summary>
    /// Cuerpos que sustituyen a las consultas de <see cref="ListerBuildings"/> en 1.6, mas el
    /// mantenimiento de los indices desde un postfijo de Harmony en <c>Add</c>/<c>Remove</c>.
    ///
    /// Por que postfijo y no sustitucion de cuerpo en Add/Remove: en 1.6 esos metodos llevan un
    /// sistema <c>TrackingScope</c> que no existia en el 1.4/1.5 que asumia el mod original, y
    /// <c>Track</c> es publico y lo usan el generador de mapas. Copiar sus cuerpos (95 y 67
    /// instrucciones) seria justo el error que prohibe el analisis del proyecto.
    ///
    /// Lo que NO se hace, con el motivo:
    /// <list type="bullet">
    /// <item><b>Cache de escaneres profundos:</b> no es portable. <c>CompDeepScanner</c> no referencia
    /// el lister; el unico consumidor lee el campo publico directamente, asi que no hay punto de
    /// intercepcion.</item>
    /// <item><b>Cache del estado de encendido:</b> invalida por diseno. <c>CompPowerTrader.PowerOn</c>
    /// cambia sin pasar por el lister, asi que solo se cachea QUE edificios tienen cada def y el estado
    /// se lee en el momento de la consulta.</item>
    /// <item><b>Cache de conjuntos:</b> la de bancos de investigacion y la de tipos SI se hacen; la de
    /// escaneres no (ver arriba).</item>
    /// </list>
    /// </summary>
    public static class ListerBuildingsCaches
    {
        /// <summary>Acceso a la cache inyectada, creandola en el primer uso (empieza a null).</summary>
        internal static ListerBuildingsCache Cache(ListerBuildings lister)
        {
            ref ListerBuildingsCache slot = ref lister.ReforjedBuildingsCache();

            if (slot == null)
                slot = new ListerBuildingsCache();

            return slot;
        }

        // ─── Mantenimiento (postfijos de Add/Remove) ─────────────────────────────────

        /// <summary>
        /// Adaptador del postfijo de <c>Add(Building)</c>. Se usan <c>__instance</c> y <c>__0</c> (la
        /// convencion de Harmony) en vez del nombre real del parametro del juego: asi el enganche no
        /// depende de como se llame alli dentro.
        /// </summary>
        public static void AddPostfix(ListerBuildings __instance, Building __0)
            => OnBuildingAdded(__instance, __0);

        /// <summary>Adaptador del postfijo de <c>Remove(Building)</c>.</summary>
        public static void RemovePostfix(ListerBuildings __instance, Building __0)
            => OnBuildingRemoved(__instance, __0);

        /// <summary>
        /// Mantenimiento al aparecer un edificio. Corre SIEMPRE, tambien cuando el original salio antes
        /// por la guarda de roca natural, asi que esa guarda hay que repetirla aqui.
        /// </summary>
        internal static void OnBuildingAdded(ListerBuildings lister, Building b)
        {
            // El postfijo solo corre si el original no lanzo, asi que en la practica b y b.def no son
            // nulos; se comprueba igualmente para no depender de eso.
            if (b?.def == null)
                return;

            if (b.def.building is { isNaturalRock: true })
                return;

            ListerBuildingsCache cache = Cache(lister);

            if (b.Faction == Faction.OfPlayer)
            {
                GetDefList(cache.ColonistByDef, b.def).Add(b);
                cache.ColonistRegistered++;
                AddToTypeLists(cache.ColonistByType, b);
            }
            else
            {
                GetDefList(cache.NonColonistByDef, b.def).Add(b);
                cache.NonColonistRegistered++;
            }
        }

        /// <summary>
        /// Mantenimiento al desaparecer un edificio.
        ///
        /// El vanilla quita de AMBAS listas sin mirar faccion ni roca, asi que aqui se intenta en las dos
        /// y solo se descuenta el contador si el edificio estaba de verdad. Eso hace el mantenimiento
        /// tolerante al bug de <c>Notify_FactionRemoved</c>, donde <c>SetFaction</c> reentra en Remove y
        /// Add mientras se itera.
        /// </summary>
        internal static void OnBuildingRemoved(ListerBuildings lister, Building b)
        {
            ListerBuildingsCache cache = Cache(lister);

            if (RemoveFromDefList(cache.ColonistByDef, b))
                cache.ColonistRegistered--;

            if (RemoveFromDefList(cache.NonColonistByDef, b))
                cache.NonColonistRegistered--;

            RemoveFromTypeLists(cache.ColonistByType, b);
        }

        // ─── Consultas sustituidas ──────────────────────────────────────────────────

        /// <summary>
        /// Sustituye a <c>AllBuildingsColonistOfDef(ThingDef)</c>.
        ///
        /// Conserva el contrato del vanilla: devuelve LA MISMA lista estatica compartida y la limpia en
        /// cada llamada. Lo unico que cambia es de donde se rellena (indice O(k) en vez de recorrer
        /// todos los edificios coloniales).
        /// </summary>
        public static List<Building> AllBuildingsColonistOfDef(ListerBuildings lister, ThingDef def)
        {
            List<Building> result = ListerBuildings.allBuildingsColonistOfDefResult;
            result.Clear();

            if (ColonistIndexIsUsable(lister, nameof(AllBuildingsColonistOfDef)))
            {
                if (def != null && Cache(lister).ColonistByDef.TryGetValue(def, out List<Building>? cached))
                    result.AddRange(cached);

                return result;
            }

            List<Building> all = lister.allBuildingsColonist;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].def == def)
                    result.Add(all[i]);
            }

            return result;
        }

        /// <summary>Sustituye a <c>ColonistsHaveBuilding(ThingDef)</c>: el vanilla recorre toda la lista.</summary>
        public static bool ColonistsHaveBuildingByDef(ListerBuildings lister, ThingDef def)
        {
            if (ColonistIndexIsUsable(lister, nameof(ColonistsHaveBuildingByDef)))
            {
                return def != null
                       && Cache(lister).ColonistByDef.TryGetValue(def, out List<Building>? cached)
                       && cached.Count > 0;
            }

            List<Building> all = lister.allBuildingsColonist;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].def == def)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Sustituye a <c>ColonistsHaveResearchBench()</c>, que en el vanilla es
        /// <c>AllBuildingsColonistOfClass&lt;Building_ResearchBench&gt;().Any()</c>: un recorrido perezoso.
        /// </summary>
        public static bool ColonistsHaveResearchBench(ListerBuildings lister)
        {
            if (ColonistIndexIsUsable(lister, nameof(ColonistsHaveResearchBench)))
            {
                return Cache(lister).ColonistByType.TryGetValue(typeof(Building_ResearchBench), out IList? benches)
                       && benches.Count > 0;
            }

            List<Building> all = lister.allBuildingsColonist;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is Building_ResearchBench)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Sustituye a <c>ColonistsHaveBuildingWithPowerOn(ThingDef)</c>.
        ///
        /// El estado de encendido se lee EN EL MOMENTO, no se cachea: <c>CompPowerTrader.PowerOn</c>
        /// cambia sin pasar por el lister. Lo unico que aporta el indice es no recorrer todos los
        /// edificios coloniales para encontrar los de ese def. La condicion replica la del vanilla,
        /// incluido que un edificio SIN <c>CompPowerTrader</c> cuenta como encendido.
        /// </summary>
        public static bool ColonistsHaveBuildingWithPowerOn(ListerBuildings lister, ThingDef def)
        {
            if (ColonistIndexIsUsable(lister, nameof(ColonistsHaveBuildingWithPowerOn)))
            {
                if (def == null || !Cache(lister).ColonistByDef.TryGetValue(def, out List<Building>? cached))
                    return false;

                for (int i = 0; i < cached.Count; i++)
                {
                    CompPowerTrader? comp = cached[i].TryGetComp<CompPowerTrader>();
                    if (comp == null || comp.PowerOn)
                        return true;
                }

                return false;
            }

            List<Building> all = lister.allBuildingsColonist;
            for (int i = 0; i < all.Count; i++)
            {
                Building b = all[i];
                if (b.def != def)
                    continue;

                CompPowerTrader? comp = b.TryGetComp<CompPowerTrader>();
                if (comp == null || comp.PowerOn)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Sustituye a <c>AllBuildingsColonistOfClass&lt;T&gt;()</c> (con restriccion <c>T : Building</c>):
        /// devuelve la lista del tipo en vez de recorrer todos los edificios coloniales con <c>isinst</c>.
        /// </summary>
        public static IEnumerable<T> AllBuildingsColonistOfClass<T>(ListerBuildings lister) where T : Building
        {
            if (ColonistIndexIsUsable(lister, nameof(AllBuildingsColonistOfClass)))
            {
                return Cache(lister).ColonistByType.TryGetValue(typeof(T), out IList? list)
                    ? (IEnumerable<T>)list
                    : EmptyBuildings<T>();
            }

            return ScanColonistOfType<T>(lister);
        }

        /// <summary>
        /// Sustituye a <c>AllColonistBuildingsOfType&lt;T&gt;()</c>, que en 1.6 **no tiene restriccion** y
        /// se usa con interfaces (por ejemplo <c>IHaulSource</c> en el comerciante). El indice por tipo
        /// solo conoce la jerarquia de <see cref="Building"/>, asi que para cualquier otro T (una
        /// interfaz, o algo ajeno) se recorre: devolver la lista del indice ahi daria vacio, que es una
        /// respuesta equivocada en silencio.
        /// </summary>
        public static IEnumerable<T> AllColonistBuildingsOfType<T>(ListerBuildings lister)
        {
            if (typeof(Building).IsAssignableFrom(typeof(T))
                && ColonistIndexIsUsable(lister, nameof(AllColonistBuildingsOfType)))
            {
                return Cache(lister).ColonistByType.TryGetValue(typeof(T), out IList? list)
                    ? (IEnumerable<T>)list
                    : EmptyBuildings<T>();
            }

            return ScanColonistOfType<T>(lister);
        }

        /// <summary>
        /// Sustituye a <c>AllBuildingsNonColonistOfDef(ThingDef)</c>, que recorre la lista de no
        /// coloniales en cada enumeracion.
        /// </summary>
        public static IEnumerable<Building> AllBuildingsNonColonistOfDef(ListerBuildings lister, ThingDef def)
        {
            if (NonColonistIndexIsUsable(lister, nameof(AllBuildingsNonColonistOfDef)))
            {
                return def != null && Cache(lister).NonColonistByDef.TryGetValue(def, out List<Building>? cached)
                    ? cached
                    : EmptyBuildings<Building>();
            }

            return ScanNonColonistByDef(lister, def);
        }

        // ─── Comprobacion de confianza ──────────────────────────────────────────────

        private static bool ColonistCacheIsTrustworthy(ListerBuildings lister)
            => Cache(lister).ColonistRegistered == lister.allBuildingsColonist.Count;

        private static bool NonColonistCacheIsTrustworthy(ListerBuildings lister)
            => Cache(lister).NonColonistRegistered == lister.allBuildingsNonColonist.Count;

        /// <summary>
        /// ¿Se puede responder desde el indice de coloniales?
        ///
        /// Importante: distinguir "no cuadra" de "no esta en el indice". Con el indice en vigor, que un
        /// def no aparezca significa que NO HAY ninguno, y eso se responde sin recorrer nada. Tratar la
        /// ausencia como un fallo y recorrer la lista convertiria el caso MAS COMUN (preguntar por algo
        /// que no se tiene) en un recorrido O(n), que es justo lo que se quiere evitar.
        /// </summary>
        private static bool ColonistIndexIsUsable(ListerBuildings lister, string what)
        {
            if (ColonistCacheIsTrustworthy(lister))
                return true;

            WarnFallback(what);
            return false;
        }

        private static bool NonColonistIndexIsUsable(ListerBuildings lister, string what)
        {
            if (NonColonistCacheIsTrustworthy(lister))
                return true;

            WarnFallback(what);
            return false;
        }

        /// <summary>
        /// Avisa UNA vez de que los indices no cuadran y las consultas estan usando el recorrido normal.
        /// Sin esto, la caida al respaldo seria invisible salvo pulsando el boton de autocomprobacion.
        /// </summary>
        private static void WarnFallback(string what)
        {
            if (!PerformanceFishReforjedSettings.EnableInternalLogging)
                return;

            Log.ErrorOnce($"[PerformanceFishReforjed] Los indices de edificios no cuadran con las listas del " +
                          $"juego (detectado en {what}): las consultas de edificios estan usando el recorrido " +
                          $"normal (correctas, pero sin ganancia). Pulsa «Autocomprobar indices de ListerBuildings».",
                          FallbackWarningKey);
        }

        private const int FallbackWarningKey = 913377;

        private static IEnumerable<T> EmptyBuildings<T>()
        {
            yield break;
        }

        // ─── Respaldo: los recorridos del vanilla ───────────────────────────────────

        private static IEnumerable<Building> ScanNonColonistByDef(ListerBuildings lister, ThingDef? def)
        {
            List<Building> all = lister.allBuildingsNonColonist;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].def == def)
                    yield return all[i];
            }
        }

        /// <summary>
        /// Recorrido por indice (no un iterador de la lista) para parecerse al del vanilla, que relee el
        /// tamano en cada vuelta y por tanto tolera cambios durante la enumeracion.
        /// </summary>
        private static IEnumerable<T> ScanColonistOfType<T>(ListerBuildings lister)
        {
            List<Building> all = lister.allBuildingsColonist;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is T t)
                    yield return t;
            }
        }

        // ─── Indices ────────────────────────────────────────────────────────────────

        private static List<Building> GetDefList(Dictionary<ThingDef, List<Building>> byDef, ThingDef def)
        {
            if (byDef.TryGetValue(def, out List<Building>? list))
                return list;

            list = new List<Building>();
            byDef.Add(def, list);
            return list;
        }

        private static bool RemoveFromDefList(Dictionary<ThingDef, List<Building>> byDef, Building b)
        {
            // Sin def no se puede localizar en el indice: se devuelve false y el contador se queda como
            // estaba, lo que hace que la comprobacion de confianza falle y las consultas caigan al
            // recorrido del vanilla. Correcto, solo mas lento.
            if (b?.def == null)
                return false;

            if (!byDef.TryGetValue(b.def, out List<Building>? list))
                return false;

            return list.Remove(b);
        }

        /// <summary>
        /// Anade el edificio a la lista de su tipo y a la de TODOS sus tipos base hasta
        /// <see cref="Building"/> incluido.
        ///
        /// Incluir <see cref="Building"/> no es un detalle: el original de PF se detiene ANTES, asi que
        /// su cache devolveria una lista vacia para <c>AllBuildingsColonistOfClass&lt;Building&gt;()</c>
        /// cuando el vanilla devuelve todos los edificios coloniales.
        /// </summary>
        private static void AddToTypeLists(Dictionary<Type, IList> byType, Building b)
        {
            Type? type = b.GetType();

            while (type != null && typeof(Building).IsAssignableFrom(type))
            {
                GetTypeList(byType, type).Add(b);
                type = type.BaseType;
            }
        }

        private static void RemoveFromTypeLists(Dictionary<Type, IList> byType, Building b)
        {
            Type? type = b.GetType();

            while (type != null && typeof(Building).IsAssignableFrom(type))
            {
                if (byType.TryGetValue(type, out IList? list))
                    list.Remove(b);

                type = type.BaseType;
            }
        }

        private static IList GetTypeList(Dictionary<Type, IList> byType, Type type)
        {
            if (byType.TryGetValue(type, out IList? list))
                return list;

            list = NewListFor(type);
            byType.Add(type, list);
            return list;
        }

        private static List<T> NewList<T>() => new List<T>();

        private static readonly MethodInfo NewListDefinition =
            typeof(ListerBuildingsCaches).GetMethod(nameof(NewList), BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("No se encontro el creador de listas por tipo.");

        private static IList NewListFor(Type type)
            => (IList)NewListDefinition.MakeGenericMethod(type).Invoke(null, null)!;
    }
}
