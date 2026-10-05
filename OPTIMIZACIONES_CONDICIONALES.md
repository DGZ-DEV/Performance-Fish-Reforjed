# Performance Fish Reforjed — Optimizaciones condicionales (auditoría)

Documento técnico: **qué funciones PROPIAS de los mods de `C:\Users\User\Desktop\MODS` se
podrían optimizar desde nuestro mod**, activadas solo cuando ese mod está detectado (capa
`CompatManager`), **sin modificar ni un byte de los mods**.

Disciplina aplicada (heredada de las reglas del proyecto): **no optimizo por existir**. Solo:
(1) método autocontenido, (2) sin efectos secundarios relevantes, (3) en un punto caliente real,
(4) cuyo resultado no cambie sorpresivamente, y (5) verificable. Si no cumple, se reporta como
"no tocar".

---

## Mecanismo técnico (por qué diferente al prepatch)

Los **53 prepatches del Reforjed se aplican con Prepatcher a los ensamblados del juego al
arrancar**, antes de que carguen los mods. Un DLL de mod no está disponible para Cecil en esa fase.
Por tanto, **optimizar funciones de otros mods exige Harmony en runtime** (transpiler/postfix/
prefix con buffer), aplicado **condicionalmente** cuando `CompatManager.IsActive(mod)` es true y
después de que el mod esté cargado. Esto encaja: la capa ya detecta cada mod y respeta su toggle.

---

## Viabilidad por mod (auditoría sobre decompilado real, ilspycmd)

| Mod | ¿Optimizable? | Candidato(s) | Hot path | Riesgo |
|---|---|---|---|---|
| CombatExtended | ✅ Sí | `CompInventory.AmmoCountOfDef` (LINQ → for); `JobGiver_CheckReload.DoReloadCheck`; `NonSnapAttackTargetFinder.FriendlyFireConeTargetScoreOffset` (Distinct→HashSet); `SuppressionUtility.GetCoverPositionFrom` (buffers) | think/job-giver por frame + targeting de torretas por disparo | Bajo (loops manuales, sin tocar semántica) |
| Vehicle-Framework | ✅ Sí | `VehiclePawn.AllCapablePawns` (List+LINQ por acceso) | UI/caravana/targeting de vehículos | Medio (resultado depende de `handlers` mutables; cachear exige invalidación) |
| Character Editor | ✅ Sí (cuando el editor está abierto) | `SZWidgets.CreateSearch` (~15 call-sites, `new List` por frame); `RecordTool.DrawRecordCard` (3× `ToList()` por frame) | render per-frame de la ventana del editor | Medio (invalidación por búsqueda/pawn) |
| Achtung! | 🟡 Marginal | `DraftedColonistsForPositioning`, `Tools.IsFreeTarget`, `QuotaCache.Get` | forced-work (ya amortiguado por diseño) | Bajo, pero ganancia pequeña |
| AllowTool | 🟡 Marginal | `PartyHuntHandler.TryFindHuntingTarget` (Sort→min), `AnyHuntingPartyMembersInCombat` (for) | caza de grupo per-tick | Bajo, solo si se usa Party Hunt |
| PickUpAndHaul | ❌ No | — (métodos propios son stubs; lógica vive en transpilers sobre vanilla, ya cubiertos) | — | — |
| VanillaExpandedFramework | ❌ No | — (LINQ es startup/patch-time o por-evento; usa `listerThings` ya cacheado) | — | — |
| VanillaVehiclesExpanded | ❌ No | — (`GarageDoor` loop por evento, no hot global) | — | — |
| RimHUD | ❌ No | — UI pura, solo al tener la ventana abierta | — | — |
| HugsLib | ❌ No | — librería, sin hotspots propios | — | — |
| Dubs Mint Menus | ❌ No | — UI | — | — |
| Dubs Performance Analyzer | ❌ No | — profiler; su LINQ es instrumentación/UI | — | — |
| MissileGirl | ❌ No | — framework "rocket", todo startup/ensamblados | — | — |
| Slower Pawn Tick Rate | ❌ No | — trivial (reescrituras de tick) | — | — |
| kNumbers | N/A | inerte (0.16) | — | — |

---

## Candidatos recomendados (orden de valor/riesgo)

### 1. CombatExtended — `CompInventory.AmmoCountOfDef(AmmoDef)` (mejor relación)
```csharp
return ammoListCached.Where((Thing t) => t.def == def).Sum((Thing t) => t.stackCount);
```
- Reescribe con un `for` manual sobre `ammoListCached`. Cero cambio semántico.
- **Hot real**: 6+ call-sites (recarga por think, job givers, gizmo por frame).
- Se aplica vía transpiler Harmony condicionado a `CompatManager.IsActive(CombatExtended)`.
- Riesgo: bajo.

### 2. Vehicle-Framework — `VehiclePawn.AllCapablePawns`
```csharp
List<Pawn> list = new List<Pawn>();
foreach (VehicleHandler handler in handlers) { if (handler.handlers.Count > 0) list.AddRange(handler.handlers); }
return list.Where(x => x.health.capacities.CapableOf(Manipulation))?.ToList() ?? new List<Pawn>();
```
- Sustituye `Where().ToList()` por un `for` que filtra mientras rellena una lista simple (sin LINQ).
- **Hot real**: llamado en UI de vehículo, caravanas y targeting.
- Riesgo: medio **si se cachea**; la versión solo-LINQ→for es segura. La variante con caché (para
  evitar hasta la asignación de `List`) necesita invalidación por embarque/desembarque y cambio de
  capacidades — recomiendo hasta ahí evaluarla por separado.

### 3. CombatExtended — `FriendlyFireConeTargetScoreOffset` (targeting de torreta)
- Cadena `.Where().Select().SelectMany().TakeWhile().Distinct()` por target candidato. `Distinct`
  asigna un `HashSet` por llamada + iteradores. Reescribir en loop manual + HashSet reutilizado.
- **Hot real**: cadencia de targeting de cada torreta.
- Riesgo: medio-bajo (la clase ya usa buffers estáticos; hay que respetarlos).

---

## Veredicto

Con los 4 mods que se auditaron completa y automáticamente (Achtung, AllowTool, Character Editor,
CombatExtended) + los 11 revisados manualmente, **solo CombatExtended y Vehicle-Framework ofrecen
optimizaciones con ganancia medible y riesgo manejable**; Character Editor solo cuando su ventana
está abierta. El resto ya está bien cacheado/optimizado por sus autores o su lógica propia no
controla puntos calientes globales.

Espera la decisión: estas optimizaciones **cambian el modelo actual** (de solo coexistir, a tocar
el comportamiento interno de mods de terceros), así que deben activarse por mod con su propio
toggle y verificarse por simulación antes de confiarlas a una build.

---

## Patrón de implementación seguro (cuando se decida implementar)

1. **Transpiler Harmony, no prepatch.** Los métodos objetivo viven en DLLs de mods que cargan
   después de la fase de Prepatcher. Se aplica con `[HarmonyPatch]` + `Transpiler` registrado
   dinámicamente solo si `CompatManager.IsActive(mod)` al arrancar (después de que el mod esté
   cargado), con `loadAfter` opcional para ordenar la carga tras el mod objetivo.
   - Reemplazar LINQ por **loops manuales** + **buffers reutilizados** (`List.Clear()` en vez de
     `new`), conservando exactamente el mismo resultado. Nunca cambiar semántica.
2. **Toggle individual.** Cada optimización se liga al toggle `Compat_<Mod>` existente; si el
   usuario lo desactiva, no se registra el parche. El mod objetivo nunca se modifica.
3. **Verificación por simulación.** Ampliar `Tools/Verifier` para, dado el DLL del mod, aplicar
   el transpiler a una copia del método y comprobar que produce el mismo IL resultante ante un
   conjunto de entradas (o que la transformación es no-op si el patrón no coincide). Pequeño, pero
   necesario antes de incluirlo en una build.
4. **Salvaguarda:** si el transpiler no encuentra el patrón esperado (firma/IL distinto del mod
   objetivo), **no romper**: dejar el método sin tocar y loguearlo, no fallar la carga.

El objetivo es que el Reforjed **siga funcionando si el mod objetivo no está instalado, cambia de
versión, o el usuario desactiva la optimización**. Eso ya lo garantiza la capa existente.