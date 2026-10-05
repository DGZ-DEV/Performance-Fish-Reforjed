using System;
using HarmonyLib;
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

        /// <summary>Pagina de ajustes minima: solo el registro de log.</summary>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled("Log messages", ref PerformanceFishReforjedSettings.EnableInternalLogging,
                "Write mod diagnostics to the in-game console when its patches are applied.");

            listing.Gap(8f);
            listing.Label($"Prepatch stamp: {ReadStamp() ?? "not found"}");

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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref EnableInternalLogging, "enableInternalLogging", true);
        }
    }
}