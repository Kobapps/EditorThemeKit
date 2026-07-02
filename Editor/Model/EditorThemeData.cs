using System;
using System.Collections.Generic;
using UnityEngine;

namespace EditorThemeKit
{
    /// <summary>
    /// The set of user-editable "semantic" colors. Each maps (in
    /// <see cref="UssThemeGenerator"/>) to one or more concrete Unity
    /// <c>--unity-colors-*</c> design tokens.
    /// </summary>
    public enum ThemeColorKey
    {
        WindowBackground,
        HeaderBackground,
        ToolbarBackground,
        TabBackground,
        TabBackgroundSelected,
        InputBackground,
        ButtonBackground,
        Border,
        Text,
        TextSelected,
        Accent,
        ScrollbarThumb,
    }

    /// <summary>
    /// Which of Unity's two built-in editor skins a theme builds on. Applying a Light theme
    /// while Unity is on the Dark (Pro) skin leaves default text unreadable, so the applier
    /// switches Unity's base skin to match this.
    /// </summary>
    public enum EditorThemeSkin
    {
        Dark,
        Light,
    }

    /// <summary>
    /// A serializable editor theme: a name, an originating preset id, and a semantic
    /// color palette. Serialized with <see cref="JsonUtility"/> (Unity's
    /// <see cref="Color"/> round-trips natively), so no ScriptableObject asset is needed
    /// for the setting itself.
    /// </summary>
    [Serializable]
    public class EditorThemeData
    {
        [Serializable]
        public struct Entry
        {
            public ThemeColorKey key;
            public Color color;

            public Entry(ThemeColorKey key, Color color)
            {
                this.key = key;
                this.color = color;
            }
        }

        public string displayName = "Custom";

        /// <summary>Id of the preset this theme is based on, or "custom".</summary>
        public string presetId = "custom";

        /// <summary>Which Unity base skin this theme builds on (Dark/Light).</summary>
        public EditorThemeSkin baseSkin = EditorThemeSkin.Dark;

        /// <summary>Whether the IMGUI accent pass (text/selection colors) is applied.</summary>
        public bool applyImguiPass = true;

        /// <summary>
        /// Experimental: deep IMGUI reskin — recolor text across all built-in styles and
        /// flatten key IMGUI backgrounds (box/window/fields/buttons/help boxes) to theme
        /// colors. Opt-in and fully reversible. Requires <see cref="applyImguiPass"/>.
        /// </summary>
        public bool deepImgui = false;

        [SerializeField]
        private List<Entry> entries = new List<Entry>();

        [NonSerialized]
        private Dictionary<ThemeColorKey, Color> _lookup;

        public IReadOnlyList<Entry> Entries => entries;

        private void EnsureLookup()
        {
            if (_lookup != null && _lookup.Count == entries.Count)
                return;

            _lookup = new Dictionary<ThemeColorKey, Color>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
                _lookup[entries[i].key] = entries[i].color;
        }

        public Color Get(ThemeColorKey key, Color fallback)
        {
            EnsureLookup();
            return _lookup.TryGetValue(key, out var c) ? c : fallback;
        }

        public bool TryGet(ThemeColorKey key, out Color color)
        {
            EnsureLookup();
            return _lookup.TryGetValue(key, out color);
        }

        public void Set(ThemeColorKey key, Color color)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].key == key)
                {
                    entries[i] = new Entry(key, color);
                    _lookup = null;
                    return;
                }
            }

            entries.Add(new Entry(key, color));
            _lookup = null;
        }

        public EditorThemeData Clone()
        {
            var copy = new EditorThemeData
            {
                displayName = displayName,
                presetId = presetId,
                baseSkin = baseSkin,
                applyImguiPass = applyImguiPass,
                deepImgui = deepImgui,
                entries = new List<Entry>(entries.Count),
            };
            for (int i = 0; i < entries.Count; i++)
                copy.entries.Add(entries[i]);
            return copy;
        }

        /// <summary>
        /// A stable content signature used to skip redundant regeneration/apply work.
        /// </summary>
        public string Signature()
        {
            EnsureLookup();
            var sb = new System.Text.StringBuilder(64);
            sb.Append(presetId).Append('|').Append(applyImguiPass ? '1' : '0')
              .Append(deepImgui ? '1' : '0').Append((int)baseSkin);
            // Iterate the enum so ordering is deterministic regardless of entry order.
            foreach (ThemeColorKey key in Enum.GetValues(typeof(ThemeColorKey)))
            {
                sb.Append('|');
                if (_lookup.TryGetValue(key, out var c))
                    sb.Append(ColorUtility.ToHtmlStringRGBA(c));
                else
                    sb.Append("--------");
            }
            return sb.ToString();
        }
    }
}
