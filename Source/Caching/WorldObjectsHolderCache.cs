// This file is a Modification (MPL-2.0, section 1.10) of the original
// Performance Fish by bradson (https://github.com/bbradson/Performance-Fish),
// which is covered by the Mozilla Public License 2.0.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PerformanceFishReforjed.Caching;
using RimWorld.Planet;
using Verse;

namespace PerformanceFishReforjed.Caching
{
    public class WorldObjectsHolderCache
    {
        public int CachedWorldObjectsVersion = -2;
        public int CachedMapsVersion = -2;
        public readonly List<WorldObject> CachedWorldObjects = new List<WorldObject>();
        private static readonly HashSet<Type> SkippableComps = InitializeSkippableComps();
        private static readonly HashSet<Type> SkippableWorldObjects = InitializeSkippableWorldObjects();
        public void UpdateCache(WorldObjectsHolder holder)
        {
            CachedWorldObjects.Clear();
            var instanceWorldObjects = holder.worldObjects;
            for (var i = instanceWorldObjects.Count; i-- > 0;)
            {
                var worldObject = instanceWorldObjects[i];
                if (!ShouldSkip(worldObject))
                {
                    CachedWorldObjects.Add(worldObject);
                }
            }
            CachedWorldObjectsVersion = holder.worldObjects._version;
            CachedMapsVersion = GetCurrentMapsVersion();
        }
        public bool IsCacheDirty(WorldObjectsHolder holder)
        {
            return (int)typeof(List<WorldObject>).GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic)
                       .GetValue(holder.worldObjects) != CachedWorldObjectsVersion ||
                   GetCurrentMapsVersion() != CachedMapsVersion;
        }
        public List<WorldObject> GetCachedWorldObjects(WorldObjectsHolder holder)
        {
            if (IsCacheDirty(holder))
            {
                UpdateCache(holder);
            }
            return CachedWorldObjects;
        }
        private bool ShouldSkip(WorldObject worldObject)
        {
            if (!(worldObject is MapParent { HasMap: true }))
            {
                if (SkippableWorldObjects.Contains(worldObject.GetType()))
                {
                    if (worldObject is Settlement settlement)
                    {
                        if (settlement.trader?.stock != null)
                        {
                            return false;
                        }
                    }
                    return ComponentSkipTest(worldObject);
                }
            }
            return false;
        }
        private bool ComponentSkipTest(WorldObject worldObject)
        {
            var comps = worldObject.comps;
            for (var i = comps.Count; i-- > 0;)
            {
                var comp = comps[i];
                if (comp is EnterCooldownComp { Active: true })
                    return false;
                if (!SkippableComps.Contains(comp.GetType()))
                    return false;
                InvokeCompTick(comp);
            }
            return true;
        }
        private void InvokeCompTick(object comp)
        {
            if (comp == null) return;
            var compType = comp.GetType();
            var method = compType.GetMethod("CompTick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
            {
                try
                {
                    method.Invoke(comp, null);
                }
                catch (Exception ex)
                {
                    Log.Error($"Exception invoking CompTick on {compType}: {ex}");
                }
            }
        }
        private int GetCurrentMapsVersion()
        {
            var gameIntType = typeof(Current).GetNestedType("gameInt", BindingFlags.NonPublic);
            if (gameIntType != null)
            {
                var mapsField = gameIntType.GetField("maps", BindingFlags.Public | BindingFlags.Static);
                if (mapsField != null)
                {
                    var mapsList = mapsField.GetValue(null);
                    if (mapsList != null)
                    {
                        var listType = mapsList.GetType();
                        var versionField = listType.GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (versionField != null)
                        {
                            return (int)versionField.GetValue(mapsList);
                        }
                    }
                }
            }
            return -2;
        }
        private static HashSet<Type> InitializeSkippableComps()
        {
            var whitelisted = new[]
            {
                typeof(WorldObjectComp),
                typeof(FormCaravanComp),
                typeof(TimedDetectionRaids),
                typeof(EnterCooldownComp)
            };
            return MakeSubclassHashSet(typeof(WorldObjectComp), "CompTick", whitelisted);
        }
        private static HashSet<Type> InitializeSkippableWorldObjects()
        {
            var whitelisted = new[]
            {
                typeof(WorldObject),
                typeof(MapParent),
                typeof(Settlement)
            };
            return MakeSubclassHashSet(typeof(WorldObject), "Tick", whitelisted);
        }
        private static HashSet<Type> MakeSubclassHashSet(Type baseType, string methodName, Type[] allowedDeclaringTypes)
        {
            var result = new HashSet<Type>();
            foreach (var type in AppDomain.CurrentDomain.GetAssemblies()
                                                    .SelectMany(a => a.GetTypes()))
            {
                if (!baseType.IsAssignableFrom(type))
                    continue;
                var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null)
                    continue;
                if (method.DeclaringType == type)
                    continue;
                if (allowedDeclaringTypes.Contains(type))
                {
                    result.Add(type);
                }
            }
            return result;
        }
    }
}