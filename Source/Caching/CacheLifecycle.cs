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
                Log.Error("[PerformanceFishReforjed] Game.FinalizeInit not found: comp caches " +
                          "will NOT be cleared when switching games.");
                return;
            }

            MethodInfo? postfix = AccessTools.Method(typeof(CacheLifecycle), nameof(OnGameFinalizeInit));
            if (postfix == null)
            {
                Log.Error("[PerformanceFishReforjed] Cache-clearing postfix not found.");
                return;
            }

            try
            {
                PerformanceFishReforjedMod.HarmonyInstance.Patch(target, postfix: new HarmonyMethod(postfix));

                if (PerformanceFishReforjedSettings.EnableInternalLogging)
                    Log.Message("[PerformanceFishReforjed] Cache clearing hooked to Game.FinalizeInit.");
            }
            catch (Exception e)
            {
                Log.Error($"[PerformanceFishReforjed] Failed to patch Game.FinalizeInit: {e}");
            }

            ApplyListerBuildingsPostfixes();
            ApplyItemCountGridPostfixes();
            ApplyStorageSettingsInvalidation();
            ApplyBuildingSpawnDespawnPostfixes();
            ApplySlotGroupCapacityPatches();
            ApplyStorageGroupCapacityPatches();
            ApplyMassUtilityPatches();
        }

        /// <summary>
        /// Engancha parches de compatibilidad de GasGrid (ExposeData para persistencia).
        ///
        /// DESACTIVADO (Hallazgos C2/H1): el prepatch de gas se revirtió a vanilla íntegro, y este
        /// parche copiaba entre los grids paralelos y el array vanilla alrededor de ExposeData.
        /// Con el prepatch desactivado volvería a copiar los grids vacíos sobre el array real del
        /// juego en cada guardado, corrompiendo la partida. Se deja vacío a propósito.
        /// </summary>
        private static void ApplyGasGridPatches()
        {
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
                Log.Error("[PerformanceFishReforjed] StoreUtility.NoStorageBlockersIn not found: the " +
                          "storage-blocker shortcut will not be applied.");
                return;
            }

            MethodInfo? prefix = AccessTools.Method(typeof(Caching.StorageBlockerGrid),
                nameof(Caching.StorageBlockerGrid.NoStorageBlockersInPrefix));

            if (prefix == null)
            {
                Log.Error("[PerformanceFishReforjed] Storage-blocker prefix not found.");
                return;
            }

            try
            {
                PerformanceFishReforjedMod.HarmonyInstance.Patch(target, prefix: new HarmonyMethod(prefix));

                if (PerformanceFishReforjedSettings.EnableInternalLogging)
                    Log.Message("[PerformanceFishReforjed] Storage-blocker shortcut hooked to StoreUtility.NoStorageBlockersIn.");
            }
            catch (Exception e)
            {
                Log.Error($"[PerformanceFishReforjed] Failed to patch StoreUtility.NoStorageBlockersIn: {e}");
            }
        }

        private static void PatchThingGridPostfix(Type cacheType, string methodName, string postfixName)
        {
            MethodInfo? target = AccessTools.DeclaredMethod(typeof(ThingGrid), methodName,
                new[] { typeof(Thing), typeof(IntVec3) });

            if (target == null)
            {
                Log.Error($"[PerformanceFishReforjed] ThingGrid.{methodName}(Thing, IntVec3) not found: " +
                          $"{cacheType.Name} will not be maintained.");
                return;
            }

            MethodInfo? postfix = AccessTools.Method(cacheType, postfixName);
            if (postfix == null)
            {
                Log.Error($"[PerformanceFishReforjed] Postfix {postfixName} not found on {cacheType.Name}.");
                return;
            }

            try
            {
                PerformanceFishReforjedMod.HarmonyInstance.Patch(target, postfix: new HarmonyMethod(postfix));

                if (PerformanceFishReforjedSettings.EnableInternalLogging)
                    Log.Message($"[PerformanceFishReforjed] {cacheType.Name} hooked to ThingGrid.{methodName}.");
            }
            catch (Exception e)
            {
                Log.Error($"[PerformanceFishReforjed] Failed to patch ThingGrid.{methodName} with {cacheType.Name}.{postfixName}: {e}");
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
                            Log.Message("[PerformanceFishReforjed] AllowedToAccept cache hooked to StorageSettings.TryNotifyChanged.");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Failed to patch StorageSettings.TryNotifyChanged: {e}");
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
                            Log.Message("[PerformanceFishReforjed] AllowedToAccept cache hooked to StorageSettings.set_Priority.");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Failed to patch StorageSettings.set_Priority: {e}");
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
                            Log.Message("[PerformanceFishReforjed] StorageBlockerGrid hooked to Building.SpawnSetup.");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Failed to patch Building.SpawnSetup: {e}");
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
                            Log.Message("[PerformanceFishReforjed] StorageBlockerGrid hooked to Building.DeSpawn (prefix).");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Failed to patch Building.DeSpawn (prefix): {e}");
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
                            Log.Message("[PerformanceFishReforjed] StorageBlockerGrid hooked to Building.DeSpawn (postfix).");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[PerformanceFishReforjed] Failed to patch Building.DeSpawn (postfix): {e}");
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
                Log.Error($"[PerformanceFishReforjed] ListerBuildings.{methodName}(Building) not found: " +
                          $"building indexes will not be maintained (queries will fall back to the normal scan).");
                return;
            }

            MethodInfo? postfix = AccessTools.Method(typeof(Caching.ListerBuildingsCaches), postfixName);
            if (postfix == null)
            {
                Log.Error($"[PerformanceFishReforjed] Postfix {postfixName} not found.");
                return;
            }

            try
            {
                PerformanceFishReforjedMod.HarmonyInstance.Patch(target, postfix: new HarmonyMethod(postfix));

                if (PerformanceFishReforjedSettings.EnableInternalLogging)
                    Log.Message($"[PerformanceFishReforjed] Building index hooked to ListerBuildings.{methodName}.");
            }
            catch (Exception e)
            {
                Log.Error($"[PerformanceFishReforjed] Failed to patch ListerBuildings.{methodName}: {e}");
            }
        }

        /// <summary>
        /// Engancha el parche de capacidad de SlotGroup (C5).
        ///
        /// DESACTIVADO (Hallazgo L1): los enganches de esta familia nunca llegaron a aplicarse (los
        /// nombres de metodo con barra no los encuentra <c>AccessTools.Method</c>), y si se
        /// conectaran tal como estan escritos romperian el juego:
        /// <list type="bullet">
        /// <item><c>SlotGroupCapacityCache.IsLikelyFull</c> considera lleno un grupo por encima de
        /// 3 objetos por celda, ignorando los limites por celda que fijan los edificios de
        /// almacenamiento y los mods: un almacen legítimo quedaria "lleno" y nunca recibiria
        /// objetos acarreados.</item>
        /// <item>El atajo sobre <c>StoreUtility.TryFindBestBetterStoreCellForWorker</c> corta el
        /// metodo original cuando esa cache (desincronizada) dice "lleno": se dejan de acarrear
        /// objetos a celdas con sitio real.</item>
        /// </list>
        /// Se deja vacio a proposito: el comportamiento es exactamente el de vanilla.
        /// </summary>
        private static void ApplySlotGroupCapacityPatches()
        {
        }

        /// <summary>
        /// Engancha el parche de capacidad de StorageGroup (C6).
        ///
        /// DESACTIVADO (Hallazgo L1): si se conectara tal como esta escrito, romperia el juego:
        /// <c>StorageGroupCapacityCache.Recalculate</c> comprueba <c>members[i] is SlotGroup</c>,
        /// pero <c>StorageGroup.members</c> contiene <c>IStorageGroupMember</c> (no <c>SlotGroup</c>),
        /// asi que la condicion nunca es true y el grupo enlazado tendria capacidad cero y nunca
        /// recibiria objetos acarreados.
        /// Se deja vacio a proposito: el comportamiento es exactamente el de vanilla.
        /// </summary>
        private static void ApplyStorageGroupCapacityPatches()
        {
        }

        /// <summary>
        /// Engancha los parches de cache de masa de pawns (MassUtilityCaching).
        ///
        /// DESACTIVADO (Hallazgo L1 + Hallazgo de la auditoria): nunca llego a aplicarse por el
        /// mismo motivo que C5/C6, y si se conectara seria incorrecto: <c>PawnMassCache</c> comparte
        /// un unico <c>LastUpdatedTick</c> entre gear e inventario (con TTL distintos de 3072 y 1024),
        /// asi que refrescar uno invalidaria con la marca del otro y la masa del equipo podria leerse
        /// como masa de inventario hasta 3072 ticks. Se deja vacio a proposito: el comportamiento es
        /// exactamente el de vanilla.
        /// </summary>
        private static void ApplyMassUtilityPatches()
        {
        }

        /// <summary>Postfix: partida nueva o cargada, las caches anteriores ya no valen.</summary>
        private static void OnGameFinalizeInit()
        {
            CacheRegistry.ClearAll();
            DefStatCache.InvalidateAll();
            ClearCount++;

            if (PerformanceFishReforjedSettings.EnableInternalLogging)
            {
                Log.Message($"[PerformanceFishReforjed] Caches cleared on game load " +
                            $"(clearing #{ClearCount}, {CacheRegistry.Count} caches registered).");
            }
        }
    }
}