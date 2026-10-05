using System;
using System.Runtime.CompilerServices;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Wrapper estático para <see cref="RefCache{TKey, TValue}"/> que proporciona
    /// una cache global por combinación de tipos genéricos.
    ///
    /// Es el equivalente de <c>Cache.ByReference</c> del original de Performance Fish,
    /// pero usando RefCache en lugar de FishTable.
    ///
    /// La cache se registra en CacheRegistry y se vacía al cargar partida.
    /// </summary>
    public static class ByReference<T, TResult>
        where T : notnull, IEquatable<T>
        where TResult : struct
    {
        private static readonly RefCache<T, TResult> _cache = new();

        /// <summary>
        /// Devuelve una referencia a la entrada de la clave, creándola vacía si no existía.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref TResult GetOrAddReference(T key) =>
            ref _cache.GetOrAdd(key);

        /// <summary>
        /// Devuelve una referencia a la entrada existente. Lanza si no existe.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref TResult GetExistingReference(T key) =>
            ref _cache.GetReference(key);

        /// <summary>Quita una clave de la cache.</summary>
        public static void Remove(T key) => _cache.Remove(key);

        /// <summary>Vacía toda la cache.</summary>
        public static void Clear() => _cache.Clear();
    }

    /// <summary>
    /// Wrapper estático para <see cref="RefCache{TKey, TValue}"/> que NO se vacía automáticamente.
    ///
    /// Es el equivalente de <c>Cache.ByReferenceUnclearable</c> del original.
    /// Se usa para caches por thread o de vida larga que no deben vaciarse al cargar partida.
    /// </summary>
    public static class ByReferenceUnclearable<T, TResult>
        where T : notnull, IEquatable<T>
        where TResult : struct
    {
        // NO registra en CacheRegistry (registerForGameLoadCleanup = false)
        private static readonly RefCache<T, TResult> _cache = new(registerForGameLoadCleanup: false);

        /// <summary>
        /// Devuelve una referencia a la entrada de la clave, creándola vacía si no existía.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref TResult GetOrAddReference(T key) =>
            ref _cache.GetOrAdd(key);

        /// <summary>
        /// Devuelve una referencia a la entrada existente. Lanza si no existe.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref TResult GetExistingReference(T key) =>
            ref _cache.GetReference(key);
    }
}
