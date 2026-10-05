# Performance Fish Reforjed — Patch catalog / Catálogo de parches (RimWorld 1.6)

> **EN:** Point-by-point list of every patch adapted to **RimWorld 1.6.9655**, with its practical in-game effect.
> **ES:** Listado punto por punto de cada parche adaptado a **RimWorld 1.6.9655**, con su efecto en la práctica dentro del juego.

> **Estado de revisión (2026-10-05, Hallazgos):** tras el informe `2026-10-05-findings-for-author.es.md` se
> **revirtieron a vanilla** los grupos que estaban rotos (WorldObjectsHolder, GasGrid) y se **quitaron las
> reescrituras sin ganancia** (StoreUtilitySlotGroup, WorkGiver_DoBill, `ThingWithComps.GetComp<T>` y
> `HediffUtility.TryGetComp`). El catálogo de abajo refleja lo que la DLL aplica **hoy**.

**Total: 32 IL rewrites / reescrituras de IL** (verified by simulation against the real Assembly-CSharp; all `failed=0` in the prepatch stamp / verificados por simulación contra el Assembly-CSharp real; todos con `failed=0` en la marca de prepatching).

---

## A. IL prepatch — body rewrites / Prepatch de IL — reescritura de cuerpos (32)

**EN:** Applied BEFORE the game loads (via Prepatcher): the original method body is replaced by a direct call to the cached/optimized version. Zero cost on the hot path.
**ES:** Se aplican ANTES de cargar el juego (vía Prepatcher): el cuerpo del método original se sustituye por una llamada directa a la versión cacheada/optimizada. Coste cero en el camino caliente.

### A1. ThingDef stat cache — `DefStatCache` (4)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 1 | `ThingDef.BaseMarketValue` | **EN:** Market value is cached: opening an object/weapon panel no longer recomputes the stat each time. **ES:** El valor de mercado se cachea: abrir el panel de un objeto/arma no recalcula el stat cada vez. |
| 2 | `ThingDef.BaseMass` | **EN:** Def mass cached: hauling and weight calculations respond faster. **ES:** Masa del def cacheada: cálculos de acarreo y peso responden más rápido. |
| 3 | `ThingDef.BaseFlammability` | **EN:** Flammability cached: fire/thermal damage evaluates objects without recomputing. **ES:** Inflamabilidad cacheada: el fuego/daño térmico evalúa objetos sin recalcular. |
| 4 | `ThingDef.BaseMaxHitPoints` | **EN:** Max HP cached: damaging an object does not recompute the HP stat per hit. **ES:** PV máximos cacheados: al dañar un objeto no se recalcula el stat de PV cada golpe. |

### A2. Component cache — `GetCompCaching` (9)

**EN:** Two vanilla methods were **removed from this group** after review: `ThingWithComps.GetComp<T>` (1.6 already indexes comps by type, no gain) and `HediffUtility.TryGetComp<T>` (the only method of the group called from render worker threads, which wrote to the shared caches without a lock). Both behave exactly like vanilla now.
**ES:** Se **quitaron dos métodos** de este grupo tras la revisión: `ThingWithComps.GetComp<T>` (1.6 ya indexa los comps por tipo, sin ganancia) y `HediffUtility.TryGetComp<T>` (el único del grupo que se llamaba desde los hilos de trabajo del renderizado y escribía en las caches compartidas sin lock). Ambos se comportan exactamente como vanilla ahora.

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 5 | `ThingDef.GetCompProperties<T>` | **EN:** Comp properties of a def cached: crafting/inspecting objects does not scan comps. **ES:** Propiedades de comp de un def cacheadas: fabricar/inspeccionar objetos no recorre comps. |
| 6 | `ThingDef.HasComp(Type)` | **EN:** "Does it have this comp?" answers from a per-type cache. **ES:** "¿Tiene este comp?" responde desde una caché por tipo. |
| 7 | `ThingDef.HasComp<T>` (generic/genérico) | **EN:** Same for generic type, with one table **per T** (each `HasComp<X>()` can never leak into `HasComp<Y>()`). **ES:** Ídem por tipo genérico, con una tabla **por T** (cada `HasComp<X>()` nunca se filtra a `HasComp<Y>()`). |
| 8 | `HediffDef.CompProps<T>` | **EN:** Comp props of a wound cached. **ES:** Props de comp de una herida cacheados. |
| 9 | `Ability.CompOfType<T>` | **EN:** Ability comps (psylink, etc.) without scans. **ES:** Comps de habilidades (psylink, etc.) sin recorridos. |
| 10 | `WorldObject.GetComponent<T>` | **EN:** World object comps (settlements, etc.) cached. **ES:** Comps de objetos de mundo (aldeas, etc.) cacheados. |
| 11 | `Map.GetComponent<T>` | **EN:** Map comps (events, etc.) cached: less lag when querying the map. **ES:** Comps de mapa (eventos, etc.) cacheados: menos lag al consultar el mapa. |
| 12 | `World.GetComponent<T>` | **EN:** World comps cached. **ES:** Comps de mundo cacheados. |
| 13 | `Game.GetComponent<T>` | **EN:** Game comps (mods, etc.) cached. **ES:** Comps de juego (modos, etc.) cacheados. |

### A3. Thing lists — `ListerThings` (6)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 14 | `ListerThings.Add(Thing)` | **EN:** Keeps the per-def/per-group indexes in sync when registering a thing. **ES:** Mantiene sincronizados los índices por def/grupo al registrar una cosa. |
| 15 | `ListerThings.Remove(Thing)` | **EN:** Same when removing it. **ES:** Ídem al quitarla. |
| 16 | `ListerThings.Contains(Thing)` | **EN:** "Does this thing exist on the map?" answers from the index, without scanning lists. **ES:** "¿Existe esta cosa en el mapa?" se responde desde el índice, sin recorrer listas. |
| 17 | `ListerThings.Clear()` | **EN:** Empties the caches along with the lists (they survive Clear). **ES:** Vacía las caches junto con las listas (superviven a Clear). |
| 18 | `ListerThings.GetThingsOfType<T>()` | **EN:** All things of a type come from the per-type index (construction, cleaning). **ES:** Todas las cosas de un tipo se devuelven desde el índice por tipo (construcción, limpieza). |
| 19 | `ListerThings.GetThingsOfType<T>(List)` | **EN:** Same, filling an existing list. **ES:** Ídem rellenando una lista existente. |

### A4. Colonist buildings — `ListerBuildings` (7)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 20 | `AllBuildingsColonistOfDef(def)` | **EN:** "How many generators do I have?" answers from an index, without scanning everything. **ES:** "¿Cuántos generadores tengo?" responde desde un índice, sin recorrer todo. |
| 21 | `ColonistsHaveBuilding(def)` | **EN:** "Do I have a workbench X?" queries the index: faster colony management. **ES:** "¿Tengo alguna mesa de trabajo X?" consulta al índice: más rápido al gestionar colonia. |
| 22 | `ColonistsHaveResearchBench()` | **EN:** "Is there a research bench?" answers instantly (research alert). **ES:** "¿Hay banco de investigación?" responde inmediato (aviso de investigación). |
| 23 | `ColonistsHaveBuildingWithPowerOn(def)` | **EN:** "Is there an X with power on?" without scanning buildings. **ES:** "¿Hay un X con electricidad encendida?" sin recorrer edificios. |
| 24 | `AllBuildingsColonistOfClass<T>()` | **EN:** Buildings by class returned from the index. **ES:** Edificios por clase devueltos desde índice. |
| 25 | `AllColonistBuildingsOfType(ThingDef)` | **EN:** Same by def. **ES:** Ídem por def. |
| 26 | `AllBuildingsNonColonistOfDef(def)` | **EN:** NON-colonist buildings by def from index (ruins, enemies). **ES:** Edificios NO colonos por def desde índice (ruinas, enemigos). |

### A5. Per-cell counter — `GridsUtility` (1)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 27 | `GridsUtility.GetItemCount(IntVec3, Map)` | **EN:** Item count in a cell is read from a maintained counter, without scanning the cell's list: **smoother hauling**, as it is called once per candidate cell when searching for a storage spot. **ES:** El número de objetos en una celda se lee de un contador mantenido, sin recorrer la lista de la celda: **acarreo más fluido**, ya que se llama una vez por celda candidata al buscar sitio de almacén. |

### A6. Storage settings — `StorageSettings` (2)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 28 | `StorageSettings.AllowedToAccept(Thing)` | **EN:** "Does this storage accept this thing?" answers from a **cache keyed by the thing's ID**, without recomputing filters. **ES:** "¿Este almacén acepta esta cosa?" se responde desde una **caché por ID de la cosa**, sin recomputar filtros. |
| 29 | `StorageSettings.AllowedToAccept(ThingDef)` | **EN:** Same by def, in a **separate cache** (def hashes can never collide with thing IDs). **ES:** Ídem por def, en una **caché separada** (los hashes de defs nunca colisionan con IDs de cosas). |

### A7. Rooms — `Room` (1)

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 30 | `Room.ContainedBeds` | **EN:** Room beds are listed from the bed index instead of scanning regions, **de-duplicated** (each bed listed once even if its cells touch the room several ways). **ES:** Las camas de una habitación se listan desde el índice de camas en vez de recorrer regiones, **sin duplicados** (cada cama se lista una vez aunque sus celdas toquen la habitación de varias formas). |

### A8. World pawns — `WorldPawns` (2)

**EN:** `DefPreventingMothball` is NOT rewritten (MissileGirl transpiles it; the rewrite was removed in `4d16e7b`).
**ES:** `DefPreventingMothball` NO se reescribe (MissileGirl lo transpila; la reescritura se quitó en `4d16e7b`).

| # | Vanilla method replaced / Método vanilla sustituido | Practical effect / Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 31 | `WorldPawns.AllPawnsAlive` | **EN:** Cached list of alive world pawns: the world tab responds faster. **ES:** Lista de pawns vivos del mundo cacheada: la pestaña de mundo responde más rápido. |
| 32 | `WorldPawns.AllPawnsAliveOrDead` | **EN:** Same including dead, with **independent cache versions per list** (each list refreshes on its own changes). **ES:** Ídem incluyendo muertos, con **versiones de caché independientes por lista** (cada lista se refresca con sus propios cambios). |

---

## B. Runtime patches — Harmony (cache support / soporte de las caches)

**EN:** Do not rewrite IL: they hook at startup to **keep the caches coherent** and clear them between games.
**ES:** No reescriben IL: se enganchan al arrancar para **mantener coherentes** las caches y vaciarlas entre partidas.

| # | Patch / Parche | Estado | Practical effect / Qué hace en la práctica |
|---|--------|--------|--------------------------|
| H1 | `Game.FinalizeInit` (postfix) | **Activo** | **EN:** Clears all caches when switching games: never returns data from a previous game. **ES:** Vacía todas las caches al cambiar de partida: nunca devuelve datos de una partida anterior. |
| H2 | `ThingGrid.RegisterInCell` / `DeregisterInCell` | **Activo** | **EN:** Keeps the per-cell counter (A5) in sync when things spawn/despawn. **ES:** Mantiene el contador por celda (A5) sincronizado al aparecer/desaparecer objetos. |
| H3 | `ListerBuildings` (several postfixes) | **Activo** | **EN:** Keeps the building indexes (A4) in sync when buildings are added/removed. **ES:** Mantiene los índices de edificios (A4) al añadir/quitar edificios. |
| H4 | `Building` spawn/despawn | **Activo** | **EN:** Building indexes correct when building/deconstructing. **ES:** Índices de edificios correctos al construir/deconstruir. |
| H5 | `StorageSettings` invalidation | **Activo** | **EN:** Clears the AllowedToAccept caches when a storage filter changes. **ES:** Vacía las caches de AllowedToAccept al cambiar un filtro de almacén. |
| H6 | `SlotGroup` / `StorageGroup` capacities | **Desactivado (L1)** | **EN:** Would have kept the capacity heuristics coherent, but the heuristics were wrong (3 objects/cell ignores building limits; `StorageGroup.members is SlotGroup` is never true). Left as vanilla. **ES:** Habrían mantenido coherentes los heurísticos de capacidad, pero eran incorrectos (3 objetos/celda ignora los límites de los edificios; `StorageGroup.members is SlotGroup` nunca es true). Se deja como vanilla. |
| H7 | `MassUtility.GearMass` / `InventoryMass` | **Desactivado (L1)** | **EN:** Gear/inventory mass cache shared a single timestamp between two different TTLs, so one family could read the other's stale value. Left as vanilla. **ES:** La caché de masa compartía una única marca de tiempo entre dos TTL distintos, así que una familia podía leer el valor viejo de la otra. Se deja como vanilla. |
| H8 | `GasGrid.ExposeData` | **Desactivado (C2/H1)** | **EN:** Gas is fully vanilla now; there is no parallel grid to persist. **ES:** El gas es íntegramente vanilla; no hay grid paralelo que persistir. |
| H9 | `StoreUtility.TryFindBestBetterStoreCellForWorker` (capacity shortcut) | **Desactivado (L1)** | **EN:** The shortcut could stop hauling to cells that have real space when the heuristic said "full". Left as vanilla. **ES:** El atajo podía dejar de acarrear a celdas con sitio real cuando el heurístico decía "lleno". Se deja como vanilla. |
| H10 | `HediffSet` queries | **No conectado** | **EN:** Dead `[HarmonyPatch]` code: nothing calls `PatchAll`, so it never runs. **ES:** Código muerto `[HarmonyPatch]`: nada llama a `PatchAll`, así que nunca se ejecuta. |
| H11 | `Room.Role` / `Room.Owners` | **No conectado** | **EN:** Dead `[HarmonyPatch]` code: never runs. **ES:** Código muerto `[HarmonyPatch]`: nunca se ejecuta. |
| H12 | `ThingOwner<T>` | **No conectado** | **EN:** Dead `[HarmonyPatch]` code: never runs (the `Remove` postfix is written correctly and is safe to wire later). **ES:** Código muerto `[HarmonyPatch]`: nunca se ejecuta (el postfix de `Remove` está escrito correctamente y es seguro conectarlo más adelante). |

---

## Summary / Resumen

| Block / Bloque | Interventions / Intervenciones |
|--------|---------|
| A. IL prepatch | 32 |
| B. Runtime Harmony (activos) | 5 |
| **Total activo** | **37** |

**Verification / Verificación:** the prepatch stamp in the game log must show
`gettersRewritten=4, compPatches=9/9, listerPatches=6/6, buildingPatches=7/7, gridPatches=1/1,
storagePatches=2/2, slotGroupPatches=0/0, roomPatches=1/1, worldPawnsPatches=2/2,
worldObjectsHolderPatches=0/0, gasGridPatches=0/0, workGiverPatches=0/0` and **no `Failed` above 0**.
