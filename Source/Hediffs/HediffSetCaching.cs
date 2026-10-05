using Verse;
using Verse.Noise;
using Verse.AI;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using PerformanceFishReforjed.Caching;

namespace PerformanceFishReforjed.Hediffs;

public struct HediffCacheValue
{
	private int _version = -1;
	private int _nextRefreshTick;
	private List<Hediff> _hediffsInSet = [];
	public Hediff? Hediff;

	public HediffCacheValue() { }

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Update(HediffSet set, Hediff? hediff)
	{
		_hediffsInSet = set.hediffs;
		Hediff = hediff;
		_version = _hediffsInSet._version;
		_nextRefreshTick = GenTicks.TickLongInterval;
	}

	public bool Dirty
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _version != _hediffsInSet._version || GenTicks.TicksGame >= _nextRefreshTick;
	}
}

public struct VisibleHediffCacheValue
{
	public HediffCacheValue Value;

	public VisibleHediffCacheValue() { Value = new(); }

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Update(HediffSet set, Hediff? hediff) => Value.Update(set, hediff);
}

public struct NotMissingPartsCacheValue
{
	private int _version = -1;
	private int _nextRefreshTick;
	private List<Hediff> _hediffsInSet = [];
	public List<BodyPartRecord> Parts = [];

	public NotMissingPartsCacheValue() { }

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Update(HediffSet set, ref IEnumerable<BodyPartRecord> result)
	{
		_hediffsInSet = set.hediffs;
		_version = set.hediffs._version;
		_nextRefreshTick = GenTicks.TickLongInterval;

		var previousPartsVersion = Parts._version;
		Parts.Clear();
		Parts.AddRange(result);
		Parts._version = previousPartsVersion;

		result = Parts;
	}

	public bool Dirty
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _version != _hediffsInSet._version || GenTicks.TicksGame >= _nextRefreshTick;
	}
}

public static class HediffSetCaching
{
	public static readonly RefCache<CompoundKey<int, int>, HediffCacheValue> _hediffCache = new();
	public static readonly RefCache<CompoundKey<int, int>, VisibleHediffCacheValue> _visibleHediffCache = new();
	public static readonly RefCache<int, NotMissingPartsCacheValue> _notMissingPartsCache = new();
}

[HarmonyPatch(typeof(HediffSet), nameof(HediffSet.GetFirstHediffOfDef))]
public static class HediffSet_GetFirstHediffOfDef_Patch
{
	[HarmonyPrefix]
	public static bool Prefix(HediffSet __instance, HediffDef def, bool mustBeVisible, ref Hediff __result)
	{
		if ((__instance.hediffs.Count >= 5) & def != null)
		{
			var key = new CompoundKey<int, int>(__instance.pawn.thingIDNumber, def.shortHash);

			if (mustBeVisible)
			{
				ref var cache = ref HediffSetCaching._visibleHediffCache.GetOrAdd(key);
				if (!cache.Value.Dirty)
				{
					__result = cache.Value.Hediff;
					return false;
				}
			}
			else
			{
				ref var cache = ref HediffSetCaching._hediffCache.GetOrAdd(key);
				if (!cache.Dirty)
				{
					__result = cache.Hediff;
					return false;
				}
			}
		}

		return true;
	}

	[HarmonyPostfix]
	public static void Postfix(HediffSet __instance, HediffDef def, bool mustBeVisible, Hediff __result)
	{
		if (def == null)
			return;

		var key = new CompoundKey<int, int>(__instance.pawn.thingIDNumber, def.shortHash);
		if (mustBeVisible)
		{
			ref var cache = ref HediffSetCaching._visibleHediffCache.GetOrAdd(key);
			cache.Value.Update(__instance, __result);
		}
		else
		{
			ref var cache = ref HediffSetCaching._hediffCache.GetOrAdd(key);
			cache.Update(__instance, __result);
		}
	}
}

[HarmonyPatch(typeof(HediffSet), nameof(HediffSet.HasHediff))]
public static class HediffSet_HasHediff_Patch
{
	[HarmonyPrefix]
	public static bool Prefix(HediffSet __instance, HediffDef def, bool mustBeVisible, ref bool __result)
	{
		__result = __instance.GetFirstHediffOfDef(def, mustBeVisible) != null;
		return false;
	}
}

[HarmonyPatch(typeof(HediffSet), nameof(HediffSet.GetNotMissingParts))]
public static class HediffSet_GetNotMissingParts_Patch
{
	[HarmonyPrefix]
	public static bool Prefix(HediffSet __instance, BodyPartHeight height, BodyPartDepth depth, BodyPartTagDef tag,
		BodyPartRecord partParent, ref IEnumerable<BodyPartRecord> __result, out bool __state)
	{
		if ((height != BodyPartHeight.Undefined)
			| (depth != BodyPartDepth.Undefined)
			| (tag != null)
			| (partParent != null))
		{
			__state = false;
			return true;
		}

		ref var cache = ref HediffSetCaching._notMissingPartsCache.GetOrAdd(__instance.pawn.thingIDNumber);

		if (cache.Dirty)
		{
			__state = true;
			return true;
		}

		__result = cache.Parts;
		__state = false;
		return false;
	}

	[HarmonyPostfix]
	public static void Postfix(HediffSet __instance, bool __state, ref IEnumerable<BodyPartRecord> __result)
	{
		if (!__state)
			return;

		ref var cache = ref HediffSetCaching._notMissingPartsCache.GetOrAdd(__instance.pawn.thingIDNumber);
		cache.Update(__instance, ref __result);
	}
}

[HarmonyPatch(typeof(HediffSet), nameof(HediffSet.DirtyCache))]
public static class HediffSet_DirtyCache_Patch
{
	[HarmonyPostfix]
	public static void Postfix(HediffSet __instance)
	{
		HediffSetCaching._hediffCache.Clear();
		HediffSetCaching._visibleHediffCache.Clear();
		HediffSetCaching._notMissingPartsCache.Clear();
	}
}
