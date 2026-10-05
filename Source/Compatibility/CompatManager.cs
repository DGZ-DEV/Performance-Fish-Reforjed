using System.Text;
using Verse;

namespace PerformanceFishReforjed.Compatibility
{
    /// <summary>
    /// Capa de compatibilidad del Reforjed con los mods detectados.
    ///
    /// Modelo de coexistencia:
    ///  - Las reescrituras de IL del Reforjed sustituyen el CUERPO de un metodo al cargar
    ///    (vía Prepatcher). Los parches de Harmony de cualquier otro mod envuelven el método FINAL,
    ///    sea cual sea su cuerpo. Por tanto una reescritura de cuerpo y un Harmony patch NO compiten
    ///    por el mismo método: se apilan de forma segura.
    ///  - El riesgo real no es el mecanismo, sino la SEMANTICA: si otro mod reescribe el mismo cuerpo
    ///    (poco habitual, requiere su propio prepatch) o si espera la firma/fum alcohol exacta del
    ///    vanilla. El Reforjed conserva las firmas vanilla, así que eso nunca cambia.
    ///
    /// Por eso el trabajo real de compatibilidad es: (1) confirmar con datos (no conjeturas) que un
    /// mod no rompe nuestra reescritura, (2) informar de los métodos compartidos en el log, y
    /// (3) permitir al usuario forzar la desactivación de un ajuste concreto por mod si encuentra un
    /// caso particular.
    /// </summary>
    public static class CompatManager
    {
        /// <summary>
        /// Determina si un mod está activo Y su toggle de compatibilidad está habilitado.
        /// </summary>
        public static bool IsActive(CompatMods.Mod mod)
        {
            if (!PerformanceFishReforjedSettings.EnableCompatibility)
                return false;
            if (!GetToggle(mod))
                return false;
            return CompatMods.IsActive(mod);
        }

        /// <summary>Lee el toggle de compatibilidad de un mod desde los ajustes.</summary>
        public static bool GetToggle(CompatMods.Mod mod)
        {
            switch (mod)
            {
                case CompatMods.Mod.Achtung: return PerformanceFishReforjedSettings.Compat_Achtung;
                case CompatMods.Mod.CharacterEditor: return PerformanceFishReforjedSettings.Compat_CharacterEditor;
                case CompatMods.Mod.CombatExtended: return PerformanceFishReforjedSettings.Compat_CombatExtended;
                case CompatMods.Mod.DubsMintMenus: return PerformanceFishReforjedSettings.Compat_DubsMintMenus;
                case CompatMods.Mod.DubsPerformanceAnalyzer: return PerformanceFishReforjedSettings.Compat_DubsPerformanceAnalyzer;
                case CompatMods.Mod.kNumbers: return PerformanceFishReforjedSettings.Compat_kNumbers;
                case CompatMods.Mod.MissileGirl: return PerformanceFishReforjedSettings.Compat_MissileGirl;
                case CompatMods.Mod.SlowerPawnTickRate: return PerformanceFishReforjedSettings.Compat_SlowerPawnTickRate;
                case CompatMods.Mod.PickUpAndHaul: return PerformanceFishReforjedSettings.Compat_PickUpAndHaul;
                case CompatMods.Mod.RimHUD: return PerformanceFishReforjedSettings.Compat_RimHUD;
                case CompatMods.Mod.AllowTool: return PerformanceFishReforjedSettings.Compat_AllowTool;
                case CompatMods.Mod.HugsLib: return PerformanceFishReforjedSettings.Compat_HugsLib;
                case CompatMods.Mod.VanillaExpandedFramework: return PerformanceFishReforjedSettings.Compat_VanillaExpandedFramework;
                case CompatMods.Mod.VanillaVehiclesExpanded: return PerformanceFishReforjedSettings.Compat_VanillaVehiclesExpanded;
                case CompatMods.Mod.VehicleFramework: return PerformanceFishReforjedSettings.Compat_VehicleFramework;
                default: return false;
            }
        }

        private static bool _logged;

        /// <summary>
        /// Escribe en el log el resumen de mods detectados. Se llama una vez tras la carga.
        /// </summary>
        public static void LogDetectedMods()
        {
            if (_logged)
                return;
            _logged = true;

            StringBuilder sb = new StringBuilder();
            sb.Append("[PerformanceFishReforjed] Compatibility scan: ");
            bool any = false;
            foreach (CompatMods.Mod m in (CompatMods.Mod[])System.Enum.GetValues(typeof(CompatMods.Mod)))
            {
                bool present = CompatMods.IsActive(m);
                bool enabled = IsActive(m);
                sb.Append(CompatMods.DisplayName(m)).Append('=').Append(present ? (enabled ? "ON" : "present-disabled") : "off").Append(", ");
                if (present)
                    any = true;
            }

            if (!any)
                sb.Append("no external mods detected.");

            if (PerformanceFishReforjedSettings.EnableInternalLogging)
                Log.Message(sb.ToString());
        }
    }
}