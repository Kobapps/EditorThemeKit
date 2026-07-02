using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace EditorThemeKit
{
    /// <summary>
    /// Generates the editor-theme USS. Unity 6's editor chrome (window bodies, dock tabs,
    /// title bars, toolbars, controls) is styled by internal USS classes with specific
    /// names — <c>.TabWindowBackground</c>, <c>.ScrollViewAlt</c>, <c>.dockHeader</c>,
    /// <c>.IN Title</c>, <c>.AppToolbar</c>, <c>.Toolbar</c>, <c>.dragtab-label</c>, etc.
    /// Overriding those (via Unity's editor stylesheet-extension mechanism — a
    /// <c>dark.uss</c>/<c>light.uss</c> under an <c>Editor/StyleSheets/Extensions/</c>
    /// folder) is what recolors the chrome. This mirrors the proven approach of the
    /// EditorThemes asset.
    ///
    /// Each semantic color fans out to the group of internal selectors it drives. Button-
    /// and dropdown-like selectors additionally get hover/focus/active/checked pseudo-states
    /// plus matching border + background-image tint.
    /// </summary>
    public static class UssThemeGenerator
    {
        // Semantic color -> the internal editor USS selectors it paints.
        private static readonly (ThemeColorKey key, string[] selectors)[] Groups =
        {
            (ThemeColorKey.WindowBackground, new[]
            {
                "TabWindowBackground", "ScrollViewAlt", "ProjectBrowserTopBarBg",
                "ProjectBrowserBottomBarBg",
                // Hierarchy / list rows (IMGUI TreeView + object lists).
                "TV Line", "OL EntryBackEven", "OL EntryBackOdd", "OL Box",
            }),
            (ThemeColorKey.HeaderBackground, new[]
            {
                // NOTE: the dock header bar + tabs are IMGUI (DockArea/HostView cached
                // GUIStyles) and are handled by ImguiThemePass, not USS. Keep the inspector
                // title + tree bold line here.
                "TV LineBold", "IN Title",
            }),
            (ThemeColorKey.ToolbarBackground, new[]
            {
                "ToolbarDropDownToogleRight", "ToolbarPopupLeft", "ToolbarPopup",
                "toolbarbutton", "PreToolbar", "AppToolbar", "GameViewBackground",
                "CN EntryInfoSmall", "Toolbar", "toolbarbuttonRight", "ProjectBrowserIconAreaBg",
            }),
            (ThemeColorKey.ButtonBackground, new[]
            {
                "AppCommandLeft", "AppCommandMid", "AppCommand", "AppToolbarButtonLeft",
                "AppToolbarButtonRight", "DropDown", "Button", ".unity-button",
                ".unity-base-popup-field__input", ".unity-popup-field__input",
                ".unity-enum-field__input", ".unity-object-field__input",
                ".unity-object-field__selector",
            }),
            (ThemeColorKey.InputBackground, new[]
            {
                "SceneTopBarBg", "MiniPopup", "ExposablePopupMenu", "minibutton",
                "ToolbarSearchTextField", ".unity-base-field__input",
                ".unity-search-field__search-button", ".unity-search-field__cancel-button",
            }),
            (ThemeColorKey.Border, new[]
            {
                // Window/pane split dividers (UITK). IMGUI dock/pane separators are handled
                // in ImguiThemePass.
                ".unity-two-pane-split-view__dragline-anchor",
                ".unity-two-pane-split-view__dragline",
            }),
            // NOTE: Accent/selection is NOT a class group — the list/tree selection is driven
            // by Unity's `--unity-colors-highlight-*` design tokens, emitted as a :root block
            // below (the class-selector bridge doesn't reach the IMGUI tree selection).
        };

        public static string Generate(EditorThemeData theme)
        {
            var sb = new StringBuilder(8192);
            sb.Append("/* ===== Editor Theme Kit — auto-generated. Do not edit. ===== */\n");
            sb.Append("/* Theme: ").Append(theme.displayName).Append(" */\n");

            foreach (var (key, selectors) in Groups)
            {
                if (!theme.TryGet(key, out var color))
                    continue;
                foreach (var sel in selectors)
                {
                    Block(sb, sel, color);
                    if (IsButtonOrDropdown(sel))
                        PseudoStates(sb, sel, color);
                }
            }

            // Text color on labels / text elements.
            if (theme.TryGet(ThemeColorKey.Text, out var text))
            {
                sb.Append("\n.unity-text-element { color: ").Append(Rgba(text)).Append("; }\n");
                sb.Append(".unity-label { color: ").Append(Rgba(text)).Append("; }\n");
            }

            // Selection / highlight — driven by Unity's design tokens (reaches the IMGUI
            // tree/list selection via the editor stylesheet-extension merge).
            if (theme.TryGet(ThemeColorKey.Accent, out var accent))
            {
                var selText = theme.Get(ThemeColorKey.TextSelected, Color.white);
                // Keep the selected-item text readable: darken a bright accent (preserving its
                // hue) when the selection text is light, or lighten a dark accent when it's dark.
                var hlBg = ReadableHighlight(accent, selText);
                sb.Append("\n:root {\n");
                Token(sb, "highlight-background", hlBg);
                Token(sb, "highlight-background-hover", Lighten(hlBg, 0.06f));
                Token(sb, "highlight-background-inactive", Desaturate(hlBg, 0.4f));
                Token(sb, "highlight", hlBg);
                Token(sb, "selection-background", hlBg);
                Token(sb, "highlight-text", selText);
                Token(sb, "highlight-text-inactive", selText);
                Token(sb, "object_selector-highlight", hlBg);
                sb.Append("}\n");
            }

            return sb.ToString();
        }

        private static float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        // Adjusts the highlight background so the selection text contrasts with it. Light text
        // → scale the accent down to a dark target luminance (keeps hue); dark text → blend
        // toward white. Leaves the accent as-is when contrast is already sufficient.
        private static Color ReadableHighlight(Color accent, Color text)
        {
            float bg = Lum(accent);
            if (Lum(text) > 0.5f)
            {
                const float target = 0.38f;
                if (bg > target)
                {
                    float s = target / Mathf.Max(bg, 0.001f);
                    return new Color(accent.r * s, accent.g * s, accent.b * s, accent.a);
                }
            }
            else
            {
                const float target = 0.62f;
                if (bg < target)
                    return Color.Lerp(accent, Color.white, (target - bg) / Mathf.Max(1f - bg, 0.001f));
            }
            return accent;
        }

        private static void Token(StringBuilder sb, string name, Color c)
        {
            sb.Append("\t--unity-colors-").Append(name).Append(": ").Append(Rgba(c)).Append(";\n");
        }

        private static Color Desaturate(Color c, float t)
        {
            float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            return Color.Lerp(c, new Color(lum, lum, lum, c.a), Mathf.Clamp01(t));
        }

        private static void Block(StringBuilder sb, string selectorName, Color color)
        {
            string rgba = Rgba(color);
            sb.Append('\n').Append(Selector(selectorName)).Append("\n{\n");
            sb.Append("\tbackground-color: ").Append(rgba).Append(";\n");
            if (IsButtonOrDropdown(selectorName))
            {
                sb.Append("\t-unity-background-image-tint-color: ").Append(rgba).Append(";\n");
                sb.Append("\tborder-top-color: ").Append(rgba).Append(";\n");
                sb.Append("\tborder-right-color: ").Append(rgba).Append(";\n");
                sb.Append("\tborder-bottom-color: ").Append(rgba).Append(";\n");
                sb.Append("\tborder-left-color: ").Append(rgba).Append(";\n");
            }
            sb.Append("}\n");
        }

        private static void PseudoStates(StringBuilder sb, string sel, Color c)
        {
            var hover = Lighten(c, 0.16f);
            var active = Darken(c, 0.18f);
            Block(sb, sel + ":hover", hover);
            Block(sb, sel + ":focus", hover);
            Block(sb, sel + ":active", active);
            Block(sb, sel + ":checked", active);
        }

        // Names beginning with '.' / '#' are raw selectors; bare names are Unity IMGUI style
        // names exposed as USS classes, so prefix with '.'. A few (Button) are element types.
        private static readonly HashSet<string> RawTypeSelectors = new HashSet<string> { "Button" };

        private static string Selector(string name)
        {
            if (name.StartsWith(".") || name.StartsWith("#") || RawTypeSelectors.Contains(name))
                return name;
            return "." + name;
        }

        private static bool IsButtonOrDropdown(string name)
        {
            string n = name.ToLowerInvariant();
            return n.Contains("button") || n.Contains("dropdown") || n.Contains("popup")
                || n.Contains("command") || n == "button" || n.Contains("field__input")
                || n.Contains("field__selector") || n.Contains("minibutton");
        }

        private static string Rgba(Color c)
        {
            Color32 c32 = c;
            string a = Mathf.Clamp01(c.a).ToString("0.###", CultureInfo.InvariantCulture);
            return string.Format(CultureInfo.InvariantCulture, "rgba({0}, {1}, {2}, {3})", c32.r, c32.g, c32.b, a);
        }

        private static Color Lighten(Color c, float amt) =>
            new Color(Mathf.Clamp01(c.r + amt), Mathf.Clamp01(c.g + amt), Mathf.Clamp01(c.b + amt), c.a);

        private static Color Darken(Color c, float amt) =>
            new Color(Mathf.Clamp01(c.r - amt), Mathf.Clamp01(c.g - amt), Mathf.Clamp01(c.b - amt), c.a);
    }
}
