using System;
using Mono.Cecil;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Reescritura de los metodos de <see cref="Verse.ListerThings"/> para que mantengan los indices
    /// y las listas por tipo de <see cref="Caching.ListerThingsCaches"/>.
    ///
    /// Son seis objetivos y tres de ellos comparten nombre con otro (las dos sobrecargas de
    /// GetThingsOfType, y HasComp/GetComponent no aplican aqui): por eso se declara la aridad
    /// generica y el numero de parametros de cada uno. Los objetivos estan verificados contra
    /// Assembly-CSharp 1.6.9655.19392 con Cecil.
    /// </summary>
    internal static class ListerThingsPrepatch
    {
        internal static int PatchesApplied;
        internal static int PatchesFailed;

        internal const int ExpectedPatches = 6;

        private const string ListerTypeName = "Verse.ListerThings";

        private static readonly Type Caches = typeof(Caching.ListerThingsCaches);

        internal static void Apply(ModuleDefinition module)
        {
            PatchesApplied = 0;
            PatchesFailed = 0;

            // Add y Remove son los que mantienen todo sincronizado; Contains y GetThingsOfType son
            // los que se benefician; Clear tiene que vaciar las caches porque sobreviven a el.
            Rewrite(module, nameof(Caching.ListerThingsCaches.Add), 0, 1, nameof(Caching.ListerThingsCaches.Add));
            Rewrite(module, nameof(Caching.ListerThingsCaches.Remove), 0, 1, nameof(Caching.ListerThingsCaches.Remove));
            Rewrite(module, nameof(Caching.ListerThingsCaches.Contains), 0, 1, nameof(Caching.ListerThingsCaches.Contains));
            Rewrite(module, nameof(Caching.ListerThingsCaches.Clear), 0, 0, nameof(Caching.ListerThingsCaches.Clear));

            // Dos sobrecargas con el MISMO nombre: la de 0 parametros devuelve IEnumerable<T> y la de
            // 1 parametro rellena una lista. Se distinguen por el numero de parametros.
            Rewrite(module, nameof(Caching.ListerThingsCaches.GetThingsOfType), 1, 0,
                nameof(Caching.ListerThingsCaches.GetThingsOfType));
            Rewrite(module, nameof(Caching.ListerThingsCaches.GetThingsOfType), 1, 1,
                nameof(Caching.ListerThingsCaches.GetThingsOfType));
        }

        private static void Rewrite(ModuleDefinition module, string methodName, int genericArity,
            int parameterCount, string replacementName)
        {
            try
            {
                if (BodyRewriter.Rewrite(module, ListerTypeName, methodName, genericArity, parameterCount,
                        false, Caches, replacementName))
                {
                    PatchesApplied++;
                }
                else
                {
                    PatchesFailed++;
                }
            }
            catch (Exception)
            {
                PatchesFailed++;
            }
        }
    }
}
