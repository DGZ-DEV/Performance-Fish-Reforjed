$root = "C:\Users\User\Desktop\Performance Fish Reforjed"
$files = @(
  "Source\Prepatch\PrepatchManager.cs",
  "Source\Prepatch\ReforjedFields.cs",
  "Source\Caching\CompCaches.cs",
  "Source\Prepatch\GetCompCachingPrepatch.cs",
  "Source\Caching\IntCache.cs",
  "Source\Caching\ByReference.cs",
  "Source\Caching\ListerThingsCaches.cs",
  "Source\Caching\ListerBuildingsCaches.cs",
  "Source\Caching\WorldPawnsCache.cs",
  "Source\Hediffs\HediffSetCaching.cs",
  "Source\Caching\DefStatCache.cs",
  "Source\Prepatch\RoomPrepatch.cs",
  "Source\Prepatch\RoomHarmonyPatches.cs",
  "Source\Caching\AllowedToAcceptCache.cs",
  "Source\Caching\ItemCountGrid.cs",
  "Source\Prepatch\GridsUtilityPrepatch.cs",
  "Source\Caching\StorageBlockerGrid.cs"
)

$header = @"
// This file is a Modification (MPL-2.0, section 1.10) of the original
// Performance Fish by bradson (https://github.com/bbradson/Performance-Fish),
// which is covered by the Mozilla Public License 2.0.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

"@

foreach ($rel in $files) {
  $path = Join-Path $root $rel
  if (-not (Test-Path $path)) { Write-Host "FALTA: $rel"; continue }
  $content = [System.IO.File]::ReadAllText($path)
  if ($content.StartsWith("// This Source Code Form")) { Write-Host "ya tiene: $rel"; continue }
  if ($content.StartsWith("// This file is a Modification")) { Write-Host "ya tiene: $rel"; continue }
  $new = $header + $content
  [System.IO.File]::WriteAllText($path, $new)
  Write-Host "OK: $rel"
}
