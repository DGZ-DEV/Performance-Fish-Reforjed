using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PerformanceFishReforjed.Compatibility
{
    /// <summary>
    /// Deteccion optativa de mods de terceros por <c>packageId</c>.
    ///
    /// Regla de diseno del proyecto: el modulo NO puede depender en ningun caso de que estos mods
    /// esten instalados. La deteccion se hace SOLO por identificador de paquete contra la lista de
    /// mods activos de RimWorld (<see cref="ModsConfig"/>) y nunca por referencia dura a sus
    /// ensamblados. Si un mod no esta presente, todo el codigo de compatibilidad simplemente no
    /// actua, y el rendimiento/base del Reforjed queda intacto.
    ///
    /// Cada mod se consulta con <see cref="IsActive(ModRef)"/>. La presencia se cachea una vez por
    /// carga porque la lista de mods no cambia en runtime.
    /// </summary>
    public static class CompatMods
    {
        /// <summary>Identificadores de los mods cubiertos por la capa de compatibilidad.</summary>
        public enum Mod
        {
            Achtung,
            CharacterEditor,
            CombatExtended,
            DubsMintMenus,
            DubsPerformanceAnalyzer,
            kNumbers,
            MissileGirl,
            SlowerPawnTickRate,
            PickUpAndHaul,
            RimHUD,
            AllowTool,
            HugsLib,
            VanillaExpandedFramework,
            VanillaVehiclesExpanded,
            VehicleFramework
        }

        private static readonly Dictionary<Mod, string> PackageIds = new Dictionary<Mod, string>
        {
            [Mod.Achtung] = "brrainz.achtung",
            [Mod.CharacterEditor] = "void.charactereditor",
            [Mod.CombatExtended] = "CETeam.CombatExtended",
            [Mod.DubsMintMenus] = "Dubwise.DubsMintMenus",
            [Mod.DubsPerformanceAnalyzer] = "Dubwise.DubsPerformanceAnalyzer",
            [Mod.kNumbers] = "koisama.numbers",
            [Mod.MissileGirl] = "vr.missilegirl",
            [Mod.SlowerPawnTickRate] = "Arkymn.SlowerPawnTickRate",
            [Mod.PickUpAndHaul] = "Mehni.PickUpAndHaul",
            [Mod.RimHUD] = "Jaxe.RimHUD",
            [Mod.AllowTool] = "UnlimitedHugs.AllowTool",
            [Mod.HugsLib] = "UnlimitedHugs.HugsLib",
            [Mod.VanillaExpandedFramework] = "OskarPotocki.VanillaFactionsExpanded.Core",
            [Mod.VanillaVehiclesExpanded] = "OskarPotocki.VanillaVehiclesExpanded",
            [Mod.VehicleFramework] = "SmashPhil.VehicleFramework"
        };

        /// <summary>Mods cuyo packageId no es fiable y se detectan por nombre de ensamblado ("fallback").</summary>
        private static readonly Dictionary<Mod, string> AssemblyNameFallbacks = new Dictionary<Mod, string>
        {
            // kNumbers (koisama) es el "Numbers" antiguo: su About.xml no declara packageId moderno
            // (targetVersion 0.16). Se detecta por la presencia del ensamblado "RWNumbers" cargado.
            [Mod.kNumbers] = "RWNumbers"
        };

        private static Dictionary<Mod, bool>? _cache;

        /// <summary>
        /// Devuelve true si el mod esta activo. El resultado se cachea tras la primera consulta
        /// (la lista de mods es fija durante una sesion).
        /// </summary>
        public static bool IsActive(Mod mod)
        {
            if (_cache == null)
            {
                _cache = new Dictionary<Mod, bool>();
                foreach (Mod m in (Mod[])System.Enum.GetValues(typeof(Mod)))
                    _cache[m] = Detect(m);
            }

            return _cache[mod];
        }

        /// <summary>True si cualquiera de los mods dados esta activo.</summary>
        public static bool AnyActive(params Mod[] mods)
        {
            foreach (Mod m in mods)
                if (IsActive(m))
                    return true;
            return false;
        }

        /// <summary>Nombre legible para el log/ajustes de un mod.</summary>
        public static string DisplayName(Mod mod)
        {
            switch (mod)
            {
                case Mod.Achtung: return "Achtung!";
                case Mod.CharacterEditor: return "Character Editor";
                case Mod.CombatExtended: return "Combat Extended";
                case Mod.DubsMintMenus: return "Dubs Mint Menus";
                case Mod.DubsPerformanceAnalyzer: return "Dubs Performance Analyzer";
                case Mod.kNumbers: return "Numbers (kNumbers)";
                case Mod.MissileGirl: return "MissileGirl";
                case Mod.SlowerPawnTickRate: return "Performance - Slower Pawn Tick Rate";
                case Mod.PickUpAndHaul: return "Pick Up And Haul";
                case Mod.RimHUD: return "RimHUD";
                case Mod.AllowTool: return "Allow Tool";
                case Mod.HugsLib: return "HugsLib";
                case Mod.VanillaExpandedFramework: return "Vanilla Expanded Framework";
                case Mod.VanillaVehiclesExpanded: return "Vanilla Vehicles Expanded";
                case Mod.VehicleFramework: return "Vehicle Framework";
                default: return mod.ToString();
            }
        }

        private static bool Detect(Mod mod)
        {
            if (PackageIds.TryGetValue(mod, out string? pkg) && ModsConfig.IsActive(pkg))
                return true;

            // Fallback por nombre de ensamblado solo cuando el packageId no declaro positivo.
            if (AssemblyNameFallbacks.TryGetValue(mod, out string? asmName))
                return IsAssemblyLoaded(asmName);

            return false;
        }

        private static bool IsAssemblyLoaded(string assemblyName)
        {
            foreach (System.Reflection.Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.GetName().Name?.Equals(assemblyName, System.StringComparison.OrdinalIgnoreCase) == true)
                        return true;
                }
                catch
                {
                    // ignorar ensamblados que no se puedan inspeccionar
                }
            }

            return false;
        }
    }
}