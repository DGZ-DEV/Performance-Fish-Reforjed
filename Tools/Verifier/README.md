# Verificador por simulación del prepatch

Herramienta de consola (`Tools/Verifier`) que ejecuta la **misma pipeline de prepatch del mod
compilado** contra el `Assembly-CSharp.dll` real de RimWorld 1.6 y comprueba que las **32
reescrituras de IL** se aplican sin fallos (los grupos revertidos por la revisión de 2026-10-05
aparecen como `0/0` por diseño).

## Por qué existe

El prepatch del Reforjed reescribe el cuerpo de 32 métodos del juego al cargar (vía Prepatcher).
Para no asumir que los nombres/signaturas de esos métodos existen, esta herramienta:

1. Copia el `Assembly-CSharp.dll` real a `%TEMP%` (no toca el del juego).
2. Carga nuestro `PerformanceFishReforjed.dll` compilado.
3. Invoca `PrepatchManager.Run(module)` sobre esa copia, vía reflexión (Mono.Cecil viene IL-merged
   en el `0Harmony.dll` de Prepatcher, igual que en el propio mod).
4. Lee los contadores de cada grupo de parches y exige `applied == expected` y `failed == 0`.

## Cómo ejecutarlo

```powershell
dotnet build "Tools\Verifier\PFRVerifier.csproj" -c Release
dotnet "Tools\bin\PFRVerifier.dll"
```

Salida correcta esperada:

```
DefStatCachePrepatch          applied=4/4   failed=0
GetCompCachingPrepatch        applied=9/9   failed=0
ListerThingsPrepatch          applied=6/6   failed=0
ListerBuildingsPrepatch       applied=7/7   failed=0
GridsUtilityPrepatch          applied=1/1   failed=0
StorageSettingsPrepatch       applied=2/2   failed=0
StoreUtilitySlotGroupPrepatch applied=0/0   failed=0
RoomPrepatch                  applied=1/1   failed=0
WorldPawnsPrepatch            applied=2/2   failed=0
WorldObjectsHolderPrepatch    applied=0/0   failed=0
GasGridPrepatch               applied=0/0   failed=0
WorkGiver_DoBillPrepatch      applied=0/0   failed=0

TOTAL reescrituras de cuerpo: 32/32
RESULTADO: TODOS LOS PARCHES VERIFICADOS (0 fallos).
```

## Requisitos de entorno

- RimWorld en `C:\RimWorld` con Prepatcher instalado (`C:\RimWorld\Mods\Prepatcher`).
- .NET SDK (se usa .NET 8 para resolver los facades `netstandard` que la reflexión sobre el
  `Assembly-CSharp` 1.6 necesita; `net472` no los trae).

## Notas

- No modifica el `Assembly-CSharp.dll` del juego: trabaja sobre una copia en `%TEMP%`.
- No ejecuta código del juego: solo la lógica de reescritura IL (Mono.Cecil + reflexión), que es
  exactamente la parte crítica que puede fallar en silencio si las firmas no coinciden.
- Límite conocido (Hallazgo L4): el verificador comprueba que cada reescritura **se aplicó**, no
  que los enganches Harmony se hayan conectado ni que las respuestas cacheadas coincidan con
  vanilla. Los hallazgos C1/C2/H1-H6/M1-M5 se detectaron y corrigieron en el código (ver
  `2026-10-05-findings-for-author.es.md`); las comprobaciones en el juego de ese informe son la
  verificación de comportamiento definitiva.