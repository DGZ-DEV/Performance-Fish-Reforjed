using System;
using Mono.Cecil;
using Prepatcher;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Punto de entrada del motor de prepatching.
    ///
    /// Prepatcher invoca este metodo ANTES de que Assembly-CSharp se cargue, entregando el
    /// modulo del juego ya leido con Mono.Cecil. Eso permite reescribir su IL y anadir campos
    /// y tipos al juego, que es la base sobre la que se construyen las caches de rendimiento.
    ///
    /// IMPORTANTE (comprobacion de vida): Prepatcher reinicia el proceso despues de parchear.
    /// Cualquier estado que guardemos en campos estaticos se pierde en ese reinicio, asi que la
    /// unica prueba fiable de que el motor corrio es dejar la marca DENTRO del ensamblado que se
    /// serializa y se recarga; de eso se encarga Stamp.
    ///
    /// Nota: Mono.Cecil viaja ILMerged dentro de 0Harmony.dll (tipos internos), por eso el
    /// proyecto publiciza 0Harmony. Es el mismo camino que usa el propio Prepatcher.
    /// </summary>
    public static class FreePatchEntry
    {
        /// <summary>Nombre completo del tipo que inyectamos en Assembly-CSharp como marca.</summary>
        internal const string StampTypeName = "PerformanceFishReforjed.PrepatchStamp";

        /// <summary>Campo constante del tipo de marca.</summary>
        internal const string StampFieldName = "Stamp";

        [FreePatch]
        public static void Start(ModuleDefinition module)
        {
            int typeCount = module.Types.Count;
            int methodCount = 0;
            foreach (TypeDefinition type in module.Types)
                methodCount += type.Methods.Count;

            // Fase 1c: reescritura masiva de IL y atributos de ensamblado.
            PrepatchManager.Run(module);

            string stamp = string.Join("|", new[]
            {
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                module.Assembly.Name.Name ?? "?",
                typeCount.ToString(),
                methodCount.ToString(),
                $"initLocalsOff={PrepatchManager.InitLocalsDisabled}/{PrepatchManager.MethodsProcessed}",
                $"attrs={PrepatchManager.AttributesChanged}",
                $"gettersRewritten={DefStatCachePrepatch.GettersRewritten}",
                $"compPatches={GetCompCachingPrepatch.PatchesApplied}/{GetCompCachingPrepatch.ExpectedPatches}",
                $"compPatchesFailed={GetCompCachingPrepatch.PatchesFailed}",
                $"listerPatches={ListerThingsPrepatch.PatchesApplied}/{ListerThingsPrepatch.ExpectedPatches}",
                $"listerPatchesFailed={ListerThingsPrepatch.PatchesFailed}",
                $"buildingPatches={ListerBuildingsPrepatch.PatchesApplied}/{ListerBuildingsPrepatch.ExpectedPatches}",
                $"buildingPatchesFailed={ListerBuildingsPrepatch.PatchesFailed}",
                $"gridPatches={GridsUtilityPrepatch.PatchesApplied}/{GridsUtilityPrepatch.ExpectedPatches}",
                $"gridPatchesFailed={GridsUtilityPrepatch.PatchesFailed}",
                $"storagePatches={StorageSettingsPrepatch.PatchedCount}/{StorageSettingsPrepatch.PatchedCount + StorageSettingsPrepatch.FailedCount}",
                $"storagePatchesFailed={StorageSettingsPrepatch.FailedCount}",
                $"slotGroupPatches={StoreUtilitySlotGroupPrepatch.PatchedCount}/{StoreUtilitySlotGroupPrepatch.PatchedCount + StoreUtilitySlotGroupPrepatch.FailedCount}",
                $"slotGroupPatchesFailed={StoreUtilitySlotGroupPrepatch.FailedCount}",
                $"roomPatches={RoomPrepatch.PatchesApplied}/{RoomPrepatch.ExpectedPatches}",
                $"roomPatchesFailed={RoomPrepatch.PatchesFailed}",
                $"worldPawnsPatches={WorldPawnsPrepatch.PatchesApplied}/{WorldPawnsPrepatch.ExpectedPatches}",
                $"worldPawnsPatchesFailed={WorldPawnsPrepatch.PatchesFailed}",
                $"worldObjectsHolderPatches={WorldObjectsHolderPrepatch.PatchesApplied}/{WorldObjectsHolderPrepatch.ExpectedPatches}",
                $"worldObjectsHolderPatchesFailed={WorldObjectsHolderPrepatch.PatchesFailed}",
                $"gasGridPatches={GasGridPrepatch.PatchesApplied}/{GasGridPrepatch.ExpectedPatches}",
                $"gasGridPatchesFailed={GasGridPrepatch.PatchesFailed}",
                $"workGiverPatches={WorkGiver_DoBillPrepatch.PatchesApplied}/{WorkGiver_DoBillPrepatch.ExpectedPatches}",
                $"workGiverPatchesFailed={WorkGiver_DoBillPrepatch.PatchesFailed}"
            });

            AddStamp(module, stamp);
        }

        /// <summary>
        /// Anade (o reemplaza) un tipo marcador dentro del ensamblado del juego, con un campo
        /// constante que lleva la informacion de diagnostico. Al ser un campo literal no hace
        /// falta constructor estatico ni inicializacion en runtime.
        /// </summary>
        private static void AddStamp(ModuleDefinition module, string stamp)
        {
            TypeDefinition? existing = module.GetType(StampTypeName);
            if (existing != null)
                module.Types.Remove(existing);

            var stampType = new TypeDefinition(
                "PerformanceFishReforjed",
                "PrepatchStamp",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed,
                module.TypeSystem.Object);

            var stampField = new FieldDefinition(
                StampFieldName,
                FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault,
                module.TypeSystem.String)
            {
                Constant = stamp
            };

            stampType.Fields.Add(stampField);
            module.Types.Add(stampType);
        }
    }
}