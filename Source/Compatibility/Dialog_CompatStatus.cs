using UnityEngine;
using Verse;

namespace PerformanceFishReforjed.Compatibility
{
    /// <summary>
    /// Ventana de ajustes de compatibilidad: lista cada mod cubierto, si esta detectado, y permite
    /// alternar su ajuste de compatibilidad. No desactiva el rendimiento base: solo controla los
    /// ajustes de coexistencia de cada mod.
    /// </summary>
    public class Dialog_CompatStatus : Window
    {
        private Vector2 _scrollPos;
        private readonly bool[] _flags;
        private readonly string[] _labels;
        private readonly bool[] _present;

        public Dialog_CompatStatus()
        {
            doCloseButton = true;
            closeOnCancel = true;
            absorbInputAroundWindow = true;
            forcePause = true;
            doWindowBackground = true;

            var mods = (CompatMods.Mod[])System.Enum.GetValues(typeof(CompatMods.Mod));
            _flags = new bool[mods.Length];
            _labels = new string[mods.Length];
            _present = new bool[mods.Length];
            for (int i = 0; i < mods.Length; i++)
            {
                _flags[i] = CompatManager.GetToggle(mods[i]);
                _labels[i] = CompatMods.DisplayName(mods[i]);
                _present[i] = CompatMods.IsActive(mods[i]);
            }
        }

        public override Vector2 InitialSize
        {
            get
            {
                int rows = _flags.Length;
                float h = 150f + rows * 26f;
                return new Vector2(680f, Mathf.Min(h, 720f));
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("Compatibility for other mods");
            Text.Font = GameFont.Small;
            listing.Gap(6f);

            listing.Label("Each row shows a covered mod: whether it is present ('[detected]') and " +
                          "whether its compatibility adjustments are enabled. The Reforjed " +
                          "optimizations run regardless; these only tune coexistence.");
            listing.Gap(6f);

            Rect scroll = listing.GetRect(inRect.height - listing.CurHeight - 30f);
            if (scroll.height < 40f)
                scroll.height = 40f;

            Rect content = new Rect(0f, 0f, scroll.width - 30f, 20f + _flags.Length * 26f);
            Widgets.BeginScrollView(scroll, ref _scrollPos, content);

            var inner = new Listing_Standard();
            inner.Begin(content);

            var mods = (CompatMods.Mod[])System.Enum.GetValues(typeof(CompatMods.Mod));
            for (int i = 0; i < mods.Length; i++)
            {
                bool enabled = _flags[i];
                string label = _labels[i] + (_present[i] ? "  [detected]" : "");
                inner.CheckboxLabeled(label, ref enabled, "Enable compatibility adjustments for this mod.");
                if (enabled != _flags[i])
                {
                    _flags[i] = enabled;
                    SetToggle(mods[i], enabled);
                }
            }

            inner.End();
            Widgets.EndScrollView();

            listing.End();
        }

        private static void SetToggle(CompatMods.Mod mod, bool value)
        {
            switch (mod)
            {
                case CompatMods.Mod.Achtung: PerformanceFishReforjedSettings.Compat_Achtung = value; break;
                case CompatMods.Mod.CharacterEditor: PerformanceFishReforjedSettings.Compat_CharacterEditor = value; break;
                case CompatMods.Mod.CombatExtended: PerformanceFishReforjedSettings.Compat_CombatExtended = value; break;
                case CompatMods.Mod.DubsMintMenus: PerformanceFishReforjedSettings.Compat_DubsMintMenus = value; break;
                case CompatMods.Mod.DubsPerformanceAnalyzer: PerformanceFishReforjedSettings.Compat_DubsPerformanceAnalyzer = value; break;
                case CompatMods.Mod.kNumbers: PerformanceFishReforjedSettings.Compat_kNumbers = value; break;
                case CompatMods.Mod.MissileGirl: PerformanceFishReforjedSettings.Compat_MissileGirl = value; break;
                case CompatMods.Mod.SlowerPawnTickRate: PerformanceFishReforjedSettings.Compat_SlowerPawnTickRate = value; break;
                case CompatMods.Mod.PickUpAndHaul: PerformanceFishReforjedSettings.Compat_PickUpAndHaul = value; break;
                case CompatMods.Mod.RimHUD: PerformanceFishReforjedSettings.Compat_RimHUD = value; break;
                case CompatMods.Mod.AllowTool: PerformanceFishReforjedSettings.Compat_AllowTool = value; break;
                case CompatMods.Mod.HugsLib: PerformanceFishReforjedSettings.Compat_HugsLib = value; break;
                case CompatMods.Mod.VanillaExpandedFramework: PerformanceFishReforjedSettings.Compat_VanillaExpandedFramework = value; break;
                case CompatMods.Mod.VanillaVehiclesExpanded: PerformanceFishReforjedSettings.Compat_VanillaVehiclesExpanded = value; break;
                case CompatMods.Mod.VehicleFramework: PerformanceFishReforjedSettings.Compat_VehicleFramework = value; break;
            }
        }
    }
}