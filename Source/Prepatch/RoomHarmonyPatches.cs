using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using PerformanceFishReforjed.Caching;

namespace PerformanceFishReforjed.Prepatch;

/// <summary>
/// Parches Harmony adicionales para Room: Role_Patch y Owners_Patch.
///
/// Estos parches usan throttling por TTL para evitar recalcular estadísticas de
/// room demasiado frecuentemente.
/// </summary>
public static class RoomHarmonyPatches
{
	// Caché para Role_Patch
	private static readonly RefCache<int, RoomRoleCacheValue> _roleCache = new();

	// Caché para Owners_Patch
	private static readonly RefCache<int, RoomOwnersCacheValue> _ownersCache = new();

	public struct RoomRoleCacheValue
	{
		private int _nextUpdateTick;

		public RoomRoleCacheValue()
		{
			_nextUpdateTick = -2;
		}

		public bool Dirty
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GenTicks.TicksGame >= _nextUpdateTick;
		}

		public void SetDirty(Room room)
		{
			// TTL de ~512 ticks con jitter por room ID
			_nextUpdateTick = GenTicks.TicksGame + 384 + (room.ID % 256);
		}
	}

	public struct RoomOwnersCacheValue
	{
		private int _nextRefreshTick;
		public readonly List<Pawn> Owners;

		public RoomOwnersCacheValue()
		{
			_nextRefreshTick = -2;
			Owners = new List<Pawn>();
		}

		public void Update(Room room, IEnumerable<Pawn>? result)
		{
			Owners.Clear();

			if (result != null)
				Owners.AddRange(result);

			// TTL de ~512 ticks con jitter por room ID
			_nextRefreshTick = GenTicks.TicksGame + 384 + (room.ID % 256);
		}

		public bool Dirty
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GenTicks.TicksGame >= _nextRefreshTick;
		}
	}

	[HarmonyPatch(typeof(Room), nameof(Room.Role), MethodType.Getter)]
	public static class Room_Role_Patch
	{
		[HarmonyPrefix]
		public static bool Prefix(Room __instance, ref RoomRoleDef __result)
		{
			// Si ya tiene un role, dejar que el vanilla lo maneje
			if (__instance.role != null)
				return true;

			ref var cache = ref _roleCache.GetOrAdd(__instance.ID);

			if (!cache.Dirty)
			{
				__result = null;
				return false;
			}

			return true;
		}

		[HarmonyPostfix]
		public static void Postfix(Room __instance, RoomRoleDef __result)
		{
			ref var cache = ref _roleCache.GetOrAdd(__instance.ID);
			cache.SetDirty(__instance);
		}
	}

	[HarmonyPatch(typeof(Room), nameof(Room.Owners), MethodType.Getter)]
	public static class Room_Owners_Patch
	{
		[HarmonyPrefix]
		public static bool Prefix(Room __instance, ref IEnumerable<Pawn> __result, out bool __state)
		{
			ref var cache = ref _ownersCache.GetOrAdd(__instance.ID);

			if (cache.Dirty)
			{
				if (CanCache(__instance))
				{
					__state = true;
					return true;
				}
				else
				{
					cache.Update(__instance, (IEnumerable<Pawn>?)null);
				}
			}

			__result = cache.Owners;
			__state = false;
			return false;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static bool CanCache(Room __instance)
		{
			return __instance.TouchesMapEdge == false
				&& __instance.IsHuge == false
				&& (!__instance.statsAndRoleDirty || __instance.ContainedBeds.Any<Building_Bed>());
		}

		[HarmonyPostfix]
		public static void Postfix(Room __instance, bool __state, ref IEnumerable<Pawn> __result)
		{
			if (!__state)
				return;

			ref var cache = ref _ownersCache.GetOrAdd(__instance.ID);
			cache.Update(__instance, __result);
			__result = cache.Owners;
		}
	}
}
