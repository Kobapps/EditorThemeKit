using System;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace EditorThemeKit
{
    /// <summary>
    /// Owns the live editor theme. Generates USS overriding Unity's internal editor
    /// selectors and writes it as <c>dark.uss</c> + <c>light.uss</c> under an
    /// <c>Editor/StyleSheets/Extensions/</c> folder — Unity's editor stylesheet-extension
    /// mechanism then merges it into the editor skin and recolors the chrome (window
    /// bodies, dock tabs, title bars, toolbars, controls). A companion IMGUI pass covers
    /// the legacy controls USS can't reach. The active theme is saved per-user under
    /// <c>UserSettings/</c> and re-applied on startup.
    ///
    /// Writing the USS + <see cref="AssetDatabase.Refresh()"/> reimports a stylesheet asset;
    /// it does NOT recompile C# or trigger a domain reload, so switching stays instant.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorThemeApplier
    {
        // A folder literally named "Editor" makes Unity treat this as editor resources and
        // scan the Extensions subfolder for skin stylesheets.
        private const string ExtFolder = "Assets/EditorThemeKit.Generated/Editor/StyleSheets/Extensions";
        private const string DarkUss = ExtFolder + "/dark.uss";
        private const string LightUss = ExtFolder + "/light.uss";

        private static EditorThemeData _current;

        public static EditorThemeData Current => _current;
        public static event Action<EditorThemeData> ThemeChanged;

        static EditorThemeApplier()
        {
            EditorApplication.delayCall += InitialLoad;
        }

        private static void InitialLoad()
        {
            try
            {
                if (ThemeStorage.Exists())
                    Apply(ThemeStorage.LoadOrDefault(), persist: false);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Editor Theme Kit] Startup apply failed: {e.Message}");
            }
        }

        /// <summary>
        /// Applies <paramref name="theme"/> live. When <paramref name="persist"/> is true the
        /// theme is also saved to the per-user <c>UserSettings/</c> folder.
        /// </summary>
        public static void Apply(EditorThemeData theme, bool persist = true)
        {
            if (theme == null)
                return;

            _current = theme;

            // Match Unity's base skin to the theme so default (unstyled) text stays readable
            // — a Light theme on the Dark skin renders light-on-light text, and vice versa.
            EnsureBaseSkin(theme.baseSkin);

            try
            {
                var uss = UssThemeGenerator.Generate(theme);
                WriteExtensionUss(uss);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Editor Theme Kit] Failed to write theme USS: {e.Message}");
            }

            ImguiThemePass.Apply(theme);

            if (persist)
                ThemeStorage.Save(theme);

            ThemeChanged?.Invoke(theme);
        }

        /// <summary>Removes the theme and restores the stock Unity skin, live.</summary>
        public static void RevertToDefault(bool clearSaved = true)
        {
            try
            {
                DeleteWithMeta(ToAbsolute(DarkUss));
                DeleteWithMeta(ToAbsolute(LightUss));
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Editor Theme Kit] Revert issue: {e.Message}");
            }

            ImguiThemePass.Revert();

            _current = null;
            if (clearSaved)
                ThemeStorage.Clear();

            ThemeChanged?.Invoke(null);
        }

        // Writes the same USS to dark.uss and light.uss (so it applies in either skin) and
        // reimports. Unity re-merges editor extension stylesheets on import — no domain reload.
        private static void WriteExtensionUss(string uss)
        {
            Directory.CreateDirectory(ToAbsolute(ExtFolder));
            File.WriteAllText(ToAbsolute(DarkUss), uss);
            File.WriteAllText(ToAbsolute(LightUss), uss);
            WriteFolderReadme(ToAbsolute("Assets/EditorThemeKit.Generated"));
            AssetDatabase.Refresh();
        }

        // Switches Unity's editor skin (Pro/Personal) to match the theme's base skin, if it
        // doesn't already. SwitchSkinAndRepaintAllViews toggles, so guard on the current skin.
        private static void EnsureBaseSkin(EditorThemeSkin skin)
        {
            try
            {
                bool wantPro = skin != EditorThemeSkin.Light;
                if (EditorGUIUtility.isProSkin != wantPro)
                    InternalEditorUtility.SwitchSkinAndRepaintAllViews();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Editor Theme Kit] Could not switch base skin: {e.Message}");
            }
        }

        private static void DeleteWithMeta(string absPath)
        {
            if (File.Exists(absPath)) File.Delete(absPath);
            if (File.Exists(absPath + ".meta")) File.Delete(absPath + ".meta");
        }

        private static string ToAbsolute(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void WriteFolderReadme(string absFolder)
        {
            var readme = Path.Combine(absFolder, "README.txt");
            if (File.Exists(readme))
                return;
            Directory.CreateDirectory(absFolder);
            File.WriteAllText(readme,
                "Generated by Editor Theme Kit. The Editor/StyleSheets/Extensions/*.uss files\n" +
                "here are build artifacts (Unity's editor skin-extension mechanism reads them).\n" +
                "Safe to git-ignore: Assets/EditorThemeKit.Generated/\n");
        }
    }
}
