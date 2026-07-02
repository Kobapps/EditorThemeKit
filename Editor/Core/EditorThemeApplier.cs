using System;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

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
        private static Color _borderColor;
        private static bool _hasBorder;
        private static double _lastBorderScan;

        public static EditorThemeData Current => _current;
        public static event Action<EditorThemeData> ThemeChanged;

        static EditorThemeApplier()
        {
            EditorApplication.delayCall += InitialLoad;
            EditorApplication.update += OnUpdate;
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

            // Draw a 1px border on every window's content root in the Border color. Unity 6
            // has no themeable inter-window divider style, but each window's rootVisualElement
            // paints — a border on each makes the lines between docked windows clearly visible.
            _borderColor = theme.Get(ThemeColorKey.Border, new Color(0.15f, 0.15f, 0.15f, 1f));
            _hasBorder = true;
            PaintAllWindowBorders();

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

            _hasBorder = false;
            ClearWindowBorders();

            _current = null;
            if (clearSaved)
                ThemeStorage.Clear();

            ThemeChanged?.Invoke(null);
        }

        // Writes the same USS to dark.uss and light.uss (so it applies in either skin) and
        // reimports. Unity re-merges editor extension stylesheets on import — no domain reload.
        private static void WriteExtensionUss(string uss)
        {
            bool folderIsNew = !Directory.Exists(ToAbsolute(ExtFolder));
            Directory.CreateDirectory(ToAbsolute(ExtFolder));
            File.WriteAllText(ToAbsolute(DarkUss), uss);
            File.WriteAllText(ToAbsolute(LightUss), uss);
            WriteFolderReadme(ToAbsolute("Assets/EditorThemeKit.Generated"));

            if (folderIsNew)
            {
                // First run: the folder isn't tracked yet — a full refresh registers it.
                AssetDatabase.Refresh();
            }
            else
            {
                // Reimport only the two files so Unity re-merges the extension stylesheets
                // without a full project refresh (lighter, less visible reload on each switch).
                AssetDatabase.ImportAsset(DarkUss, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(LightUss, ImportAssetOptions.ForceUpdate);
            }

            // Offer (once per project) to git-ignore the generated folder.
            GitignoreHelper.MaybePromptOnce();
        }

        // Keeps window borders painted as windows open/change.
        private static void OnUpdate()
        {
            if (!_hasBorder)
                return;
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastBorderScan < 0.4)
                return;
            _lastBorderScan = now;
            PaintAllWindowBorders();
        }

        private static void PaintAllWindowBorders()
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
                PaintWindowBorder(w);
        }

        private static void PaintWindowBorder(EditorWindow window)
        {
            if (window == null || !_hasBorder)
                return;
            var r = window.rootVisualElement;
            if (r == null)
                return;
            var c = new StyleColor(_borderColor);
            r.style.borderTopColor = c;
            r.style.borderBottomColor = c;
            r.style.borderLeftColor = c;
            r.style.borderRightColor = c;
            r.style.borderTopWidth = 1;
            r.style.borderBottomWidth = 1;
            r.style.borderLeftWidth = 1;
            r.style.borderRightWidth = 1;
        }

        private static void ClearWindowBorders()
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                var r = w != null ? w.rootVisualElement : null;
                if (r == null) continue;
                r.style.borderTopWidth = 0;
                r.style.borderBottomWidth = 0;
                r.style.borderLeftWidth = 0;
                r.style.borderRightWidth = 0;
            }
        }

        private static EditorThemeSkin? _appliedSkin;

        /// <summary>Diagnostic: number of times the editor skin was actually switched.</summary>
        internal static int SkinSwitchCount { get; private set; }

        // Only switches Unity's editor skin (Pro/Personal) when the theme's base skin actually
        // differs from what's active. Switching between two same-skin themes (e.g. dark → dark)
        // must NOT touch the skin. `isProSkin` updates a frame late after a switch, so also
        // track the last-applied skin to avoid a spurious re-switch mid-transition.
        private static void EnsureBaseSkin(EditorThemeSkin skin)
        {
            try
            {
                bool wantPro = skin != EditorThemeSkin.Light;

                if (_appliedSkin == skin)
                    return; // already applied this base skin — never re-switch

                if (EditorGUIUtility.isProSkin != wantPro)
                {
                    InternalEditorUtility.SwitchSkinAndRepaintAllViews();
                    SkinSwitchCount++;
                }
                _appliedSkin = skin;
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
