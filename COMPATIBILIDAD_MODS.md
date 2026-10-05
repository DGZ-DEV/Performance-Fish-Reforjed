# Compatibilidad con otros mods — Performance Fish Reforjed
# Mod compatibility — Performance Fish Reforjed

Resumen limpio, basado en el análisis real (descompilación) del proyecto, de **qué mods son
compatibles con Performance Fish Reforjed** y con qué matiz.

Clean summary, based on the project's real (decompilation) analysis, of **which mods are compatible
with Performance Fish Reforjed** and with what caveat.

---

## Leyenda / Legend
- ✅ **Compatible** — coexiste sin conflicto. Compatible without conflict.
- ⚠️ **Compatible con nota** — coexiste, con un matiz verificad verificado. Compatible, with a verified caveat.
- ➖ **Sin efecto** — inerte o sin superficie en 1.6. No effect (inert or no 1.6 surface).

| Mod | packageId | Estado / Status | Nota / Note |
|---|---|---|---|
| Vanilla Expanded Framework | `OskarPotocki.VanillaFactionsExpanded.Core` | ✅ | Parchea `MassUtility.Capacity` y `WorkGiver_DoBill` internos; Reforjed usa métodos base. No chocan. |
| Vehicle-Framework | `SmashPhil.VehicleFramework` | ✅ | Transpila `GearAndInventoryMass`; Reforjed toca `GearMass`/`InventoryMass` (cadena intacta). |
| PickUpAndHaul | `Mehni.PickUpAndHaul` | ✅ | Métodos distintos; perfecciona el hauling sin solaparse. |
| Dubs Performance Analyzer | `Dubwise.DubsPerformanceAnalyzer` | ✅ | Profiling only; mide las versiones cacheadas (mejora). |
| Combat Extended | `CETeam.CombatExtended` | ✅ | Build cargable verificada (113 HarmonyPatch, sin solapamiento directo). |
| Dubs Mint Menus | `Dubwise.DubsMintMenus` | ✅ | UI pura. |
| RimHUD | `Jaxe.RimHUD` | ✅ | `Game.FinalizeInit` postfix apilado sin conflicto. |
| Achtung! | `brrainz.achtung` | ⛔ Desactivado | Trae su propio `0Harmony v1.2.0.1` viejo que no resuelve `Harmony.CodeInstruction` en la pila actual (2.x). Por desactualizado se desactivó; no afecta al Reforjed. |
| AllowTool | `UnlimitedHugs.AllowTool` | ✅ | UI / designadores. |
| Character Editor | `void.charactereditor` | ✅ | UI de edición (editor abierto). |
| HugsLib | `UnlimitedHugs.HugsLib` | ✅ | Librería; sin hotspots propios. |
| VanillaVehiclesExpanded | `OskarPotocki.VanillaVehiclesExpanded` | ✅ | Depende de Vehicle-Framework. |
| Slower Pawn Tick Rate | `Arkymn.SlowerPawnTickRate` | ✅ | Tick rates distintos de los nuestros. |
| MissileGirl | `vr.missilegirl` | ⚠️ | Comparte `HediffSet.DirtyCache` (postfix + postfix se apilan, verificado seguro); orden revisado. |
| kNumbers | `koisama.numbers` | ➖ | "Numbers" 0.16, inerte en 1.6; sin superficie. |

---

## Notas clave / Key notes

1. **Nunca se modifica el DLL de los otros mods.** Toda optimización vive en nuestro
   `PerformanceFishReforjed.dll` y se activa en memoria solo si el mod está instalado y su toggle
   está activado. We never modify other mods' DLLs; every optimization lives in our
   `PerformanceFishReforjed.dll`, applied in memory only when the mod is installed and its toggle on.
2. **Método compartido seguro:** `Game.FinalizeInit` (RimHUD + HugsLib + Reforjed), `HediffSet.DirtyCache`
   (MissileGirl + Reforjed) y las cadenas de `MassUtility` / `WorkGiver_DoBill` se apilan sin conflicto
   (verificado por descompilación).
3. **Se puede desactivar por mod** desde la página de ajustes → "Compatibility details".
   Each mod can be disabled individually in settings → "Compatibility details".
4. **Optimización condicional implementada:** Combat Extended `CompInventory.AmmoCountOfDef`
   (LINQ → bucle) se activa solo si CE está presente. Conditional optimization implemented.

---

Documentación completa con evidencia: [`COMPATIBILIDAD.md`](COMPATIBILIDAD.md) (análisis detallado).