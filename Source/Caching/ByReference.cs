// This file is a Modification (MPL-2.0, section 1.10) of the original
// Performance Fish by bradson (https://github.com/bbradson/Performance-Fish),
// which is covered by the Mozilla Public License 2.0.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
