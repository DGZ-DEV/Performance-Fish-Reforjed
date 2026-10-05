using System;
using PerformanceFishReforjed.Caching;
using PerformanceFishReforjed.Compatibility;
using PerformanceFishReforjed.Compatibility.Optimizations;
using Verse;

namespace PerformanceFishReforjed
{
    /// <summary>
    /// Cierra la puesta en marcha.
    ///
    /// Activar la cache despues de la carga es imprescindible: durante la carga los defs se
    /// consultan antes de que sus stats esten resueltas, y cachear entonces dejaria valores
    /// incorrectos guardados para siempre.
    ///
    /// Tambien engancha el ciclo de vida de las caches indexadas por identificador de objeto,
    /// que deben vaciarse al cambiar de partida (los identificadores se reinician y una entrada
    /// vieja devolveria el comp de un objeto de la partida anterior).
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class Startup
    {
        static Startup()
        {
            DefStatCache.Ready = true;

            DefStatCache.ResetCounters();

            CacheLifecycle.ApplyPatches();

            // Diagnostico de mods detectados (solo informa; no altera el rendimiento base).
            CompatManager.LogDetectedMods();

            // Optimizaciones condicionales: se tocan funciones PROPIAS de mods detectados (nunca su
            // DLL), y solo si su toggle esta activo. Sin dependencia de carga: si el mod no esta
            // presente, la resolucion por reflexion falla y no se aplica nada.
            if (CompatManager.IsActive(CompatMods.Mod.CombatExtended))
                CombatExtendedAmmoCountOptimization.TryPatch();
        }
    }
}