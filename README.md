# Performance Fish Reforjed

> **EN** — From-scratch reimplementation for **RimWorld 1.6** of the "Performance Fish" style
> performance optimizations, written specifically for this version: no code from old mods, no
> obsolete methods, verified against the `Assembly-CSharp` of **1.6.9655**.
>
> **ES** — Reimplementación desde cero para **RimWorld 1.6** de las optimizaciones de rendimiento
> estilo "Performance Fish", escrita específicamente para esta versión: sin código de mods
> antiguos, sin métodos obsoletos, verificada contra el `Assembly-CSharp` de **1.6.9655**.

**EN:** 53 IL rewrites + 12 runtime hooks targeting the CPU hotspots (comps, thing lists, buildings, gas, storage, production and medicine).
**ES:** 53 reescrituras de IL + 12 enganches de runtime que atacan los puntos calientes de CPU (comps, listas de cosas, edificios, gas, almacenamiento, producción y medicina).

---

## Requirements / Requisitos

- **RimWorld 1.6** (verified on / verificado en 1.6.9655).
- **Harmony** ([brrainz.harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)).
- **Prepatcher** ([zetrith.prepatcher](https://steamcommunity.com/sharedfiles/filedetails/?id=2934420800)).

**EN:** Prepatcher must be **enabled and placed first** in the mod list: it is the engine that applies the IL rewrite before the game loads.
**ES:** Prepatcher debe estar **activo y el primero** en la lista de mods: es el motor que aplica la reescritura de IL antes de que se cargue el juego.

---

## Installation / Instalación

1. Copy the whole folder into `RimWorld/Mods/` (e.g. `Mods/PerformanceFishReforjed/`).
   Copia la carpeta completa en `RimWorld/Mods/` (p. ej. `Mods/PerformanceFishReforjed/`).
2. Enable the mod in the mod list. / Activa el mod en el menú de mods.
3. Start the game and check the log for the **prepatch stamp** (see Verification).
   Arranca el juego y comprueba en el log la **marca de prepatching** (ver Verificación).

> **EN:** The mod does **not** save or modify the game's `Assembly-CSharp.dll` on disk: the rewrite happens **in memory** at load time and is redone on every launch.
> **ES:** El mod **no** guarda ni modifica el `Assembly-CSharp.dll` del juego en disco: la reescritura se hace **en memoria** al cargar y se rehace en cada arranque.

---

## What it does, point by point / Qué hace, punto por punto

**EN:** See **[PARCHES.md](PARCHES.md)** for the full catalog: every patch with its name adapted to 1.6 and its practical in-game effect.
**ES:** Consulta **[PARCHES.md](PARCHES.md)** para el catálogo completo: cada parche con su nombre adaptado a 1.6 y su efecto en la práctica dentro del juego.

**EN:** Quick summary of what you notice in a large colony (mid/late game):
**ES:** Resumen rápido de lo que se nota en una colonia grande (media/late game):

- **EN:** Less stutter when switching tasks and assigning production jobs.
  **ES:** Menos tartamudeo al cambiar de tarea y al asignar trabajos de producción.
- **EN:** Smoother hauling (storage cell search and per-cell item counting).
  **ES:** Acarreo más fluido (búsqueda de celdas de almacén y conteo de objetos por celda).
- **EN:** Smoother gas combat (parallel 64-bit gas grids).
  **ES:** Combates con gas más fluidos (grids de gas paralelos de 64 bits).
- **EN:** Faster responses when inspecting objects, opening storage menus and using the world tab.
  **ES:** Respuestas más rápidas al inspeccionar objetos, abrir menús de almacenamiento y gestionar la pestaña de mundo.
- **EN:** Lighter medicine/diagnosis (cached medical categories and hediffs).
  **ES:** Medicina y diagnóstico más ligeros (categorías médicas y hediffs cacheados).

---

## Verification / Verificación

**EN:** On game load, the log must show:
**ES:** Al cargar el juego, el log debe mostrar:

```
[PerformanceFishReforjed] Prepatch engine OK. Stamp: ...
```

**EN:** And the stamp must contain **all** counters at their expected values with **no `Failed` above 0**:
**ES:** Y dentro de la marca deben aparecer **todos** los contadores con sus valores esperados y **ningún `Failed` mayor que 0**:

```
gettersRewritten=4 | compPatches=11/11 | listerPatches=6/6 | buildingPatches=7/7 |
gridPatches=1/1 | storagePatches=2/2 | slotGroupPatches=3/3 | roomPatches=1/1 |
worldPawnsPatches=3/3 | worldObjectsHolderPatches=1/1 | gasGridPatches=6/6 |
workGiverPatches=8/8
```

**EN:** If any counter shows `0/N` or `Failed>0`, your RimWorld version differs from the verified one (1.6.9655): the game keeps working with vanilla behavior in those spots, but those optimizations are disabled.
**ES:** Si algún contador aparece como `0/N` o con `Failed>0`, tu versión de RimWorld no coincide con la verificada (1.6.9655): el juego sigue funcionando con comportamiento vanilla en esos puntos, pero esas optimizaciones quedan desactivadas.

---

## Repository structure / Estructura del repositorio

```
About/                 Mod metadata (About.xml) / Metadatos del mod
Assemblies/            Compiled assembly / Ensamblado compilado
Source/                Source code / Código fuente
  Caching/             Cache implementations and runtime patches
                       Implementación de las caches y parches de runtime
  Prepatch/            IL rewriting (Prepatcher) and injected fields
                       Reescritura de IL (Prepatcher) y campos inyectados
  Hediffs/             Hediff caching / Caché de hediffs
  Listers/             Content indexes / Índices de contenidos
LoadFolders.xml        Version loading / Carga por versión
PARCHES.md             Point-by-point patch catalog / Catálogo punto por punto de los parches
```

---

## Compatibility and warnings / Compatibilidad y advertencias

- **EN:** Verified against **1.6.9655**. On other 1.6 versions patches may not apply; the game does **not** crash (they fall back to vanilla behavior) but the gains disappear.
  **ES:** Verificado contra **1.6.9655**. En otras versiones de 1.6 los parches pueden no aplicarse; el juego **no** falla (caen a comportamiento vanilla) pero la ganancia desaparece.
- **EN:** Designed **not to break anything**: every rewrite replicates the exact vanilla behavior (verified against the real IL), and caches are cleared when switching games.
  **ES:** Diseñado para **no romper nada**: cada reescritura replica el comportamiento exacto del vanilla (verificado contra el IL real), y las caches se vacían al cambiar de partida.
- **EN:** If other mods patch the same methods, Harmony load order may affect the interaction; when in doubt, open an issue with the prepatch stamp from your log.
  **ES:** Si otros mods parchean los mismos métodos, el orden de carga de Harmony puede afectar la interacción; ante dudas, abre un issue con la marca de prepatching de tu log.

---

## Credits / Créditos

**EN:** This mod is a **from-scratch reimplementation** for 1.6 of the optimization catalog of **Performance Fish**, the original mod by **bradson**. The ideas and cache architecture come from that project; the code here is rewritten and verified against the real 1.6 IL. See **[CREDITS.md](CREDITS.md)** for full attribution.
**ES:** Este mod es una **reimplementación desde cero** para 1.6 del catálogo de optimizaciones de **Performance Fish**, el mod original de **bradson**. Las ideas y la arquitectura de caches provienen de ese proyecto; el código aquí está reescrito y verificado contra el IL real de 1.6. Visita **[CREDITS.md](CREDITS.md)** para la atribución completa.

- Original author / Autor original: **bradson** — [github.com/bbradson/Performance-Fish](https://github.com/bbradson/Performance-Fish)
- Author of this version / Autor de esta versión: **DGZ** (Reforjed series / serie Reforjed)

---

## License / Licencia

**EN:** Reforjed series project (author: DGZ). Free to use for study and modding; attribution appreciated. The original Performance Fish license (MPL-2.0) applies to the parts derived from it — see CREDITS.md.
**ES:** Proyecto de la serie **Reforjed** (autor: DGZ). Uso libre para fines de estudio y modding; atribución apreciada. La licencia del Performance Fish original (MPL-2.0) aplica a las partes derivadas de él — ver CREDITS.md.
