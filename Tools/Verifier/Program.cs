using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PFRVerifier
{
    /// <summary>
    /// Verificador por simulacion del prepatch del Reforjed.
    ///
    /// Abre una copia real de Assembly-CSharp 1.6, ejecuta la MISMA pipeline de prepatch del mod
    /// compilado (PrepatchManager.Run) via reflexion y comprueba que todos los contadores de
    /// parches alcanzan su valor esperado con cero fallos.
    ///
    /// Mono.Cecil viene IL-merged dentro del 0Harmony de Prepatcher y es interno; por eso todo el
    /// acceso a Cecil se hace por reflexion en runtime, sin declarar sus tipos en origen.
    /// </summary>
    internal static class Program
    {
        private const string ModDll = @"C:\Users\User\Desktop\Performance Fish Reforjed\Assemblies\PerformanceFishReforjed.dll";
        private const string GameDll = @"C:\RimWorld\RimWorldWin64_Data\Managed\Assembly-CSharp.dll";
        private const string HarmonyDll = @"C:\RimWorld\Mods\Prepatcher\Assemblies\0Harmony.dll";
        private const string WorkCopy = @"C:\Windows\Temp\pf_sim_Assembly-CSharp.dll";

        private static int _failures;

        private static int Main()
        {
            Directory.CreateDirectory(@"C:\Windows\Temp");
            File.Copy(GameDll, WorkCopy, overwrite: true);
            Console.WriteLine($"[PFRVerifier] Copia de Assembly-CSharp preparada ({new FileInfo(WorkCopy).Length} bytes).");

            // Ensamblados del juego y de Prepatcher: los cargamos manualmente para que la reflexion
            // de ImportReference encuentre Assembly-CSharp y sus dependencias por identidad.
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                string name = new AssemblyName(e.Name).Name;
                string direct = @"C:\RimWorld\RimWorldWin64_Data\Managed\" + name + ".dll";
                if (File.Exists(direct))
                    return Assembly.LoadFrom(direct);
                string prepatcher = @"C:\RimWorld\Mods\Prepatcher\Assemblies\" + name + ".dll";
                if (File.Exists(prepatcher))
                    return Assembly.LoadFrom(prepatcher);
                string binLocal = AppDomain.CurrentDomain.BaseDirectory + name + ".dll";
                if (File.Exists(binLocal))
                    return Assembly.LoadFrom(binLocal);
                // netstandard: delegar al facade de .NET.
                return null;
            };

            // Cargar 0Harmony (provee Mono.Cecil interno) y el modulo del mod.
            Assembly harmony = Assembly.LoadFrom(HarmonyDll);
            Type moduleDefinitionType = harmony.GetType("Mono.Cecil.ModuleDefinition");
            if (moduleDefinitionType == null)
            {
                Console.WriteLine("ERROR: Mono.Cecil.ModuleDefinition no esta en 0Harmony.");
                return 1;
            }
            Console.WriteLine($"[PFRVerifier] Mono.Cecil resolvido desde 0Harmony ({moduleDefinitionType.AssemblyQualifiedName}).");

            Type moduleReadOptionsType = harmony.GetType("Mono.Cecil.ReaderParameters") ?? harmony.GetType("Mono.Cecil.Metadata.ReaderParameters");
            Assembly mod = Assembly.LoadFrom(ModDll);
            Type prepatchManager = mod.GetType("PerformanceFishReforjed.Prepatch.PrepatchManager");
            if (prepatchManager == null)
            {
                Console.WriteLine("ERROR: no se encontro PrepatchManager.");
                return 1;
            }

            // ModuleDefinition.ReadModule(string) -> ModuleDefinition
            MethodInfo readModule = moduleDefinitionType.GetMethod("ReadModule", new[] { typeof(string) });
            if (readModule == null)
            {
                Console.WriteLine("ERROR: ModuleDefinition.ReadModule(string) no encontrado.");
                return 1;
            }

            object module = readModule.Invoke(null, new object[] { WorkCopy });
            MethodInfo run = prepatchManager.GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (run == null)
            {
                Console.WriteLine("ERROR: PrepatchManager.Run no encontrado.");
                return 1;
            }

            try
            {
                run.Invoke(null, new object[] { module });
            }
            catch (TargetInvocationException tie)
            {
                Console.WriteLine("ERROR durante la ejecucion del prepatch:");
                Console.WriteLine(tie.InnerException ?? tie);
                return 1;
            }

            VerifyCounters(mod);

            Console.WriteLine();
            if (_failures > 0)
            {
                Console.WriteLine($"\n[PFRVerifier] RESULTADO: {_failures} FALLO(S).");
                return 1;
            }

            Console.WriteLine("\n[PFRVerifier] RESULTADO: TODOS LOS PARCHES VERIFICADOS (0 fallos).");
            return 0;
        }

        private static void VerifyCounters(Assembly mod)
        {
            string[] counterTypes =
            {
                "PerformanceFishReforjed.Prepatch.DefStatCachePrepatch",       // gettersRewritten=4
                "PerformanceFishReforjed.Prepatch.GetCompCachingPrepatch",      // 11/11
                "PerformanceFishReforjed.Prepatch.ListerThingsPrepatch",        // 6/6
                "PerformanceFishReforjed.Prepatch.ListerBuildingsPrepatch",     // 7/7
                "PerformanceFishReforjed.Prepatch.GridsUtilityPrepatch",        // 1/1
                "PerformanceFishReforjed.Prepatch.StorageSettingsPrepatch",     // storagePatches=2/2
                "PerformanceFishReforjed.Prepatch.StoreUtilitySlotGroupPrepatch",// slotGroupPatches=3/3
                "PerformanceFishReforjed.Prepatch.RoomPrepatch",                // 1/1
                "PerformanceFishReforjed.Prepatch.WorldPawnsPrepatch",          // 3/3
                "PerformanceFishReforjed.Prepatch.WorldObjectsHolderPrepatch",  // 1/1
                "PerformanceFishReforjed.Prepatch.GasGridPrepatch",             // 6/6
                "PerformanceFishReforjed.Prepatch.WorkGiver_DoBillPrepatch",    // 8/8
            };

            int totalApplied = 0;
            int totalExpected = 0;

            foreach (string typeName in counterTypes)
            {
                Type t = mod.GetType(typeName);
                if (t == null)
                {
                    Fail("No existe el tipo de contador " + typeName);
                    continue;
                }

                bool isDefStat = typeName.EndsWith("DefStatCachePrepatch");
                bool isStartStyle = typeName.EndsWith("StorageSettingsPrepatch")
                                    || typeName.EndsWith("StoreUtilitySlotGroupPrepatch");

                int applied;
                int failed;
                int expected;

                if (isDefStat)
                {
                    applied = GetIntStatic(t, "GettersRewritten");
                    failed = 0;
                    expected = 4;
                }
                else if (isStartStyle)
                {
                    applied = GetIntStatic(t, "PatchedCount");
                    failed = GetIntStatic(t, "FailedCount");
                    expected = typeName.EndsWith("StorageSettingsPrepatch") ? 2 : 3;
                }
                else
                {
                    applied = GetIntStatic(t, "PatchesApplied");
                    failed = GetIntStatic(t, "PatchesFailed");
                    expected = GetIntStatic(t, "ExpectedPatches");
                }

                Console.WriteLine($"  {t.Name,-38} applied={applied}/{expected}  failed={failed}");

                if (failed > 0)
                    Fail($"{typeName}: {failed} patch(es) fallaron.");
                if (applied != expected)
                    Fail($"{typeName}: se aplicaron {applied} pero se esperaban {expected}.");

                totalApplied += Math.Max(applied, 0);
                totalExpected += expected;
            }

            Console.WriteLine($"\n  TOTAL reescrituras de cuerpo: {totalApplied}/{totalExpected}");
            if (totalApplied != totalExpected)
                Fail("El total de reescrituras no coincide con el esperado.");
        }

        private static int GetIntStatic(Type type, string fieldName)
        {
            FieldInfo f = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (f == null)
            {
                Fail($"No existe el campo {fieldName} en {type.Name}.");
                return -1;
            }
            return (int)f.GetValue(null);
        }

        private static void Fail(string msg)
        {
            _failures++;
            Console.WriteLine("  [FALLO] " + msg);
        }
    }
}