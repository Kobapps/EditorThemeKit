using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EditorThemeKit
{
    /// <summary>
    /// Reads/writes the active theme as JSON under the project's <c>UserSettings/</c>
    /// folder, so the choice is per-user and does not travel with the shared project.
    /// </summary>
    public static class ThemeStorage
    {
        private const string FolderName = "EditorThemeKit";
        private const string FileName = "ActiveTheme.json";

        /// <summary>Absolute path to <c>&lt;project&gt;/UserSettings</c>.</summary>
        public static string UserSettingsRoot
        {
            get
            {
                // Application.dataPath is "<project>/Assets".
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                  ?? Directory.GetCurrentDirectory();
                return Path.Combine(projectRoot, "UserSettings");
            }
        }

        public static string FolderPath => Path.Combine(UserSettingsRoot, FolderName);

        public static string FilePath => Path.Combine(FolderPath, FileName);

        public static bool Exists() => File.Exists(FilePath);

        /// <summary>Loads the saved theme, or null if none/invalid.</summary>
        public static EditorThemeData Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return null;

                var json = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                var data = JsonUtility.FromJson<EditorThemeData>(json);
                return (data != null && data.Entries.Count > 0) ? data : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Editor Theme Kit] Failed to load theme: {e.Message}");
                return null;
            }
        }

        /// <summary>Loads the saved theme, or the default preset if none is saved.</summary>
        public static EditorThemeData LoadOrDefault() => Load() ?? ThemePresets.Default();

        public static void Save(EditorThemeData data)
        {
            if (data == null)
                return;

            try
            {
                Directory.CreateDirectory(FolderPath);
                var json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Editor Theme Kit] Failed to save theme: {e.Message}");
            }
        }

        /// <summary>Deletes the saved theme (used by "Revert to Unity default").</summary>
        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Editor Theme Kit] Failed to clear theme: {e.Message}");
            }
        }

        // ---- Named custom themes (per-user library) --------------------------------

        public static string ThemesFolder => Path.Combine(FolderPath, "Themes");

        private static string CustomThemePath(string name) =>
            Path.Combine(ThemesFolder, MakeSafeFileName(name) + ".json");

        /// <summary>Saves a named custom theme to <c>UserSettings/EditorThemeKit/Themes/</c>.</summary>
        public static void SaveCustomTheme(EditorThemeData data, string name)
        {
            if (data == null || string.IsNullOrWhiteSpace(name))
                return;
            try
            {
                Directory.CreateDirectory(ThemesFolder);
                data.displayName = name;
                data.presetId = ThemePresets.CustomId;
                File.WriteAllText(CustomThemePath(name), JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Editor Theme Kit] Failed to save custom theme '{name}': {e.Message}");
            }
        }

        public static EditorThemeData LoadCustomTheme(string name)
        {
            try
            {
                var path = CustomThemePath(name);
                if (!File.Exists(path))
                    return null;
                var data = JsonUtility.FromJson<EditorThemeData>(File.ReadAllText(path));
                return (data != null && data.Entries.Count > 0) ? data : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Editor Theme Kit] Failed to load custom theme '{name}': {e.Message}");
                return null;
            }
        }

        /// <summary>Names of all saved custom themes, sorted.</summary>
        public static List<string> ListCustomThemes()
        {
            var names = new List<string>();
            try
            {
                if (Directory.Exists(ThemesFolder))
                {
                    foreach (var file in Directory.GetFiles(ThemesFolder, "*.json"))
                        names.Add(Path.GetFileNameWithoutExtension(file));
                    names.Sort(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Editor Theme Kit] Failed to list custom themes: {e.Message}");
            }
            return names;
        }

        public static bool CustomThemeExists(string name) => File.Exists(CustomThemePath(name));

        public static void DeleteCustomTheme(string name)
        {
            try
            {
                var path = CustomThemePath(name);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Editor Theme Kit] Failed to delete custom theme '{name}': {e.Message}");
            }
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }
}
