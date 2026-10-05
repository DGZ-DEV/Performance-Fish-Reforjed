using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace PFRSim
{
    /// <summary>
    /// Simulador de optimizacion de Performance Fish Reforjed.
    ///
    /// FASE A (estructural): corre la pipeline real del prepatch sobre una copia del Assembly-CSharp
    ///    y revisa con Mono.Cecil el IL de cada objetivo activo, comprobando que su cuerpo contiene
    ///    una llamada a un miembro de PerformanceFishReforjed (la reescritura quedó ENGANCHADA a
    ///    nuestra cache, no es un cuerpo generico ni muerto). Tambien imprime los contadores que el
    ///    propio mod mantiene al aplicar el prepatch.
    ///
    /// FASE B (benchmark headless): compara, sobre datos sinteticos identicos y con aserción de que
    ///    ambos devuelven lo mismo, la primitiva de cache O(1) (tabla hash de sonda abierta, copia
    ///    literal de nuestro Source/Caching/IntCache.cs) contra el barrido vanilla O(n). Esta es la
    ///    estructura de datos que hace la optimización: sustituir un escaneo de lista por consulta
    ///    a tabla. Mide ns/op y speedup.
    ///
    /// LIMITACION HONESTA: el DLL del mod compila contra el runtime Mono/Unity del juego y accede a
    ///    campos internos de CoreLib (p.ej. List&lt;T&gt;._version) via IgnoresAccessChecksTo("mscorlib");
    ///    el runtime .NET Core 8 de este simulador NO honra ese atributo, asi que el camino caliente
    ///    completo (ThingDef.HasComp, StorageSettings.AllowedToAccept, etc.) NO puede ejecutarse tal
    ///    cual headless. La Fase A prueba que el juego usa esa cache (enganche estructural); la Fase B
    ///    prueba que la estructura de datos es mas rapida que el algoritmo al que sustituye.
    /// </summary>
    internal static class Program
    {
        // Para Fase A: localizar el Assembly-CSharp del juego. Fase B trabaja con tipos sinteticos.
        private const string GameDll = @"C:\RimWorld\RimWorldWin64_Data\Managed\Assembly-CSharp.dll";
        private const string HarmonyDll = @"C:\RimWorld\Mods\Prepatcher\Assemblies\0Harmony.dll";
        private const string ModDll = @"C:\Users\User\Desktop\Performance Fish Reforjed\Assemblies\PerformanceFishReforjed.dll";
        private const string WorkCopy = @"C:\Windows\Temp\pf_sim_Assembly-CSharp.dll";

        private static int _failures;

        // (tipo, metodo, genericArity, paramCount)
        private static readonly (string Type, string Method, int Gen, int Params)[] ActiveTargets =
        {
            // DefStatCachePrepatch (4)
            ("Verse.ThingDef","get_BaseMarketValue",0,0),
            ("Verse.ThingDef","get_BaseMass",0,0),
            ("Verse.ThingDef","get_BaseFlammability",0,0),
            ("Verse.ThingDef","get_BaseMaxHitPoints",0,0),
            // GetCompCachingPrepatch (9)
            ("Verse.ThingDef","GetCompProperties",1,0),
            ("Verse.ThingDef","HasComp",0,1),
            ("Verse.ThingDef","HasComp",1,0),
            ("Verse.HediffDef","CompProps",1,0),
            ("RimWorld.Ability","CompOfType",1,0),
            ("RimWorld.Planet.WorldObject","GetComponent",1,0),
            ("Verse.Map","GetComponent",1,0),
            ("RimWorld.Planet.World","GetComponent",1,0),
            ("Verse.Game","GetComponent",1,0),
            // ListerThingsPrepatch (6)
            ("Verse.ListerThings","Add",0,1),
            ("Verse.ListerThings","Remove",0,1),
            ("Verse.ListerThings","Contains",0,1),
            ("Verse.ListerThings","Clear",0,0),
            ("Verse.ListerThings","GetThingsOfType",1,0),
            ("Verse.ListerThings","GetThingsOfType",1,1),
            // ListerBuildingsPrepatch (7)
            ("Verse.ListerBuildings","AllBuildingsColonistOfDef",0,1),
            ("Verse.ListerBuildings","ColonistsHaveBuilding",0,1),
            ("Verse.ListerBuildings","ColonistsHaveResearchBench",0,0),
            ("Verse.ListerBuildings","ColonistsHaveBuildingWithPowerOn",0,1),
            ("Verse.ListerBuildings","AllBuildingsColonistOfClass",1,0),
            ("Verse.ListerBuildings","AllColonistBuildingsOfType",1,0),
            ("Verse.ListerBuildings","AllBuildingsNonColonistOfDef",0,1),
            // GridsUtilityPrepatch (1)
            ("Verse.GridsUtility","GetItemCount",0,2),
            // StorageSettingsPrepatch (2)
            ("RimWorld.StorageSettings","AllowedToAccept",0,1),
            // RoomPrepatch (1)
            ("Verse.Room","get_ContainedBeds",0,0),
            // WorldPawnsPrepatch (2)
            ("RimWorld.Planet.WorldPawns","get_AllPawnsAlive",0,0),
            ("RimWorld.Planet.WorldPawns","get_AllPawnsAliveOrDead",0,0),
        };

        private static int Main()
        {
            Console.WriteLine("=======================================================");
            Console.WriteLine(" PFRSim - Simulador de optimizacion de Reforjed");
            Console.WriteLine("=======================================================");

            Directory.CreateDirectory(@"C:\Windows\Temp");
            File.Copy(GameDll, WorkCopy, overwrite: true);
            Console.WriteLine($"[prep] Copia de Assembly-CSharp preparada ({new FileInfo(WorkCopy).Length} bytes).");
            Console.WriteLine($"       Version objetivo: {ReadAssemblyFileVersion(GameDll)}");

            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                string name = new AssemblyName(e.Name).Name;
                string direct = @"C:\RimWorld\RimWorldWin64_Data\Managed\" + name + ".dll";
                if (File.Exists(direct)) return Assembly.LoadFrom(direct);
                string prepatcher = @"C:\RimWorld\Mods\Prepatcher\Assemblies\" + name + ".dll";
                if (File.Exists(prepatcher)) return Assembly.LoadFrom(prepatcher);
                string binLocal = AppDomain.CurrentDomain.BaseDirectory + name + ".dll";
                if (File.Exists(binLocal)) return Assembly.LoadFrom(binLocal);
                return null;
            };

            Assembly harmony = Assembly.LoadFrom(HarmonyDll);
            Assembly mod = Assembly.LoadFrom(ModDll);
            Type prepatchManager = mod.GetType("PerformanceFishReforjed.Prepatch.PrepatchManager");
            if (prepatchManager == null) { Console.WriteLine("ERROR: PrepatchManager no encontrado."); return 1; }

            ModuleDefinition module = ModuleDefinition.ReadModule(WorkCopy);
            MethodInfo run = prepatchManager.GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (run == null) { Console.WriteLine("ERROR: PrepatchManager.Run no encontrado."); return 1; }
            try { run.Invoke(null, new object[] { module }); }
            catch (TargetInvocationException tie)
            {
                Console.WriteLine("ERROR durante el prepatch:\n" + (tie.InnerException ?? tie));
                return 1;
            }
            Console.WriteLine("[prep] PrepatchManager.Run ejecutado sobre la copia.");
            Console.WriteLine();

            PhaseA(module, mod);
            Console.WriteLine();
            PhaseB();

            Console.WriteLine();
            Console.WriteLine(_failures > 0
                ? $"[PFRSim] RESULTADO: {_failures} FALLO(S)."
                : "[PFRSim] RESULTADO: SIN fallos estructurales; benchmarks completados.");
            return _failures > 0 ? 1 : 0;
        }

        // ------------------------------------------------------------------ FASE A
        private static void PhaseA(ModuleDefinition module, Assembly mod)
        {
            Console.WriteLine("-- FASE A: enganche estructural del prepatch --");

            var distinctWired = new HashSet<string>();
            int totalWired = 0;
            foreach (var (typeName, methodName, gen, prms) in ActiveTargets)
            {
                var methods = FindMethods(module, typeName, methodName, gen, prms);
                if (methods.Count == 0)
                {
                    Fail($"FASE A: no existe {typeName}.{methodName} (gen={gen}, params={prms})");
                    continue;
                }
                int wiredHere = methods.Count(m => CallsReforjed(m));
                totalWired += wiredHere;
                if (wiredHere == 0)
                    Fail($"FASE A: {typeName}.{methodName} no llama a PerformanceFishReforjed");
                else
                    distinctWired.Add(typeName + "." + methodName);
            }

            Console.WriteLine($"      Nombres (tipo.metodo) distintos con al menos 1 sobrecarga enganchada: {distinctWired.Count}");
            Console.WriteLine($"      Sobrecargas enganchadas a nuestro codigo: {totalWired}/32");

            // Contadores que el propio mod mantiene al aplicar el prepatch.
            // DefStat usa GettersRewritten; StorageSettings usa PatchedCount/FailedCount; el resto PatchesApplied.
            PrintCounter(mod, "PerformanceFishReforjed.Prepatch.DefStatCachePrepatch", "GettersRewritten");
            PrintCounter(mod, "PerformanceFishReforjed.Prepatch.GetCompCachingPrepatch", "PatchesApplied");
            PrintCounter(mod, "PerformanceFishReforjed.Prepatch.ListerThingsPrepatch", "PatchesApplied");
            PrintCounter(mod, "PerformanceFishReforjed.Prepatch.ListerBuildingsPrepatch", "PatchesApplied");
            PrintCounter(mod, "PerformanceFishReforjed.Prepatch.GridsUtilityPrepatch", "PatchesApplied");
            PrintCounter(mod, "PerformanceFishReforjed.Prepatch.RoomPrepatch", "PatchesApplied");
            PrintCounter(mod, "PerformanceFishReforjed.Prepatch.WorldPawnsPrepatch", "PatchesApplied");
            PrintCounter(mod, "PerformanceFishReforjed.Prepatch.StorageSettingsPrepatch", "PatchedCount");

            // Sellos estaticos: prueban que el motor de prepatch corrio y escribio las transformaciones.
            Type stamps = mod.GetType("PerformanceFishReforjed.Prepatch.PrepatchStamps");
            if (stamps != null)
            {
                var fields = stamps.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                   .Where(f => f.IsStatic);
                foreach (FieldInfo f in fields)
                    Console.WriteLine($"      stamp {f.Name} = {f.GetValue(null)}");
            }
        }

        private static void PrintCounter(Assembly mod, string typeName, string fieldName)
        {
            Type t = mod.GetType(typeName);
            if (t == null) { Fail("FASE A: falta tipo " + typeName); return; }
            int v = GetIntStatic(t, fieldName);
            Console.WriteLine($"      {t.Name,-38} {fieldName}={v}");
            if (v < 0) Fail("FASE A: " + t.Name + " contador " + fieldName + " no leido (=-1).");
        }

        private static bool CallsReforjed(MethodDefinition m)
        {
            if (!m.HasBody) return false;
            foreach (Instruction ins in m.Body.Instructions)
            {
                if (ins.Operand is MethodReference mr && mr.DeclaringType != null &&
                    mr.DeclaringType.FullName.StartsWith("PerformanceFishReforjed"))
                    return true;
                if (ins.Operand is FieldReference fr && fr.DeclaringType != null &&
                    fr.DeclaringType.FullName.StartsWith("PerformanceFishReforjed"))
                    return true;
            }
            return false;
        }

        private static List<MethodDefinition> FindMethods(ModuleDefinition module, string typeName,
            string methodName, int gen, int prms)
        {
            var result = new List<MethodDefinition>();
            TypeDefinition type = module.GetType(typeName);
            if (type == null) return result;
            foreach (MethodDefinition m in type.Methods)
                if (m.Name == methodName && m.GenericParameters.Count == gen && m.Parameters.Count == prms)
                    result.Add(m);
            return result;
        }

        // ------------------------------------------------------------------ FASE B
        private static void PhaseB()
        {
            Console.WriteLine("-- FASE B: primitiva de cache O(1) contra barrido vanilla O(n) --");
            Console.WriteLine("  (copia literal de nuestra IntCache.cs: tabla de sonda abierta)");
            Console.WriteLine();

            int defsCount = 64;
            int compsPerDef = 7;
            int hitsPerWorkload = 400000;

            // Generar identifiers (claves) sinteticos; cada def agrupa compsPerDef "class-hashes".
            var keys = new int[defsCount];
            for (int d = 0; d < defsCount; d++) keys[d] = d * 31 + 1;

            // ---- Correctness: la cache y el barrido deben coincidir en TODA la carga.
            {
                var cache = new HashCache(defsCount * compsPerDef);
                // sembrar: hash(key)->presente
                for (int d = 0; d < defsCount; d++)
                    cache.Put(keys[d], d);

                bool eq = true;
                for (int w = 0; w < hitsPerWorkload; w++)
                {
                    int k = keys[w % defsCount];
                    bool cachedHit = cache.ContainsKey(k);
                    bool vanillaHit = VanillaScan(defsCount, compsPerDef, keys, k);
                    if (cachedHit != vanillaHit) { eq = false; break; }
                }
                Console.WriteLine($"      Igualdad de resultados cache==barrido: {(eq ? "OK (todas las cargas coinciden)" : "DISCREPANCIA!")}");
                if (!eq) Fail("FASE B: la cache no coincide con el barrido en la carga de prueba.");
            }

            // ---- Benchmark lookup cache O(1)
            var cacheMain = new HashCache(defsCount * compsPerDef);
            for (int d = 0; d < defsCount; d++) cacheMain.Put(keys[d], d);
            // warmup
            for (int i = 0; i < 20000; i++) _ = cacheMain.ContainsKey(keys[i % defsCount]);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < hitsPerWorkload; i++) _ = cacheMain.ContainsKey(keys[i % defsCount]);
            sw.Stop();
            double cacheNs = sw.Elapsed.TotalMilliseconds * 1_000_000 / hitsPerWorkload;

            // ---- Benchmark barrido vanilla O(n)
            for (int i = 0; i < 20000; i++) _ = VanillaScan(defsCount, compsPerDef, keys, keys[i % defsCount]);
            sw.Restart();
            for (int i = 0; i < hitsPerWorkload; i++) _ = VanillaScan(defsCount, compsPerDef, keys, keys[i % defsCount]);
            sw.Stop();
            double vanillaNs = sw.Elapsed.TotalMilliseconds * 1_000_000 / hitsPerWorkload;

            // ---- escala: la desventaja del barrido crece con n; la cache no.
            int bigCount = 512;
            var bigKeys = new int[bigCount];
            for (int d = 0; d < bigCount; d++) bigKeys[d] = d * 131 + 7;
            var bigCache = new HashCache(bigCount * 3);
            for (int d = 0; d < bigCount; d++) bigCache.Put(bigKeys[d], d);
            for (int i = 0; i < 20000; i++) _ = bigCache.ContainsKey(bigKeys[i % bigCount]);
            sw.Restart();
            for (int i = 0; i < hitsPerWorkload; i++) _ = bigCache.ContainsKey(bigKeys[i % bigCount]);
            sw.Stop();
            double cacheBigNs = sw.Elapsed.TotalMilliseconds * 1_000_000 / hitsPerWorkload;

            for (int i = 0; i < 20000; i++) _ = VanillaScan(bigCount, 48, bigKeys, bigKeys[i % bigCount]);
            sw.Restart();
            for (int i = 0; i < hitsPerWorkload; i++) _ = VanillaScan(bigCount, 48, bigKeys, bigKeys[i % bigCount]);
            sw.Stop();
            double vanillaBigNs = sw.Elapsed.TotalMilliseconds * 1_000_000 / hitsPerWorkload;

            Console.WriteLine();
            Console.WriteLine($"      Escenario tipico ({defsCount} defs x {compsPerDef} comps):");
            PrintLine("cache O(1)", cacheNs, vanillaNs);
            Console.WriteLine($"      Escenario grande   ({bigCount} defs x 48 comps):");
            PrintLine("cache O(1)", cacheBigNs, vanillaBigNs);

            Console.WriteLine();
            Console.WriteLine("  Interpretacion: el barrido vanilla cuesta proporcional a la longitud de la");
            Console.WriteLine("  lista; la cache es O(1) independientemente de cuantos comps/defs haya.");
            Console.WriteLine("  El speedup depende del tamano de la lista que se sustituye.");
        }

        private static void PrintLine(string name, double cachedNs, double vanillaNs)
        {
            double speedup = cachedNs > 0 ? vanillaNs / cachedNs : 0;
            Console.WriteLine($"         {name,-12} cached={cachedNs,8:F1} ns/op   vanilla={vanillaNs,8:F1} ns/op   speedup=×{speedup:F2}");
        }

        /// <summary>Barrido vanilla: recorrer la lista de comps comparando la clave objetivo.</summary>
        private static bool VanillaScan(int defsCount, int compsPerDef, int[] keys, int target)
        {
            // La lista "vanilla" es una secuencia de claves, una por def, buscando coincidencia.
            // Replica el costo: recorrer hasta (generalmente) el final comparando identidad.
            int found = 0;
            for (int d = 0; d < defsCount; d++)
            {
                if (keys[d] == target)
                {
                    // en el juego: comps[i].compClass == el tipo buscado? entonces true; si no,
                    // sigue. Aqui la coincidencia existe (cada target es una clave real).
                    for (int c = 0; c < compsPerDef; c++)
                    {
                        // comparación seria compClass == target; aproximamos el costo de la comparación.
                        if ((keys[d] + c * 3) == target) found++;
                    }
                    break; // vanilla rompe en la primera coincidencia de compClass
                }
            }
            return found > 0;
        }

        /// <summary>
        /// Copia literal del algoritmo de <c>IntCache&lt;TValue&gt;</c> de nuestro projecto, especializado
        /// a int. Mismo mezclador, mismo sondeo lineal, mismo crecimiento por potencia de dos.
        /// </summary>
        private sealed class HashCache
        {
            private const int Empty = int.MinValue;
            private int[] _keys;
            private int[] _values;
            private int _mask;
            private int _count;

            public HashCache(int capacity)
            {
                int size = 16;
                while (size < capacity) size <<= 1;
                _keys = new int[size];
                _values = new int[size];
                _mask = size - 1;
                for (int i = 0; i < size; i++) _keys[i] = Empty;
            }

            public void Put(int key, int value)
            {
                if ((_count + 1) * 4 >= _keys.Length * 3) Grow();
                int index = Mix(key) & _mask;
                while (true)
                {
                    int slot = _keys[index];
                    if (slot == key) { _values[index] = value; return; }
                    if (slot == Empty) { _keys[index] = key; _values[index] = value; _count++; return; }
                    index = (index + 1) & _mask;
                }
            }

            public bool ContainsKey(int key)
            {
                int index = Mix(key) & _mask;
                while (true)
                {
                    int slot = _keys[index];
                    if (slot == key) return true;
                    if (slot == Empty) return false;
                    index = (index + 1) & _mask;
                }
            }

            private void Grow()
            {
                int[] oldKeys = _keys;
                int[] oldVals = _values;
                int newSize = _keys.Length * 2;
                _keys = new int[newSize];
                _values = new int[newSize];
                _mask = newSize - 1;
                _count = 0;
                for (int i = 0; i < newSize; i++) _keys[i] = Empty;
                for (int i = 0; i < oldKeys.Length; i++)
                {
                    if (oldKeys[i] != Empty)
                        Put(oldKeys[i], oldVals[i]);
                }
            }

            private static int Mix(int key)
            {
                // Replica EXACTA del mezclador murmur-style de IntCache.cs (lineas 310-321).
                uint x = (uint)key;
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;
                return (int)x;
            }
        }

        // ------------------------------------------------------------------ helpers
        private static int GetIntStatic(Type type, string fieldName)
        {
            FieldInfo f = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            return f == null ? -1 : (int)f.GetValue(null);
        }

        private static void Fail(string msg)
        {
            _failures++;
            Console.WriteLine("   [FALLO] " + msg);
        }

        private static string ReadAssemblyFileVersion(string path)
        {
            try { return AssemblyName.GetAssemblyName(path).Version.ToString(); }
            catch { return "?"; }
        }
    }
}