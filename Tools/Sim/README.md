# PFRSim — Simulador de optimización de Performance Fish Reforjed

Responde a la pregunta del informe (`2026-10-05-findings-for-author.es.md`, hallazgo **L4**): no
basta con que el prepatch "aplique" los parches; hay que demostrar si el mod **optimiza de verdad**.

## Qué hace

Corre la **pipeline real de prepatch del mod** (`PrepatchManager.Run`) sobre una copia del
`Assembly-CSharp.dll` de esta máquina y hace dos cosas:

### Fase A — Enganche estructural (el parche quedó conectado a nuestro código)

Revisa con Mono.Cecil el IL del `Assembly-CSharp` ya parcheado y comprueba que cada uno de los
**32 objetivos activos** tiene en su cuerpo una llamada a un miembro de `PerformanceFishReforjed`
(la cache), es decir que la reescritura **no es un cuerpo genérico ni muerto**: quedó ENGANCHADA.

Reporta además los contadores que el propio mod mantiene al aplicar el prepatch
(`GettersRewritten`, `PatchesApplied`, `PatchedCount`), que deben coincidir con lo esperado.

### Fase B — Benchmark headless (la estructura de datos es más rápida y devuelve lo mismo)

La optimización central del motor de prepatching sustituye un **barrido de lista O(n)** por una
**consulta a tabla hash O(1)** (`IntCache`, nuestro remplazo del `FishTable` original). La Fase B
compara, sobre datos sintéticos idénticos y **con aserción de que ambos dan el mismo resultado**
(`cache == barrido`),:

* la primitiva de cache O(1) (réplica **literal** del algoritmo de `Source/Caching/IntCache.cs`,
  incluido su mezclador murmur-style exacto), contra
* el barrido vanilla O(n) sobre listas del mismo tamaño.

Mide `ns/op` y el `speedup` resultante en dos escenarios (típico y grande).

## Resultado observado (Assembly-CSharp 1.6.9655.19392)

```
FASE A: Sobrecargas enganchadas a nuestro codigo: 32/32
FASE B: Igualdad de resultados cache==barrido: OK
        Escenario tipico (64 defs x 7 comps):   cached=24,7 ns/op  vanilla=136,4 ns/op  speedup=×5,5
        Escenario grande  (512 defs x 48 comps): cached=25,8 ns/op  vanilla=867,3 ns/op  speedup=×33,6
```

La cache es O(1) e independiente del tamaño; el barrido vanilla crece con la lista. El speedup
depende del tamaño de la lista que sustituye.

## Límites del simulador (importante)

El DLL del mod compila contra el runtime **Mono/Unity** del juego y accede a campos internos de
CoreLib (p. ej. `List<T>._version`) vía `IgnoresAccessChecksTo("mscorlib")`. El runtime **.NET Core
8** de este simulador **no honra** ese atributo (el campo vive en `System.Private.CoreLib`), así que
el camino caliente completo (`ThingDef.HasComp`, `StorageSettings.AllowedToAccept`, etc.) **no puede
ejecutarse tal cual headless**.

Por eso:

* La **Fase A** prueba de forma definitiva que el juego usa nuestra cache (enganche estructural).
* La **Fase B** prueba que la estructura de datos con la que se sustituye el algoritmo vanilla es
  más rápida **y** devuelve exactamente lo mismo.

Las caches que dependen de estado vivo (listas reales por sección, `DefDatabase`, regiones, instancia
`WorldPawns`, `StatWorker`, mapa con rejilla) **no pueden fabricarse headless** sin falsificar el
resultado; su ganancia real se mide en el juego (p. ej. con Dubs Performance Analyzer).

## Compilar y ejecutar

```powershell
dotnet build "Tools\Sim\PFRSim.csproj"
dotnet "Tools\bin\PFRSim.dll"
```

La salida queda en `Tools\bin\`. Copia `0Harmony.dll` junto a `PFRSim.dll` si no está (el csproj ya
hace `Private` por defecto, de modo que suele copiarse solo). Requiere .NET SDK 8+ y las rutas
absolutas de `C:\RimWorld` que se usan en `Program.cs`.

## Estructura

* `PFRSim.csproj` — net8.0, referencia 0Harmony (Mono.Cecil publicizado con Krafs.Publicizer) y
  Assembly-CSharp real en compile-time.
* `Program.cs` — Fase A (enganche IL) + Fase B (benchmark O(1) vs O(n)).