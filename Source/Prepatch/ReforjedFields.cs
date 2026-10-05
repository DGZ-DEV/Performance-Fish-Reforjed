using System;
using System.Collections;
using System.Collections.Generic;
using PerformanceFishReforjed.Caching;
using Prepatcher;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Campos que Prepatcher inyecta dentro de tipos del juego.
    ///
    /// La maquinaria la implementa Prepatcher: nosotros solo declaramos el accesor como
    /// <c>extern</c> y el rellena el cuerpo, anade el campo real al tipo destino y lo deja
    /// inicializado. Eso permite guardar estado por objeto sin diccionarios externos.
    ///
    /// Las caches de stats necesitan una mascara de bits porque un campo recien inyectado vale
    /// 0, y 0 es un valor de stat legitimo: sin la mascara no se puede distinguir "sin calcular"
    /// de "0 de verdad".
    /// </summary>
    public static class ReforjedFields
    {
        // ─── Caches de stats de ThingDef (DefStatCache) ─────────────────────────────

        /// <summary>Mascara de bits: cada bit indica que su valor de stat esta cacheado.</summary>
        [PrepatcherField]
        internal static extern ref int ReforjedStatMask(this ThingDef def);

        [PrepatcherField]
        internal static extern ref float ReforjedBaseMarketValue(this ThingDef def);

        [PrepatcherField]
        internal static extern ref float ReforjedBaseMass(this ThingDef def);

        [PrepatcherField]
        internal static extern ref float ReforjedBaseFlammability(this ThingDef def);

        [PrepatcherField]
        internal static extern ref float ReforjedBaseMaxHitPoints(this ThingDef def);

        // ─── Caches por Mapa ────────────────────────────────────────────────────────

        /// <summary>Cache de masa de pawns (gear + inventory), indexada por thingIDNumber.</summary>
        [PrepatcherField]
        internal static extern ref IntCache<PawnMassCache> ReforjedMassCache(this Verse.Map map);

        /// <summary>Contador de objetos por celda.</summary>
        [PrepatcherField]
        internal static extern ref ItemCountGridData ReforjedItemCountGrid(this Verse.Map map);

        /// <summary>Bitmap de bloqueantes de almacenamiento por celda.</summary>
        [PrepatcherField]
        internal static extern ref StorageBlockerGridData ReforjedStorageBlockerGrid(this Verse.Map map);

        // ─── Caches de almacenamiento ───────────────────────────────────────────────

        /// <summary>Cache de AllowedToAccept de un StorageSettings.</summary>
        [PrepatcherField]
        internal static extern ref AllowedToAcceptCache ReforjedAllowedToAcceptCache(this StorageSettings settings);

        /// <summary>Cache de capacidad de un SlotGroup.</summary>
        [PrepatcherField]
        internal static extern ref SlotGroupCapacityCache ReforjedCapacityCache(this SlotGroup slotGroup);

        /// <summary>Cache de capacidad de un StorageGroup.</summary>
        [PrepatcherField]
        internal static extern ref StorageGroupCapacityCache ReforjedCapacityCache(this StorageGroup storageGroup);

        // ─── Caches de Listers ──────────────────────────────────────────────────────

        [PrepatcherField]
        internal static extern ref IntCache<int> ReforjedIndexMapByDef(this ListerThings lister);

        [PrepatcherField]
        internal static extern ref GroupIndexCaches ReforjedIndexMapByGroup(this ListerThings lister);

        [PrepatcherField]
        internal static extern ref Dictionary<Type, IList> ReforjedThingsByType(this ListerThings lister);

        [PrepatcherField]
        internal static extern ref ListerBuildingsCache ReforjedBuildingsCache(this ListerBuildings lister);

        [PrepatcherField]
        internal static extern ref IntCache<int> ReforjedThingOwnerIndexMap<T>(this ThingOwner<T> owner) where T : Thing;

        // ─── Caches de mundo ────────────────────────────────────────────────────────

        [PrepatcherField]
        internal static extern ref WorldPawnsCache ReforjedWorldPawnsCache(this RimWorld.Planet.WorldPawns pawns);

        [PrepatcherField]
        internal static extern ref WorldObjectsHolderCache ReforjedCachedWorldObjects(this RimWorld.Planet.WorldObjectsHolder holder);

        // ─── Gas grid ───────────────────────────────────────────────────────────────

        [PrepatcherField]
        internal static extern ref ParallelGasGrid[] ReforjedParallelGasGrids(this GasGrid grid);

        // ─── WorkGiver_DoBill (Fase 6) ─────────────────────────────────────────────

        /// <summary>
        /// Petição de trabalho potencial caché por instancia de WorkGiver_DoBill. El resultado de
        /// <c>get_PotentialWorkThingRequest</c> es constante para un WorkGiverDef (sus defs fijos no
        /// cambian en runtime), así que se puede resolver una sola vez.
        /// </summary>
        [PrepatcherField]
        internal static extern ref WorkRequestCache ReforjedWorkRequestCache(this RimWorld.WorkGiver_DoBill wg);

        // ─── Reflection cache (Fase 5) ─────────────────────────────────────────────

        /// <summary>
        /// Cache de nombres de tipo completos y firmas de reflexión para métodos clave.
        /// Evita resolver Type.GetFullName, GetField, GetMethod, GetProperty,
        /// GetConstructors, GetCustomAttributes en cada llamada.
        ///
        /// La vida del cache va atada a la instancia del tipo del juego.
        /// Se registra en CacheRegistry para vaciado al cargar partida.
        ///
        /// NOTA: los campos Prepatcher requieren un tipo destino concreto. No se puede declarar un
        /// accesor genérico <c>ReforjedReflectionCache&lt;T&gt;(this T obj)</c>; Prepatcher no puede
        /// inyectar un campo en un tipo abierto <c>T</c> ("Couldn't resolve target type for new field").
        /// Por eso cada tipo que necesita cache tiene su propia sobrecarga concreta (ThingDef, Map,
        /// Game, ...). Si se necesita cache para un tipo no listado, añadir una sobrecarga explícita.
        /// </summary>
        [PrepatcherField]
        internal static extern ref IntCache<int> ReforjedReflectionCache(this ThingDef def);

        [PrepatcherField]
        internal static extern ref IntCache<int> ReforjedReflectionCache(this HediffDef def);

        [PrepatcherField]
        internal static extern ref IntCache<int> ReforjedReflectionCache(this Hediff hediff);

        [PrepatcherField]
        internal static extern ref IntCache<int> ReforjedReflectionCache(this Ability ability);

        [PrepatcherField]
        internal static extern ref IntCache<int> ReforjedReflectionCache(this Verse.ThingWithComps thingWithComps);

        [PrepatcherField]
        internal static extern ref IntCache<int> ReforjedReflectionCache(this RimWorld.Planet.WorldObject worldObject);

        [PrepatcherField]
        internal static extern ref IntCache<int> ReforjedReflectionCache(this Verse.Map map);

        [PrepatcherField]
        internal static extern ref IntCache<int> ReforjedReflectionCache(this RimWorld.Planet.World world);

        [PrepatcherField]
        internal static extern ref IntCache<int> ReforjedReflectionCache(this Verse.Game game);
    }
}