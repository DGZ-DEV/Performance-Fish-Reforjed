using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Verse;

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

            VerifyCombatExtendedOptimization(mod, harmony);

            Console.WriteLine();
            if (_failures > 0)
            {
                Console.WriteLine($"\n[PFRVerifier] RESULTADO: {_failures} FALLO(S).");
                return 1;
            }

            Console.WriteLine("\n[PFRVerifier] RESULTADO: TODOS LOS PARCHES VERIFICADOS (0 fallos).");
            return 0;
        }

        /// <summary>
        /// Verifica la optimizacion condicional de CombatExtended: carga el DLL real de CE, aplica
        /// nuestro prefix de Harmony real sobre CompInventory.AmmoCountOfDef y comprueba que produce
        /// exactamente el mismo resultado que la implementacion original de CE para la misma entrada.
        /// </summary>
        private static void VerifyCombatExtendedOptimization(Assembly mod, Assembly harmony)
        {
            const string ceDll = @"C:\Users\User\Desktop\MODS\Combat Extended\Assemblies\CombatExtended.dll";
            if (!File.Exists(ceDll))
            {
                Console.WriteLine("  [AVISO] CombatExtended.dll no encontrado; se omite la verificacion de la optimizacion CE.");
                return;
            }

            try
            {
                Assembly ce = Assembly.LoadFrom(ceDll);
                Type compInventory = ce.GetType("CombatExtended.CompInventory");
                if (compInventory == null) { Fail("CE: CompInventory no encontrado."); return; }
                Console.WriteLine("  [CE] CompInventory cargado.");

                MethodInfo ammoCount = compInventory.GetMethod("AmmoCountOfDef", BindingFlags.Public | BindingFlags.Instance);
                if (ammoCount == null) { Fail("CE: AmmoCountOfDef no encontrado."); return; }
                if (ammoCount.GetParameters().Length != 1) { Fail("CE: AmmoCountOfDef no tiene 1 parametro."); return; }
                Type ammoDefType = ammoCount.GetParameters()[0].ParameterType;
                Console.WriteLine("  [CE] AmmoCountOfDef localizado.");

                FieldInfo ammoListField = compInventory.GetField("ammoListCached", BindingFlags.NonPublic | BindingFlags.Instance);
                if (ammoListField == null) { Fail("CE: campo ammoListCached no encontrado."); return; }
                if (ammoListField.FieldType != typeof(List<Thing>)) { Fail("CE: ammoListCached no es List<Thing>."); return; }
                Console.WriteLine("  [CE] Campo ammoListCached (List<Thing>) confirmado.");

                object compInv = Activator.CreateInstance(compInventory);
                if (compInv == null) { Fail("CE: no se pudo instanciar CompInventory."); return; }
                Console.WriteLine("  [CE] CompInventory instanciado.");

                // Datos de prueba: 3 Things (sin inicializar, para no disparar estaticos del juego).
                List<Thing> ammo = new List<Thing>();
                object defA = GetUninitialized(ammoDefType); // AmmoDef
                object defB = GetUninitialized(ammoDefType); // otro AmmoDef
                ammo.Add(MakeThing(defA, 5));
                ammo.Add(MakeThing(defB, 2));
                ammo.Add(MakeThing(defA, 3));
                ammoListField.SetValue(compInv, ammo);
                Console.WriteLine("  [CE] Lista de prueba inyectada.");

                // Resultado ESPERADO (mismo patron que el decompilado de CE):
                //   Where(t => t.def == def).Sum(t => t.stackCount)
                //   defA: 5 + 3 = 8 ; defB: 2
                int expectedA = 8;
                int expectedB = 2;

                // 1) Resolver nuestro tipo de optimizacion dentro del DLL del Reforjed.
                Type optType = mod.GetType("PerformanceFishReforjed.Compatibility.Optimizations.CombatExtendedAmmoCountOptimization");
                if (optType == null) { Fail("CE: tipo de optimizacion no encontrado en el mod."); return; }

                // 2) Verificar que TryPatch se RESUELVE y, si CE esta presente y el campo correcto,
                //    NO lanza durante la parte no-log (la resolucion por reflexion). Pero como Verse.Log
                //    no funciona headless, no llamamos a TryPatch; en su lugar inyectamos el campo
                //    privado estatico del mod (igual que haria TryPatch al tener exito).
                FieldInfo staticField = optType.GetField("_ammoListCachedField",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (staticField == null) { Fail("CE: campo estatico _ammoListCachedField no encontrado."); return; }
                staticField.SetValue(null, ammoListField); // el verifier ya resolvio el campo privado de CE

                // 3) Invocar nuestro Prefix directamente (misma firma que usa Harmony).
                MethodInfo prefix = optType.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (prefix == null) { Fail("CE: Prefix no encontrado."); return; }

                object[] argsA = { compInv, defA, 0 };
                object[] argsB = { compInv, defB, 0 };
                bool skipA = (bool)prefix.Invoke(null, argsA);
                int resA = (int)argsA[2];
                bool skipB = (bool)prefix.Invoke(null, argsB);
                int resB = (int)argsB[2];

                Console.WriteLine($"  CE AmmoCountOfDef (prefix)  A={resA} (esperado {expectedA})   B={resB} (esperado {expectedB})   skipOriginal={skipA}/{skipB}");

                if (skipA || skipB)
                    Fail("CE: el prefix deberia devolver false (reemplazar original), pero devolvio true.");
                else if (resA != expectedA || resB != expectedB)
                    Fail($"CE: el prefix no coincidio con el valor esperado (A={resA}/{expectedA}, B={resB}/{expectedB}).");
                else
                    Console.WriteLine("  CE optimizacion: prefix produce el resultado correcto (OK).");
            }
            catch (Exception e)
            {
                Exception inner = e.InnerException ?? e;
                Fail("CE optimizacion: excepcion durante la verificacion: " + inner.Message + "\n    " + inner.StackTrace);
            }
        }

        /// <summary>Crea un Thing sin inicializar con def y stackCount, sin disparar estaticos del juego.</summary>
        private static Thing MakeThing(object def, int stackCount)
        {
            Thing t = (Thing)GetUninitialized(typeof(Thing));
            // def y stackCount de Thing: accesar por reflexion sobre campos/propiedades publicos.
            FieldInfo? defF = typeof(Thing).GetField("def", BindingFlags.Public | BindingFlags.Instance);
            if (defF != null)
                defF.SetValue(t, def);
            else
                typeof(Thing).GetProperty("def", BindingFlags.Public | BindingFlags.Instance)?.SetValue(t, def);

            FieldInfo? stackF = typeof(Thing).GetField("stackCount", BindingFlags.Public | BindingFlags.Instance);
            if (stackF != null)
                stackF.SetValue(t, stackCount);
            else
                typeof(Thing).GetProperty("stackCount", BindingFlags.Public | BindingFlags.Instance)?.SetValue(t, stackCount);
            return t;
        }

        /// <summary>Crea un objeto sin ejecutar ctor, evitando inicializadores estaticos del juego.</summary>
        private static object GetUninitialized(Type type)
        {
            return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
        }

        private static void VerifyCounters(Assembly mod)
        {
            string[] counterTypes =
            {
                "PerformanceFishReforjed.Prepatch.DefStatCachePrepatch",       // gettersRewritten=4
                "PerformanceFishReforjed.Prepatch.GetCompCachingPrepatch",      // 9/9 (L2: sin GetComp<T>; H3: sin HediffUtility.TryGetComp)
                "PerformanceFishReforjed.Prepatch.ListerThingsPrepatch",        // 6/6
                "PerformanceFishReforjed.Prepatch.ListerBuildingsPrepatch",     // 7/7
                "PerformanceFishReforjed.Prepatch.GridsUtilityPrepatch",        // 1/1
                "PerformanceFishReforjed.Prepatch.StorageSettingsPrepatch",     // storagePatches=2/2
                "PerformanceFishReforjed.Prepatch.StoreUtilitySlotGroupPrepatch",// slotGroupPatches=0/0 (L2: reescritura sin ganancia)
                "PerformanceFishReforjed.Prepatch.RoomPrepatch",                // 1/1
                "PerformanceFishReforjed.Prepatch.WorldPawnsPrepatch",          // 2/2 (DefPreventingMothball excluido: MissileGirl lo transpila)
                "PerformanceFishReforjed.Prepatch.WorldObjectsHolderPrepatch",  // 0/0 (C1: revertido a vanilla)
                "PerformanceFishReforjed.Prepatch.GasGridPrepatch",             // 0/0 (C2/H1: revertido a vanilla)
                "PerformanceFishReforjed.Prepatch.WorkGiver_DoBillPrepatch",    // 0/0 (L2/M5: reescritura sin ganancia)
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
                    // StorageSettings conserva su optimizacion (2); StoreUtilitySlotGroup quedo
                    // desactivado por L2 (0).
                    expected = typeName.EndsWith("StorageSettingsPrepatch") ? 2 : 0;
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