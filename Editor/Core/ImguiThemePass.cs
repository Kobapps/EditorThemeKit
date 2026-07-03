using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace EditorThemeKit
{
    /// <summary>
    /// Retints the legacy IMGUI controls that the editor USS can't reach — inspector
    /// buttons (incl. Add Component), dropdowns, and component title bars. Ported from the
    /// EditorThemes asset's approach: it tints by <b>copying the original style texture and
    /// shading per-pixel by luminance</b>, so Unity's gradients/borders are preserved
    /// (rather than flat-filled). Runs during <c>OnGUI</c> because IMGUI styles are rebuilt
    /// frequently; originals are snapshotted for a clean revert.
    /// </summary>
    [InitializeOnLoad]
    internal static class ImguiThemePass
    {
        private static readonly string[] ExplicitButtonStyleNames =
            { "button", "PR DropHere", "AC ComponentButton", "CN EntryBackEven", "CN EntryBackOdd" };
        private static readonly string[] ProvenFlatButtonStyleNames =
            { "minibuttonleft", "minibuttonmid", "minibuttonright", "AC Button" };
        private static readonly string[] ExplicitDropdownStyleNames =
            { "DropDownButton", "MiniPullDown", "MiniPopup", "ObjectFieldButton", "Popup" };

        private static readonly Dictionary<GUIStyleState, Snapshot> Snapshots = new Dictionary<GUIStyleState, Snapshot>();
        private static readonly List<Texture2D> CreatedTextures = new List<Texture2D>();
        private static readonly Dictionary<string, Texture2D> TintedTextures = new Dictionary<string, Texture2D>();
        private static readonly HashSet<GUIStyle> AppliedStyles = new HashSet<GUIStyle>();

        private static Color _buttonColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        private static Color _dropdownColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        private static Color _headerColor = new Color(0.28f, 0.28f, 0.28f, 1f);
        private static Color _tabColor = new Color(0.24f, 0.24f, 0.24f, 1f);
        private static Color _tabSelColor = new Color(0.30f, 0.30f, 0.30f, 1f);
        private static Color _borderColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        private static Color _selColor = new Color(0.24f, 0.37f, 0.53f, 1f);
        private static Color _selTextColor = Color.white;
        private static Color _windowColor = new Color(0.22f, 0.22f, 0.22f, 1f);
        private static Color _textColor = Color.white;

        // Originals of static Color fields we overwrite (e.g. the hierarchy visibility column),
        // restored on revert.
        private static readonly List<(FieldInfo field, Color original)> ColorFieldSnaps
            = new List<(FieldInfo, Color)>();
        private static bool _hasTheme;
        private static bool _dockApplied;
        private static double _lastDockScan;

        static ImguiThemePass()
        {
            Editor.finishedDefaultHeaderGUI += _ => EnsureAppliedFromOnGUI();
            EditorApplication.update += OnUpdate;
        }

        // The dock header/tabs use shared static GUIStyles that persist once set, so we apply
        // them from update (not OnGUI) — this reaches them even when no inspector is drawing.
        // Retries until DockArea's styles are initialized, then stops until the next apply.
        private static void OnUpdate()
        {
            if (!_hasTheme || _dockApplied)
                return;
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastDockScan < 0.3)
                return;
            _lastDockScan = now;
            try
            {
                if (RetintDock())
                {
                    _dockApplied = true;
                    SafeRepaint();
                }
            }
            catch { /* styles not ready yet; retry next tick */ }
        }

        public static void Apply(EditorThemeData theme)
        {
            RestoreSnapshots();

            if (theme == null || !theme.applyImguiPass)
            {
                _hasTheme = false;
                return;
            }

            _buttonColor = theme.Get(ThemeColorKey.ButtonBackground, new Color(0.35f, 0.35f, 0.35f, 1f));
            _dropdownColor = theme.Get(ThemeColorKey.InputBackground, _buttonColor);
            _headerColor = theme.Get(ThemeColorKey.HeaderBackground, Lighten(_buttonColor, 0.08f));
            _tabColor = theme.Get(ThemeColorKey.TabBackground, theme.Get(ThemeColorKey.WindowBackground, _headerColor));
            _tabSelColor = theme.Get(ThemeColorKey.TabBackgroundSelected, _headerColor);
            _borderColor = theme.Get(ThemeColorKey.Border, Darken(_headerColor, 0.1f));
            _textColor = theme.TryGet(ThemeColorKey.Text, out var t) ? t : ReadableText(_buttonColor);
            var accent = theme.Get(ThemeColorKey.Accent, new Color(0.30f, 0.50f, 0.70f, 1f));
            _selTextColor = theme.Get(ThemeColorKey.TextSelected, Color.white);
            _selColor = ReadableHighlight(accent, _selTextColor); // match the tree/list selection
            _windowColor = theme.Get(ThemeColorKey.WindowBackground, new Color(0.22f, 0.22f, 0.22f, 1f));
            _hasTheme = true;
            _dockApplied = false; // re-apply dock styles for the new colors
            SafeRepaint();
        }

        // Same contrast rule as the USS highlight tokens, so IMGUI selection matches UITK.
        private static Color ReadableHighlight(Color accent, Color text)
        {
            float bg = 0.2126f * accent.r + 0.7152f * accent.g + 0.0722f * accent.b;
            float tl = 0.2126f * text.r + 0.7152f * text.g + 0.0722f * text.b;
            if (tl > 0.5f)
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

        public static void Revert()
        {
            RestoreSnapshots();
            _hasTheme = false;
            _dockApplied = false;
            SafeRepaint();
        }

        private static void EnsureAppliedFromOnGUI()
        {
            if (!_hasTheme || Event.current == null)
                return;
            ApplyNow();
        }

        private static void ApplyNow()
        {
            AppliedStyles.Clear();

            foreach (var style in ProvenFlatButtonStyles())
                ReplaceStyleBackground(style, _buttonColor);
            foreach (var style in ComponentHeaderStyles())
                ReplaceStyleBackground(style, _headerColor);
            foreach (var style in ButtonStyles())
                TintStyle(style, _buttonColor);
            foreach (var style in DropdownStyles())
                TintStyle(style, _dropdownColor);
        }

        // The dock header bar and window tabs are painted by DockArea/HostView using their
        // own shared, cached GUIStyles (USS name-bridging doesn't reach the header, nor give
        // a distinct selected tab). Retint them directly: header/strip -> HeaderBackground,
        // unselected tabs -> TabBackground, and the SELECTED tab (on* states) ->
        // TabBackgroundSelected so it stands out. Returns false if the styles aren't
        // initialized yet (so the caller retries).
        private static bool RetintDock()
        {
            var dragTab = CachedStyle("UnityEditor.DockArea", "dragTab");
            if (dragTab == null)
                return false; // DockArea styles not built yet.

            HeaderStyle(CachedStyle("UnityEditor.DockArea", "background"));
            HeaderStyle(CachedStyle("UnityEditor.DockArea", "dockTitleBarStyle"));
            HeaderStyle(CachedStyle("UnityEditor.HostView", "background"));
            HeaderStyle(CachedStyle("UnityEditor.HostView", "tabWindowBackground"));

            DockTab(dragTab);
            DockTab(CachedStyle("UnityEditor.DockArea", "dragTabFirst"));

            // Window / pane divider lines (IMGUI separators paint no background by default,
            // so on themed backgrounds they vanish). Give them the Border color so they show.
            var skin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);
            if (skin != null)
            {
                SeparatorLine(skin.FindStyle("DefaultLineSeparator"));
                SeparatorLine(skin.FindStyle("AnimLeftPaneSeparator"));
                SeparatorLine(skin.FindStyle("dockareaStandalone"));
            }

            // Project-browser selection uses ObjectListArea's own cached styles (not the tree
            // highlight token), so recolor their selected states to match.
            SelectionStyle(CachedStyle("UnityEditor.ObjectListArea", "resultsLabel"));
            SelectionStyle(CachedStyle("UnityEditor.ObjectListArea", "resultsGridLabel"));
            SelectionStyle(CachedStyle("UnityEditor.ObjectListArea", "resultsGrid"));

            // Hierarchy scene-visibility column (the left strip) is painted from static Color
            // fields, not styles.
            const string vis = "UnityEditor.SceneVisibilityHierarchyGUI";
            // Slightly darker than the window so the column reads as a subtle gutter.
            var colBg = Darken(_windowColor, 0.03f);
            SetColorField(vis, "backgroundColor", colBg);
            SetColorField(vis, "hoveredBackgroundColor", Lighten(colBg, 0.06f));
            SetColorField(vis, "selectedBackgroundColor", _selColor);
            SetColorField(vis, "selectedNoFocusBackgroundColor", Desaturate(_selColor, 0.35f));
            return true;
        }

        private static Color Desaturate(Color c, float t)
        {
            float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            return Color.Lerp(c, new Color(lum, lum, lum, c.a), Mathf.Clamp01(t));
        }

        // Overwrites a static Color field on a type's nested Styles class (snapshotting the
        // original for revert).
        private static void SetColorField(string typeName, string field, Color color)
        {
            try
            {
                var t = FindType(typeName);
                var nested = t?.GetNestedType("Styles", BindingFlags.Public | BindingFlags.NonPublic);
                var f = nested?.GetField(field,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                if (f == null || f.FieldType != typeof(Color))
                    return;
                ColorFieldSnaps.Add((f, (Color)f.GetValue(null)));
                f.SetValue(null, color);
            }
            catch { /* best-effort */ }
        }

        private static void SelectionStyle(GUIStyle style)
        {
            if (style == null) return;
            SelState(style.onNormal);
            SelState(style.onActive);
            SelState(style.onFocused);
            SelState(style.onHover);
        }

        private static void SelState(GUIStyleState st)
        {
            if (st == null) return;
            if (!Snapshots.ContainsKey(st))
                Snapshots.Add(st, new Snapshot(st.background, st.textColor));
            st.background = FlatTexture(_selColor);
            st.textColor = _selTextColor;
        }

        private static void SeparatorLine(GUIStyle style)
        {
            if (style == null) return;
            ReplaceState(style.normal, _borderColor);
            ReplaceState(style.onNormal, _borderColor);
        }

        private static void HeaderStyle(GUIStyle style)
        {
            if (style == null) return;
            ReplaceState(style.normal, _headerColor);
            ReplaceState(style.onNormal, _headerColor);
            ReplaceState(style.focused, _headerColor);
            ReplaceState(style.active, _headerColor);
        }

        // Unselected tab states -> _tabColor; selected (on*) states -> _tabSelColor.
        private static void DockTab(GUIStyle style)
        {
            if (style == null) return;
            ReplaceState(style.normal, _tabColor);
            ReplaceState(style.hover, Lighten(_tabColor, 0.06f));
            ReplaceState(style.focused, _tabColor);
            ReplaceState(style.active, _tabSelColor);
            ReplaceState(style.onNormal, _tabSelColor);
            ReplaceState(style.onHover, _tabSelColor);
            ReplaceState(style.onActive, _tabSelColor);
            ReplaceState(style.onFocused, _tabSelColor);
        }

        private static Type FindType(string full)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType(full);
                if (t != null) return t;
            }
            return null;
        }

        private static GUIStyle CachedStyle(string typeName, string field)
        {
            try
            {
                var t = FindType(typeName);
                var nested = t?.GetNestedType("Styles", BindingFlags.Public | BindingFlags.NonPublic);
                var f = nested?.GetField(field,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                return f?.GetValue(null) as GUIStyle;
            }
            catch { return null; }
        }

        // --- style collections -------------------------------------------------------

        private static IEnumerable<GUIStyle> ButtonStyles()
        {
            yield return Skin(s => s.button);
            yield return EdStyle(() => EditorStyles.miniButton);
            yield return EdStyle(() => EditorStyles.miniButtonLeft);
            yield return EdStyle(() => EditorStyles.miniButtonMid);
            yield return EdStyle(() => EditorStyles.miniButtonRight);
            foreach (var s in Named(ExplicitButtonStyleNames)) yield return s;
            foreach (var s in CustomStyles())
            {
                var n = s.name ?? "";
                if ((IsExplicit(n, ExplicitButtonStyleNames) || IsActionButton(n)) && !IsIcon(n))
                    yield return s;
            }
        }

        private static IEnumerable<GUIStyle> DropdownStyles()
        {
            yield return EdStyle(() => EditorStyles.popup);
            foreach (var s in Named(ExplicitDropdownStyleNames)) yield return s;
            foreach (var s in CustomStyles())
            {
                var n = s.name ?? "";
                if (IsDropdown(n) && !IsIcon(n)) yield return s;
            }
        }

        private static IEnumerable<GUIStyle> ProvenFlatButtonStyles()
        {
            foreach (var s in Named(ProvenFlatButtonStyleNames)) yield return s;
            foreach (var s in CustomStyles())
                if (IsExplicit(s.name ?? "", ProvenFlatButtonStyleNames)) yield return s;
        }

        private static IEnumerable<GUIStyle> ComponentHeaderStyles()
        {
            foreach (var s in Named(new[] { "IN Title" })) yield return s;
            foreach (var s in CustomStyles())
                if (string.Equals(s.name, "IN Title", StringComparison.OrdinalIgnoreCase)) yield return s;
        }

        private static GUIStyle Skin(Func<GUISkin, GUIStyle> get)
        {
            try { return GUI.skin != null ? get(GUI.skin) : null; } catch { return null; }
        }

        private static GUIStyle EdStyle(Func<GUIStyle> get)
        {
            try { return get(); } catch { return null; }
        }

        private static IEnumerable<GUIStyle> Named(string[] names)
        {
            if (GUI.skin == null) yield break;
            foreach (var name in names)
            {
                GUIStyle s = null;
                try { s = GUI.skin.FindStyle(name); } catch { }
                if (s != null) yield return s;
            }
        }

        private static IEnumerable<GUIStyle> CustomStyles()
        {
            if (GUI.skin == null || GUI.skin.customStyles == null) yield break;
            foreach (var s in GUI.skin.customStyles)
                if (s != null) yield return s;
        }

        private static bool IsExplicit(string name, string[] set)
        {
            foreach (var s in set)
                if (string.Equals(name, s, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool IsActionButton(string n) =>
            n.IndexOf("component", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("override", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsDropdown(string n) =>
            n.IndexOf("drop", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("popup", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("pulldown", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsIcon(string n) =>
            n.IndexOf("toolbar", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("invisible", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("paneoptions", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("treeview", StringComparison.OrdinalIgnoreCase) >= 0;

        // --- tinting -----------------------------------------------------------------

        private static void TintStyle(GUIStyle style, Color normal)
        {
            if (style == null || AppliedStyles.Contains(style)) return;
            AppliedStyles.Add(style);
            var hover = Lighten(normal, 0.16f);
            var active = Darken(normal, 0.18f);
            TintState(style.normal, normal); TintState(style.hover, hover);
            TintState(style.active, active); TintState(style.focused, hover);
            TintState(style.onNormal, normal); TintState(style.onHover, hover);
            TintState(style.onActive, active); TintState(style.onFocused, hover);
        }

        private static void ReplaceStyleBackground(GUIStyle style, Color normal)
        {
            if (style == null || AppliedStyles.Contains(style)) return;
            AppliedStyles.Add(style);
            var hover = Lighten(normal, 0.16f);
            var active = Darken(normal, 0.18f);
            ReplaceState(style.normal, normal); ReplaceState(style.hover, hover);
            ReplaceState(style.active, active); ReplaceState(style.focused, hover);
            ReplaceState(style.onNormal, normal); ReplaceState(style.onHover, hover);
            ReplaceState(style.onActive, active); ReplaceState(style.onFocused, hover);
        }

        private static void ReplaceState(GUIStyleState state, Color color)
        {
            if (state == null) return;
            if (!Snapshots.ContainsKey(state))
                Snapshots.Add(state, new Snapshot(state.background, state.textColor));
            state.background = FlatTexture(color);
            state.textColor = _textColor;
        }

        private static void TintState(GUIStyleState state, Color color)
        {
            if (state == null) return;
            if (!Snapshots.TryGetValue(state, out var snap))
            {
                snap = new Snapshot(state.background, state.textColor);
                Snapshots.Add(state, snap);
            }

            var source = snap.Background != null ? snap.Background : FallbackButtonBackground();
            if (source != null)
                state.background = TintedTexture(source, color);
            state.textColor = _textColor;
        }

        private static Texture2D FallbackButtonBackground() =>
            GUI.skin != null && GUI.skin.button != null && GUI.skin.button.normal != null
                ? GUI.skin.button.normal.background
                : null;

        private static Texture2D TintedTexture(Texture2D source, Color color)
        {
            string key = RuntimeHelpers.GetHashCode(source) + ":" + ColorUtility.ToHtmlStringRGBA(color);
            if (TintedTextures.TryGetValue(key, out var existing) && existing != null)
                return existing;

            var copy = CopyTexture(source);
            if (copy == null) return source;
            var px = copy.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                var sp = px[i];
                float lum = (sp.r + sp.g + sp.b) / 3f;
                var shaded = Color.Lerp(Darken(color, 0.22f), Lighten(color, 0.22f), lum);
                px[i] = new Color(shaded.r, shaded.g, shaded.b, sp.a * color.a);
            }
            copy.SetPixels(px);
            copy.Apply();
            TintedTextures[key] = copy;
            return copy;
        }

        private static Texture2D FlatTexture(Color color)
        {
            string key = "flat:" + ColorUtility.ToHtmlStringRGBA(color);
            if (TintedTextures.TryGetValue(key, out var existing) && existing != null)
                return existing;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "ETK_Flat_" + ColorUtility.ToHtmlStringRGBA(color),
            };
            tex.SetPixels(new[] { color, color, color, color });
            tex.Apply();
            CreatedTextures.Add(tex);
            TintedTextures[key] = tex;
            return tex;
        }

        // Copies a (possibly non-readable) texture via a RenderTexture blit so we can tint it.
        private static Texture2D CopyTexture(Texture2D source)
        {
            try
            {
                var prev = RenderTexture.active;
                var rt = RenderTexture.GetTemporary(source.width, source.height, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    name = "ETK_Tinted_" + source.name,
                };
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                copy.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                CreatedTextures.Add(copy);
                return copy;
            }
            catch { return null; }
        }

        private static void RestoreSnapshots()
        {
            foreach (var pair in Snapshots)
            {
                if (pair.Key == null) continue;
                pair.Key.background = pair.Value.Background;
                pair.Key.textColor = pair.Value.TextColor;
            }
            Snapshots.Clear();

            foreach (var tex in CreatedTextures)
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            CreatedTextures.Clear();
            TintedTextures.Clear();

            foreach (var snap in ColorFieldSnaps)
            {
                try { snap.field.SetValue(null, snap.original); }
                catch { /* best-effort */ }
            }
            ColorFieldSnaps.Clear();
        }

        private static Color Lighten(Color c, float a) =>
            new Color(Mathf.Clamp01(c.r + a), Mathf.Clamp01(c.g + a), Mathf.Clamp01(c.b + a), c.a);

        private static Color Darken(Color c, float a) =>
            new Color(Mathf.Clamp01(c.r - a), Mathf.Clamp01(c.g - a), Mathf.Clamp01(c.b - a), c.a);

        private static Color ReadableText(Color bg)
        {
            float lum = 0.2126f * bg.r + 0.7152f * bg.g + 0.0722f * bg.b;
            return lum > 0.55f ? Color.black : Color.white;
        }

        private static void SafeRepaint()
        {
            try { InternalEditorUtility.RepaintAllViews(); } catch { }
        }

        private readonly struct Snapshot
        {
            public readonly Texture2D Background;
            public readonly Color TextColor;
            public Snapshot(Texture2D bg, Color text) { Background = bg; TextColor = text; }
        }
    }
}
