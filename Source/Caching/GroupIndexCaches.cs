using System;
using System.Collections.Generic;
using Verse;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Mapa de indices <c>(grupo, clave de thing) -> posicion en la lista del grupo</c>.
    ///
    /// En lugar de empaquetar grupo y clave en un solo entero (el original usa 8 bits de grupo y 24
    /// de identificador, lo que limita la partida a 16,8 millones de cosas), aqui hay una cache por
    /// grupo en un array indexado por el valor del enum. Es igual de rapido —un acceso a array— y no
    /// tiene limite artificial.
    ///
    /// Las caches se crean bajo demanda: la mayoria de los grupos no llega a usarse nunca. Y no se
    /// registran para el vaciado por carga de partida, porque van atadas a la vida del objeto que
    /// las contiene (un <c>ListerThings</c> por mapa, que se recrea en cada partida).
    /// </summary>
    internal sealed class GroupIndexCaches
    {
        private IntCache<int>?[] _byGroup;

        internal GroupIndexCaches()
        {
            _byGroup = new IntCache<int>?[InitialSize()];
        }

        /// <summary>
        /// Devuelve la cache del grupo, creandola (y ampliando el array) si hace falta.
        /// </summary>
        internal IntCache<int> For(ThingRequestGroup group)
        {
            int index = (int)group;

            if (index < 0)
            {
                // Inalcanzable con un enum real de RimWorld. Se lanza en vez de devolver otra cache,
                // porque mezclar indices de dos grupos daria respuestas equivocadas en silencio.
                throw new ArgumentOutOfRangeException(nameof(group), group,
                    "ThingRequestGroup con valor negativo.");
            }

            if (index >= _byGroup.Length)
                Array.Resize(ref _byGroup, index + 1);

            IntCache<int>? cache = _byGroup[index];
            if (cache == null)
            {
                cache = new IntCache<int>(16, registerForGameLoadCleanup: false);
                _byGroup[index] = cache;
            }

            return cache;
        }

        /// <summary>Vacia todas las caches sin destruirlas (las necesita Clear).</summary>
        internal void ClearAll()
        {
            for (int i = 0; i < _byGroup.Length; i++)
                _byGroup[i]?.Clear();
        }

        /// <summary>Caches creadas hasta ahora (diagnostico).</summary>
        internal int CreatedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _byGroup.Length; i++)
                {
                    if (_byGroup[i] != null)
                        count++;
                }

                return count;
            }
        }

        /// <summary>
        /// Tamano inicial: el mayor valor del enum mas uno. Se calcula una vez; si algun dia
        /// apareciera un valor mayor, <see cref="For"/> amplia el array.
        /// </summary>
        private static int InitialSize()
        {
            int max = 0;

            foreach (ThingRequestGroup group in (ThingRequestGroup[])Enum.GetValues(typeof(ThingRequestGroup)))
            {
                int value = (int)group;
                if (value > max)
                    max = value;
            }

            return max + 1;
        }
    }
}
