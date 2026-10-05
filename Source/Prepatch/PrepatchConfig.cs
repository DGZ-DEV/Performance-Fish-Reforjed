using System;
using System.Collections.Generic;
using System.IO;

namespace PerformanceFishReforjed.Prepatch
{
    /// <summary>
    /// Ajustes de prepatch leidos ANTES de que exista el <c>Mod</c> del juego.
    ///
    /// Hallazgo L3: <c>PrepatchManager</c> aplicaba todos los grupos incondicionalmente porque
    /// corre antes de que RimWorld cargue los ajustes del mod. Esto lee el XML de ajustes
    /// directamente del disco en esa fase y permite desactivar grupos completos (por ejemplo uno
    /// que falle en la version de Assembly-CSharp del jugador) sin quitar el mod.
    ///
    /// Reglas de seguridad:
    /// <list type="bullet">
    /// <item>Si el archivo no existe, no se puede leer o no contiene la clave, el grupo queda
    /// ACTIVO (comportamiento por defecto). Un fallo de lectura NUNCA desactiva nada.</item>
    /// <item>Los ajustes de la pagina del mod (<c>PerformanceFishReforjedSettings</c>) escriben
    /// las mismas claves; el prepatch las lee en el siguiente arranque, como todos los cambios de
    /// prepatch.</item>
    /// </list>
    /// </summary>
    internal static class PrepatchConfig
    {
        private static Dictionary<string, bool>? _disabledCache;

        /// <summary>True si el grupo indicado esta desactivado por el jugador.</summary>
        internal static bool IsGroupDisabled(string groupKey)
        {
            try
            {
                Dictionary<string, bool>? disabled = Load();
                return disabled != null && disabled.TryGetValue(groupKey, out bool value) && value;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static Dictionary<string, bool>? Load()
        {
            if (_disabledCache != null)
                return _disabledCache;

            string? path = LocateSettingsFile();
            if (path == null)
            {
                _disabledCache = new Dictionary<string, bool>();
                return _disabledCache;
            }

            Dictionary<string, bool> result = new Dictionary<string, bool>();
            try
            {
                string text = File.ReadAllText(path);
                foreach (string key in GroupKeys)
                {
                    result[key] = ReadBool(text, key);
                }
            }
            catch (Exception)
            {
                // Lectura fallida: nada desactivado.
                result = new Dictionary<string, bool>();
            }

            _disabledCache = result;
            return _disabledCache;
        }

        /// <summary>Claves de los grupos que se pueden desactivar.</summary>
        private static readonly string[] GroupKeys =
        {
            "prepatchDisableDefStatCache",
            "prepatchDisableGetCompCaching",
            "prepatchDisableListerThings",
            "prepatchDisableListerBuildings",
            "prepatchDisableGridsUtility",
            "prepatchDisableStorageSettings",
            "prepatchDisableRoom",
            "prepatchDisableWorldPawns",
        };

        /// <summary>
        /// Busca el XML de ajustes donde RimWorld guarda los ModSettings. En 1.6 (Windows) es
        /// <c>%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\
        /// PerformanceFishReforjed.xml</c>. Tambien acepta una copia junto al DLL del mod para
        /// pruebas. Sin dependencias del juego (GenFilePaths puede no estar inicializado cuando
        /// Prepatcher parchea).
        /// </summary>
        private static string? LocateSettingsFile()
        {
            try
            {
                string localLow = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string candidatesPath = Path.Combine(localLow, "Ludeon Studios", "RimWorld by Ludeon Studios", "Config", "PerformanceFishReforjed.xml");
                if (File.Exists(candidatesPath))
                    return candidatesPath;

                string modLocal = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PerformanceFishReforjed.xml");
                if (File.Exists(modLocal))
                    return modLocal;
            }
            catch (Exception)
            {
            }

            return null;
        }

        /// <summary>
        /// Lee un booleano del XML de Scribe: <c>&lt;clave&gt;True&lt;/clave&gt;</c> o con valor
        /// entre <c>value=</c>. Se hace con busqueda de texto (sin System.Xml) para no depender de
        /// nada mas que mscorlib en la fase de prepatch.
        /// </summary>
        private static bool ReadBool(string xml, string key)
        {
            int idx = xml.IndexOf("<" + key + ">", StringComparison.Ordinal);
            if (idx < 0)
                idx = xml.IndexOf(key + ">", StringComparison.Ordinal);
            if (idx < 0)
                return false;

            int valueStart = xml.IndexOf(">", idx, StringComparison.Ordinal);
            if (valueStart < 0)
                return false;
            valueStart++;

            int valueEnd = xml.IndexOf("<", valueStart, StringComparison.Ordinal);
            if (valueEnd < 0)
                return false;

            string value = xml.Substring(valueStart, valueEnd - valueStart).Trim();
            return value.Equals("True", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
