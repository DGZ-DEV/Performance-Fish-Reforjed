using System;
using PerformanceFishReforjed.Caching;
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
        }
    }
}