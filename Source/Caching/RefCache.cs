using System;
using System.Collections;
using System.Runtime.CompilerServices;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Mapa abierto (open addressing) de <c>TKey</c> a un struct, con acceso por <c>ref</c>.
    ///
    /// Es la pieza que sustituye al <c>FishTable</c> del original de Performance Fish,
    /// adaptada para usar los patrones de Reforjed. Se evita <see cref="Dictionary{TKey,TValue}"/>
    /// porque no permite obtener una referencia al valor almacenado.
    ///
    /// Requiere que <c>TKey</c> implemente <c>IEquatable&lt;TKey&gt;</c> para Equals/GetHashCode
    /// eficientes. El original usa function pointers de FisheryLib, pero eso añade complejidad
    /// que no es necesaria para RimWorld 1.6.
    /// </summary>
    public sealed class RefCache<TKey, TValue> : IClearable
        where TKey : notnull, IEquatable<TKey>
        where TValue : struct
    {
        private struct Entry
        {
            public TKey Key;
            public TValue Value;
            public bool HasValue; // centinela para marcar huecos
        }

        private Entry[] _entries;
        private int _mask;
        private int _count;

        /// <param name="capacity">Capacidad inicial; se redondea a potencia de dos.</param>
        /// <param name="registerForGameLoadCleanup">
        /// Si se apunta al registro de limpieza por carga de partida.
        /// </param>
        internal RefCache(int capacity = 64, bool registerForGameLoadCleanup = true)
        {
            int size = 16;
            while (size < capacity)
                size <<= 1;

            _entries = new Entry[size];
            _mask = size - 1;

            if (registerForGameLoadCleanup)
                CacheRegistry.Track(this);
        }

        /// <summary>Entradas ocupadas.</summary>
        internal int Count => _count;

        /// <summary>Huecos disponibles (potencia de dos).</summary>
        internal int Capacity => _entries.Length;

        /// <summary>
        /// Devuelve una referencia a la entrada de la clave, creándola vacía si no existía.
        ///
        /// La referencia es válida hasta la siguiente inserción que provoque crecimiento.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref TValue GetOrAdd(TKey key)
        {
            // El crecimiento se comprueba ANTES de buscar hueco
            if ((_count + 1) * 4 >= _entries.Length * 3)
                Grow();

            int index = Mix(key.GetHashCode()) & _mask;

            while (true)
            {
                ref Entry entry = ref _entries[index];

                if (entry.HasValue && entry.Key.Equals(key))
                    return ref entry.Value;

                if (!entry.HasValue)
                    break;

                index = (index + 1) & _mask;
            }

            _entries[index].Key = key;
            _entries[index].Value = default;
            _entries[index].HasValue = true;
            _count++;

            return ref _entries[index].Value;
        }

        /// <summary>Consulta sin crear entrada. Devuelve false si no existe.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGet(TKey key, out TValue value)
        {
            int index = Mix(key.GetHashCode()) & _mask;

            while (true)
            {
                ref Entry entry = ref _entries[index];

                if (entry.HasValue && entry.Key.Equals(key))
                {
                    value = entry.Value;
                    return true;
                }

                if (!entry.HasValue)
                {
                    value = default;
                    return false;
                }

                index = (index + 1) & _mask;
            }
        }

        /// <summary>Consulta sin crear entrada ni copiar el valor.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool ContainsKey(TKey key)
        {
            int index = Mix(key.GetHashCode()) & _mask;

            while (true)
            {
                ref Entry entry = ref _entries[index];

                if (entry.HasValue && entry.Key.Equals(key))
                    return true;

                if (!entry.HasValue)
                    return false;

                index = (index + 1) & _mask;
            }
        }

        /// <summary>
        /// Devuelve una referencia a la entrada existente. Lanza si no existe.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref TValue GetReference(TKey key)
        {
            int index = Mix(key.GetHashCode()) & _mask;

            while (true)
            {
                ref Entry entry = ref _entries[index];

                if (entry.HasValue && entry.Key.Equals(key))
                    return ref entry.Value;

                if (!entry.HasValue)
                    throw new InvalidOperationException($"Key '{key}' not found in cache.");

                index = (index + 1) & _mask;
            }
        }

        /// <summary>Quita una clave. Devuelve true si existía.</summary>
        internal bool Remove(TKey key)
        {
            int index = Mix(key.GetHashCode()) & _mask;

            while (true)
            {
                ref Entry entry = ref _entries[index];

                if (!entry.HasValue)
                    return false;

                if (entry.Key.Equals(key))
                    break;

                index = (index + 1) & _mask;
            }

            // Borrado con desplazamiento hacia atrás (misma lógica que IntCache)
            int hole = index;

            while (true)
            {
                int next = hole;
                TKey candidate;

                while (true)
                {
                    next = (next + 1) & _mask;
                    ref Entry nextEntry = ref _entries[next];

                    if (!nextEntry.HasValue)
                    {
                        _entries[hole] = default;
                        _count--;
                        return true;
                    }

                    candidate = nextEntry.Key;

                    if (HoleIsOnProbePath(hole, Mix(candidate.GetHashCode()) & _mask, next))
                        break;
                }

                _entries[hole] = _entries[next];
                hole = next;
            }
        }

        /// <summary>
        /// ¿Está <paramref name="hole"/> dentro del camino de sondeo?
        /// Mismo cálculo que IntCache usando distancias cíclicas.
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

            Array.Clear(_entries, 0, _entries.Length);
            _count = 0;
        }

        private void Grow()
        {
            int newSize = _entries.Length << 1;
            var oldEntries = _entries;

            _entries = new Entry[newSize];
            _mask = newSize - 1;
            _count = 0;

            for (int i = 0; i < oldEntries.Length; i++)
            {
                ref Entry oldEntry = ref oldEntries[i];
                if (!oldEntry.HasValue)
                    continue;

                int index = Mix(oldEntry.Key.GetHashCode()) & _mask;
                while (_entries[index].HasValue)
                    index = (index + 1) & _mask;

                _entries[index] = oldEntry;
                _count++;
            }
        }

        /// <summary>Mezcla de bits estilo murmur para que hashes consecutivos no colisionen.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Mix(int hash)
        {
            uint x = (uint)hash;
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return (int)x;
        }
    }
}
