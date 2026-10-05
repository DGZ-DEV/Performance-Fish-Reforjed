# Performance Fish Reforjed — Patch catalog / Catálogo de parches (RimWorld 1.6)

> **EN:** Point-by-point list of every patch adapted to **RimWorld 1.6.9655**, with its practical in-game effect.
> **ES:** Listado punto por punto de cada parche adaptado a **RimWorld 1.6.9655**, con su efecto en la práctica dentro del juego.

**Total: 53 IL rewrites / reescrituras de IL** (verified by simulation against the real Assembly-CSharp; all `failed=0` in the prepatch stamp / verificados por simulación contra el Assembly-CSharp real; todos con `failed=0` en la marca de prepatching).

---

## A. IL prepatch — body rewrites / Prepatch de IL — reescritura de cuerpos (53)

**EN:** Applied BEFORE the game loads (via Prepatcher): the original method body is replaced by a direct call to the cached/optimized version. Zero cost on the hot path.
**ES:** Se aplican ANTES de cargar el juego (vía Prepatcher): el cuerpo del método original se sustituye por una llamada directa a la versión cacheada/optimizada. Coste cero en el camino caliente.

### A1. ThingDef stat cache — `DefStatCache` (4)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 1 | `ThingDef.BaseMarketValue` | **EN:** Market value is cached: opening an object/weapon panel no longer recomputes the stat each time. **ES:** El valor de mercado se cachea: abrir el panel de un objeto/arma no recalcula el stat cada vez. |
| 2 | `ThingDef.BaseMass` | **EN:** Def mass cached: hauling and weight calculations respond faster. **ES:** Masa del def cacheada: cálculos de acarreo y peso responden más rápido. |
| 3 | `ThingDef.BaseFlammability` | **EN:** Flammability cached: fire/thermal damage evaluates objects without recomputing. **ES:** Inflamabilidad cacheada: el fuego/daño térmico evalúa objetos sin recalcular. |
| 4 | `ThingDef.BaseMaxHitPoints` | **EN:** Max HP cached: damaging an object does not recompute the HP stat per hit. **ES:** PV máximos cacheados: al dañar un objeto no se recalcula el stat de PV cada golpe. |

### A2. Component cache — `GetCompCaching` (11)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 5 | `ThingWithComps.GetComp<T>` | **EN:** Getting a component (power, food comp…) stops scanning the list: any comp query is cheaper. **ES:** Obtener un componente (generador, comp de comida…) deja de recorrer la lista: cualquier consulta de comps es más barata. |
| 6 | `ThingDef.GetCompProperties<T>` | **EN:** Comp properties of a def cached: crafting/inspecting objects does not scan comps. **ES:** Propiedades de comp de un def cacheadas: fabricar/inspeccionar objetos no recorre comps. |
| 7 | `ThingDef.HasComp(Type)` | **EN:** "Does it have this comp?" answers from a per-type cache. **ES:** "¿Tiene este comp?" responde desde una caché por tipo. |
| 8 | `ThingDef.HasComp<T>` (generic/genérico) | **EN:** Same for generic type. **ES:** Ídem por tipo genérico. |
| 9 | `HediffUtility.TryGetComp<T>` | **EN:** Hediff comps (wounds with comps) answer without scanning the list. **ES:** Comps de hediffs (heridas con comps) responden sin recorrer la lista. |
| 10 | `HediffDef.CompProps<T>` | **EN:** Comp props of a wound cached. **ES:** Props de comp de una herida cacheados. |
| 11 | `Ability.CompOfType<T>` | **EN:** Ability comps (psylink, etc.) without scans. **ES:** Comps de habilidades (psylink, etc.) sin recorridos. |
| 12 | `WorldObject.GetComponent<T>` | **EN:** World object comps (settlements, etc.) cached. **ES:** Comps de objetos de mundo (aldeas, etc.) cacheados. |
| 13 | `Map.GetComponent<T>` | **EN:** Map comps (events, etc.) cached: less lag when querying the map. **ES:** Comps de mapa (eventos, etc.) cacheados: menos lag al consultar el mapa. |
| 14 | `World.GetComponent<T>` | **EN:** World comps cached. **ES:** Comps de mundo cacheados. |
| 15 | `Game.GetComponent<T>` | **EN:** Game comps (mods, etc.) cached. **ES:** Comps de juego (modos, etc.) cacheados. |

### A3. Thing lists — `ListerThings` (6)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 16 | `ListerThings.Add(Thing)` | **EN:** Keeps the per-def/per-group indexes in sync when registering a thing. **ES:** Mantiene sincronizados los índices por def/grupo al registrar una cosa. |
| 17 | `ListerThings.Remove(Thing)` | **EN:** Same when removing it. **ES:** Ídem al quitarla. |
| 18 | `ListerThings.Contains(Thing)` | **EN:** "Does this thing exist on the map?" answers from the index, without scanning lists. **ES:** "¿Existe esta cosa en el mapa?" se responde desde el índice, sin recorrer listas. |
| 19 | `ListerThings.Clear()` | **EN:** Empties the caches along with the lists (they survive Clear). **ES:** Vacía las caches junto con las listas (superviven a Clear). |
| 20 | `ListerThings.GetThingsOfType<T>()` | **EN:** All things of a type come from the per-type index (construction, cleaning). **ES:** Todas las cosas de un tipo se devuelven desde el índice por tipo (construcción, limpieza). |
| 21 | `ListerThings.GetThingsOfType<T>(List)` | **EN:** Same, filling an existing list. **ES:** Ídem rellenando una lista existente. |

### A4. Colonist buildings — `ListerBuildings` (7)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 22 | `AllBuildingsColonistOfDef(def)` | **EN:** "How many generators do I have?" answers from an index, without scanning everything. **ES:** "¿Cuántos generadores tengo?" responde desde un índice, sin recorrer todo. |
| 23 | `ColonistsHaveBuilding(def)` | **EN:** "Do I have a workbench X?" queries the index: faster colony management. **ES:** "¿Tengo alguna mesa de trabajo X?" consulta al índice: más rápido al gestionar colonia. |
| 24 | `ColonistsHaveResearchBench()` | **EN:** "Is there a research bench?" answers instantly (research alert). **ES:** "¿Hay banco de investigación?" responde inmediato (aviso de investigación). |
| 25 | `ColonistsHaveBuildingWithPowerOn(def)` | **EN:** "Is there an X with power on?" without scanning buildings. **ES:** "¿Hay un X con electricidad encendida?" sin recorrer edificios. |
| 26 | `AllBuildingsColonistOfClass<T>()` | **EN:** Buildings by class returned from the index. **ES:** Edificios por clase devueltos desde índice. |
| 27 | `AllColonistBuildingsOfType(ThingDef)` | **EN:** Same by def. **ES:** Ídem por def. |
| 28 | `AllBuildingsNonColonistOfDef(def)` | **EN:** NON-colonist buildings by def from index (ruins, enemies). **ES:** Edificios NO colonos por def desde índice (ruinas, enemigos). |

### A5. Per-cell counter — `GridsUtility` (1)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 29 | `GridsUtility.GetItemCount(IntVec3, Map)` | **EN:** Item count in a cell is read from a maintained counter, without scanning the cell's list: **smoother hauling**, as it is called once per candidate cell when searching for a storage spot. **ES:** El número de objetos en una celda se lee de un contador mantenido, sin recorrer la lista de la celda: **acarreo más fluido**, ya que se llama una vez por celda candidata al buscar sitio de almacén. |

### A6. Storage settings — `StorageSettings` (2)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 30 | `StorageSettings.AllowedToAccept(Thing)` | **EN:** "Does this storage accept this thing?" answers from a per-def cache, without recomputing filters. **ES:** "¿Este almacén acepta esta cosa?" se responde desde una caché por def, sin recomputar filtros. |
| 31 | `StorageSettings.AllowedToAccept(ThingDef)` | **EN:** Same by def: hauling many objects, the cell decision is cheaper. **ES:** Ídem por def: al acarrear muchos objetos, la decisión de celda es más barata. |

### A7. SlotGroups — `StoreUtilitySlotGroup` (3)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 32–34 | SlotGroup capacities and membership | **EN:** Storage cells know their capacity without rescanning: better mass hauling and faster storage menu opening. **ES:** Las celdas de almacén saben su capacidad sin recorrer cada vez: mejora el acarreo masivo y la apertura de menús de almacenamiento. |

### A8. Rooms — `Room` (1)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 35 | `Room.ContainedBeds` | **EN:** Room beds are listed from the bed index instead of scanning regions: faster when viewing bed assignment/dormitories. **ES:** Las camas de una habitación se listan desde el índice de camas en vez de recorrer regiones: al ver "asignación de camas" o dormitorios, respuesta más rápida. |

### A9. World pawns — `WorldPawns` (3)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 36 | `WorldPawns.AllPawnsAlive` | **EN:** Cached list of alive world pawns: the world tab responds faster. **ES:** Lista de pawns vivos del mundo cacheada: la pestaña de mundo responde más rápido. |
| 37 | `WorldPawns.AllPawnsAliveOrDead` | **EN:** Same including dead: history/caravans smoother. **ES:** Ídem incluyendo muertos: historia/caravanas más fluida. |
| 38 | `WorldPawns.DefPreventingMothball(Pawn)` | **EN:** "Can this pawn not be mothballed?" is cached: fewer computations when pausing caravans. **ES:** "¿Este pawn no puede congelarse?" se cachea: menos cálculos al pausar caravanas. |

### A10. World objects — `WorldObjectsHolder` (1)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 39 | `WorldObjectsHolder.WorldObjectsHolderTick()` | **EN:** World object tick uses the cache instead of scanning the whole list every tick. **ES:** El tick de objetos de mundo usa la caché en vez de recorrer toda la lista cada tick. |

### A11. Gas — `GasGrid` (6)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 40 | `GasGrid.Tick` | **EN:** Gas (toxic, smoke, rot stink) advances with parallel 64-bit grids, without per-cell recomputation: **smoother gas combat**. **ES:** El gas (tóxico, humo, hedor) avanza con grids paralelos de 64 bits, sin recalcular por celda: **combates con gas más fluidos**. |
| 41 | `GasGrid.AddGas` | **EN:** Adding gas uses the parallel grid: less lag when throwing gas grenades. **ES:** Añadir gas usa el grid paralelo: menos lag al lanzar granadas de gas. |
| 42 | `GasGrid.AnyGasAt` | **EN:** "Is there gas here?" answers from the bit grid, without scanning cells. **ES:** "¿Hay gas aquí?" responde desde el grid de bits, sin recorrer celdas. |
| 43 | `GasGrid.DensityAt` | **EN:** Gas density queried instantly (gas damage, filters). **ES:** Densidad de gas consultada al instante (daño por gas, filtros). |
| 44 | `GasGrid.Debug_ClearAll` | **EN:** Debug clearing synced with the grid (no cost in normal games). **ES:** Limpieza de depuración sincronizada con el grid (sin coste en partida normal). |
| 45 | `GasGrid.Notify_ThingSpawned` | **EN:** Grids stay coherent when things spawn. **ES:** Al aparecer cosas, los grids se mantienen coherentes. |

### A12. Production work — `WorkGiver_DoBill` (8)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 46 | `WorkGiver_DoBill.get_PotentialWorkThingRequest` | **EN:** The potential work request is **cached per instance** (constant per WorkGiverDef): each work scan no longer recomputes it. **ES:** La petición de trabajo potencial se **cachea por instancia** (constante por WorkGiverDef): cada escaneo de trabajo no la recalcula. |
| 47 | `WorkGiver_DoBill.MaxPathDanger(Pawn)` | **EN:** Returns `Danger.Some` directly like vanilla, without the redundant body. **ES:** Devuelve directamente `Danger.Some` como el vanilla, sin el cuerpo redundante. |
| 48 | `WorkGiver_DoBill.ThingIsUsableBillGiver(Thing)` | **EN:** Deciding whether a thing is a bill giver (bench/table) is cheaper: production and cooking respond sooner. **ES:** Decidir si una cosa es "bill giver" (mesa/banco) es más barato: producción y cocina responden antes. |
| 49 | `WorkGiver_DoBill.ShouldSkip(Pawn, bool)` | **EN:** Cuts the bill-giver scan *before* scanning everything when there is nothing to do: **less stutter when switching tasks**. **ES:** Corta el barrido de bill givers *antes* de recorrer todo si no hay nada que hacer: **menos tartamudeo al cambiar de tarea**. |
| 50 | `WorkGiver_DoBill.IsUsableIngredient(Thing, Bill)` | **EN:** Filtering recipe ingredients without extra layers: cooking/crafting picks the ingredient faster. **ES:** Filtrar ingredientes de una receta sin capas extra: cocinar/fabricar elige ingrediente más rápido. |
| 51 | `WorkGiver_DoBill.GetBillGiverRootCell(Thing, Pawn)` | **EN:** Bill giver interaction cell computed directly: the colonist picks the right bench without extra cost. **ES:** Celda de interacción del bill giver calculada directa: el colono elige el banco correcto sin coste extra. |
| 52 | `WorkGiver_DoBill.GetMedicalCareCategory(Thing)` | **EN:** Medical category (Normal/Herbal/Best) resolved directly: tending wounded in combat responds sooner. **ES:** Categoría médica (Normal/Herbal/Best) resuelta directa: curar a heridos en combate responde antes. |
| 53 | `WorkGiver_DoBill.TryFindBestBillIngredientsInSet(...)` | **EN:** The "mix or not mix ingredients" decision dispatches straight to AllowMix/NoMix: cooking batches without overhead. **ES:** La decisión "mezclar o no ingredientes" despacha directo a AllowMix/NoMix: cocinar lotes sin sobrecarga. |

---

## B. Runtime patches — Harmony (cache support / soporte de las caches)

**EN:** Do not rewrite IL: they hook at startup to **keep the caches coherent** and clear them between games.
**ES:** No reescriben IL: se enganchan al arrancar para **mantener coherentes** las caches y vaciarlas entre partidas.

| # | Patch / Parche | Practical effect / Qué hace en la práctica |
|---|--------|--------------------------|
| H1 | `Game.FinalizeInit` (postfix) | **EN:** Clears all caches when switching games: never returns data from a previous game. **ES:** Vacía todas las caches al cambiar de partida: nunca devuelve datos de una partida anterior. |
| H2 | `ThingGrid.RegisterInCell` / `DeregisterInCell` | **EN:** Keeps the per-cell counter (A5) in sync when things spawn/despawn. **ES:** Mantiene el contador por celda (A5) sincronizado al aparecer/desaparecer objetos. |
| H3 | `ListerBuildings` (several postfixes) | **EN:** Keeps the building indexes (A4) in sync when buildings are added/removed. **ES:** Mantiene los índices de edificios (A4) al añadir/quitar edificios. |
| H4 | `Building` spawn/despawn | **EN:** Building indexes correct when building/deconstructing. **ES:** Índices de edificios correctos al construir/deconstruir. |
| H5 | `SlotGroup.Notify_AddedCell` / `Notify_LostCell` | **EN:** SlotGroup capacities (A7) when storage cells are added/removed. **ES:** Capacidades de SlotGroup (A7) al añadir/quitar celdas de almacén. |
| H6 | `StorageGroup.RemoveMember` / `Notify_SettingsChanged` | **EN:** Storage group capacities when members/settings change. **ES:** Capacidades de grupo de almacenamiento al cambiar miembros/ajustes. |
| H7 | `MassUtility.GearMass` / `InventoryMass` | **EN:** Gear and inventory mass cached: the load panel responds instantly. **ES:** Masa del equipo e inventario cacheadas: el panel de carga responde al instante. |
| H8 | `GasGrid.ExposeData` | **EN:** Gas persistence when saving/loading with gas on the map. **ES:** Persistencia del gas al guardar/cargar con gas presente en el mapa. |
| H9 | `StoreUtility.TryFindBestBetterStoreCellForWorker` | **EN:** Storage cell search with known capacity: **smoother mass hauling**. **ES:** Búsqueda de celda de almacén con capacidad conocida: **acarreo masivo más fluido**. |
| H10 | `HediffSet.GetFirstHediffOfDef` / `HasHediff` / `GetNotMissingParts` / `DirtyCache` | **EN:** Wound queries cached and marked dirty on change: medicine and diagnosis respond sooner. **ES:** Consultas de heridas cacheadas y marcadas como sucias al cambiar: medicina y diagnóstico responden antes. |
| H11 | `Room.Role` / `Room.Owners` | **EN:** Room role and owners cached (only when the room is stable). **ES:** Rol y propietarios de habitación cacheados (solo si la habitación es estable). |
| H12 | `ThingOwner<T>.ExposeData` / `TryAdd` / `Remove` | **EN:** Content indexes (inventories, bags) kept in sync. **ES:** Índices de contenidos (inventarios, bolsas) mantenidos sincronizados. |

---

## Summary / Resumen

| Block / Bloque | Interventions / Intervenciones |
|--------|---------|
| A. IL prepatch | 53 |
| B. Runtime Harmony | 12 |
| **Total** | **65** |

**Verification / Verificación:** the prepatch stamp in the game log must show
`compPatches=11/11, listerPatches=6/6, buildingPatches=7/7, gridPatches=1/1, storagePatches=2/2,
slotGroupPatches=3/3, roomPatches=1/1, worldPawnsPatches=3/3, worldObjectsHolderPatches=1/1,
gasGridPatches=6/6, workGiverPatches=8/8, gettersRewritten=4` and **no `Failed` above 0**.
