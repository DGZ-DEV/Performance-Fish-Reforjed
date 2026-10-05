// This file is a Modification (MPL-2.0, section 1.10) of the original
// Performance Fish by bradson (https://github.com/bbradson/Performance-Fish),
// which is covered by the Mozilla Public License 2.0.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld.Planet;
using Verse;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Cache for WorldPawns.AllPawnsAlive and AllPawnsAliveOrDead results.
    ///
    /// Each result tracks its OWN set of version markers. Antes (Hallazgo H6) las dos listas
    /// compartían un único conjunto de versiones en <c>UpdateCache</c>: cuando una se reconstruía
    /// marcaba todas como frescas y la otra devolvía su resultado viejo hasta que un cambio
    /// posterior llegara primero a ella, dejando fuera de la recolección de basura del mundo a los
    /// pawns nuevos.
    /// </summary>
    public class WorldPawnsCache
    {
        // ─── AllPawnsAlive: depende de pawnsAlive + pawnsMothballed ─────────────
        private int AlivePawnsAliveVersion = -2;
        private int AliveMothballedVersion = -2;

        // ─── AllPawnsAliveOrDead: depende de AllPawnsAlive (cache) + pawnsDead ──
        private int OrDeadPawnsDeadVersion = -2;

        public readonly List<Pawn> allPawnsAliveResult = new List<Pawn>();
        public readonly List<Pawn> allPawnsAliveOrDeadResult = new List<Pawn>();

        private static readonly FieldInfo pawnsAliveVersionField = typeof(HashSet<Pawn>).GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo pawnsMothballedVersionField = typeof(HashSet<Pawn>).GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo pawnsDeadVersionField = typeof(HashSet<Pawn>).GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo listVersionField = typeof(List<Pawn>).GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic);

        public List<Pawn> GetAllPawnsAlive(WorldPawns instance)
        {
            if (IsAliveDirty(instance))
            {
                allPawnsAliveResult.Clear();
                allPawnsAliveResult.AddRange(instance.pawnsAlive);
                allPawnsAliveResult.AddRange(instance.pawnsMothballed);
                AlivePawnsAliveVersion = (int)pawnsAliveVersionField.GetValue(instance.pawnsAlive);
                AliveMothballedVersion = (int)pawnsMothballedVersionField.GetValue(instance.pawnsMothballed);
            }
            return allPawnsAliveResult;
        }

        public List<Pawn> GetAllPawnsAliveOrDead(WorldPawns instance)
        {
            if (IsOrDeadDirty(instance))
            {
                allPawnsAliveOrDeadResult.Clear();
                allPawnsAliveOrDeadResult.AddRange(instance.AllPawnsAlive); // usa la caché de arriba
                allPawnsAliveOrDeadResult.AddRange(instance.pawnsDead);
                OrDeadPawnsDeadVersion = (int)pawnsDeadVersionField.GetValue(instance.pawnsDead);
            }
            return allPawnsAliveOrDeadResult;
        }

        private bool IsAliveDirty(WorldPawns instance)
        {
            return (int)pawnsAliveVersionField.GetValue(instance.pawnsAlive) != AlivePawnsAliveVersion
                || (int)pawnsMothballedVersionField.GetValue(instance.pawnsMothballed) != AliveMothballedVersion;
        }

        private bool IsOrDeadDirty(WorldPawns instance)
        {
            // El pawnsDead de una caravana/trading caravan a veces se marca por reflexión en el
            // mismo _version que un añadido de pawnsDead; para no fiarse sola de eso, también se
            // invalida si la lista viva cambió (el resultado la incluye vía AllPawnsAlive).
            return (int)pawnsDeadVersionField.GetValue(instance.pawnsDead) != OrDeadPawnsDeadVersion
                || IsAliveDirty(instance);
        }
    }
}