using System;
using System.Reflection;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MethodBody = Mono.Cecil.Cil.MethodBody;
using MethodImplAttributes = Mono.Cecil.MethodImplAttributes;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Reescritura de los cuatro getters de stats de <see cref="Verse.ThingDef"/> para que pasen
    /// por la cache en lugar de recalcular <c>StatWorker.GetValueAbstract</c> en cada acceso.
    ///
    /// Cada cuerpo se sustituye por una unica llamada a nuestra cache:
    /// <code>ldarg.0; call Cache.X; ret</code>
    ///
    /// Se hace en el prepatch y no con Harmony para que el coste en el camino caliente sea cero:
    /// la llamada es directa, sin el envoltorio de un parche.
    /// </summary>
    internal static class DefStatCachePrepatch
    {
        internal static int GettersRewritten;

        private const string ThingDefTypeName = "Verse.ThingDef";
        private const string StatCacheTypeName = "PerformanceFishReforjed.Caching.DefStatCache";

        private static readonly (string Getter, string Replacement)[] Targets =
        {
            ("get_BaseMarketValue", nameof(Caching.DefStatCache.BaseMarketValue)),
            ("get_BaseMass", nameof(Caching.DefStatCache.BaseMass)),
            ("get_BaseFlammability", nameof(Caching.DefStatCache.BaseFlammability)),
            ("get_BaseMaxHitPoints", nameof(Caching.DefStatCache.BaseMaxHitPoints))
        };

        internal static void Apply(ModuleDefinition module)
        {
            GettersRewritten = 0;

            TypeDefinition? thingDef = module.GetType(ThingDefTypeName);
            if (thingDef == null)
                return;

            foreach ((string getter, string replacement) in Targets)
                ReplaceBodyWithCall(module, thingDef, getter, StatCacheTypeName, replacement);
        }

        /// <summary>
        /// Sustituye el cuerpo del getter por <c>ldarg.0; call reemplazo; ret</c>.
        /// Devuelve false sin tocar nada si el objetivo no existe o no tiene cuerpo.
        /// </summary>
        private static bool ReplaceBodyWithCall(ModuleDefinition module, TypeDefinition type,
            string getterName, string replacementTypeName, string replacementMethodName)
        {
            MethodDefinition? target = null;
            foreach (MethodDefinition method in type.Methods)
            {
                if (method.Name == getterName && method.Parameters.Count == 0)
                {
                    target = method;
                    break;
                }
            }

            if (target == null || !target.HasBody)
                return false;

            MethodInfo? replacement = AccessTools.Method(typeof(Caching.DefStatCache), replacementMethodName);
            if (replacement == null)
                return false;

            MethodBody body = target.Body;
            body.Variables.Clear();
            body.ExceptionHandlers.Clear();
            body.Instructions.Clear();

            ILProcessor il = body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Call, module.ImportReference(replacement)));
            il.Append(il.Create(OpCodes.Ret));

            body.MaxStackSize = 1;
            target.ImplAttributes |= MethodImplAttributes.AggressiveInlining;
            GettersRewritten++;
            return true;
        }
    }
}
