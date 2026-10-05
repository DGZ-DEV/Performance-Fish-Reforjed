# Performance Fish Reforjed

Reimplementación para **RimWorld 1.6** de las optimizaciones de rendimiento del estilo
"Performance Fish", escrita desde cero para esta versión: sin código de mods antiguos, sin métodos
obsoletos, verificada contra el `Assembly-CSharp` de **1.6.9655**.

**53 reescrituras de IL + 12 enganches de runtime** que atacan los puntos calientes de CPU
(comps, listas de cosas, edificios, gas, almacenamiento, producción y medicina).

---

## Requisitos

- **RimWorld 1.6** (verificado en 1.6.9655).
- **Harmony** ([brrainz.harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)).
- **Prepatcher** ([zetrith.prepatcher](https://steamcommunity.com/sharedfiles/filedetails/?id=2934420800)).

Prepatcher debe estar **activo y por delante** en la lista de mods: es el motor que aplica la
reescritura de IL antes de que se cargue el juego.

---

## Instalación

1. Copia la carpeta completa en `RimWorld/Mods/` (por ejemplo `Mods/PerformanceFishReforjed/`).
2. Activa el mod en el menú de mods.
3. Arranca el juego y comprueba en el log la **marca de prepatching** (ver *Verificación*).

> El mod **no** guarda ni modifica el `Assembly-CSharp.dll` del juego en disco: la reescritura se
> hace **en memoria** al cargar, y se rehace en cada arranque.

---

## Qué hace, punto por punto

Consulta **[PARCHES.md](PARCHES.md)** para el catálogo completo: cada parche con su nombre
adaptado a la versión 1.6 y su efecto en la práctica dentro del juego.

Resumen rápido de lo que se nota en una colonia grande (media/late game):

- **Menos tartamudeo** al cambiar de tarea y al asignar trabajos de producción.
- **Acarreo más fluido** (búsqueda de celdas de almacén y conteo de objetos por celda).
- **Combates con gas** más fluidos (grids de gas paralelos de 64 bits).
- **Respuestas más rápidas** al inspeccionar objetos, abrir menús de almacenamiento y
  gestionar la pestaña de mundo.
- **Medicina y diagnóstico** más ligeros (categorías médicas y hediffs cacheados).

---

## Verificación de que el mod está activo

Al cargar el juego, el log debe mostrar la línea:

```
[PerformanceFishReforjed] Motor de prepatching OK. Marca: ...
```

Y dentro de la marca deben aparecer **todos** los contadores con sus valores esperados y
**ningún `Failed` mayor que 0**:

```
gettersRewritten=4 | compPatches=11/11 | listerPatches=6/6 | buildingPatches=7/7 |
gridPatches=1/1 | storagePatches=2/2 | slotGroupPatches=3/3 | roomPatches=1/1 |
worldPawnsPatches=3/3 | worldObjectsHolderPatches=1/1 | gasGridPatches=6/6 |
workGiverPatches=8/8
```

Si algún contador aparece como `0/N` o con `Failed>0`, la versión de RimWorld no coincide con la
verificada (1.6.9655): el juego sigue funcionando con comportamiento vanilla en esos puntos, pero
esas optimizaciones quedan desactivadas.

---

## Estructura del repositorio

```
About/                 Metadatos del mod (About.xml)
Assemblies/            Ensamblado compilado
Source/                Código fuente
  Caching/             Implementación de las caches y parches de runtime
  Prepatch/            Reescritura de IL (Prepatcher) y campos inyectados
  Hediffs/             Caché de hediffs
  Listers/             Índices de contenidos
LoadFolders.xml        Carga por versión
PARCHES.md             Catálogo punto por punto de los parches
```

---

## Compatibilidad y advertencias

- Verificado contra **1.6.9655**. En otras versiones de 1.6 los parches pueden no aplicarse; el
  juego **no** falla (caen a comportamiento vanilla) pero la ganancia desaparece.
- Diseñado para **no romper nada**: cada reescritura replica el comportamiento exacto del vanilla
  (verificado contra el IL real), y las caches se vacían al cambiar de partida.
- Si usas otros mods que parcheen los mismos métodos, el orden de carga de Harmony puede afectar
  la interacción; ante dudas, abre un issue con la marca de prepatching de tu log.

---

## Créditos

Este mod es una **reimplementación desde cero** para 1.6 del catálogo de optimizaciones de
**Performance Fish**, el mod original de **bradson**. Las ideas y la arquitectura de caches
provienen de ese proyecto; el código aquí está reescrito y verificado contra el IL real de 1.6.
Por favor, visita **[CREDITS.md](CREDITS.md)** para la atribución completa.

- Autor original: **bradson** — [github.com/bbradson/Performance-Fish](https://github.com/bbradson/Performance-Fish)
- Autor de esta versión: **DGZ** (serie Reforjed)

---

## Licencia

Proyecto de la serie **Reforjed** (autor: DGZ). Uso libre para fines de estudio y modding;
atribución apreciada.
