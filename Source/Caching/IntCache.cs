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

namespace PerformanceFishReforjed.Caching
{
    /// <summary>Una cache que se puede vaciar. Lo usa el registro de limpieza global.</summary>
    internal interface IClearable
    {
        void Clear();
    }

    /// <summary>
    /// Registro de todas las caches creadas, para poder vaciarlas de una vez.
    ///
    /// Hace falta porque las caches viven en campos estaticos de tipos genericos: no se pueden
    /// enumerar (habria que conocer todas las instanciaciones cerradas), asi que cada cache se
    /// apunta a si misma al construirse.
    ///
    /// Cuando se vacia: al cargar una partida o empezar una nueva. Las claves de estas caches son
    /// identificadores unicos DENTRO de una partida (thingIDNumber, loadID, shortHash, ID...),
    /// pero se reinician en cada partida, asi que una entrada vieja podria devolver el comp de un
    /// objeto de otra partida. Vaciarlas al cargar es lo que hace que el indice sea correcto.
    /// </summary>
    internal static class CacheRegistry
    {
        private static readonly List<IClearable> Caches = new List<IClearable>();

        internal static int Count => Caches.Count;

        internal static void Track(IClearable cache)
        {
            lock (Caches)
                Caches.Add(cache);
        }

        internal static void ClearAll()
        {
            lock (Caches)
            {
                for (int i = 0; i < Caches.Count; i++)
                    Caches[i].Clear();
            }
        }
    }

    /// <summary>
    /// Mapa abierto (open addressing) de <c>int</c> a un struct, con acceso por <c>ref</c>.
    ///
    /// Es la pieza que sustituye al <c>FishTable</c>/<c>Cache.ByInt</c> del original. Se evita
    /// <see cref="Dictionary{TKey,TValue}"/> justamente porque no permite obtener una referencia
    /// al valor almacenado: con <c>ref</c> se lee y se escribe la entrada cacheada sin copiarla
    /// ni volver a buscar la clave.
    ///
    /// Sobre el centinela: las claves legitimas son siempre identificadores no negativos, asi que
    /// <see cref="Empty"/> (<c>int.MinValue</c>) marca hueco libre. Los sitios de llamada de este
    /// proyecto rechazan claves negativas ANTES de llegar aqui (ver GetCompCaches), que es la
    /// misma precaucion que toma Performance Fish. La validez de la ENTRADA no se marca con un
    /// centinela numerico sino con un campo <c>bool</c>, porque un hueco recien creado vale 0 y 0
    /// es un valor legitimo de <c>_version</c>: es la trampa #3 de PENDIENTE.md.
    /// </summary>
    internal sealed class IntCache<TValue> : IClearable where TValue : struct
    {
        private const int Empty = int.MinValue;

        private int[] _keys;
        private TValue[] _values;
        private int _mask;
        private int _count;

        /// <param name="capacity">Capacidad inicial; se redondea a potencia de dos.</param>
        /// <param name="registerForGameLoadCleanup">
        /// Si se apunta al registro de limpieza por carga de partida. Las caches GLOBALES indexadas
        /// por identificadores de partida (thingIDNumber, shortHash...) sí lo necesitan. Las caches
        /// POR INSTANCIA (por ejemplo los mapas de indice de cada ListerThings) NO: su vida va
        /// atada a la del objeto que las contiene, que se recrea en cada partida, y vaciarlas sin
        /// vaciar las listas las dejaria incoherentes.
        /// </param>
        internal IntCache(int capacity = 64, bool registerForGameLoadCleanup = true)
        {
            int size = 16;
            while (size < capacity)
                size <<= 1;

            _keys = new int[size];
            _values = new TValue[size];
            _mask = size - 1;

            for (int i = 0; i < size; i++)
                _keys[i] = Empty;

            if (registerForGameLoadCleanup)
                CacheRegistry.Track(this);
        }

        /// <summary>Entradas ocupadas.</summary>
        internal int Count => _count;

        /// <summary>Huecos disponibles (potencia de dos).</summary>
        internal int Capacity => _keys.Length;

        /// <summary>
        /// Devuelve una referencia a la entrada de la clave, creandola vacia si no existia.
        ///
        /// La referencia es valida hasta la siguiente insercion que provoque crecimiento, asi que
        /// el patron de uso es "pedir la referencia, leer o escribir, y no volver a insertar
        /// mientras se use". Es lo mismo que exige el original.
        /// </summary>
        internal ref TValue GetOrAdd(int key)
        {
            // El crecimiento se comprueba ANTES de buscar hueco: crecer reasigna los arrays y una
            // referencia devuelta antes del crecimiento dejaria de apuntar a la cache.
            if ((_count + 1) * 4 >= _keys.Length * 3)
                Grow();

            int index = Mix(key) & _mask;

            while (true)
            {
                int slotKey = _keys[index];

                if (slotKey == key)
                    return ref _values[index];

                if (slotKey == Empty)
                    break;

                index = (index + 1) & _mask;
            }

            _keys[index] = key;
            _values[index] = default; // entrada limpia: los structs de entrada marcan "no valida"
            _count++;

            return ref _values[index];
        }

        /// <summary>Consulta sin crear entrada. No se usa en los caminos calientes de lectura.</summary>
        internal bool TryGet(int key, out TValue value)
        {
            int index = Mix(key) & _mask;

            while (true)
            {
                int slotKey = _keys[index];

                if (slotKey == key)
                {
                    value = _values[index];
                    return true;
                }

                if (slotKey == Empty)
                {
                    value = default;
                    return false;
                }

                index = (index + 1) & _mask;
            }
        }

        /// <summary>Quita una clave. Devuelve true si existia. Lo necesitan los mapas de indice.</summary>
        public bool Remove(int key)
        {
            int index = Mix(key) & _mask;

            while (true)
            {
                int slotKey = _keys[index];

                if (slotKey == Empty)
                    return false;

                if (slotKey == key)
                    break;

                index = (index + 1) & _mask;
            }

            // Borrado con desplazamiento hacia atras. NO se marca el hueco como borrado: un hueco
            // intermedio romperia la cadena de sondeo y dejaria inalcanzables las claves que
            // colisionaron detras. Se mueven hacia atras los elementos del cluster que puedan
            // retroceder sin salirse de su propio camino de sondeo.
            int hole = index;

            while (true)
            {
                int next = hole;
                int candidate;

                while (true)
                {
                    next = (next + 1) & _mask;
                    candidate = _keys[next];

                    if (candidate == Empty)
                    {
                        _keys[hole] = Empty;
                        _values[hole] = default;
                        _count--;
                        return true;
                    }

                    // Se sigue buscando mientras el elemento NO pueda retroceder; se desplaza el
                    // PRIMERO que si pueda. (La primera version de esto tenia la condicion
                    // invertida y dejaba claves detras de un hueco, inalcanzables: lo destapo la
                    // prueba de
                    // <see cref="Diagnostics.IntCacheSelfTest"/>.
                    if (HoleIsOnProbePath(hole, Mix(candidate) & _mask, next))
                        break;
                }

                _keys[hole] = candidate;
                _values[hole] = _values[next];
                hole = next;
            }
        }

        /// <summary>Consulta si la clave esta sin crear entrada ni copiar el valor.</summary>
        public bool ContainsKey(int key)
        {
            int index = Mix(key) & _mask;

            while (true)
            {
                int slotKey = _keys[index];

                if (slotKey == key)
                    return true;

                if (slotKey == Empty)
                    return false;

                index = (index + 1) & _mask;
            }
        }

        /// <summary>
        /// ¿Esta <paramref name="hole"/> dentro del camino de sondeo que va de
        /// <paramref name="home"/> a <paramref name="current"/>, es decir en el intervalo ciclico
        /// [home, current)? Solo entonces el elemento de <paramref name="current"/> puede
        /// retroceder hasta el hueco sin salirse de su propio camino de sondeo (si lo hiciera,
        /// quedaria inalcanzable para las busquedas).
        ///
        /// Se calcula con distancias ciclicas en lugar de comparaciones con ramas: la version con
        /// ramas es facil de escribir mal en el caso de vuelta al principio, y de hecho la primera
        /// version de este metodo estaba mal ahi. Lo destapo la prueba de
        /// <see cref="Diagnostics.IntCacheSelfTest"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HoleIsOnProbePath(int hole, int home, int current)
        {
            int mask = _mask;
            int distanceFromHome = (hole - home) & mask;
            int distanceToCurrent = (current - home) & mask;
            return distanceFromHome < distanceToCurrent;
        }

        public void Clear()
        {
            if (_count == 0)
                return;

            for (int i = 0; i < _keys.Length; i++)
                _keys[i] = Empty;

            Array.Clear(_values, 0, _values.Length);
            _count = 0;
        }

        private void Grow()
        {
            int newSize = _keys.Length << 1;
            var oldKeys = _keys;
            var oldValues = _values;

            _keys = new int[newSize];
            _values = new TValue[newSize];
            _mask = newSize - 1;

            for (int i = 0; i < newSize; i++)
                _keys[i] = Empty;

            _count = 0;

            for (int i = 0; i < oldKeys.Length; i++)
            {
                int key = oldKeys[i];
                if (key == Empty)
                    continue;

                int index = Mix(key) & _mask;
                while (_keys[index] != Empty)
                    index = (index + 1) & _mask;

                _keys[index] = key;
                _values[index] = oldValues[i];
                _count++;
            }
        }

        /// <summary>Mezcla de bits estilo murmur para que claves consecutivas no colisionen.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Mix(int key)
        {
            uint x = (uint)key;
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return (int)x;
        }
    }

    /// <summary>
    /// Titular estatico de una <see cref="IntCache{TValue}"/> por cada tipo de valor.
    ///
    /// El campo estatico de un tipo generico esta separado por cada instanciacion cerrada, asi que
    /// <c>IntCaches&lt;Entrada&lt;ThingComp&gt;&gt;</c> y <c>IntCaches&lt;Entrada&lt;MapComponent&gt;&gt;</c>
    /// son caches distintas sin ningun diccionario de diccionarios por medio. Es exactamente el
    /// truco que usa el original (<c>Cache.ByInt&lt;TOwner, TValue&gt;</c>) pero sin la dimension
    /// TOwner, que aqui no hace falta porque el tipo de entrada ya distingue el uso.
    /// </summary>
    internal static class IntCaches<TValue> where TValue : struct
    {
        internal static readonly IntCache<TValue> Cache = new IntCache<TValue>();
    }
}