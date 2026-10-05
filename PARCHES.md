# Performance Fish Reforjed — Catálogo de parches (RimWorld 1.6)

Listado punto por punto de cada parche adaptado a **RimWorld 1.6.9655**, con su efecto en la
práctica dentro del juego.

**Total: 53 parches** (verificados por simulación contra el Assembly-CSharp real; todos con
`failed=0` en la marca de prepatching).

---

## A. Prepatch de IL — reescritura de cuerpos (53)

Se aplican ANTES de cargar el juego (via Prepatcher): el cuerpo del método original se sustituye
por una llamada directa a la versión cacheada/optimizada. Coste cero en el camino caliente.

### A1. Caché de stats de ThingDef — `DefStatCache` (4)

| # | Método vanilla sustituido | Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 1 | `ThingDef.BaseMarketValue` | Valor de mercado del def se cachea: abrir el panel de un objeto/arma no recalcula el stat cada vez. |
| 2 | `ThingDef.BaseMass` | Masa del def cacheada: cálculos de acarreo y peso responden más rápido. |
| 3 | `ThingDef.BaseFlammability` | Inflamabilidad cacheada: el fuego/daño térmico evalúa objetos sin recalcular. |
| 4 | `ThingDef.BaseMaxHitPoints` | PV máximos cacheados: al dañar un objeto no se recalcula el stat de PV cada golpe. |

### A2. Caché de componentes — `GetCompCaching` (11)

| # | Método vanilla sustituido | Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 5 | `ThingWithComps.GetComp<T>` | Obtener un componente (generador, comp de comida…) deja de recorrer la lista: cualquier consulta de comps es más barata. |
| 6 | `ThingDef.GetCompProperties<T>` | Propiedades de comp de un def cacheadas: fabricar/inspeccionar objetos no recorre comps. |
| 7 | `ThingDef.HasComp(Type)` | "¿Tiene este comp?" responde desde una caché por tipo. |
| 8 | `ThingDef.HasComp<T>` (genérico) | Ídem por tipo genérico. |
| 9 | `HediffUtility.TryGetComp<T>` | Comps de hediffs (heridas con comps) responden sin recorrer la lista. |
| 10 | `HediffDef.CompProps<T>` | Props de comp de una herida cacheados. |
| 11 | `Ability.CompOfType<T>` | Comps de habilidades (psylink, etc.) sin recorridos. |
| 12 | `WorldObject.GetComponent<T>` | Comps de objetos de mundo (aldeas, etc.) cacheados. |
| 13 | `Map.GetComponent<T>` | Comps de mapa (eventos, etc.) cacheados: menos lag al consultar el mapa. |
| 14 | `World.GetComponent<T>` | Comps de mundo cacheados. |
| 15 | `Game.GetComponent<T>` | Comps de juego (modos, etc.) cacheados. |

### A3. Listas de cosas — `ListerThings` (6)

| # | Método vanilla sustituido | Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 16 | `ListerThings.Add(Thing)` | Mantiene sincronizados los índices por def/grupo al registrar una cosa. |
| 17 | `ListerThings.Remove(Thing)` | Ídem al quitarla. |
| 18 | `ListerThings.Contains(Thing)` | "¿Existe esta cosa en el mapa?" se responde desde el índice, sin recorrer listas. |
| 19 | `ListerThings.Clear()` | Vacía las caches junto con las listas (superviven a Clear). |
| 20 | `ListerThings.GetThingsOfType<T>()` | Todas las cosas de un tipo se devuelven desde el índice por tipo (construcción, limpieza). |
| 21 | `ListerThings.GetThingsOfType<T>(List)` | Ídem rellenando una lista existente. |

### A4. Edificios colonos — `ListerBuildings` (7)

| # | Método vanilla sustituido | Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 22 | `AllBuildingsColonistOfDef(def)` | "¿Cuántos generadores tengo?" responde desde un índice, sin recorrer todo. |
| 23 | `ColonistsHaveBuilding(def)` | "¿Tengo alguna mesa de trabajo X?" consulta al índice: más rápido al gestionar colonia. |
| 24 | `ColonistsHaveResearchBench()` | "¿Hay banco de investigación?" responde inmediato (aviso de investigación). |
| 25 | `ColonistsHaveBuildingWithPowerOn(def)` | "¿Hay un X con electricidad encendida?" sin recorrer edificios. |
| 26 | `AllBuildingsColonistOfClass<T>()` | Edificios por clase devueltos desde índice. |
| 27 | `AllColonistBuildingsOfType(ThingDef)` | Ídem por def. |
| 28 | `AllBuildingsNonColonistOfDef(def)` | Edificios NO colonos por def desde índice (ruinas, enemigos). |

### A5. Contador por celda — `GridsUtility` (1)

| # | Método vanilla sustituido | Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 29 | `GridsUtility.GetItemCount(IntVec3, Map)` | Número de objetos en una celda se lee de un contador mantenido, sin recorrer la lista de la celda: **acarreo (hauling) más fluido**, ya que se llama una vez por celda candidata al buscar sitio de almacén. |

### A6. Ajustes de almacenamiento — `StorageSettings` (2)

| # | Método vanilla sustituido | Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 30 | `StorageSettings.AllowedToAccept(Thing)` | "¿Este almacén acepta esta cosa?" se responde desde una caché por def, sin recomputar filtros. |
| 31 | `StorageSettings.AllowedToAccept(ThingDef)` | Ídem por def: al acarrear muchos objetos, la decisión de celda es más barata. |

### A7. SlotGroups — `StoreUtilitySlotGroup` (3)

| # | Método vanilla sustituido | Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 32–34 | Capacidades y pertenencia de SlotGroup | Las celdas de almacén saben su capacidad sin recorrer cada vez: mejora el acarreo masivo y la apertura de menús de almacenamiento. |

### A8. Habitaciones — `Room` (1)

| # | Método vanilla sustituido | Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 35 | `Room.ContainedBeds` | Camas de una habitación se listan desde el índice de camas en vez de recorrer regiones: al ver "asignación de camas" o dormitorios, respuesta más rápida. |

### A9. Pawns de mundo — `WorldPawns` (3)

| # | Método vanilla sustituido | Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 36 | `WorldPawns.AllPawnsAlive` | Lista de pawns vivos del mundo cacheada: la pestaña de mundo responde más rápido. |
| 37 | `WorldPawns.AllPawnsAliveOrDead` | Ídem incluyendo muertos: historia/caravanas más fluida. |
| 38 | `WorldPawns.DefPreventingMothball(Pawn)` | "¿Este pawn no puede congelarse?" se cachea: menos cálculos al pausar caravanas. |

### A10. Objetos de mundo — `WorldObjectsHolder` (1)

| # | Método vanilla sustituido | Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 39 | `WorldObjectsHolder.WorldObjectsHolderTick()` | El tick de objetos de mundo usa la caché en vez de recorrer toda la lista cada tick. |

### A11. Gas — `GasGrid` (6)

| # | Método vanilla sustituido | Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 40 | `GasGrid.Tick` | El gas (tóxico, humo, hedor) avanza con grids paralelos de 64 bits, sin recalcular por celda: **combates con gas más fluidos**. |
| 41 | `GasGrid.AddGas` | Añadir gas usa el grid paralelo: menos lag al lanzar granadas de gas. |
| 42 | `GasGrid.AnyGasAt` | "¿Hay gas aquí?" responde desde el grid de bits, sin recorrer celdas. |
| 43 | `GasGrid.DensityAt` | Densidad de gas consultada al instante (daño por gas, filtros). |
| 44 | `GasGrid.Debug_ClearAll` | Limpieza de depuración sincronizada con el grid (sin coste en partida normal). |
| 45 | `GasGrid.Notify_ThingSpawned` | Al aparecer cosas, los grids se mantienen coherentes. |

### A12. Trabajo de producción — `WorkGiver_DoBill` (8)

| # | Método vanilla sustituido | Qué hace en la práctica |
|---|---------------------------|--------------------------|
| 46 | `WorkGiver_DoBill.get_PotentialWorkThingRequest` | La petición de trabajo potencial se **cachea por instancia** (constante por WorkGiverDef): cada escaneo de trabajo no la recalcula. |
| 47 | `WorkGiver_DoBill.MaxPathDanger(Pawn)` | Devuelve directamente `Danger.Some` como el vanilla, sin el cuerpo redundante. |
| 48 | `WorkGiver_DoBill.ThingIsUsableBillGiver(Thing)` | Decidir si una cosa es "bill giver" (mesa/banco) es más barato: producción y cocina responden antes. |
| 49 | `WorkGiver_DoBill.ShouldSkip(Pawn, bool)` | Corta el barrido de bill givers *antes* de recorrer todo si no hay nada que hacer: **menos tartamudeo al cambiar de tarea**. |
| 50 | `WorkGiver_DoBill.IsUsableIngredient(Thing, Bill)` | Filtrar ingredientes de una receta sin capas extra: cocinar/fabricar elige ingrediente más rápido. |
| 51 | `WorkGiver_DoBill.GetBillGiverRootCell(Thing, Pawn)` | Celda de interacción del bill giver calculada directa: el colono elige el banco correcto sin coste extra. |
| 52 | `WorkGiver_DoBill.GetMedicalCareCategory(Thing)` | Categoría médica (Normal/Herbal/Best) resuelta directa: curar a heridos en combate responde antes. |
| 53 | `WorkGiver_DoBill.TryFindBestBillIngredientsInSet(...)` | La decisión "mezclar o no ingredientes" despacha directo a AllowMix/NoMix: cocinar lotes sin sobrecarga. |

---

## B. Parches de runtime — Harmony (soporte de las caches)

No reescriben IL: se enganchan al arrancar para **mantener coherentes** las caches y vaciarlas
entre partidas.

| # | Parche | Qué hace en la práctica |
|---|--------|--------------------------|
| H1 | `Game.FinalizeInit` (postfix) | Vacía todas las caches al cambiar de partida: nunca devuelve datos de una partida anterior. |
| H2 | `ThingGrid.RegisterInCell` / `DeregisterInCell` | Mantiene el contador por celda (A5) sincronizado al aparecer/desaparecer objetos. |
| H3 | `ListerBuildings` (varios postfix) | Mantiene los índices de edificios (A4) al añadir/quitar edificios. |
| H4 | `Building` spawn/despawn | Índices de edificios correctos al construir/deconstruir. |
| H5 | `SlotGroup.Notify_AddedCell` / `Notify_LostCell` | Capacidades de SlotGroup (A7) al añadir/quitar celdas de almacén. |
| H6 | `StorageGroup.RemoveMember` / `Notify_SettingsChanged` | Capacidades de grupo de almacenamiento al cambiar miembros/ajustes. |
| H7 | `MassUtility.GearMass` / `InventoryMass` | Masa del equipo e inventario cacheadas: el panel de carga responde al instante. |
| H8 | `GasGrid.ExposeData` | Persistencia del gas al guardar/cargar con gas presente en el mapa. |
| H9 | `StoreUtility.TryFindBestBetterStoreCellForWorker` | Búsqueda de celda de almacén con capacidad conocida: **acarreo masivo más fluido**. |
| H10 | `HediffSet.GetFirstHediffOfDef` / `HasHediff` / `GetNotMissingParts` / `DirtyCache` | Consultas de heridas cacheadas y marcadas como sucias al cambiar: medicina y diagnóstico responden antes. |
| H11 | `Room.Role` / `Room.Owners` | Rol y propietarios de habitación cacheados (solo si la habitación es estable). |
| H12 | `ThingOwner<T>.ExposeData` / `TryAdd` / `Remove` | Índices de contenidos (inventarios, bolsas) mantenidos sincronizados. |

---

## Resumen

| Bloque | Intervenciones |
|--------|---------|
| A. Prepatch de IL | 53 |
| B. Runtime Harmony | 12 |
| **Total** | **65 intervenciones** |

**Verificación:** la marca de prepatching en el log del juego debe mostrar
`compPatches=11/11, listerPatches=6/6, buildingPatches=7/7, gridPatches=1/1, storagePatches=2/2,
slotGroupPatches=3/3, roomPatches=1/1, worldPawnsPatches=3/3, worldObjectsHolderPatches=1/1,
gasGridPatches=6/6, workGiverPatches=8/8, gettersRewritten=4` y **ningún** `...Failed` mayor que 0.
