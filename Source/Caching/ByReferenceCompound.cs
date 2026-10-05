using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Struct de clave compuesta para usar con <see cref="RefCache{TKey, TValue}"/>.
    ///
    /// Es el equivalente de la clave compuesta usada en
    /// <c>Cache.ByReference&lt;T1, T2, TResult&gt;</c> del original.
    ///
    /// El layout Sequential y Pack=1 aseguran que la comparación por bytes funcione
    /// correctamente para el hash.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CompoundKey<T1, T2> : IEquatable<CompoundKey<T1, T2>>
        where T1 : notnull, IEquatable<T1>
        where T2 : notnull, IEquatable<T2>
    {
        public T1 First;
        public T2 Second;

        public CompoundKey(T1 first, T2 second)
        {
            First = first;
            Second = second;
        }

        public bool Equals(CompoundKey<T1, T2> other) =>
            First.Equals(other.First) && Second.Equals(other.Second);

        public override bool Equals(object obj) =>
            obj is CompoundKey<T1, T2> other && Equals(other);

        public override int GetHashCode()
        {
            // Combine los hashes de ambas partes
            // Usar combinación manual para evitar conflicto con 0Harmony.HashCode
            unchecked
            {
                int hash = First.GetHashCode();
                hash = (hash * 397) ^ Second.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(CompoundKey<T1, T2> left, CompoundKey<T1, T2> right) =>
            left.Equals(right);

        public static bool operator !=(CompoundKey<T1, T2> left, CompoundKey<T1, T2> right) =>
            !left.Equals(right);
    }

    /// <summary>
    /// Wrapper estático para caches con claves compuestas de dos elementos.
    /// </summary>
    public static class ByReference<T1, T2, TResult>
        where T1 : notnull, IEquatable<T1>
        where T2 : notnull, IEquatable<T2>
        where TResult : struct
    {
        private static readonly RefCache<CompoundKey<T1, T2>, TResult> _cache = new();

        /// <summary>
        /// Devuelve una referencia a la entrada de las claves, creándola vacía si no existía.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref TResult GetOrAddReference(T1 first, T2 second) =>
            ref _cache.GetOrAdd(new CompoundKey<T1, T2>(first, second));

        /// <summary>
        /// Devuelve una referencia a la entrada existente. Lanza si no existe.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static ref TResult GetExistingReference(T1 first, T2 second) =>
            ref _cache.GetReference(new CompoundKey<T1, T2>(first, second));

        /// <summary>Quita una clave de la cache.</summary>
        public static void Remove(T1 first, T2 second) =>
            _cache.Remove(new CompoundKey<T1, T2>(first, second));

        /// <summary>Vacía toda la cache.</summary>
        public static void Clear() => _cache.Clear();
    }

    /// <summary>
    /// Variante sin Clear para claves compuestas.
    /// </summary>
    public static class ByReferenceUnclearable<T1, T2, TResult>
        where T1 : notnull, IEquatable<T1>
        where T2 : notnull, IEquatable<T2>
        where TResult : struct
    {
        private static readonly RefCache<CompoundKey<T1, T2>, TResult> _cache =
            new(registerForGameLoadCleanup: false);

        /// <summary>
        /// Devuelve una referencia a la entrada de las claves, creándola vacía si no existía.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref TResult GetOrAddReference(T1 first, T2 second) =>
            ref _cache.GetOrAdd(new CompoundKey<T1, T2>(first, second));

        /// <summary>
        /// Devuelve una referencia a la entrada existente. Lanza si no existe.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static ref TResult GetExistingReference(T1 first, T2 second) =>
            ref _cache.GetReference(new CompoundKey<T1, T2>(first, second));
    }
}
