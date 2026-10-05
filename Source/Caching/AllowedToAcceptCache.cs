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
using PerformanceFishReforjed.Prepatch;
using RimWorld;
using Verse;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Valor cacheado para AllowedToAccept: bool + timestamp de expiración (ticks de juego).
    /// Se usa un struct para almacenarlo en un mapa abierto (IntCache).
    /// </summary>
    internal struct AllowedToAcceptEntry
    {
        /// <summary>Resultado cacheado: true = aceptado, false = rechazado.</summary>
        internal bool Value;

        /// <summary>Tick de juego en el que expira la entrada. 0 = no calculada / expirada.</summary>
        internal int ExpireTick;
    }

    /// <summary>
    /// Cache de <c>StorageSettings.AllowedToAccept(Thing)</c> y <c>AllowedToAccept(ThingDef)</c>.
    ///
    /// Indexado por clave entera:
    /// - Para <c>Thing</c>: <c>thingIDNumber</c> (>= 0 para cosas spawnadas). Si es -1, no se cachea.
    /// - Para <c>ThingDef</c>: <c>shortHash</c> (>= 0). Los defs no spawnados no tienen thingIDNumber.
    ///
    /// Cada entrada tiene un TTL de ~34 s (2048 ticks) con jitter basado en la clave, para limitar
    /// la "rancidez" de respuestas cacheadas cuando cambian condiciones no notificadas (HP, calidad,
    /// deterioro de la cosa en el suelo).
    ///
    /// Invalidación instantánea (además del TTL): en <c>TryNotifyChanged</c> (cambio de filtro/preset)
    /// y en <c>set_Priority</c> (prioridad de almacenamiento).
    /// </summary>
    internal sealed class AllowedToAcceptCache : IClearable
    {
        // TTL base: 2048 ticks = ~34.1 s a 60 ticks/s.
        private const int BaseTtlTicks = 2048;

        // Jitter: se suma un offset derivado de la clave para desfasar expiraciones.
        // Factor del original: key % 512.
        private const int JitterModulo = 512;

        // Mapas abiertos SEPARADOS por espacio de claves: uno para cosa (thingIDNumber) y otro
        // para def (shortHash). Un solo mapa compartido mezclaba los dos espacios y una cosa cuyo
        // thingIDNumber coincidía con el shortHash de un def en caché recibía la respuesta de ese
        // def (Hallazgo H4 de 2026-10-05-findings-for-author.es.md).
        private readonly IntCache<AllowedToAcceptEntry> _mapThing;
        private readonly IntCache<AllowedToAcceptEntry> _mapDef;

        /// <summary>
        /// Construye la cache. Capacidad inicial 128 (típicamente un almacén acepta decenas de defs/cosas).
        /// No se registra para limpieza global: su vida está ligada a la instancia de StorageSettings,
        /// que se recrea en cada partida/carga.
        /// </summary>
        internal AllowedToAcceptCache(int capacity = 128)
        {
            _mapThing = new IntCache<AllowedToAcceptEntry>(capacity, registerForGameLoadCleanup: false);
            _mapDef = new IntCache<AllowedToAcceptEntry>(capacity, registerForGameLoadCleanup: false);
        }

        /// <summary>
        /// Consulta cacheada de <c>AllowedToAccept(Thing)</c>.
        /// Devuelve <c>true</c> si hay entrada válida y no expirada, y escribe el resultado en <paramref name="result"/>.
        /// Devuelve <c>false</c> si no hay entrada o está expirada (el llamador debe evaluar el vanilla).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGet(Thing t, out bool result)
        {
            int key = t?.thingIDNumber ?? -1;
            if (key < 0)
            {
                result = false;
                return false;
            }

            return TryGetInternal(_mapThing, key, out result);
        }

        /// <summary>
        /// Consulta cacheada de <c>AllowedToAccept(ThingDef)</c>.
        /// Devuelve <c>true</c> si hay entrada válida y no expirada, y escribe el resultado en <paramref name="result"/>.
        /// Devuelve <c>false</c> si no hay entrada o está expirada.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGet(ThingDef def, out bool result)
        {
            if (def == null)
            {
                result = false;
                return false;
            }

            int key = def.shortHash;
            return TryGetInternal(_mapDef, key, out result);
        }

        /// <summary>
        /// Lógica común de consulta: busca la entrada, comprueba expiración.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetInternal(IntCache<AllowedToAcceptEntry> map, int key, out bool result)
        {
            if (!map.TryGet(key, out AllowedToAcceptEntry entry))
            {
                result = false;
                return false;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now >= entry.ExpireTick)
            {
                // Expirada: se deja la entrada (se sobrescribirá en la siguiente actualización)
                // pero se reporta como miss.
                result = false;
                return false;
            }

            result = entry.Value;
            return true;
        }

        /// <summary>
        /// Actualiza la cache con un nuevo resultado. Calcula el tick de expiración con TTL + jitter.
        /// <paramref name="isThing"/> elige el espacio de claves (cosa=thingIDNumber, def=shortHash)
        /// para que no se crucen (Hallazgo H4).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Update(bool isThing, int key, bool value)
        {
            if (key < 0)
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            int jitter = key % JitterModulo;
            int expireTick = now + BaseTtlTicks + jitter;

            IntCache<AllowedToAcceptEntry> map = isThing ? _mapThing : _mapDef;
            ref AllowedToAcceptEntry entry = ref map.GetOrAdd(key);
            entry.Value = value;
            entry.ExpireTick = expireTick;
        }

        /// <summary>
        /// Invalida toda la cache (p. ej. al cambiar filtro, prioridad o preset).
        /// Simplemente limpia los mapas; las próximas consultas forzarán evaluación vanilla.
        /// </summary>
        internal void Invalidate()
        {
            _mapThing.Clear();
            _mapDef.Clear();
        }

        /// <summary>
        /// Implementación de <see cref="IClearable"/> para limpieza global (no usada realmente aquí,
        /// la vida de esta cache va atada a StorageSettings). Se deja para completitud.
        /// </summary>
        public void Clear() => Invalidate();

        /// <summary>Entradas ocupadas (diagnóstico).</summary>
        internal int Count => _mapThing.Count + _mapDef.Count;

        /// <summary>
        /// Obtiene el número de entradas para diagnóstico (método estático para usar desde settings).
        /// </summary>
        internal static int GetCount(StorageSettings settings)
        {
            try
            {
                ref AllowedToAcceptCache cache = ref settings.ReforjedAllowedToAcceptCache();
                if (cache == null)
                    return 0;
                return cache.Count;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Postfix de Harmony para invalidar la cache cuando se llama a TryNotifyChanged o set_Priority.
        /// Recibe el StorageSettings como __instance.
        /// </summary>
        public static void InvalidatePostfix(StorageSettings __instance)
        {
            // El campo inyectado puede ser null si la instancia de StorageSettings es nueva
            // (p. ej. durante generación de mapa con BaseGen) y aún no se ha accedido a la cache.
            // En ese caso no hay nada que invalidar.
            ref AllowedToAcceptCache cache = ref __instance.ReforjedAllowedToAcceptCache();
            if (cache != null)
            {
                cache.Invalidate();
            }
        }
    }
}
