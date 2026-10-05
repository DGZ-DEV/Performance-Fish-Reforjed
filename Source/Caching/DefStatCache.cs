using System.Runtime.CompilerServices;
using System.Threading;
using PerformanceFishReforjed.Prepatch;
using RimWorld;
using UnityEngine;
using Verse;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Cache de las stats abstractas de los defs.
    ///
    /// En 1.6 los getters de <see cref="ThingDef"/> llaman siempre a
    /// <c>StatExtension.GetStatValueAbstract</c>, que a su vez hace
    /// <c>stat.Worker.GetValueAbstract(def, stuff)</c>. Ese calculo da siempre el mismo resultado
    /// para un def, asi que el motor de prepatching reescribe el cuerpo de los cuatro getters
    /// para que pasen por aqui y el resultado quede en un campo inyectado.
    ///
    /// Dos detalles de correccion, ambos obligatorios:
    ///
    /// 1. <b>Mascara de bits en lugar de centinela en el propio float.</b> Un campo recien
    ///    inyectado vale 0 y 0 es un valor de stat legitimo, asi que no se puede usar como
    ///    "sin calcular". Tampoco sirve NaN, porque el campo no se inicializa a NaN. La mascara
    ///    arranca en 0 ("ninguna cache calculada") y cada bit indica que su valor es valido.
    ///
    /// 2. <see cref="Ready"/> evita cachear durante la carga del juego. Los defs se construyen y
    ///    consultan antes de que sus stats esten resueltas; guardar entonces un valor dejaria la
    ///    cache envenenada para siempre.
    /// </summary>
    internal static class DefStatCache
    {
        internal const int MaskMarketValue = 1 << 0;
        internal const int MaskMass = 1 << 1;
        internal const int MaskFlammability = 1 << 2;
        internal const int MaskMaxHitPoints = 1 << 3;

        /// <summary>Se activa cuando el juego ha terminado de cargar. Hasta entonces no se cachea.</summary>
        internal static bool Ready;

        private static long _hits;
        private static long _misses;

        /// <summary>Consultas resueltas desde la cache.</summary>
        internal static long Hits => Interlocked.Read(ref _hits);

        /// <summary>Consultas que hubo que calcular.</summary>
        internal static long Misses => Interlocked.Read(ref _misses);

        internal static void ResetCounters()
        {
            Interlocked.Exchange(ref _hits, 0);
            Interlocked.Exchange(ref _misses, 0);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static float BaseMarketValue(ThingDef def)
        {
            int mask = def.ReforjedStatMask();
            if ((mask & MaskMarketValue) != 0)
            {
                Interlocked.Increment(ref _hits);
                return def.ReforjedBaseMarketValue();
            }

            Interlocked.Increment(ref _misses);
            float value = StatExtension.GetStatValueAbstract(def, StatDefOf.MarketValue, null);

            if (Ready)
            {
                def.ReforjedBaseMarketValue() = value;
                def.ReforjedStatMask() = mask | MaskMarketValue;
            }

            return value;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static float BaseMass(ThingDef def)
        {
            int mask = def.ReforjedStatMask();
            if ((mask & MaskMass) != 0)
            {
                Interlocked.Increment(ref _hits);
                return def.ReforjedBaseMass();
            }

            Interlocked.Increment(ref _misses);
            float value = StatExtension.GetStatValueAbstract(def, StatDefOf.Mass, null);

            if (Ready)
            {
                def.ReforjedBaseMass() = value;
                def.ReforjedStatMask() = mask | MaskMass;
            }

            return value;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static float BaseFlammability(ThingDef def)
        {
            int mask = def.ReforjedStatMask();
            if ((mask & MaskFlammability) != 0)
            {
                Interlocked.Increment(ref _hits);
                return def.ReforjedBaseFlammability();
            }

            Interlocked.Increment(ref _misses);
            float value = StatExtension.GetStatValueAbstract(def, StatDefOf.Flammability, null);

            if (Ready)
            {
                def.ReforjedBaseFlammability() = value;
                def.ReforjedStatMask() = mask | MaskFlammability;
            }

            return value;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int BaseMaxHitPoints(ThingDef def)
        {
            int mask = def.ReforjedStatMask();
            if ((mask & MaskMaxHitPoints) != 0)
            {
                Interlocked.Increment(ref _hits);
                return Mathf.RoundToInt(def.ReforjedBaseMaxHitPoints());
            }

            Interlocked.Increment(ref _misses);
            float value = StatExtension.GetStatValueAbstract(def, StatDefOf.MaxHitPoints, null);

            if (Ready)
            {
                def.ReforjedBaseMaxHitPoints() = value;
                def.ReforjedStatMask() = mask | MaskMaxHitPoints;
            }

            return Mathf.RoundToInt(value);
        }
    }
}
