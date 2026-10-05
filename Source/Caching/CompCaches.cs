// This file is a Modification (MPL-2.0, section 1.10) of the original
// Performance Fish by bradson (https://github.com/bbradson/Performance-Fish),
// which is covered by the Mozilla Public License 2.0.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Entrada de cache para "el primer comp de tipo T de este objeto".
    ///
    /// <see cref="Valid"/> y no un centinela numerico: una entrada recien creada en un array de
    /// structs vale 0, y 0 es un valor legitimo de <c>List&lt;T&gt;._version</c>. Marcar la validez
    /// con un <c>bool</c> (que por defecto es false = "no valida") es la unica forma de no
    /// confundir "sin calcular" con "calculado hace cero modificaciones". Es la trampa #3 de
    /// PENDIENTE.md aplicada al indice.
    ///
    /// Este tipo se comparte entre las familias de ThingComp, CompProperties, HediffComp,
    /// HediffCompProperties, AbilityComp y WorldObjectComp. Es seguro porque la cache es estatica
    /// por instanciacion cerrada y una clase concreta solo puede pertenecer a UNA familia (las
    /// restricciones son disjuntas): cada T vive en una sola cache y las claves no se cruzan.
    /// </summary>
    internal struct CompEntry<T> where T : class
    {
        public bool Valid;
        public int ListVersion;
        public object? List;
        public T? Comp;

        /// <summary>
        /// Valida la entrada contra la lista ACTUAL: identidad y version.
        ///
        /// La identidad no es opcional. Verificado leyendo el IL de 1.6: al cargar una partida se
        /// SUSTITUYE la instancia de la lista (<c>ThingWithComps.ExposeData</c> llama a
        /// <c>InitializeComps</c>, que hace <c>new List&lt;ThingComp&gt;()</c>, y Map/World/Game
        /// pasan <c>ref components</c> a <c>Scribe_Collections.Look</c>), no solo su contenido.
        /// Los identificadores se conservan entre guardado y carga, asi que la lista nueva puede
        /// empezar con la MISMA <c>_version</c> que la cacheada: sin comparar la identidad, la
        /// entrada se daria por buena y devolveria un comp de la partida anterior.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValidFor(object list, int listVersion)
            => Valid && ListVersion == listVersion && ReferenceEquals(List, list);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Store(object list, int listVersion, T? comp)
        {
            List = list;
            ListVersion = listVersion;
            Comp = comp;
            Valid = true;
        }
    }

    /// <summary>
    /// Entrada de cache de <c>ThingDef.HasComp(Type)</c>.
    ///
    /// Guarda el tipo EXACTO al que corresponde la respuesta y lo comprueba al leer. Motivo: la
    /// clave es una mezcla de def y tipo de comp (dos enteros en uno), asi que dos pares distintos
    /// pueden caer en el mismo hueco. Verificar el tipo hace que una colision solo cueste un
    /// recalculo, nunca una respuesta equivocada, que es lo importante: aqui la respuesta es un
    /// booleano y devolverlo mal cambia el comportamiento del juego.
    /// </summary>
    internal struct HasCompByTypeEntry
    {
        public bool Valid;
        public int ListVersion;
        public object? List;
        public bool HasComp;
        public Type? CompType;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValidFor(object list, int listVersion, Type compType)
            => Valid && ListVersion == listVersion && ReferenceEquals(List, list)
               && CompType == compType;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Store(object list, int listVersion, bool hasComp, Type compType)
        {
            List = list;
            ListVersion = listVersion;
            HasComp = hasComp;
            CompType = compType;
            Valid = true;
        }
    }

    /// <summary>
    /// Entrada de cache de <c>ThingDef.HasComp&lt;T&gt;()</c>.
    ///
    /// Es generica sobre <c>T</c> A PROPOSITO: el titular <c>IntCaches&lt;TValue&gt;</c> es
    /// estatico por instanciacion cerrada, asi que cada <c>T</c> recibe su propia tabla y la
    /// respuesta de <c>HasComp&lt;X&gt;()</c> no se filtra a <c>HasComp&lt;Y&gt;()</c> sobre el
    /// mismo def (Hallazgo H2 de 2026-10-05-findings-for-author.es.md).
    /// </summary>
    internal struct HasCompEntry<T> where T : ThingComp
    {
        public bool Valid;
        public int ListVersion;
        public object? List;
        public bool HasComp;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValidFor(object list, int listVersion)
            => Valid && ListVersion == listVersion && ReferenceEquals(List, list);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Store(object list, int listVersion, bool hasComp)
        {
            List = list;
            ListVersion = listVersion;
            HasComp = hasComp;
            Valid = true;
        }
    }

    /// <summary>
    /// Contadores de la pagina de ajustes. Se mantienen atomicos porque son la superficie de
    /// medicion del mod y leerlos desde la interfaz no debe poder corromperlos.
    /// </summary>
    internal static class CompCacheCounters
    {
        private static long _hits;
        private static long _misses;

        /// <summary>Consultas resueltas sin recorrer la lista de comps.</summary>
        internal static long Hits => Interlocked.Read(ref _hits);

        /// <summary>Consultas que obligaron a recorrer la lista (primera vez o lista cambiada).</summary>
        internal static long Misses => Interlocked.Read(ref _misses);

        internal static void AddHit() => Interlocked.Increment(ref _hits);

        internal static void AddMiss() => Interlocked.Increment(ref _misses);

        internal static void Reset()
        {
            Interlocked.Exchange(ref _hits, 0);
            Interlocked.Exchange(ref _misses, 0);
        }
    }

    /// <summary>
    /// Cache de una sola instancia (hay un unico <see cref="Game"/> y un unico
    /// <see cref="World"/>), con comprobacion de identidad del propietario.
    ///
    /// Tres parametros, y los tres hacen falta: <typeparamref name="TComp"/> es el tipo que se
    /// pregunta (si no estuviera, todas las preguntas "dame tal componente" compartirian una sola
    /// entrada), <typeparamref name="TElement"/> es el tipo de la lista (WorldComponent o
    /// GameComponent) y <typeparamref name="TOwner"/> distingue Game de World.
    ///
    /// No necesita registro de limpieza: guarda la referencia al propietario, asi que al cambiar
    /// de partida la identidad no coincide, la entrada se considera sucia y se recalcula. Es la
    /// misma idea que el original (<c>_game != game</c>), pero anadiendo <c>Valid</c> para no
    /// depender de que <c>_version</c> no valga exactamente lo mismo por casualidad.
    /// </summary>
    internal static class SingleCompCache<TComp, TElement, TOwner>
        where TComp : class
        where TElement : class
        where TOwner : class
    {
        private static bool _valid;
        private static int _listVersion;
        private static object? _list;
        private static TOwner? _owner;
        private static TComp? _comp;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryGet(TOwner owner, List<TElement> components, out TComp? comp)
        {
            // Identidad del propietario Y de la lista: al cargar partida, Map/World/Game pasan
            // "ref components" a Scribe_Collections.Look, asi que la lista se sustituye entera y la
            // nueva puede tener la misma _version que la cacheada.
            if (_valid && ReferenceEquals(_owner, owner) && ReferenceEquals(_list, components)
                && _listVersion == components._version)
            {
                comp = _comp;
                return true;
            }

            comp = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Update(TOwner owner, List<TElement> components, TComp? comp)
        {
            _owner = owner;
            _list = components;
            _listVersion = components._version;
            _comp = comp;
            _valid = true;
        }
    }

    /// <summary>
    /// Cuerpos que sustituyen a los metodos de comps de 1.6.
    ///
    /// Cada metodo de aqui reemplaza integramente el cuerpo del original mediante el motor de
    /// prepatching, asi que no hay coste de Harmony en el camino caliente. La semantica es la del
    /// vanilla de 1.6 (verificado leyendo su IL, no supuesto) con un indice por identificador de
    /// objeto delante.
    ///
    /// Invalidacion: identidad de la lista + <c>List&lt;T&gt;._version</c>. Hacen falta LAS DOS.
    /// La version detecta que se anadio, quito o reemplazo un elemento (<c>set_Item</c> tambien
    /// incrementa <c>_version</c> en el mscorlib del juego). La identidad detecta lo que la version
    /// no puede: al cargar una partida la lista se SUSTITUYE por una instancia nueva, y los
    /// identificadores (thingIDNumber, loadID, Id, ID, uniqueID) se conservan, asi que la lista
    /// nueva puede empezar con el mismo valor de version que tenia la cacheada.
    ///
    /// Los identificadores negativos (objetos aun sin registrar: <c>Ability.Id</c>,
    /// <c>Map.uniqueID</c>, <c>WorldObject.ID</c> valen -1 hasta que se asignan) NO se cachean: se
    /// responde con el recorrido directo, para no meter claves invalidas ni colisiones en el indice.
    ///
    /// Divergencia consciente: en <c>Ability.CompOfType&lt;T&gt;</c> el vanilla lanza
    /// <c>NullReferenceException</c> cuando <c>comps</c> es null (llama a
    /// <c>GenCollection.FirstOrDefault</c> sin comprobarlo, y es alcanzable porque
    /// <c>Ability.Initialize</c> deja la lista a null si el def no tiene comps). Aqui se devuelve
    /// null, igual que hace el original de Performance Fish: es un cambio de excepcion a valor,
    /// deliberado y anotado en el informe del IL.
    /// </summary>
    public static class CompCaches
    {
        // ─── ThingWithComps.GetComp<T> ───────────────────────────────────────────────

        public static T? ThingComp<T>(ThingWithComps thing) where T : ThingComp
        {
            List<ThingComp>? comps = thing.comps;
            if (comps == null)
                return null;

            int key = thing.thingIDNumber;
            if (key < 0)
                return VanillaThingComp<T>(thing, comps);

            ref CompEntry<T> entry = ref IntCaches<CompEntry<T>>.Cache.GetOrAdd(key);
            if (entry.IsValidFor(comps, comps._version))
            {
                CompCacheCounters.AddHit();
                return entry.Comp;
            }

            CompCacheCounters.AddMiss();
            T? comp = VanillaThingComp<T>(thing, comps);
            entry.Store(comps, comps._version, comp);
            return comp;
        }

        /// <summary>
        /// Replica EXACTA de <c>ThingWithComps.GetComp&lt;T&gt;</c> en 1.6, incluida su ruta rapida.
        ///
        /// Se lee el IL del juego porque la version de Performance Fish para 1.4/1.5 NO es
        /// equivalente, y esto no es un detalle teorico: el vanilla de 1.6 ya tiene una cache
        /// propia, <c>compsByType</c> (<c>Dictionary&lt;Type, ThingComp[]&gt;</c>, construida una
        /// sola vez en <c>InitializeComps</c>, nunca invalidada), y su criterio es el TIPO EXACTO,
        /// no "es un T":
        ///
        /// <list type="number">
        /// <item>Con menos de 3 comps ni siquiera la consulta: mira comps[0] y, si acaso, comps[1].</item>
        /// <item>Con 3 o mas, si <c>typeof(T)</c> esta en <c>compsByType</c> devuelve
        /// <c>arr[0]</c>, que es el primer comp de tipo EXACTO T. Un subtipo de T colocado antes en
        /// la lista NO se devuelve aqui, pero si en el recorrido lineal: por eso hay que copiar la
        /// ruta, no simplificarla a "primer <c>is T</c>".</item>
        /// <item>Si la clave no esta y T es sealed, devuelve null sin recorrer la lista.</item>
        /// <item>Si no, recorrido lineal con <c>as T</c> (aqui si valen subtipos).</item>
        /// </list>
        ///
        /// Se conservan incluso los casos patologicos (lista vacia que lanza
        /// <c>ArgumentOutOfRangeException</c> al indexar, y <c>arr</c> vacio que lanzaria
        /// <c>IndexOutOfRangeException</c>): convertir una excepcion del juego en un valor seria un
        /// cambio de comportamiento, y el objetivo de esta cache es no cambiar ninguno.
        /// </summary>
        private static T? VanillaThingComp<T>(ThingWithComps thing, List<ThingComp> comps) where T : ThingComp
        {
            int count = comps.Count;

            if (count < 3)
            {
                T? first = comps[0] as T;
                if (first != null)
                    return first;

                if (count == 2)
                {
                    T? second = comps[1] as T;
                    if (second != null)
                        return second;
                }

                return null;
            }

            Dictionary<Type, ThingComp[]>? compsByType = thing.compsByType;
            if (compsByType != null)
            {
                if (compsByType.TryGetValue(typeof(T), out ThingComp[]? exact))
                    return (T?)(object?)exact[0];

                if (GenTypes.IsSealedWithCache(typeof(T)))
                    return null;
            }

            for (int i = 0; i < count; i++)
            {
                if (comps[i] is T comp)
                    return comp;
            }

            return null;
        }

        // ─── ThingDef.GetCompProperties<T> ───────────────────────────────────────────

        public static T? ThingCompProperties<T>(ThingDef thingDef) where T : CompProperties
        {
            List<CompProperties>? comps = thingDef.comps;
            if (comps == null)
                return null;

            int key = thingDef.shortHash;
            if (key < 0)
                return FindCompProperties<T>(comps);

            ref CompEntry<T> entry = ref IntCaches<CompEntry<T>>.Cache.GetOrAdd(key);
            if (entry.IsValidFor(comps, comps._version))
            {
                CompCacheCounters.AddHit();
                return entry.Comp;
            }

            CompCacheCounters.AddMiss();
            T? comp = FindCompProperties<T>(comps);
            entry.Store(comps, comps._version, comp);
            return comp;
        }

        private static T? FindCompProperties<T>(List<CompProperties> comps) where T : CompProperties
        {
            for (int i = 0; i < comps.Count; i++)
            {
                if (comps[i] is T comp)
                    return comp;
            }

            return null;
        }

        // ─── ThingDef.HasComp(Type) ─────────────────────────────────────────────────

        public static bool HasCompByType(ThingDef thingDef, Type compType)
        {
            List<CompProperties>? comps = thingDef.comps;
            if (comps == null)
                return false;

            // compType nulo: el vanilla compara "compClass == compType" y devuelve false sin
            // lanzar (salvo que algun compClass sea nulo). Aqui se responde con el recorrido
            // directo porque TypeHandle lanzaria NullReferenceException.
            if (compType == null)
                return FindCompByType(comps, compType);

            int key = thingDef.shortHash;
            if (key < 0)
                return FindCompByType(comps, compType);

            // La clave combina def y tipo de comp: la respuesta depende de ambos. Se mezclan en
            // un solo entero en lugar de usar una clave compuesta, para no asignar nada; como esa
            // mezcla puede colisionar, la entrada guarda el tipo y se comprueba al leer.
            int combined = Combine(key, compType.TypeHandle.GetHashCode());

            ref HasCompByTypeEntry entry = ref IntCaches<HasCompByTypeEntry>.Cache.GetOrAdd(combined);
            if (entry.Valid && entry.ListVersion == comps._version
                && ReferenceEquals(entry.List, comps) && entry.CompType == compType)
            {
                CompCacheCounters.AddHit();
                return entry.HasComp;
            }

            CompCacheCounters.AddMiss();
            bool hasComp = FindCompByType(comps, compType);
            entry.Store(comps, comps._version, hasComp, compType);
            return hasComp;
        }

        private static bool FindCompByType(List<CompProperties> comps, Type? compType)
        {
            for (int i = 0; i < comps.Count; i++)
            {
                if (comps[i].compClass == compType)
                    return true;
            }

            return false;
        }

        // ─── ThingDef.HasComp<T> ────────────────────────────────────────────────────

        public static bool HasComp<T>(ThingDef thingDef) where T : ThingComp
        {
            List<CompProperties>? comps = thingDef.comps;
            if (comps == null)
                return false;

            int key = thingDef.shortHash;
            if (key < 0)
                return FindCompClass<T>(comps);

            ref HasCompEntry<T> entry = ref IntCaches<HasCompEntry<T>>.Cache.GetOrAdd(key);
            if (entry.IsValidFor(comps, comps._version))
            {
                CompCacheCounters.AddHit();
                return entry.HasComp;
            }

            CompCacheCounters.AddMiss();
            bool hasComp = FindCompClass<T>(comps);
            entry.Store(comps, comps._version, hasComp);
            return hasComp;
        }

        private static bool FindCompClass<T>(List<CompProperties> comps) where T : ThingComp
        {
            Type targetType = typeof(T);

            for (int i = 0; i < comps.Count; i++)
            {
                Type compClass = comps[i].compClass;
                if (compClass == targetType || targetType.IsAssignableFrom(compClass))
                    return true;
            }

            return false;
        }

        // ─── HediffUtility.TryGetComp<T>(Hediff) ────────────────────────────────────

        public static T? HediffComp<T>(Hediff hediff) where T : HediffComp
        {
            if (hediff is not HediffWithComps hediffWithComps)
                return null;

            List<HediffComp>? comps = hediffWithComps.comps;
            if (comps == null)
                return null;

            int key = hediffWithComps.loadID;
            if (key < 0)
                return FindHediffComp<T>(comps);

            ref CompEntry<T> entry = ref IntCaches<CompEntry<T>>.Cache.GetOrAdd(key);
            if (entry.IsValidFor(comps, comps._version))
            {
                CompCacheCounters.AddHit();
                return entry.Comp;
            }

            CompCacheCounters.AddMiss();
            T? comp = FindHediffComp<T>(comps);
            entry.Store(comps, comps._version, comp);
            return comp;
        }

        private static T? FindHediffComp<T>(List<HediffComp> comps) where T : HediffComp
        {
            for (int i = 0; i < comps.Count; i++)
            {
                if (comps[i] is T comp)
                    return comp;
            }

            return null;
        }

        // ─── HediffDef.CompProps<T> ─────────────────────────────────────────────────

        public static T? HediffCompProperties<T>(HediffDef hediffDef) where T : HediffCompProperties
        {
            List<HediffCompProperties>? comps = hediffDef.comps;
            if (comps == null)
                return null;

            int key = hediffDef.shortHash;
            if (key < 0)
                return FindHediffCompProperties<T>(comps);

            ref CompEntry<T> entry = ref IntCaches<CompEntry<T>>.Cache.GetOrAdd(key);
            if (entry.IsValidFor(comps, comps._version))
            {
                CompCacheCounters.AddHit();
                return entry.Comp;
            }

            CompCacheCounters.AddMiss();
            T? comp = FindHediffCompProperties<T>(comps);
            entry.Store(comps, comps._version, comp);
            return comp;
        }

        private static T? FindHediffCompProperties<T>(List<HediffCompProperties> comps)
            where T : HediffCompProperties
        {
            for (int i = 0; i < comps.Count; i++)
            {
                if (comps[i] is T comp)
                    return comp;
            }

            return null;
        }

        // ─── Ability.CompOfType<T> ──────────────────────────────────────────────────

        public static T? AbilityComp<T>(Ability ability) where T : AbilityComp
        {
            List<AbilityComp>? comps = ability.comps;
            if (comps == null)
                return null;

            int key = ability.Id;
            if (key < 0)
                return FindAbilityComp<T>(comps);

            ref CompEntry<T> entry = ref IntCaches<CompEntry<T>>.Cache.GetOrAdd(key);
            if (entry.IsValidFor(comps, comps._version))
            {
                CompCacheCounters.AddHit();
                return entry.Comp;
            }

            CompCacheCounters.AddMiss();
            T? comp = FindAbilityComp<T>(comps);
            entry.Store(comps, comps._version, comp);
            return comp;
        }

        private static T? FindAbilityComp<T>(List<AbilityComp> comps) where T : AbilityComp
        {
            for (int i = 0; i < comps.Count; i++)
            {
                if (comps[i] is T comp)
                    return comp;
            }

            return null;
        }

        // ─── WorldObject.GetComponent<T> ────────────────────────────────────────────

        public static T? WorldObjectComp<T>(WorldObject worldObject) where T : WorldObjectComp
        {
            List<WorldObjectComp> comps = worldObject.comps;
            if (comps == null)
                return null;

            int key = worldObject.ID;
            if (key < 0)
                return FindWorldObjectComp<T>(comps);

            ref CompEntry<T> entry = ref IntCaches<CompEntry<T>>.Cache.GetOrAdd(key);
            if (entry.IsValidFor(comps, comps._version))
            {
                CompCacheCounters.AddHit();
                return entry.Comp;
            }

            CompCacheCounters.AddMiss();
            T? comp = FindWorldObjectComp<T>(comps);
            entry.Store(comps, comps._version, comp);
            return comp;
        }

        private static T? FindWorldObjectComp<T>(List<WorldObjectComp> comps) where T : WorldObjectComp
        {
            for (int i = 0; i < comps.Count; i++)
            {
                if (comps[i] is T comp)
                    return comp;
            }

            return null;
        }

        // ─── Map.GetComponent<T> ────────────────────────────────────────────────────

        public static T? MapComponent<T>(Map map) where T : MapComponent
        {
            List<MapComponent> components = map.components;

            int key = map.uniqueID;
            if (key < 0)
                return FindMapComponent<T>(components);

            ref CompEntry<T> entry = ref IntCaches<CompEntry<T>>.Cache.GetOrAdd(key);
            if (entry.IsValidFor(components, components._version))
            {
                CompCacheCounters.AddHit();
                return entry.Comp;
            }

            CompCacheCounters.AddMiss();
            T? comp = FindMapComponent<T>(components);
            entry.Store(components, components._version, comp);
            return comp;
        }

        private static T? FindMapComponent<T>(List<MapComponent> components) where T : MapComponent
        {
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] is T comp)
                    return comp;
            }

            return null;
        }

        // ─── World.GetComponent<T> ──────────────────────────────────────────────────

        public static T? WorldComponent<T>(World world) where T : WorldComponent
        {
            List<WorldComponent> components = world.components;

            if (SingleCompCache<T, WorldComponent, World>.TryGet(world, components, out T? cached))
            {
                CompCacheCounters.AddHit();
                return cached;
            }

            CompCacheCounters.AddMiss();
            T? comp = FindWorldComponent<T>(components);
            SingleCompCache<T, WorldComponent, World>.Update(world, components, comp);
            return comp;
        }

        private static T? FindWorldComponent<T>(List<WorldComponent> components) where T : WorldComponent
        {
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] is T comp)
                    return comp;
            }

            return null;
        }

        // ─── Game.GetComponent<T> ───────────────────────────────────────────────────

        public static T? GameComponent<T>(Game game) where T : GameComponent
        {
            List<GameComponent> components = game.components;

            if (SingleCompCache<T, GameComponent, Game>.TryGet(game, components, out T? cached))
            {
                CompCacheCounters.AddHit();
                return cached;
            }

            CompCacheCounters.AddMiss();
            T? comp = FindGameComponent<T>(components);
            SingleCompCache<T, GameComponent, Game>.Update(game, components, comp);
            return comp;
        }

        private static T? FindGameComponent<T>(List<GameComponent> components) where T : GameComponent
        {
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] is T comp)
                    return comp;
            }

            return null;
        }

        // ─── Utilidades ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Mezcla dos claves no negativas en una sola sin asignar memoria. Puede colisionar (32
        /// bits para dos datos), asi que quien la use tiene que poder verificar la coincidencia
        /// guardando los datos originales: ver <see cref="HasCompByTypeEntry.CompType"/>. Nunca
        /// debe usarse donde una colision pudiera dar una respuesta equivocada en lugar de un
        /// simple recalculo.
        /// </summary>
        private static int Combine(int first, int second)
        {
            unchecked
            {
                uint mixed = (uint)first * 0x9E3779B1u;
                mixed ^= (uint)second * 0x85EBCA77u;
                return (int)(mixed & 0x7FFFFFFF);
            }
        }
    }
}
