// This file is a Modification (MPL-2.0, section 1.10) of the original
// Performance Fish by bradson (https://github.com/bbradson/Performance-Fish),
// which is covered by the Mozilla Public License 2.0.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
    /// Cuerpos que sustituyen a los metodos de <see cref="ListerThings"/> en 1.6.
    ///
    /// La semantica esta copiada del IL real del juego (ver `Tools/VANILLA_IL_LISTER.md`), no del
    /// mod original: Add/Remove/Clear tienen detalles que no se pueden cambiar sin romper el juego:
    ///
    /// <list type="bullet">
    /// <item><c>stateHashByGroup</c> se incrementa en Remove <b>aunque la lista no contuviera la
    /// cosa</b> (el vanilla descarta el resultado de <c>List.Remove</c>). Ese contador alimenta la
    /// logica de regiones (<c>StateHashOfGroup</c>), asi que hay que replicarlo tal cual.</item>
    /// <item><c>haulSources</c> se mantiene en Add y Remove.</item>
    /// <item><c>Clear</c> NO limpia <c>haulSources</c> y NO dispara callbacks; y conserva los
    /// objetos <c>List</c> de los grupos mientras descarta los de <c>listsByDef</c>. Por eso las
    /// caches propias se vacian aqui: sobreviven al Clear.</item>
    /// <item>Add filtra con <c>GroupIncludes</c>; Remove inlinea esa misma comprobacion (no llama al
    /// metodo privado). Se replica la asimetria para no depender de un metodo privado.</item>
    /// </list>
    ///
    /// Divergencias conscientes, aprobadas o derivadas del diseno:
    ///
    /// <list type="number">
    /// <item><b>El orden de las listas cambia</b> al quitar: se intercambia con el ultimo elemento
    /// (O(1)) en lugar de desplazar, como hace el mod original. Aprobado por DGZ sabiendo que puede
    /// alterar desempates en escaneos.</item>
    /// <item>Remove ya no lanza <c>NullReferenceException</c> cuando la lista del grupo no existe
    /// (el vanilla hace <c>listsByGroup[g].Remove(t)</c> sin comprobar null y las listas se crean de
    /// forma perezosa). Se salta ese grupo y se sigue; el fallo del vanilla es un error real.</item>
    /// <item><c>GetThingsOfType&lt;T&gt;()</c> devuelve la lista cacheada por tipo en lugar de
    /// filtrar todas las cosas del mapa; tiene 0 llamantes en el vanilla, asi que el comportamiento
    /// observable solo cambia para mods que lo usen.</item>
    /// </list>
    /// </summary>
    public static class ListerThingsCaches
    {
        // ─── Acceso a las caches inyectadas ─────────────────────────────────────────
        // El campo inyectado empieza a null: se declara como "ref" (el mecanismo de campos
        // inyectados que YA esta verificado en juego) y la cache se crea aqui en el primer uso. Se
        // hace asi en lugar de con [ValueInitializer] para no depender de un mecanismo que este
        // proyecto no ha probado todavia.

        internal static IntCache<int> IndexMapByDef(ListerThings lister)
        {
            ref IntCache<int> slot = ref lister.ReforjedIndexMapByDef();

            if (slot == null)
                slot = new IntCache<int>(64, registerForGameLoadCleanup: false);

            return slot;
        }

        internal static GroupIndexCaches IndexMapByGroup(ListerThings lister)
        {
            ref GroupIndexCaches slot = ref lister.ReforjedIndexMapByGroup();

            if (slot == null)
                slot = new GroupIndexCaches();

            return slot;
        }

        internal static Dictionary<Type, IList> ThingsByType(ListerThings lister)
        {
            ref Dictionary<Type, IList> slot = ref lister.ReforjedThingsByType();

            if (slot == null)
                slot = new Dictionary<Type, IList>();

            return slot;
        }

        // ─── Add ────────────────────────────────────────────────────────────────────

        public static void Add(ListerThings lister, Thing t)
        {
            if (!ListerThings.EverListable(t.def, lister.use))
                return;

            AddToDefList(lister, t);

            if (t is IHaulSource haulSource)
                lister.haulSources.Add(haulSource);

            ThingRequestGroup[] allGroups = ThingListGroupHelper.AllGroups;
            for (int i = 0; i < allGroups.Length; i++)
            {
                ThingRequestGroup group = allGroups[i];

                if (!GroupIncludes(lister, t, group))
                    continue;

                AddToGroupList(lister, t, group);
            }

            AddToTypeList(lister, t);

            lister.thingListChangedCallbacks?.onThingAdded?.Invoke(t);
        }

        // ─── Remove ─────────────────────────────────────────────────────────────────

        public static void Remove(ListerThings lister, Thing t)
        {
            if (!ListerThings.EverListable(t.def, lister.use))
                return;

            RemoveFromDefList(lister, t);

            if (t is IHaulSource haulSource)
                lister.haulSources.Remove(haulSource);

            ThingRequestGroup[] allGroups = ThingListGroupHelper.AllGroups;
            for (int i = 0; i < allGroups.Length; i++)
            {
                ThingRequestGroup group = allGroups[i];

                // Asimetria del vanilla: aqui no se llama a GroupIncludes, se inlinea.
                if (lister.use == ListerThingsUse.Region && !group.StoreInRegion())
                    continue;

                if (!group.Includes(t.def))
                    continue;

                RemoveFromGroupList(lister, t, group);
            }

            RemoveFromTypeList(lister, t);

            lister.thingListChangedCallbacks?.onThingRemoved?.Invoke(t);
        }

        // ─── Contains ───────────────────────────────────────────────────────────────

        public static bool Contains(ListerThings lister, Thing t)
        {
            // El vanilla es "AllThings.Contains(t)", o sea un recorrido lineal de TODAS las cosas del
            // mapa. El indice por def responde sin recorrer nada.
            int key = t.thingIDNumber;
            if (key >= 0 && IndexMapByDef(lister).ContainsKey(key))
                return true;

            // Respaldo identico al vanilla: cubre claves invalidas (-1) y el caso de duplicados, en
            // el que una cosa puede seguir en la lista despues de haberse quitado su clave.
            return lister.AllThings.Contains(t);
        }

        // ─── Clear ──────────────────────────────────────────────────────────────────

        public static void Clear(ListerThings lister)
        {
            // Igual que el vanilla: descarta los objetos List de listsByDef...
            lister.listsByDef.Clear();

            // ...pero CONSERVA los de listsByGroup, que solo se vacian. Ni haulSources ni callbacks.
            List<Thing>?[] listsByGroup = lister.listsByGroup;
            for (int i = 0; i < listsByGroup.Length; i++)
            {
                listsByGroup[i]?.Clear();
                lister.stateHashByGroup[i] = 0;
            }

            // Las caches inyectadas SI sobreviven al Clear (el vanilla no las conoce), asi que hay
            // que vaciarlas aqui o quedarian apuntando a listas que ya no contienen nada.
            IndexMapByDef(lister).Clear();
            IndexMapByGroup(lister).ClearAll();

            foreach (IList list in ThingsByType(lister).Values)
                list.Clear();
        }

        // ─── GetThingsOfType ────────────────────────────────────────────────────────

        public static IEnumerable<T> GetThingsOfType<T>(ListerThings lister) where T : Thing
        {
            if (typeof(T) == typeof(Thing))
            {
                // Mismo error que el vanilla (texto literal del IL): devolveria todas las cosas y
                // quien lo llame casi siempre esta equivocandose.
                Log.Error("Do not call this method with type 'Thing' directly, as it will return all "
                          + "things currently registered.");
                return EmptyThings<T>();
            }

            return (IEnumerable<T>)GetTypeList(ThingsByType(lister), typeof(T));
        }

        public static void GetThingsOfType<T>(ListerThings lister, List<T> list) where T : Thing
        {
            if (typeof(T) == typeof(Thing))
            {
                Log.Error("Do not call this method with type 'Thing' directly, as it will return all "
                          + "things currently registered.");
                return;
            }

            list.AddRange((List<T>)GetTypeList(ThingsByType(lister), typeof(T)));
        }

        // ─── Listas por def ─────────────────────────────────────────────────────────

        private static void AddToDefList(ListerThings lister, Thing t)
        {
            if (!lister.listsByDef.TryGetValue(t.def, out List<Thing>? list))
                lister.listsByDef.Add(t.def, list = new List<Thing>());

            list.Add(t);

            int key = t.thingIDNumber;
            if (key < 0)
                return;

            IndexMapByDef(lister).GetOrAdd(key) = list.Count - 1;
        }

        private static void RemoveFromDefList(ListerThings lister, Thing t)
        {
            IntCache<int> map = IndexMapByDef(lister);
            int key = t.thingIDNumber;

            if (!lister.listsByDef.TryGetValue(t.def, out List<Thing>? list))
            {
                // El vanilla se limita a no hacer nada si el def no esta; el original de PF registra
                // un error aqui. Se mantiene el silencio del vanilla.
                if (key >= 0)
                    map.Remove(key);

                return;
            }

            int index = key >= 0 ? FindIndex(map, list, key, t) : list.IndexOf(t);

            if (index >= 0)
                RemoveAtFastUnordered(list, index, map);
            else if (key >= 0)
                map.Remove(key);
        }

        // ─── Listas por grupo ───────────────────────────────────────────────────────

        private static void AddToGroupList(ListerThings lister, Thing t, ThingRequestGroup group)
        {
            int groupIndex = (int)group;
            List<Thing>? list = lister.listsByGroup[groupIndex];

            if (list == null)
            {
                list = new List<Thing>();
                lister.listsByGroup[groupIndex] = list;
                lister.stateHashByGroup[groupIndex] = 0;
            }

            list.Add(t);
            lister.stateHashByGroup[groupIndex]++;

            int key = t.thingIDNumber;
            if (key < 0)
                return;

            IndexMapByGroup(lister).For(group).GetOrAdd(key) = list.Count - 1;
        }

        private static void RemoveFromGroupList(ListerThings lister, Thing t, ThingRequestGroup group)
        {
            int groupIndex = (int)group;
            List<Thing>? list = lister.listsByGroup[groupIndex];

            if (list != null)
            {
                int key = t.thingIDNumber;

                if (key >= 0)
                {
                    IntCache<int> map = IndexMapByGroup(lister).For(group);
                    int index = FindIndex(map, list, key, t);

                    if (index >= 0)
                        RemoveAtFastUnordered(list, index, map);
                    else
                        map.Remove(key);
                }
                else
                {
                    list.Remove(t);
                }
            }

            // El vanilla incrementa SIEMPRE: tambien cuando el elemento no estaba en la lista. Y
            // alimenta la logica de regiones, asi que mover esto de sitio la rompe.
            lister.stateHashByGroup[groupIndex]++;
        }

        // ─── Listas por tipo ────────────────────────────────────────────────────────

        private static void AddToTypeList(ListerThings lister, Thing t)
        {
            Type? type = t.GetType();
            if (type == typeof(Thing))
                return;

            Dictionary<Type, IList> byType = ThingsByType(lister);

            while (type != null && type != typeof(Thing))
            {
                GetTypeList(byType, type).Add(t);
                type = type.BaseType;
            }
        }

        private static void RemoveFromTypeList(ListerThings lister, Thing t)
        {
            Type? type = t.GetType();
            if (type == typeof(Thing))
                return;

            Dictionary<Type, IList> byType = ThingsByType(lister);

            while (type != null && type != typeof(Thing))
            {
                // Aqui no hay mapa de indices: una misma cosa esta en la lista de CADA tipo de su
                // jerarquia, asi que se quita buscando (mismo coste que el original). Las listas por
                // tipo son pequenas comparadas con las de grupo.
                if (byType.TryGetValue(type, out IList? list))
                    list.Remove(t);

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
            typeof(ListerThingsCaches).GetMethod(nameof(NewList), BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("No se encontro el creador de listas por tipo.");

        private static IList NewListFor(Type type)
            => (IList)NewListDefinition.MakeGenericMethod(type).Invoke(null, null)!;

        private static IEnumerable<T> EmptyThings<T>() where T : Thing
        {
            yield break;
        }

        // ─── Utilidades ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Quita el elemento de la posicion indicada intercambiandolo con el ultimo (O(1), el orden
        /// cambia a proposito) y deja coherente el mapa de indices: elimina la clave de la cosa
        /// quitada y reapunta la de la cosa que ocupo su hueco.
        /// </summary>
        private static void RemoveAtFastUnordered(List<Thing> list, int index, IntCache<int> map)
        {
            int last = list.Count - 1;
            Thing removed = list[index];
            Thing moved = list[last];

            list[index] = moved;
            list.RemoveAt(last);

            int removedKey = removed.thingIDNumber;
            if (removedKey >= 0)
                map.Remove(removedKey);

            if (index < list.Count)
            {
                int movedKey = moved.thingIDNumber;
                if (movedKey >= 0)
                    map.GetOrAdd(movedKey) = index;
            }
        }

        /// <summary>
        /// Posicion de la cosa en la lista: primero por el mapa de indices (O(1)) y, si el mapa
        /// apunta a otra cosa, por busqueda directa. Comprobar que el indice sigue siendo valido es
        /// lo que hace que un mapa desincronizado cueste un recorrido y no una respuesta equivocada.
        /// </summary>
        private static int FindIndex(IntCache<int> map, List<Thing> list, int key, Thing t)
        {
            if (map.TryGet(key, out int index) && (uint)index < (uint)list.Count
                && ReferenceEquals(list[index], t))
            {
                return index;
            }

            return list.IndexOf(t);
        }

        /// <summary>Equivalente al GroupIncludes privado del vanilla (14 instrucciones).</summary>
        private static bool GroupIncludes(ListerThings lister, Thing t, ThingRequestGroup group)
        {
            if (lister.use == ListerThingsUse.Region && !group.StoreInRegion())
                return false;

            return group.Includes(t.def);
        }
    }
}
