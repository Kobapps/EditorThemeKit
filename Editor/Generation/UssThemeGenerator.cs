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
            (ThemeColorKey.Accent, new[]
            {
                // Selection highlight across hierarchy/project lists + UITK collections.
                "TV Selection", "OL SelectedRow", "OL SelectedRowNoFocus", "PR Insertion",
                ".unity-collection-view__item--selected",
                ".unity-list-view__item--selected",
                ".unity-tree-view__item--selected",
            }),
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

            return sb.ToString();
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
