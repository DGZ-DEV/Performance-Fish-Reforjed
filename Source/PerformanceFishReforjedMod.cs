using System;
using HarmonyLib;
using PerformanceFishReforjed.Compatibility;
using PerformanceFishReforjed.Prepatch;
using UnityEngine;
using Verse;

namespace PerformanceFishReforjed
{
    /// <summary>
    /// Clase principal del mod. La logica de optimizacion se aplica en el prepatch
    /// (<see cref="FreePatchEntry"/>); esta clase solo arranca Harmony, registra los ajustes y
    /// confirma en el log que el motor de prepatching corrio.
    /// </summary>
    public class PerformanceFishReforjedMod : Mod
    {
        /// <summary>Instancia de Harmony del mod. Id = packageId.</summary>
        internal static readonly Harmony HarmonyInstance = new Harmony("dgz.performance");

        public PerformanceFishReforjedMod(ModContentPack content) : base(content)
        {
            GetSettings<PerformanceFishReforjedSettings>();

            // El motor de prepatching se ejecuto antes de que exista esta clase (Prepatcher trabaja
            // mientras se cargan los ensamblados), asi que aqui ya podemos confirmarlo en el log.
            LogPrepatchStatus();
        }

        /// <summary>Nombre del mod en la lista de ajustes.</summary>
        public override string SettingsCategory() => "Performance Fish Reforjed";

        /// <summary>Pagina de ajustes: log, marca de prepatching y compatibilidad con otros mods.</summary>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled("Log messages", ref PerformanceFishReforjedSettings.EnableInternalLogging,
                "Write mod diagnostics to the in-game console when its patches are applied.");

            listing.CheckboxLabeled("Compatibility layer", ref PerformanceFishReforjedSettings.EnableCompatibility,
                "Apply compatibility adjustments when other mods are detected. The Reforjed still optimizes either way.");

            listing.Gap(8f);
            listing.Label("Prepatch groups (apply on next game start):");
            listing.CheckboxLabeled("ThingDef stat cache", ref PerformanceFishReforjedSettings.PrepatchGroup_DefStatCache,
                "Uncheck to disable the BaseMarketValue/BaseMass/BaseFlammability/BaseMaxHitPoints cache.");
            listing.CheckboxLabeled("Component cache", ref PerformanceFishReforjedSettings.PrepatchGroup_GetCompCaching,
                "Uncheck to disable the def/ability/map/world/game comp caches.");
            listing.CheckboxLabeled("Thing lists (ListerThings)", ref PerformanceFishReforjedSettings.PrepatchGroup_ListerThings,
                "Uncheck to disable the per-def/per-group thing indexes.");
            listing.CheckboxLabeled("Building lists (ListerBuildings)", ref PerformanceFishReforjedSettings.PrepatchGroup_ListerBuildings,
                "Uncheck to disable the colonist building indexes.");
            listing.CheckboxLabeled("Per-cell item counter", ref PerformanceFishReforjedSettings.PrepatchGroup_GridsUtility,
                "Uncheck to disable the GridsUtility.GetItemCount counter.");
            listing.CheckboxLabeled("Storage filters (AllowedToAccept)", ref PerformanceFishReforjedSettings.PrepatchGroup_StorageSettings,
                "Uncheck to disable the AllowedToAccept caches.");
            listing.CheckboxLabeled("Room beds (ContainedBeds)", ref PerformanceFishReforjedSettings.PrepatchGroup_Room,
                "Uncheck to disable the Room.ContainedBeds index.");
            listing.CheckboxLabeled("World pawns lists", ref PerformanceFishReforjedSettings.PrepatchGroup_WorldPawns,
                "Uncheck to disable the AllPawnsAlive/AllPawnsAliveOrDead caches.");

            listing.Gap(8f);
            listing.Label($"Prepatch stamp: {ReadStamp() ?? "not found"}");

            listing.Gap(8f);
            if (listing.ButtonText("Compatibility details"))
            {
                var dialog = new Dialog_CompatStatus();
                Find.WindowStack.Add(dialog);
            }

            listing.End();
        }

        /// <summary>
        /// Comprueba que el motor de prepatching llego a ejecutarse leyendo la marca que este
        /// inyecto dentro de Assembly-CSharp. No sirve un campo estatico propio porque Prepatcher
        /// reinicia el proceso despues de parchear.
        /// </summary>
        public static void LogPrepatchStatus()
        {
            string? stamp = ReadStamp();

            if (stamp == null)
            {
                Log.Warning("[PerformanceFishReforjed] Prepatch stamp not found in Assembly-CSharp. " +
                            "The prepatch engine did not run: check that Prepatcher (zetrith.prepatcher) is enabled and first in the mod list.");
                return;
            }

            Log.Message($"[PerformanceFishReforjed] Prepatch engine OK. Stamp: {stamp}");
        }

        /// <summary>
        /// Lee el campo constante que el free patch dejo en el ensamblado del juego.
        /// Formato: fecha|ensamblado|tipos|metodos|parches...
        /// </summary>
        private static string? ReadStamp()
        {
            try
            {
                Type? stampType = typeof(Game).Assembly.GetType(FreePatchEntry.StampTypeName, throwOnError: false);
                return stampType?.GetField(FreePatchEntry.StampFieldName)?.GetValue(null) as string;
            }
            catch (Exception e)
            {
                Log.Warning($"[PerformanceFishReforjed] Error reading the prepatch stamp: {e.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Ajustes del mod.
    /// </summary>
    public class PerformanceFishReforjedSettings : ModSettings
    {
        /// <summary>Escribe diagnostico en la consola del juego.</summary>
        public static bool EnableInternalLogging = true;

        /// <summary>
        /// Compatibilidad con otros mods: si true (por defecto), el Reforjed aplica automaticamente
        /// los ajustes de compatibilidad necesarios para coexistir con cada mod detectado. Los
        /// flags por-mod permiten forzar el estado si un usuario encuentra un caso particular.
        /// </summary>
        public static bool EnableCompatibility = true;

        // Toggles por grupo de prepatch (Hallazgo L3). Default true = grupo ACTIVO.
        // En el XML se guarda la clave inversa `prepatchDisable*` (true = desactivado), que es la
        // que lee PrepatchConfig antes de cargar el juego; un cambio aquí se aplica en el
        // siguiente arranque (como todo lo que toca el prepatch).
        public static bool PrepatchGroup_DefStatCache = true;
        public static bool PrepatchGroup_GetCompCaching = true;
        public static bool PrepatchGroup_ListerThings = true;
        public static bool PrepatchGroup_ListerBuildings = true;
        public static bool PrepatchGroup_GridsUtility = true;
        public static bool PrepatchGroup_StorageSettings = true;
        public static bool PrepatchGroup_Room = true;
        public static bool PrepatchGroup_WorldPawns = true;

        // Toggles individuales por mod. Default true = comportamiento recomendado (coexistir y
        // conservar la optimizacion). Poner false desactiva SOLO los ajustes de compatibilidad de
        // ese mod (no desactiva el rendimiento base del Reforjed).
        public static bool Compat_Achtung = true;
        public static bool Compat_CharacterEditor = true;
        public static bool Compat_CombatExtended = true;
        public static bool Compat_DubsMintMenus = true;
        public static bool Compat_DubsPerformanceAnalyzer = true;
        public static bool Compat_kNumbers = true;
        public static bool Compat_MissileGirl = true;
        public static bool Compat_SlowerPawnTickRate = true;
        public static bool Compat_PickUpAndHaul = true;
        public static bool Compat_RimHUD = true;
        public static bool Compat_AllowTool = true;
        public static bool Compat_HugsLib = true;
        public static bool Compat_VanillaExpandedFramework = true;
        public static bool Compat_VanillaVehiclesExpanded = true;
        public static bool Compat_VehicleFramework = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref EnableInternalLogging, "enableInternalLogging", true);
            Scribe_Values.Look(ref EnableCompatibility, "enableCompatibility", true);

            // Los grupos se guardan como "desactivados" en el XML (la clave que lee PrepatchConfig
            // en la fase de prepatch): se escribe el inverso del toggle y se lee invirtiendo.
            LookGroup(ref PrepatchGroup_DefStatCache, "prepatchDisableDefStatCache");
            LookGroup(ref PrepatchGroup_GetCompCaching, "prepatchDisableGetCompCaching");
            LookGroup(ref PrepatchGroup_ListerThings, "prepatchDisableListerThings");
            LookGroup(ref PrepatchGroup_ListerBuildings, "prepatchDisableListerBuildings");
            LookGroup(ref PrepatchGroup_GridsUtility, "prepatchDisableGridsUtility");
            LookGroup(ref PrepatchGroup_StorageSettings, "prepatchDisableStorageSettings");
            LookGroup(ref PrepatchGroup_Room, "prepatchDisableRoom");
            LookGroup(ref PrepatchGroup_WorldPawns, "prepatchDisableWorldPawns");

            Scribe_Values.Look(ref Compat_Achtung, "compatAchtung", true);
            Scribe_Values.Look(ref Compat_CharacterEditor, "compatCharacterEditor", true);
            Scribe_Values.Look(ref Compat_CombatExtended, "compatCombatExtended", true);
            Scribe_Values.Look(ref Compat_DubsMintMenus, "compatDubsMintMenus", true);
            Scribe_Values.Look(ref Compat_DubsPerformanceAnalyzer, "compatDubsPerformanceAnalyzer", true);
            Scribe_Values.Look(ref Compat_kNumbers, "compatkNumbers", true);
            Scribe_Values.Look(ref Compat_MissileGirl, "compatMissileGirl", true);
            Scribe_Values.Look(ref Compat_SlowerPawnTickRate, "compatSlowerPawnTickRate", true);
            Scribe_Values.Look(ref Compat_PickUpAndHaul, "compatPickUpAndHaul", true);
            Scribe_Values.Look(ref Compat_RimHUD, "compatRimHUD", true);
            Scribe_Values.Look(ref Compat_AllowTool, "compatAllowTool", true);
            Scribe_Values.Look(ref Compat_HugsLib, "compatHugsLib", true);
            Scribe_Values.Look(ref Compat_VanillaExpandedFramework, "compatVanillaExpandedFramework", true);
            Scribe_Values.Look(ref Compat_VanillaVehiclesExpanded, "compatVanillaVehiclesExpanded", true);
            Scribe_Values.Look(ref Compat_VehicleFramework, "compatVehicleFramework", true);
        }

        /// <summary>
        /// Guarda/lee un toggle de grupo en la clave <c>prepatchDisable*</c> del XML, invertido:
        /// el toggle público es "grupo activo" (true) y en disco se persiste "desactivado" (true),
        /// que es exactamente lo que lee <see cref="Prepatch.PrepatchConfig"/> antes del prepatch.
        /// </summary>
        private static void LookGroup(ref bool groupEnabled, string disableKey)
        {
            bool disabled = !groupEnabled;
            Scribe_Values.Look(ref disabled, disableKey, false);
            groupEnabled = !disabled;
        }
    }
}