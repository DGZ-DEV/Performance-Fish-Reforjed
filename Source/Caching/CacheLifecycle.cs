using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Vaciado de las caches cuando cambia la partida.
    ///
    /// Las caches de GetCompCaching se indexan por identificadores unicos de objeto
    /// (thingIDNumber, loadID, ID, uniqueID, shortHash). Dentro de una partida esos
    /// identificadores no se repiten, pero el contador se reinicia en cada partida: si no se
    /// vaciara, una entrada vieja podria devolver el comp de un objeto de la partida anterior,
    /// que ademas seguiria vivo en memoria por estar referenciado desde la cache.
    ///
    /// Gancho elegido: <see cref="Game.FinalizeInit"/>. Verificado leyendo el IL del juego (no
    /// supuesto): lo llaman tanto <c>Game.InitNewGame</c> (partida nueva) como <c>Game.LoadGame</c>
    /// (partida cargada), que son exactamente los dos casos que hay que cubrir.
    ///
    /// La medida es defensiva, no de rendimiento: el vaciado ocurre una vez por carga de partida.
    /// </summary>
    internal static class CacheLifecycle
    {
        /// <summary>Veces que se han vaciado las caches (diagnostico de la pagina de ajustes).</summary>
        internal static int ClearCount;

        /// <summary>
        /// Aplica el parche de Harmony. Se llama desde <see cref="Startup"/>.
        /// Si el metodo objetivo no existe, se informa por consola en lugar de fallar en silencio:
        /// sin este parche las caches solo serian correctas dentro de una unica partida.
        /// </summary>
        internal static void ApplyPatches()
        {
            MethodInfo? target = AccessTools.Method(typeof(Game), nameof(Game.FinalizeInit));
            if (target == null)
            {
                Log.Error("[PerformanceFishReforjed] No se encontro Game.FinalizeInit: las caches de comps " +
                          "NO se vaciaran al cambiar de partida.");
                return;
            }

            MethodInfo? postfix = AccessTools.Method(typeof(CacheLifecycle), nameof(OnGameFinalizeInit));
            if (postfix == null)
            {
                Log.Error("[PerformanceFishReforjed] No se encontro el postfix de limpieza de caches.");
                return;
            }

            try
            {
                PerformanceFishReforjedMod.HarmonyInstance.Patch(target, postfix: new HarmonyMethod(postfix));

                if (PerformanceFishReforjedSettings.EnableInternalLogging)
                    Log.Message("[PerformanceFishReforjed] Limpieza de caches enganchada a Game.FinalizeInit.");
            }
            catch (Exception e)
            {
                Log.Error($"[PerformanceFishReforjed] Fallo al parchear Game.FinalizeInit: {e}");
            }

            ApplyListerBuildingsPostfixes();
            ApplyItemCountGridPostfixes();
            ApplyStorageSettingsInvalidation();
            ApplyBuildingSpawnDespawnPostfixes();
            ApplySlotGroupCapacityPatches();
            ApplyStorageGroupCapacityPatches();
            ApplyMassUtilityPatches();
            ApplyGasGridPatches();
        }

        /// <summary>
        /// Engancha parches de compatibilidad de GasGrid (ExposeData para persistencia).
        /// </summary>
        private static void ApplyGasGridPatches()
        {
            MethodInfo? targetExposeData = AccessTools.Method(typeof(GasGrid), nameof(GasGrid.ExposeData));
            if (targetExposeData != null)
            {
                MethodInfo? prefixExpose = AccessTools.Method(typeof(Prepatch.GasGridHarmonyPatches.GasGrid_ExposeData_Patch), "Prefix");
                MethodInfo? postfixExpose = AccessTools.Method(typeof(Prepatch.GasGridHarmonyPatches.GasGrid_ExposeData_Patch), "Postfix");

                if (prefixExpose != null && postfixExpose != null)
                {
                    try
                    {
                        PerformanceFishReforjedMod.HarmonyInstance.Patch(targetExposeData,
                            prefix: new HarmonyMethod(prefixExpose),
                            postfix: new HarmonyMethod(postfixExpose));

                        if (PerformanceFishReforjedSettings.EnableInternalLogging)
                            Log.Message("[PerformanceFishReforjed] Persistencia optimizada enganchada a GasGrid.ExposeData.");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Fallo al parchear GasGrid.ExposeData: {e}");
                    }
                }
            }
        }

        /// <summary>
        /// Engancha el contador de objetos por celda a <c>ThingGrid.RegisterInCell</c> y
        /// <c>DeregisterInCell</c>.
        ///
        /// Se hace con postfijos de Harmony y NO con el anclaje por IL que usa el original (insertar
        /// tras el primer <c>callvirt</c> a <c>Add</c>/<c>Remove</c> de una lista): un postfijo depende
        /// del metodo, y un anclaje por instruccion deja de aplicarse en silencio cuando el IL cambia.
        /// Verificado con Cecil que esos dos metodos son los unicos que anaden y quitan de las listas
        /// de cosas por celda, y que ya calculan el indice con <c>CellIndices.CellToIndex</c>.
        /// </summary>
        private static void ApplyItemCountGridPostfixes()
        {
            PatchThingGridPostfix(typeof(Caching.ItemCountGrid), "RegisterInCell", nameof(Caching.ItemCountGrid.RegisterPostfix));
            PatchThingGridPostfix(typeof(Caching.ItemCountGrid), "DeregisterInCell", nameof(Caching.ItemCountGrid.DeregisterPostfix));

            // El atajo de bloqueantes necesita los MISMOS dos enganches (la capacidad de la celda cambia
            // cuando cambia su lista de cosas), mas un prefijo sobre el metodo que se acelera.
            PatchThingGridPostfix(typeof(Caching.StorageBlockerGrid), "RegisterInCell", nameof(Caching.StorageBlockerGrid.RegisterPostfix));
            PatchThingGridPostfix(typeof(Caching.StorageBlockerGrid), "DeregisterInCell", nameof(Caching.StorageBlockerGrid.DeregisterPostfix));

            PatchNoStorageBlockersInPrefix();
        }

        /// <summary>
        /// Prefijo sobre <c>StoreUtility.NoStorageBlockersIn</c>. Es un prefijo y no una sustitucion de
        /// cuerpo a proposito: solo corta cuando el vanilla tambien devolveria false, asi que la parte
        /// delicada (la logica de apilado y de conteo) sigue siendo la del juego.
        /// </summary>
        private static void PatchNoStorageBlockersInPrefix()
        {
            MethodInfo? target = AccessTools.DeclaredMethod(typeof(StoreUtility), "NoStorageBlockersIn",
                new[] { typeof(IntVec3), typeof(Map), typeof(Thing) });

            if (target == null)
            {
                Log.Error("[PerformanceFishReforjed] No se encontro StoreUtility.NoStorageBlockersIn: el atajo de " +
                          "bloqueantes no se aplicara.");
                return;
            }

            MethodInfo? prefix = AccessTools.Method(typeof(Caching.StorageBlockerGrid),
                nameof(Caching.StorageBlockerGrid.NoStorageBlockersInPrefix));

            if (prefix == null)
            {
                Log.Error("[PerformanceFishReforjed] No se encontro el prefijo de bloqueantes.");
                return;
            }

            try
            {
                PerformanceFishReforjedMod.HarmonyInstance.Patch(target, prefix: new HarmonyMethod(prefix));

                if (PerformanceFishReforjedSettings.EnableInternalLogging)
                    Log.Message("[PerformanceFishReforjed] Atajo de bloqueantes enganchado a StoreUtility.NoStorageBlockersIn.");
            }
            catch (Exception e)
            {
                Log.Error($"[PerformanceFishReforjed] Fallo al parchear StoreUtility.NoStorageBlockersIn: {e}");
            }
        }

        private static void PatchThingGridPostfix(Type cacheType, string methodName, string postfixName)
        {
            MethodInfo? target = AccessTools.DeclaredMethod(typeof(ThingGrid), methodName,
                new[] { typeof(Thing), typeof(IntVec3) });

            if (target == null)
            {
                Log.Error($"[PerformanceFishReforjed] No se encontro ThingGrid.{methodName}(Thing, IntVec3): el " +
                          $"{cacheType.Name} no se mantendra.");
                return;
            }

            MethodInfo? postfix = AccessTools.Method(cacheType, postfixName);
            if (postfix == null)
            {
                Log.Error($"[PerformanceFishReforjed] No se encontro el postfijo {postfixName} en {cacheType.Name}.");
                return;
            }

            try
            {
                PerformanceFishReforjedMod.HarmonyInstance.Patch(target, postfix: new HarmonyMethod(postfix));

                if (PerformanceFishReforjedSettings.EnableInternalLogging)
                    Log.Message($"[PerformanceFishReforjed] {cacheType.Name} enganchado a ThingGrid.{methodName}.");
            }
            catch (Exception e)
            {
                Log.Error($"[PerformanceFishReforjed] Fallo al parchear ThingGrid.{methodName} con {cacheType.Name}.{postfixName}: {e}");
            }
        }

        /// <summary>
        /// Engancha el mantenimiento de los indices de <see cref="Verse.ListerBuildings"/>.
        ///
        /// Se hace con postfijos de Harmony y NO sustituyendo los cuerpos de Add/Remove: en 1.6 esos
        /// metodos llevan el sistema TrackingScope (y <c>Track</c> es publico, lo usa el generador de
        /// mapas), asi que copiarlos seria perder comportamiento. Si estos postfijos no llegaran a
        /// aplicarse, las consultas lo detectan con su comprobacion de confianza y responden con el
        /// recorrido del vanilla, que es lento pero correcto.
        /// </summary>
        private static void ApplyListerBuildingsPostfixes()
        {
            PatchListerBuildingsPostfix("Add", nameof(Caching.ListerBuildingsCaches.AddPostfix));
            PatchListerBuildingsPostfix("Remove", nameof(Caching.ListerBuildingsCaches.RemovePostfix));
        }

        /// <summary>
        /// Engancha la invalidación de la cache de StorageSettings.AllowedToAccept.
        ///
        /// Puntos de invalidación instantánea:
        /// 1. StorageSettings.TryNotifyChanged() - se llama al cambiar filtro, preset, o copiar settings.
        /// 2. StorageSettings.set_Priority - se llama al cambiar la prioridad de almacenamiento.
        ///
        /// Ambos métodos disparan la invalidación de toda la cache de esa instancia de StorageSettings.
        /// Además, cada entrada tiene un TTL de ~34s (2048 ticks) con jitter para cubrir cambios
        /// que el juego no notifica (HP de la cosa en el suelo, deterioro, calidad).
        /// </summary>
        private static void ApplyStorageSettingsInvalidation()
        {
            // TryNotifyChanged - invalida al cambiar filtro/preset
            MethodInfo? targetNotify = AccessTools.Method(typeof(StorageSettings), nameof(StorageSettings.TryNotifyChanged));
            if (targetNotify != null)
            {
                MethodInfo? postfixNotify = AccessTools.Method(typeof(Caching.AllowedToAcceptCache), nameof(Caching.AllowedToAcceptCache.InvalidatePostfix));
                if (postfixNotify != null)
                {
                    try
                    {
                        PerformanceFishReforjedMod.HarmonyInstance.Patch(targetNotify, postfix: new HarmonyMethod(postfixNotify));
                        if (PerformanceFishReforjedSettings.EnableInternalLogging)
                            Log.Message("[PerformanceFishReforjed] Cache AllowedToAccept enganchada a StorageSettings.TryNotifyChanged.");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Fallo al parchear StorageSettings.TryNotifyChanged: {e}");
                    }
                }
            }

            // set_Priority - invalida al cambiar prioridad de almacenamiento
            MethodInfo? targetPriority = AccessTools.Method(typeof(StorageSettings), "set_Priority", new[] { typeof(StoragePriority) });
            if (targetPriority != null)
            {
                MethodInfo? postfixPriority = AccessTools.Method(typeof(Caching.AllowedToAcceptCache), nameof(Caching.AllowedToAcceptCache.InvalidatePostfix));
                if (postfixPriority != null)
                {
                    try
                    {
                        PerformanceFishReforjedMod.HarmonyInstance.Patch(targetPriority, postfix: new HarmonyMethod(postfixPriority));
                        if (PerformanceFishReforjedSettings.EnableInternalLogging)
                            Log.Message("[PerformanceFishReforjed] Cache AllowedToAccept enganchada a StorageSettings.set_Priority.");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Fallo al parchear StorageSettings.set_Priority: {e}");
                    }
                }
            }
        }

        /// <summary>
        /// Engancha la actualización del bit de bloqueantes cuando un edificio aparece o desaparece.
        ///
        /// Los edificios cambian <c>GetMaxItemsAllowedInCell</c> (via <c>MaxItemsInCell</c>) sin tocar
        /// la lista de cosas de la celda, así que los postfijos de ThingGrid no se disparan.
        /// Con estos postfijos en <c>Building.SpawnSetup</c> y un prefix en <c>Building.DeSpawn</c>, se recalcula
        /// el bit de la celda del edificio al momento, eliminando false negatives.
        /// </summary>
        private static void ApplyBuildingSpawnDespawnPostfixes()
        {
            // Building.SpawnSetup - se llama cuando un edificio se coloca/construye
            MethodInfo? targetSpawn = AccessTools.Method(typeof(Building), nameof(Building.SpawnSetup),
                new[] { typeof(Map), typeof(bool) });
            if (targetSpawn != null)
            {
                MethodInfo? postfixSpawn = AccessTools.Method(typeof(Caching.StorageBlockerGrid),
                    nameof(Caching.StorageBlockerGrid.BuildingSpawnedPostfix));
                if (postfixSpawn != null)
                {
                    try
                    {
                        PerformanceFishReforjedMod.HarmonyInstance.Patch(targetSpawn, postfix: new HarmonyMethod(postfixSpawn));
                        if (PerformanceFishReforjedSettings.EnableInternalLogging)
                            Log.Message("[PerformanceFishReforjed] StorageBlockerGrid enganchado a Building.SpawnSetup.");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Fallo al parchear Building.SpawnSetup: {e}");
                    }
                }
            }

            // Building.DeSpawn - se llama cuando un edificio se destruye/desaparece
            // Usamos un PREFIX para obtener el mapa antes de que el edificio sea removido,
            // y un POSTFIX para recalcular después de que sea removido.
            MethodInfo? targetDespawn = AccessTools.Method(typeof(Building), nameof(Building.DeSpawn),
                new[] { typeof(DestroyMode) });
            if (targetDespawn != null)
            {
                MethodInfo? prefixDespawn = AccessTools.Method(typeof(Caching.StorageBlockerGrid),
                    nameof(Caching.StorageBlockerGrid.BuildingDespawnedPrefix));
                if (prefixDespawn != null)
                {
                    try
                    {
                        PerformanceFishReforjedMod.HarmonyInstance.Patch(targetDespawn, prefix: new HarmonyMethod(prefixDespawn));
                        if (PerformanceFishReforjedSettings.EnableInternalLogging)
                            Log.Message("[PerformanceFishReforjed] StorageBlockerGrid enganchado a Building.DeSpawn (prefix).");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Fallo al parchear Building.DeSpawn (prefix): {e}");
                    }
                }

                MethodInfo? postfixDespawn = AccessTools.Method(typeof(Caching.StorageBlockerGrid),
                    nameof(Caching.StorageBlockerGrid.BuildingDeSpawnedPostfix));
                if (postfixDespawn != null)
                {
                    try
                    {
                        PerformanceFishReforjedMod.HarmonyInstance.Patch(targetDespawn, postfix: new HarmonyMethod(postfixDespawn));
                        if (PerformanceFishReforjedSettings.EnableInternalLogging)
                            Log.Message("[PerformanceFishReforjed] StorageBlockerGrid enganchado a Building.DeSpawn (postfix).");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Fallo al parchear Building.DeSpawn (postfix): {e}");
                    }
                }
            }
        }

        private static void PatchListerBuildingsPostfix(string methodName, string postfixName)
        {
            MethodInfo? target = AccessTools.DeclaredMethod(typeof(ListerBuildings), methodName,
                new[] { typeof(Building) });

            if (target == null)
            {
                Log.Error($"[PerformanceFishReforjed] No se encontro ListerBuildings.{methodName}(Building): los " +
                          $"indices de edificios no se mantenendran (las consultas caeran al recorrido normal).");
                return;
            }

            MethodInfo? postfix = AccessTools.Method(typeof(Caching.ListerBuildingsCaches), postfixName);
            if (postfix == null)
            {
                Log.Error($"[PerformanceFishReforjed] No se encontro el postfijo {postfixName}.");
                return;
            }

            try
            {
                PerformanceFishReforjedMod.HarmonyInstance.Patch(target, postfix: new HarmonyMethod(postfix));

                if (PerformanceFishReforjedSettings.EnableInternalLogging)
                    Log.Message($"[PerformanceFishReforjed] Indice de edificios enganchado a ListerBuildings.{methodName}.");
            }
            catch (Exception e)
            {
                Log.Error($"[PerformanceFishReforjed] Fallo al parchear ListerBuildings.{methodName}: {e}");
            }
        }

        /// <summary>
        /// Engancha el parche de capacidad de SlotGroup (C5).
        ///
        /// Engancha:
        /// 1. Postfijos en SlotGroup.Notify_AddedCell y Notify_LostCell para mantener la cache.
        /// 2. Prefijo en StoreUtility.TryFindBestBetterStoreCellForWorker para el atajo de capacidad.
        /// </summary>
        private static void ApplySlotGroupCapacityPatches()
        {
            // Notify_AddedCell
            MethodInfo? targetAdded = AccessTools.Method(typeof(SlotGroup), nameof(SlotGroup.Notify_AddedCell));
            if (targetAdded != null)
            {
                MethodInfo? postfixAdded = AccessTools.Method(typeof(Prepatch.SlotGroupCapacityPatches),
                    "Notify_AddedCell_Patch/Postfix");
                if (postfixAdded != null)
                {
                    try
                    {
                        PerformanceFishReforjedMod.HarmonyInstance.Patch(targetAdded, postfix: new HarmonyMethod(postfixAdded));
                        if (PerformanceFishReforjedSettings.EnableInternalLogging)
                            Log.Message("[PerformanceFishReforjed] SlotGroupCapacityCache enganchado a SlotGroup.Notify_AddedCell.");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Fallo al parchear SlotGroup.Notify_AddedCell: {e}");
                    }
                }
            }

            // Notify_LostCell
            MethodInfo? targetLost = AccessTools.Method(typeof(SlotGroup), nameof(SlotGroup.Notify_LostCell));
            if (targetLost != null)
            {
                MethodInfo? postfixLost = AccessTools.Method(typeof(Prepatch.SlotGroupCapacityPatches),
                    "Notify_LostCell_Patch/Postfix");
                if (postfixLost != null)
                {
                    try
                    {
                        PerformanceFishReforjedMod.HarmonyInstance.Patch(targetLost, postfix: new HarmonyMethod(postfixLost));
                        if (PerformanceFishReforjedSettings.EnableInternalLogging)
                            Log.Message("[PerformanceFishReforjed] SlotGroupCapacityCache enganchado a SlotGroup.Notify_LostCell.");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Fallo al parchear SlotGroup.Notify_LostCell: {e}");
                    }
                }
            }

            // TryFindBestBetterStoreCellForWorker - atajo de capacidad
            MethodInfo? targetWorker = AccessTools.DeclaredMethod(typeof(StoreUtility), "TryFindBestBetterStoreCellForWorker");
            if (targetWorker != null)
            {
                MethodInfo? prefixWorker = AccessTools.Method(typeof(Prepatch.StoreUtilityCapacityPatch),
                    "TryFindBestBetterStoreCellForWorker_CapacityPatch/Prefix");
                if (prefixWorker != null)
                {
                    try
                    {
                        PerformanceFishReforjedMod.HarmonyInstance.Patch(targetWorker, prefix: new HarmonyMethod(prefixWorker));
                        if (PerformanceFishReforjedSettings.EnableInternalLogging)
                            Log.Message("[PerformanceFishReforjed] Atajo de capacidad enganchado a StoreUtility.TryFindBestBetterStoreCellForWorker.");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Fallo al parchear StoreUtility.TryFindBestBetterStoreCellForWorker: {e}");
                    }
                }
            }
        }

        /// <summary>
        /// Engancha el parche de capacidad de StorageGroup (C6).
        ///
        /// Engancha:
        /// 1. Postfijos en StorageGroup.RemoveMember y Notify_SettingsChanged para mantener la cache.
        /// </summary>
        private static void ApplyStorageGroupCapacityPatches()
        {
            // RemoveMember
            MethodInfo? targetMemberRemoved = AccessTools.Method(typeof(StorageGroup), nameof(StorageGroup.RemoveMember));
            if (targetMemberRemoved != null)
            {
                MethodInfo? postfixMemberRemoved = AccessTools.Method(typeof(Prepatch.StorageGroupCapacityPatches),
                    "RemoveMember_Patch/Postfix");
                if (postfixMemberRemoved != null)
                {
                    try
                    {
                        PerformanceFishReforjedMod.HarmonyInstance.Patch(targetMemberRemoved, postfix: new HarmonyMethod(postfixMemberRemoved));
                        if (PerformanceFishReforjedSettings.EnableInternalLogging)
                            Log.Message("[PerformanceFishReforjed] StorageGroupCapacityCache enganchado a StorageGroup.RemoveMember.");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Fallo al parchear StorageGroup.RemoveMember: {e}");
                    }
                }
            }

            // Notify_SettingsChanged
            MethodInfo? targetSettingsChanged = AccessTools.Method(typeof(StorageGroup), nameof(StorageGroup.Notify_SettingsChanged));
            if (targetSettingsChanged != null)
            {
                MethodInfo? postfixSettingsChanged = AccessTools.Method(typeof(Prepatch.StorageGroupCapacityPatches),
                    "Notify_SettingsChanged_Patch/Postfix");
                if (postfixSettingsChanged != null)
                {
                    try
                    {
                        PerformanceFishReforjedMod.HarmonyInstance.Patch(targetSettingsChanged, postfix: new HarmonyMethod(postfixSettingsChanged));
                        if (PerformanceFishReforjedSettings.EnableInternalLogging)
                            Log.Message("[PerformanceFishReforjed] StorageGroupCapacityCache enganchado a StorageGroup.Notify_SettingsChanged.");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Fallo al parchear StorageGroup.Notify_SettingsChanged: {e}");
                    }
                }
            }
        }

        /// <summary>
        /// Engancha los parches de caché de masa de pawns (MassUtilityCaching).
        ///
        /// Engancha:
        /// 1. Prefijos en MassUtility.GearMass e InventoryMass.
        /// </summary>
        private static void ApplyMassUtilityPatches()
        {
            // GearMass
            MethodInfo? targetGearMass = AccessTools.Method(typeof(MassUtility), nameof(MassUtility.GearMass));
            if (targetGearMass != null)
            {
                MethodInfo? prefixGearMass = AccessTools.Method(typeof(Prepatch.MassUtilityPatches),
                    "GearMass_Patch/Prefix");
                MethodInfo? postfixGearMass = AccessTools.Method(typeof(Prepatch.MassUtilityPatches),
                    "GearMass_Patch/Postfix");
                if (prefixGearMass != null && postfixGearMass != null)
                {
                    try
                    {
                        PerformanceFishReforjedMod.HarmonyInstance.Patch(targetGearMass, prefix: new HarmonyMethod(prefixGearMass), postfix: new HarmonyMethod(postfixGearMass));
                        if (PerformanceFishReforjedSettings.EnableInternalLogging)
                            Log.Message("[PerformanceFishReforjed] Caché de masa enganchada a MassUtility.GearMass.");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Fallo al parchear MassUtility.GearMass: {e}");
                    }
                }
            }

            // InventoryMass
            MethodInfo? targetInventoryMass = AccessTools.Method(typeof(MassUtility), nameof(MassUtility.InventoryMass));
            if (targetInventoryMass != null)
            {
                MethodInfo? prefixInventoryMass = AccessTools.Method(typeof(Prepatch.MassUtilityPatches),
                    "InventoryMass_Patch/Prefix");
                MethodInfo? postfixInventoryMass = AccessTools.Method(typeof(Prepatch.MassUtilityPatches),
                    "InventoryMass_Patch/Postfix");
                if (prefixInventoryMass != null && postfixInventoryMass != null)
                {
                    try
                    {
                        PerformanceFishReforjedMod.HarmonyInstance.Patch(targetInventoryMass, prefix: new HarmonyMethod(prefixInventoryMass), postfix: new HarmonyMethod(postfixInventoryMass));
                        if (PerformanceFishReforjedSettings.EnableInternalLogging)
                            Log.Message("[PerformanceFishReforjed] Caché de masa enganchada a MassUtility.InventoryMass.");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Fallo al parchear MassUtility.InventoryMass: {e}");
                    }
                }
            }
        }

        /// <summary>Postfix: partida nueva o cargada, las caches anteriores ya no valen.</summary>
        private static void OnGameFinalizeInit()
        {
            CacheRegistry.ClearAll();
            ClearCount++;

            if (PerformanceFishReforjedSettings.EnableInternalLogging)
            {
                Log.Message($"[PerformanceFishReforjed] Caches vaciadas al cargar partida " +
                            $"(limpieza n.º {ClearCount}, {CacheRegistry.Count} caches registradas).");
            }
        }
    }
}