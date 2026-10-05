using System;
using Mono.Cecil;
using PerformanceFishReforjed.Caching;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Reescritura de los métodos de <see cref="RimWorld.WorkGiver_DoBill"/> que se pueden sustituir
    /// de forma segura por una llamada directa a <see cref="WorkGiverDoBillOptimization"/> (Fase 6).
    ///
    /// Se sustituyen SOLO los métodos autocontenidos cuya semántica está verificada contra el IL real
    /// de Assembly-CSharp 1.6.9655 (informes <c>Tools/VANILLA_IL_WORKGIVER.md</c> y
    /// <c>..._NESTED.md</c>). Los métodos pesados de selección de ingredientes que dependen de clases
    /// generadas por el compilador (AllowMix, NoMixHelper, TryFindBestIngredientsHelper) se dejan
    /// intactos: copiar sus cuerpos sería el error que desaconseja el análisis de este proyecto.
    /// </summary>
    internal static class WorkGiver_DoBillPrepatch
    {
        internal static int PatchesApplied;
        internal static int PatchesFailed;

        /// <summary>
        /// Total esperado: 0 sustituciones.
        ///
        /// DESACTIVADO (Hallazgos L2/M5): cada cuerpo de reemplazo coincide con el de vanilla, asi
        /// que no gana nada y bloquea cualquier transpiler futuro sobre esos metodos; el
        /// <c>ShouldSkip</c> de reemplazo ademas llama a la copia estatica de este mod en lugar del
        /// metodo de vanilla, saltandose los parches de Harmony ajenos. Se deja sin aplicar: el
        /// comportamiento es exactamente el de vanilla.
        /// </summary>
        internal const int ExpectedPatches = 0;

        private const string TypeName = "RimWorld.WorkGiver_DoBill";
        private static readonly Type Caches = typeof(WorkGiverDoBillOptimization);

        internal static void Apply(ModuleDefinition module)
        {
            PatchesApplied = 0;
            PatchesFailed = 0;

            // 1. get_PotentialWorkThingRequest() : ThingRequest  (instancia, 0 params)
            Rewrite(module, TypeName, "get_PotentialWorkThingRequest", 0, 0, false,
                nameof(WorkGiverDoBillOptimization.GetPotentialWorkThingRequest));

            // 2. MaxPathDanger(Pawn) : Danger  (instancia, 1 param)
            Rewrite(module, TypeName, "MaxPathDanger", 0, 1, false,
                nameof(WorkGiverDoBillOptimization.MaxPathDanger));

            // 3. ThingIsUsableBillGiver(Thing) : bool  (instancia, 1 param)
            Rewrite(module, TypeName, "ThingIsUsableBillGiver", 0, 1, false,
                nameof(WorkGiverDoBillOptimization.ThingIsUsableBillGiver));

            // 4. ShouldSkip(Pawn, bool) : bool  (instancia, 2 params)
            Rewrite(module, TypeName, "ShouldSkip", 0, 2, false,
                nameof(WorkGiverDoBillOptimization.ShouldSkip));

            // 5. IsUsableIngredient(Thing, Bill) : bool  (estática, 2 params)
            Rewrite(module, TypeName, "IsUsableIngredient", 0, 2, true,
                nameof(WorkGiverDoBillOptimization.IsUsableIngredient));

            // 6. GetBillGiverRootCell(Thing, Pawn) : IntVec3  (estática, 2 params)
            Rewrite(module, TypeName, "GetBillGiverRootCell", 0, 2, true,
                nameof(WorkGiverDoBillOptimization.GetBillGiverRootCell));

            // 7. GetMedicalCareCategory(Thing) : MedicalCareCategory  (estática, 1 param)
            Rewrite(module, TypeName, "GetMedicalCareCategory", 0, 1, true,
                nameof(WorkGiverDoBillOptimization.GetMedicalCareCategory));

            // 8. TryFindBestBillIngredientsInSet(List, Bill, List, IntVec3, bool, List) : bool
            //    (estática, 6 params)
            Rewrite(module, TypeName, "TryFindBestBillIngredientsInSet", 0, 6, true,
                nameof(WorkGiverDoBillOptimization.TryFindBestBillIngredientsInSet));
        }

        private static void Rewrite(ModuleDefinition module, string typeName, string methodName,
            int genericArity, int parameterCount, bool isStatic, string replacementName)
        {
            try
            {
                if (BodyRewriter.Rewrite(module, typeName, methodName, genericArity, parameterCount,
                        isStatic, Caches, replacementName))
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