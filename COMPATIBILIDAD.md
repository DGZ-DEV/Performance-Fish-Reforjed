# Performance Fish Reforjed — Análisis de compatibilidad (datos reales)

Análisis basado en descompilación real (ilspycmd) de los ensamblados cargables de cada mod
presente en `C:\Users\User\Desktop\MODS`, frente a la superficie optimizada del Reforjed.

## Superficie optimizada del Reforjed (qué toca nuestro mod)

**Reescrituras de IL (prepatch, 32):**
- `ThingDef`: BaseMarketValue, BaseMass, BaseFlammability, BaseMaxHitPoints
- Comps: `ThingDef.GetCompProperties`/HasComp, hediff-def/ability/map/world/game GetComponent
  (`ThingWithComps.GetComp` y `HediffUtility.TryGetComp` se **quitaron** en la revisión 2026-10-05)
- `ListerThings`: Add/Remove/Contains/Clear/GetThingsOfType
- `ListerBuildings`: AllBuildingsColonistOfDef/ColonistsHave(X)/AllColonistBuildingsOfType/AllBuildingsNonColonistOfDef
- `GridsUtility.GetItemCount`
- `StorageSettings.AllowedToAccept` (cachés separadas por Thing y por ThingDef)
- `Room.ContainedBeds`
- `WorldPawns` (AllPawnsAlive/OrDead; **DefPreventingMothball NO**: MissileGirl lo transpila)
- ~~SlotGroup capacities~~ (quitado, sin ganancia — L2)
- ~~`WorldObjectsHolder.Tick`~~ (revertido a vanilla — C1)
- ~~`GasGrid`: Tick/AddGas/AnyGasAt/DensityAt~~ (revertido a vanilla — C2/H1)
- ~~`WorkGiver_DoBill` (8 métodos)~~ (quitado, sin ganancia y rompía parches ajenos — L2/M5)

**Enganches Harmony (runtime, activos: 5):**
- `Game.FinalizeInit` (vaciado de caches + invalidación de DefStatCache)
- `ListerBuildings` / `ThingGrid.Register/Deregister` (contador por celda)
- `StorageSettings` (invalidación de AllowedToAccept al cambiar filtros)
- `Building` spawn/despawn (índices de edificios)

**Enganches desactivados o no conectados (vanilla, tras revisión 2026-10-05):**
- ~~`HediffSet`: GetFirstHediffOfDef, HasHediff, GetNotMissingParts, DirtyCache~~ (no conectado — L1)
- ~~`GasGrid.ExposeData`~~ (no hay grid paralelo — C2/H1)
- ~~`StorageGroup.RemoveMember`/Notify_SettingsChanged~~ (desactivado — L1)
- ~~`Room.Role`/`Room.Owners`~~ (no conectado — L1)
- ~~`SlotGroup.Notify_AddedCell`/Notify_LostCell~~ (desactivado — L1)
- ~~`StoreUtility.TryFindBestBetterStoreCellForWorker`~~ (desactivado — L1)
- ~~`MassUtility.GearMass`/InventoryMass~~ (desactivado — L1)
- ~~`ThingOwner.ExposeData`/TryAdd/Remove~~ (no conectado — L1; el postfix de `Remove` está escrito correcto para conectarse en el futuro)

---

## Conflictos REALES detectados (descompilado)

### 1. VanillaExpandedFramework (`OskarPotocki.VanillaFactionsExpanded.Core`)
- **`MassUtility.Capacity`** → VEF lo parchea. Reforjed **ya no** parchea `MassUtility.GearMass`/`InventoryMass` (desactivado por L1). Sin coincidencia.
- **`HediffSet.CalculatePain` / `BleedRateTotal`** → VEF. Reforjed **no** parchea HediffSet (no conectado, L1). Sin coincidencia.
- **`WorkGiver_DoBill.TryFindBestBillIngredientsInSet_AllowMix`** y **`TryFindBestIngredientsInSet_NoMixHelper`** → VEF los parchea DIRECTAMENTE. Reforjed **ya no reescribe** `TryFindBestBillIngredientsInSet` (quitado por L2/M5), así que la cadena descrita en revisiones anteriores ya no existe: VEF parchea los métodos vanilla sin intermediarios.

### 2. Vehicle-Framework (`SmashPhil.VehicleFramework`)
- **`MassUtility.GearAndInventoryMass` / `Capacity` / `CanEverCarryAnything`** → VF los parchea (transpilers que ignoran mass de vehículos). Reforjed **ya no** parchea `GearMass`/`InventoryMass` (desactivado por L1). Sin coincidencia.
- **`WorldObjectsHolder.AddToCache/RemoveFromCache/Recache`** y **`WorldObjectsHolderTick`** → VF crea su propio sistema de cache de vehículos. Reforjed **ya no** reescribe `WorldObjectsHolder.Tick` (revertido por C1). Coexisten sin intermediarios.
- **`WorldPawns.GetSituation`** → VF. Reforjed no toca ese método. OK.

### 3. PickUpAndHaul (`Mehni.PickUpAndHaul`) — parcheo DINÁMICO
- **`WorkGiver_Haul.ShouldSkip`** → PUA postfix (skip corps) + **Reforjed: `WorkGiver_DoBill.ShouldSkip`** (distinto tipo). No chocan por tipo, pero **ambos optimizan el flujo de acarreo**. OK.
- **`JobGiver_Haul.TryGiveJob`** transpiler → PUA modifica el IL de hauling. Reforjed no toca `JobGiver_Haul`. OK.
- **`Pawn_InventoryTracker.Notify_ItemRemoved`** → PUA postfix. Reforjed no lo toca directamente. OK.
- **`ITab_Pawn_Gear.DrawThingRow`** transpiler → PUA. Reforjed no toca. OK.
- Reforjed **ya no** engancha `StoreUtility.TryFindBestBetterStoreCellForWorker` (desactivado por L1). OK.

### 4. Dubs Performance Analyzer (`Dubwise.DubsPerformanceAnalyzer`)
- **Parchea casi TODO para medir** (perfautofixer). Esto es OPTIMO con nuestro mod: **sus patches envuelven** nuestros métodos reescritos y cacheados sin romperlos. **Reforjed debe CARGAR ANTES** para que DP mida las versiones cacheadas (no las vanilla). Confirmar orden de carga: Reforjed *loadAfter* DP no necesario; basta coexistencia.
- **`object.GetType` / `Type.get_Assembly` / `Log.Error`** → transpilers de diagnóstico, inertes para nosotros.
- Conflictos de "ambos mods de rendimiento": DP usa *profiling only*, no altera comportamiento; Reforjed no mide, solo optimiza. **Coexisten y se complementan.**

### 5. CombatExtended (`CETeam.CombatExtended`) — VERIFICADO con la build cargable
- Trabaja con la build **cargable** en `Combat Extended\Assemblies\CombatExtended.dll` (1.3 MB, ~28/6/2026). Se descompiló por completo (2.6 MB de código) y se extrajeron **113** targets `[HarmonyPatch]`.
- **Solapamiento con la superficie del Reforjed: NINGUNO directo.**
  - `ListerThings.EverListable` → CE. Reforjed reescribe `ListerThings.Add/Remove/Contains/Clear/GetThingsOfType` (métodos distintos); `EverListable` no choca.
  - `MassUtility.Capacity` → CE (y VEF). Reforjed **ya no** parchea `MassUtility.GearMass`/`InventoryMass` (desactivado por L1).
  - `ThingDef.PostLoad` / `ThingDef.SpecialDisplayStats` → CE. Reforjed cachea `BaseMarketValue/BaseMass/BaseFlammability/BaseMaxHitPoints` (distintos métodos).
  - `Game.LoadGame`/`Game.ExposeData` → CE. Reforjed usa `Game.FinalizeInit` (hook distinto).
  - No toca `HediffSet.DirtyCache`, `WorkGiver_DoBill`, `GasGrid`, `StorageSettings`, `StoreUtility`, `WorldObjectsHolder`, `GridsUtility.GetItemCount`, `SlotGroup`.
- Las reescrituras de IL del Reforjed coexisten con los Harmony patches de CE (envuelven el método final). Compatibilidad total.

### 6. Dubs Mint Menus (`Dubwise.DubsMintMenus`)
- UI: `HealthCardUtility.DrawPawnHealthCard`, `Listing_TreeThingFilter`, `MainTabsRoot`, `BillStack.DoListing`, `ResourceReadout`, etc. **Ninguno coincide con la superficie del Reforjed.** Compatibilidad total.

### 7. RimHUD (`Jaxe.RimHUD`)
- UI pura: `InspectPaneUtility`, `ITab_PaneTopY`, `PlaySettings`, `ActiveTip`, `MemoryUtility`, `Game.FinalizeInit`, `MapInterface`, `LetterStack`. 
- **`Game.FinalizeInit` → RimHUD lo parchea + Reforjed lo parchea para vaciar caches.** Ambos postfix; Harmony los aplasta en orden. Coexisten sin conflicto (RimHUD no toca la lógica de caches).

### 8. Achtung! (`brrainz.achtung`)
- Trabajo/jobs/UI: `Toil`, `Pawn_WorkSettings.WorkIsActive`, `WorkGiver_Repair.HasJobOnThing`, `ForbidUtility`, `Pawn_JobTracker`, `TickManager.DoSingleTick`, `SelectionDrawer`, `ThingOverlays`, `GenConstruct.BlocksConstruction`, `HaulAIUtility.PawnCanAutomaticallyHaulFast`.
- **`GenConstruct.BlocksConstruction`** → Achtung. Reforjed no toca. OK.
- **`TickManager.DoSingleTick`** → Achtung (force jobs). Reforjed no patchea TickManager. OK.
- **`Pawn_JobTracker.EndCurrentJob/ShouldStartJobFromThinkTree`** → Achtung. Reforjed no. OK.
- Sin solapamiento con la superficie optimizada.

### 9. AllowTool (`UnlimitedHugs.AllowTool`)
- UI/designators: `DesignationCategoryDef.ResolveDesignators`, `Designator_PlantsCut`, `Pawn_DraftController.set_Drafted`, `Pawn.GetGizmos`, **`ThingWithComps.GetFloatMenuOptions`**, `Toils_Haul.PlaceHauledThingInCell`, `JobDriver_Wait.CheckForAutoAttack`, `Command.GizmoOnGUI`, `DefOfHelper.RebindAllDefOfs`, `ReverseDesignatorDatabase.InitDesignators`.
- **`ThingWithComps.GetFloatMenuOptions`** → postfix de AllowTool. Reforjed reescribe `ThingWithComps.GetComp` (método distinto). **No conflicto.**
- **`Pawn.GetGizmos`** → OK. Ninguno coincide con superficie directamente.

### 10. Character Editor (`void.charactereditor`)
- `MainMenuDrawer`, `Page_ConfigureStartingPawns`, `Map.FinalizeInit`, `Game.LoadGame`, `Gene.PostAdd/PostRemove`, `ShortHashGiver.GiveShortHash`, `ShaderUtility`.
- **`Map.FinalizeInit`/`Game.LoadGame`** → CE. Reforjed usa `Game.FinalizeInit` (final). No conflictivo (distintos hooks).
- Sin solapamiento directo con superficie.

### 11. MissileGirl (`vr.missilegirl`)
- **`HediffSet.DirtyCache`** → MissileGirl parchea + Reforjed parchea `HediffSet.DirtyCache` para marcar caché sucia. **COINCIDENCIA REAL**: ambos postfix sobre el mismo método. Coexisten (postfix en postfix), pero hay que **verificar orden/autoridad**. MissileGirl probablemente reindexa misiles; Reforjed marca su cache. No compiten (acciones diferentes), pero es un método compartido a vigilar.
- **`StatWorker.GetValueUnfinalized`** → MissileGirl transpiler (hijack stats). Reforjed no toca StatWorker (solo ThingDef.Base*). OK.
- **`Pawn.Destroy` / `Notify_Equipped`/etc** → Reforjed no. OK.

### 12. HugsLib (`UnlimitedHugs.HugsLib`)
- Framework: `Game.FillComponents`, `Game.FinalizeInit`, `Game.DeinitAndRemoveMap`, `Map.FinalizeInit`, `DebugWindowsOpener`, `ModsConfig.RestartFromChangedMods`, `Dialogs`, `UIRoot`.
- **`Game.FinalizeInit`** → HugsLib postfix `WorldLoadedHook` + Reforjed `Game.FinalizeInit` limpieza caches. Coexisten OK (acciones distintas).
- Completamente inerte respecto a nuestra superficie.

### 13. VanillaVehiclesExpanded
- Depende de Vehicle-Framework. Parchea `ReloadableUtility.OwnerOf`, `GhostDrawer`, `Frame.Destroy`, `MainTabWindow_Research`, `MemoryUtility`, `VehiclePawn`, `VehicleInfoCard`, `VehiclePathFollower.CostToPayThisTick`, `Pawn.ExposeData`, `CompRottable`.
- `Pawn.ExposeData` → Reforjed no. `MemoryUtility.UnloadUnusedUnityAssets` → Reforjed no. OK.

### 14. kNumbers (`koisama.numbers`, "Numbers" 0.16)
- **INERTE**: es el "Numbers" de koisama, target 0.16, DLL de 2017, sin patches para 1.6. **No carga en 1.6.** Solo se detecta por ensamblado "RWNumbers" (slide fallback). La capa de compat registra el mod como presente pero hace 0 (no hay superficie).

### 15. Performance - Slower Pawn Tick Rate (`Arkymn.SlowerPawnTickRate`)
- `Thing.get_MaxTickIntervalRate`/`get_UpdateRateTicks`, `WorldObject.get_UpdateRateTicks`, `JobDriver_Mine.ResetTicksToPickHit/DoDamage`.
- **`Thing.MaxTickIntervalRate`** → lento. Reforjed NO toca tick rates. **Ningún choque.** Atento: si reduce ticks de objetos, los counters de regiones/path pueden recalcularse menos; Reforjed usa caches invalidadas por eventos, no por tick, así que OK.

---

## 🌐 Estado de la implementación (capa de compatibilidad)

Se ha implementado una capa de compatibilidad **completa y opcional** en el Reforjed. Cada mod de la
carpeta se detecta individualmente por `packageId` (sin dependencias duras) y recibe su propio
toggle en la página de ajustes:

- **`Source/Compatibility/CompatMods.cs`** — detección por `packageId` (con fallback por nombre de
  ensamblado para kNumbers, cuyo About.xml es 0.16 y no declara packageId moderno).
- **`Source/Compatibility/CompatManager.cs`** — decide si un mod está activo Y habilitado; scanner
  de mods al arrancar que informa en el log.
- **`Source/Compatibility/Dialog_CompatStatus.cs`** — ventana de ajustes con una fila por mod:
  presencia `[detected]` + toggle de compatibilidad.
- **`PerformanceFishReforjedMod.cs`** — ajustes persistidos (`Scribe_Values`) y página de ajustes
  con master switch "Compatibility layer".

**Modelo de coexistencia (verificado, no asumido):**
Las reescrituras de IL del Reforjed sustituyen el cuerpo del método al cargar; los parches de
Harmony de terceros envuelven el método final. Por eso **coexisten por diseño**: la reescritura de
cuerpo y el Harmony patch no compiten. La capa de compatibilidad garantiza:
1. Conservar las firmas vanilla (nunca las cambia) → ningún mod se rompe.
2. No romper las cadenas de llamadas que otros mods esperan (verificado en los casos compartidos).
3. Poder desactivar los ajustes de un mod concreto si el usuario encuentra un caso particular.

**Casos de método compartido verificados (seguros):**
- `HediffSet.DirtyCache` — MissileGirl (postfix) + Reforjed **ya no** conecta HediffSet (no conectado, L1): solo queda MissileGirl sobre vanilla.
- `WorkGiver_DoBill.TryFindBestBillIngredientsInSet*` — VEF parchea `_AllowMix`/`_NoMixHelper`; Reforjed **ya no** reescribe `TryFindBestBillIngredientsInSet` (quitado por L2/M5): VEF parchea los métodos vanilla sin intermediarios.
- `MassUtility.GearMass`/`InventoryMass` — Reforjed **ya no** los parchea (desactivado por L1): Vehicle-Framework y VEF quedan sobre vanilla.
- `Game.FinalizeInit` — RimHUD, HugsLib y Reforjed lo postfixean con propósitos distintos. Se suman.

## RESUMEN DE DECISIONES DE COMPATIBILIDAD

| Mod | Coexiste | Acción requerida en Reforjed |
|---|---|---|
| VEF | Sí | Ninguna (métodos compartidos ya no se tocan tras L1/L2) |
| Vehicle-Framework | Sí | Ninguna (ídem) |
| PickUpAndHaul | Sí | Ninguna (métodos distintos) |
| Dubs PA | Sí | Cargar antes; Medir versiones cacheadas (mejora) |
| CombatExtended | Sí | Verificada con build cargable (113 patches, sin solapamiento directo) |
| Dubs Mint Menus | Sí | Ninguna |
| RimHUD | Sí | `Game.FinalizeInit` postfix apilado, OK |
| Achtung! | Sí | Ninguna |
| AllowTool | Sí | Ninguna |
| Character Editor | Sí | Ninguna |
| MissileGirl | Sí | `DefPreventingMothball` ya no se reescribe (4d16e7b); HediffSet no conectado |
| HugsLib | Sí | Ninguna |
| VanillaVehicles | Sí | Ninguna |
| kNumbers | N/A | Inerte (0.16) |
| SlowerPawnTickRate | Sí | Ninguna |