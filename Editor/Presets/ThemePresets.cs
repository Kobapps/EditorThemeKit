using System;
using System.Collections.Generic;
using UnityEngine;

namespace EditorThemeKit
{
    /// <summary>
    /// Built-in, read-only themes. Editing any of them in the settings UI produces a
    /// "Custom" copy (see <see cref="EditorThemeKit.EditorThemeData.presetId"/>).
    /// </summary>
    public static class ThemePresets
    {
        public const string CustomId = "custom";

        public readonly struct PresetInfo
        {
            public readonly string Id;
            public readonly string DisplayName;

            public PresetInfo(string id, string displayName)
            {
                Id = id;
                DisplayName = displayName;
            }
        }

        // Order here drives the dropdown order in the settings UI.
        public static readonly PresetInfo[] All =
        {
            new PresetInfo("unity_dark", "Unity Default (Dark)"),
            new PresetInfo("unity_light", "Unity Default (Light)"),
            new PresetInfo("dracula", "Dracula"),
            new PresetInfo("monokai", "Monokai"),
            new PresetInfo("solarized_dark", "Solarized Dark"),
            new PresetInfo("nord", "Nord"),
            new PresetInfo("gruvbox_dark", "Gruvbox Dark"),
            new PresetInfo("high_contrast", "High Contrast"),
        };

        public static bool IsPreset(string id)
        {
            if (string.IsNullOrEmpty(id) || id == CustomId)
                return false;
            foreach (var p in All)
                if (p.Id == id)
                    return true;
            return false;
        }

        public static string DisplayNameOf(string id)
        {
            foreach (var p in All)
                if (p.Id == id)
                    return p.DisplayName;
            return "Custom";
        }

        /// <summary>Returns a fresh copy of the preset, or the Unity Dark default.</summary>
        public static EditorThemeData Create(string id)
        {
            switch (id)
            {
                case "unity_light": return UnityLight();
                case "dracula": return Dracula();
                case "monokai": return Monokai();
                case "solarized_dark": return SolarizedDark();
                case "nord": return Nord();
                case "gruvbox_dark": return GruvboxDark();
                case "high_contrast": return HighContrast();
                case "unity_dark":
                default:
                    return UnityDark();
            }
        }

        public static EditorThemeData Default() => UnityDark();

        private static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c))
                return c;
            return Color.magenta;
        }

        // A separator/divider color that stays visible against the window. Convention is a
        // darker groove line, so darken the window — except on near-black themes where there's
        // no room to go darker, so lighten a touch instead.
        private static Color Divider(Color w)
        {
            float lum = 0.2126f * w.r + 0.7152f * w.g + 0.0722f * w.b;
            float a = lum < 0.10f ? 0.12f : -0.16f;
            return new Color(Mathf.Clamp01(w.r + a), Mathf.Clamp01(w.g + a), Mathf.Clamp01(w.b + a), 1f);
        }

        private static EditorThemeData Build(
            string id, string name,
            string window, string header, string toolbar, string tab, string tabSel,
            string input, string button, string border, string text, string textSel,
            string accent, string scrollbar,
            EditorThemeSkin baseSkin = EditorThemeSkin.Dark)
        {
            var t = new EditorThemeData { presetId = id, displayName = name, baseSkin = baseSkin };
            t.Set(ThemeColorKey.WindowBackground, Hex(window));
            t.Set(ThemeColorKey.HeaderBackground, Hex(header));
            t.Set(ThemeColorKey.ToolbarBackground, Hex(toolbar));
            // Good-looking default: the SELECTED tab matches the window (content) color so it
            // stands out and "connects" to the body below; unselected tabs share the header /
            // strip color. (The `tab`/`tabSel` args are kept for call-site readability but the
            // relationship above is enforced here.)
            t.Set(ThemeColorKey.TabBackground, Hex(header));
            t.Set(ThemeColorKey.TabBackgroundSelected, Hex(window));
            t.Set(ThemeColorKey.InputBackground, Hex(input));
            t.Set(ThemeColorKey.ButtonBackground, Hex(button));
            // Derive a divider/separator color that visibly contrasts with the window on any
            // theme (a too-dark border on a dark theme is invisible). Users can still override
            // "Border / Separator" on custom themes.
            t.Set(ThemeColorKey.Border, Divider(Hex(window)));
            t.Set(ThemeColorKey.Text, Hex(text));
            t.Set(ThemeColorKey.TextSelected, Hex(textSel));
            t.Set(ThemeColorKey.Accent, Hex(accent));
            t.Set(ThemeColorKey.ScrollbarThumb, Hex(scrollbar));
            return t;
        }

        // Approximation of Unity 6's stock dark theme tokens.
        private static EditorThemeData UnityDark() => Build(
            "unity_dark", "Unity Default (Dark)",
            window: "383838", header: "3c3c3c", toolbar: "3c3c3c",
            tab: "353535", tabSel: "3c3c3c", input: "2a2a2a", button: "585858",
            border: "232323", text: "d2d2d2", textSel: "ffffff",
            accent: "3a72b0", scrollbar: "5f5f5f");

        private static EditorThemeData UnityLight() => Build(
            "unity_light", "Unity Default (Light)",
            window: "c8c8c8", header: "a5a5a5", toolbar: "cbcbcb",
            tab: "b8b8b8", tabSel: "cbcbcb", input: "f0f0f0", button: "e4e4e4",
            border: "939393", text: "090909", textSel: "060606",
            accent: "3a72b0", scrollbar: "9a9a9a",
            baseSkin: EditorThemeSkin.Light);

        private static EditorThemeData Dracula() => Build(
            "dracula", "Dracula",
            window: "282a36", header: "21222c", toolbar: "21222c",
            tab: "1e1f29", tabSel: "44475a", input: "1e1f29", button: "44475a",
            border: "191a21", text: "f8f8f2", textSel: "ffffff",
            accent: "bd93f9", scrollbar: "6272a4");

        private static EditorThemeData Monokai() => Build(
            "monokai", "Monokai",
            window: "272822", header: "1e1f1c", toolbar: "1e1f1c",
            tab: "1e1f1c", tabSel: "3e3d32", input: "1c1d18", button: "49483e",
            border: "141411", text: "f8f8f2", textSel: "ffffff",
            accent: "a6e22e", scrollbar: "75715e");

        private static EditorThemeData SolarizedDark() => Build(
            "solarized_dark", "Solarized Dark",
            window: "002b36", header: "073642", toolbar: "073642",
            tab: "00252e", tabSel: "073642", input: "00252e", button: "094e5f",
            border: "001f27", text: "93a1a1", textSel: "eee8d5",
            accent: "268bd2", scrollbar: "586e75");

        private static EditorThemeData Nord() => Build(
            "nord", "Nord",
            window: "2e3440", header: "292e39", toolbar: "292e39",
            tab: "272b34", tabSel: "3b4252", input: "272b34", button: "434c5e",
            border: "21252e", text: "d8dee9", textSel: "eceff4",
            accent: "88c0d0", scrollbar: "4c566a");

        private static EditorThemeData GruvboxDark() => Build(
            "gruvbox_dark", "Gruvbox Dark",
            window: "282828", header: "1d2021", toolbar: "1d2021",
            tab: "1d2021", tabSel: "3c3836", input: "1d2021", button: "504945",
            border: "141617", text: "ebdbb2", textSel: "fbf1c7",
            accent: "fabd2f", scrollbar: "665c54");

        private static EditorThemeData HighContrast() => Build(
            "high_contrast", "High Contrast",
            window: "000000", header: "0a0a0a", toolbar: "0a0a0a",
            tab: "000000", tabSel: "1a1a1a", input: "000000", button: "1f1f1f",
            border: "5a5a5a", text: "ffffff", textSel: "ffff00",
            accent: "ffcc00", scrollbar: "808080");
    }
}
