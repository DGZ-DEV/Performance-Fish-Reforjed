# Verificador por simulación del prepatch

Herramienta de consola (`Tools/Verifier`) que ejecuta la **misma pipeline de prepatch del mod
compilado** contra el `Assembly-CSharp.dll` real de RimWorld 1.6 y comprueba que los **53
reescrituras de IL** se aplican sin fallos.

## Por qué existe

El prepatch del Reforjed reescribe el cuerpo de 53 métodos del juego al cargar (vía Prepatcher).
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
GetCompCachingPrepatch        applied=11/11 failed=0
ListerThingsPrepatch          applied=6/6   failed=0
ListerBuildingsPrepatch       applied=7/7   failed=0
GridsUtilityPrepatch          applied=1/1   failed=0
StorageSettingsPrepatch       applied=2/2   failed=0
StoreUtilitySlotGroupPrepatch applied=3/3   failed=0
RoomPrepatch                  applied=1/1   failed=0
WorldPawnsPrepatch            applied=3/3   failed=0
WorldObjectsHolderPrepatch    applied=1/1   failed=0
GasGridPrepatch               applied=6/6   failed=0
WorkGiver_DoBillPrepatch      applied=8/8   failed=0

TOTAL reescrituras de cuerpo: 53/53
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