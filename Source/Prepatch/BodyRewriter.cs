using System;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MethodBody = Mono.Cecil.Cil.MethodBody;
using MethodImplAttributes = Mono.Cecil.MethodImplAttributes;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Motor de sustitucion de cuerpos de metodo, compartido por todos los prepatches.
    ///
    /// Sustituye el cuerpo del objetivo por una llamada directa a un metodo nuestro:
    /// <code>ldarg.0; [ldarg.N...]; call reemplazo; ret</code>
    ///
    /// Tres detalles que no son opcionales:
    ///
    /// 1. El objetivo se localiza por nombre + aridad generica + numero de parametros + estaticidad,
    ///    NUNCA solo por nombre: en 1.6 hay sobrecargas reales (ThingDef.HasComp, los cuatro
    ///    GetComponent, Verse.HediffUtility.TryGetComp, ListerThings.GetThingsOfType...). Elegir mal
    ///    la sobrecarga sustituye el cuerpo equivocado y el fallo es silencioso.
    ///
    /// 2. Si el objetivo es generico, la llamada no puede ser a la definicion abierta: hay que
    ///    construir un <see cref="GenericInstanceMethod"/> cuyos argumentos sean los parametros
    ///    genericos DEL PROPIO metodo del juego, para que el reemplazo reciba el mismo T.
    ///
    /// 3. El reemplazo es estatico, asi que la llamada es <c>call</c> y no <c>callvirt</c>: sin
    ///    comprobacion de nulo ni resolucion virtual en el camino caliente.
    ///
    /// Se publica como metodo "Rewrite" que devuelve si pudo aplicarse; quien lo use cuenta los
    /// exitos y los fallos para poder reflejarlos en la marca de prepatching (el log del juego
    /// todavia no existe cuando esto corre).
    /// </summary>
    internal static class BodyRewriter
    {
        /// <summary>
        /// Busca el objetivo y, si lo encuentra, le sustituye el cuerpo. Devuelve false sin tocar
        /// nada si el tipo o el metodo no existen, o si el reemplazo no aparece.
        /// </summary>
        /// <param name="replacementType">Tipo que declara el metodo de reemplazo.</param>
        /// <param name="replacementName">Nombre del metodo de reemplazo (unico dentro del tipo).</param>
        /// <param name="firstParameterTypeContains">
        /// Si no es null, exige que el tipo del PRIMER parametro del objetivo contenga ese texto. Hace
        /// falta porque hay sobrecargas con el mismo nombre y el mismo numero de parametros que solo
        /// se distinguen por el tipo (por ejemplo <c>ListerBuildings.ColonistsHaveBuilding(ThingDef)</c>
        /// frente a <c>ColonistsHaveBuilding(Func&lt;Thing,bool&gt;)</c>).
        /// </param>
        internal static bool Rewrite(ModuleDefinition module, string typeName, string methodName,
            int genericArity, int parameterCount, bool isStatic, Type replacementType,
            string replacementName, string? firstParameterTypeContains = null)
        {
            MethodDefinition? target = FindTarget(module, typeName, methodName, genericArity,
                parameterCount, isStatic, firstParameterTypeContains);

            if (target == null)
                return false;

            // El reemplazo recibe el receptor delante (o sea, un parametro mas si el objetivo es de
            // instancia), que es lo que distingue las dos sobrecargas de GetThingsOfType.
            int replacementParameters = parameterCount + (isStatic ? 0 : 1);
            MethodInfo? replacement = FindReplacement(replacementType, replacementName,
                genericArity, replacementParameters);

            if (replacement == null)
                return false;

            ReplaceBody(module, target, replacement);
            return true;
        }

        /// <summary>Localiza un metodo por nombre, aridad generica, numero de parametros, estaticidad y, opcionalmente, tipo del primer parametro.</summary>
        internal static MethodDefinition? FindTarget(ModuleDefinition module, string typeName,
            string methodName, int genericArity, int parameterCount, bool isStatic,
            string? firstParameterTypeContains = null)
        {
            TypeDefinition? type = module.GetType(typeName);
            if (type == null)
                return null;

            foreach (MethodDefinition method in type.Methods)
            {
                if (method.Name != methodName)
                    continue;

                if (method.GenericParameters.Count != genericArity)
                    continue;

                if (method.Parameters.Count != parameterCount)
                    continue;

                if (method.IsStatic != isStatic)
                    continue;

                if (!method.HasBody)
                    continue;

                if (firstParameterTypeContains != null)
                {
                    if (parameterCount == 0)
                        continue;

                    if (!method.Parameters[0].ParameterType.FullName.Contains(firstParameterTypeContains))
                        continue;
                }

                return method;
            }

            return null;
        }

        /// <summary>
        /// Localiza el reemplazo en el tipo indicado. La aridad y el numero de parametros se
        /// comprueban para que dos sobrecargas con el mismo nombre no se confundan.
        /// </summary>
        internal static MethodInfo? FindReplacement(Type type, string name, int genericArity,
            int parameterCount)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != name)
                    continue;

                if (method.IsGenericMethodDefinition != (genericArity > 0))
                    continue;

                if (genericArity > 0 && method.GetGenericArguments().Length != genericArity)
                    continue;

                if (method.GetParameters().Length != parameterCount)
                    continue;

                return method;
            }

            return null;
        }

        /// <summary>
        /// Escribe el cuerpo nuevo: pasa el receptor y los argumentos al reemplazo y devuelve su
        /// resultado, instanciando los genericos con los parametros del propio objetivo.
        /// </summary>
        private static void ReplaceBody(ModuleDefinition module, MethodDefinition target,
            MethodInfo replacement)
        {
            MethodReference call = module.ImportReference(replacement);

            if (target.HasGenericParameters && replacement.IsGenericMethodDefinition)
            {
                var instance = new GenericInstanceMethod(call);

                foreach (GenericParameter parameter in target.GenericParameters)
                    instance.GenericArguments.Add(parameter);

                call = instance;
            }

            MethodBody body = target.Body;
            body.Variables.Clear();
            body.ExceptionHandlers.Clear();
            body.Instructions.Clear();

            ILProcessor il = body.GetILProcessor();

            int argumentCount = 0;

            if (!target.IsStatic)
            {
                il.Append(il.Create(OpCodes.Ldarg_0));
                argumentCount++;
            }

            for (int i = 0; i < target.Parameters.Count; i++)
            {
                il.Append(il.Create(OpCodes.Ldarg, target.Parameters[i]));
                argumentCount++;
            }

            il.Append(il.Create(OpCodes.Call, call));
            il.Append(il.Create(OpCodes.Ret));

            body.MaxStackSize = argumentCount < 1 ? 1 : argumentCount;
            target.ImplAttributes |= MethodImplAttributes.AggressiveInlining;
        }
    }
}
