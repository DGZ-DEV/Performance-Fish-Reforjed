// This file is a Modification (MPL-2.0, section 1.10) of the original
// Performance Fish by bradson (https://github.com/bbradson/Performance-Fish),
// which is covered by the Mozilla Public License 2.0.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.CompilerServices;
using System.Security;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using ConstructorInfo = System.Reflection.ConstructorInfo;
using MethodBody = Mono.Cecil.Cil.MethodBody;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Motor de reescritura del ensamblado del juego.
    ///
    /// Reimplementado desde cero para 1.6 tomando como referencia el PrepatchManager de
    /// Performance Fish. Esta es la pieza que habilita todo lo demas: sin ella no se puede
    /// tocar el IL del juego ni anadirle campos, que es lo que sostiene las caches.
    ///
    /// Trabaja sobre el ModuleDefinition que entrega Prepatcher antes de que Assembly-CSharp
    /// se cargue. Todo lo que se modifique aqui se serializa y el juego lo recarga.
    /// </summary>
    internal static class PrepatchManager
    {
        /// <summary>Metodos con cuerpo recorridos.</summary>
        internal static int MethodsProcessed;

        /// <summary>Metodos a los que se les desactivo la inicializacion de locales.</summary>
        internal static int InitLocalsDisabled;

        /// <summary>Atributos anadidos o reescritos.</summary>
        internal static int AttributesChanged;

        /// <summary>
        /// Ejecuta todas las fases del motor. El orden importa: primero la reescritura de IL y
        /// despues los atributos, para que el ensamblado quede marcado como modificado.
        /// </summary>
        internal static void Run(ModuleDefinition module)
        {
            MethodsProcessed = 0;
            InitLocalsDisabled = 0;
            AttributesChanged = 0;

            ModifyAllTypes(module);
            AddAttributes(module);

            // Hallazgo L3: cada grupo consulta PrepatchConfig (ajustes leidos del XML en esta fase,
            // antes de que exista el Mod del juego) y se salta entero si el jugador lo desactivo.
            // Un grupo desactivado deja sus contadores en 0/0 en la marca, igual que uno que no
            // existe en esta version de Assembly-CSharp.
            if (!PrepatchConfig.IsGroupDisabled("prepatchDisableDefStatCache"))
                DefStatCachePrepatch.Apply(module);
            if (!PrepatchConfig.IsGroupDisabled("prepatchDisableGetCompCaching"))
                GetCompCachingPrepatch.Apply(module);
            if (!PrepatchConfig.IsGroupDisabled("prepatchDisableListerThings"))
                ListerThingsPrepatch.Apply(module);
            if (!PrepatchConfig.IsGroupDisabled("prepatchDisableListerBuildings"))
                ListerBuildingsPrepatch.Apply(module);
            if (!PrepatchConfig.IsGroupDisabled("prepatchDisableGridsUtility"))
                GridsUtilityPrepatch.Apply(module);
            if (!PrepatchConfig.IsGroupDisabled("prepatchDisableStorageSettings"))
                StorageSettingsPrepatch.Start(module);
            if (!PrepatchConfig.IsGroupDisabled("prepatchDisableRoom"))
                RoomPrepatch.Apply(module);
            if (!PrepatchConfig.IsGroupDisabled("prepatchDisableWorldPawns"))
                WorldPawnsPrepatch.Apply(module);
        }

        // ─── Reescritura de IL ───────────────────────────────────────────────────────

        private static void ModifyAllTypes(ModuleDefinition module)
        {
            Collection<TypeDefinition> types = module.Types;
            for (int i = 0; i < types.Count; i++)
                ModifyAllMethodsIn(types[i]);
        }

        /// <summary>
        /// Recorre los metodos del tipo y luego los de sus tipos anidados. Se itera hacia atras
        /// porque una fase posterior puede eliminar instrucciones.
        /// </summary>
        private static void ModifyAllMethodsIn(TypeDefinition type)
        {
            Collection<MethodDefinition> methods = type.Methods;
            for (int i = methods.Count; i-- > 0;)
            {
                MethodBody? body = methods[i].Body;
                if (body != null)
                    ModifyMethodBody(body);
            }

            Collection<TypeDefinition> nestedTypes = type.NestedTypes;
            for (int i = nestedTypes.Count; i-- > 0;)
                ModifyAllMethodsIn(nestedTypes[i]);
        }

        private static void ModifyMethodBody(MethodBody body)
        {
            MethodsProcessed++;
            ApplySkipLocalsInit(body);
        }

        /// <summary>
        /// Desactiva el zeroing de variables locales. El codigo generado por C# siempre asigna
        /// antes de leer, asi que el zeroing es coste puro en cada llamada.
        /// </summary>
        private static void ApplySkipLocalsInit(MethodBody body)
        {
            if (!body.InitLocals) return;
            body.InitLocals = false;
            InitLocalsDisabled++;
        }

        // ─── Atributos ───────────────────────────────────────────────────────────────

        private static void AddAttributes(ModuleDefinition module)
        {
            AddModuleAttributes(module);
            AddAssemblyAttributes(module);
        }

        private static void AddModuleAttributes(ModuleDefinition module)
            => TryAddAttribute(module, module.CustomAttributes, typeof(UnverifiableCodeAttribute));

        /// <summary>
        /// Anade al ensamblado del juego los atributos que necesita para que el IL que le
        /// inyectamos pueda saltarse las comprobaciones de visibilidad y verificacion.
        /// </summary>
        private static void AddAssemblyAttributes(ModuleDefinition module)
        {
            IList<CustomAttribute> attributes = module.Assembly.CustomAttributes;

            if (TryAddAttribute(module, attributes, typeof(IgnoresAccessChecksToAttribute), typeof(string))
                is { } ignoresAccessChecksTo)
            {
                ignoresAccessChecksTo.ConstructorArguments.Add(
                    new CustomAttributeArgument(module.TypeSystem.String, "mscorlib"));
            }

            TryAddAttribute(module, attributes, typeof(AllowPartiallyTrustedCallersAttribute));
            TryAddAttribute(module, attributes, typeof(SecurityTransparentAttribute));

            if (TryAddAttribute(module, attributes, typeof(SecurityRulesAttribute), typeof(SecurityRuleSet))
                is { } securityRules)
            {
                securityRules.ConstructorArguments.Add(new CustomAttributeArgument(
                    module.ImportReference(typeof(SecurityRuleSet)), SecurityRuleSet.Level2));

                securityRules.Properties.Add(new CustomAttributeNamedArgument(
                    nameof(SecurityRulesAttribute.SkipVerificationInFullTrust),
                    new CustomAttributeArgument(module.TypeSystem.Boolean, true)));
            }

            // DebuggableAttribute ya existe en el ensamblado: se reescribe a (false, false) para
            // quitar generacion de informacion de depuracion en runtime.
            foreach (CustomAttribute attribute in attributes)
            {
                if (attribute.AttributeType.Name != nameof(DebuggableAttribute))
                    continue;

                ConstructorInfo? ctor = typeof(DebuggableAttribute).GetConstructor(new[] { typeof(bool), typeof(bool) });
                if (ctor == null) break;

                attribute.Constructor = module.ImportReference(ctor);
                attribute.ConstructorArguments.Clear();
                attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.Boolean, false));
                attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.Boolean, false));
                AttributesChanged++;
                break;
            }
        }

        /// <summary>
        /// Anade un atributo solo si no estaba ya, devolviendo la instancia para poder completar
        /// sus argumentos. Devuelve null si ya existia o si el constructor no se encontro.
        /// </summary>
        private static CustomAttribute? TryAddAttribute(ModuleDefinition module,
            IList<CustomAttribute> attributes, Type attributeType, params Type[] parameters)
        {
            if (ContainsAttribute(attributes, attributeType.Name))
                return null;

            ConstructorInfo? ctor = AccessTools.Constructor(attributeType, parameters);
            if (ctor == null)
                return null;

            var attribute = new CustomAttribute(module.ImportReference(ctor));
            attributes.Add(attribute);
            AttributesChanged++;
            return attribute;
        }

        private static bool ContainsAttribute(IList<CustomAttribute> attributes, string name)
        {
            for (int i = attributes.Count; i-- > 0;)
                if (attributes[i].AttributeType.Name == name)
                    return true;

            return false;
        }
    }
}
