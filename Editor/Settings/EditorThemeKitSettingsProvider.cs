using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace EditorThemeKit
{
    /// <summary>
    /// Project Settings page (<c>Project Settings → Editor Theme Kit</c>). Shows a gallery
    /// of theme swatch-cards (built-in presets + saved custom themes) for one-click
    /// selection; a collapsible section reveals the full color pickers for customizing or
    /// editing; and custom themes can be saved/deleted (per-user). Edits apply live and the
    /// active theme persists to <c>UserSettings/</c>.
    /// </summary>
    internal sealed class EditorThemeKitSettingsProvider : SettingsProvider
    {
        // Colors shown on each card's swatch strip (kept short, per request).
        private static readonly ThemeColorKey[] SwatchKeys =
        {
            ThemeColorKey.WindowBackground, ThemeColorKey.HeaderBackground,
            ThemeColorKey.ToolbarBackground, ThemeColorKey.ButtonBackground,
            ThemeColorKey.Accent, ThemeColorKey.Text,
        };

        private static readonly (ThemeColorKey key, string label)[] ColorRows =
        {
            (ThemeColorKey.WindowBackground, "Window Background"),
            (ThemeColorKey.HeaderBackground, "Header / Title Bar"),
            (ThemeColorKey.ToolbarBackground, "Toolbar Background"),
            (ThemeColorKey.TabBackground, "Tab Background"),
            (ThemeColorKey.TabBackgroundSelected, "Tab Background (Selected)"),
            (ThemeColorKey.InputBackground, "Input / Field Background"),
            (ThemeColorKey.ButtonBackground, "Button Background"),
            (ThemeColorKey.Border, "Border / Separator"),
            (ThemeColorKey.Text, "Text"),
            (ThemeColorKey.TextSelected, "Text (Selected)"),
            (ThemeColorKey.Accent, "Accent / Selection"),
            (ThemeColorKey.ScrollbarThumb, "Scrollbar Thumb"),
        };

        private EditorThemeData _working;
        private VisualElement _root;
        private VisualElement _gallery;
        private Foldout _customize;
        private Toggle _imguiToggle;
        private EnumField _skinField;
        private readonly Dictionary<ThemeColorKey, ColorField> _colorFields = new Dictionary<ThemeColorKey, ColorField>();
        private readonly List<(string id, VisualElement card)> _cards = new List<(string, VisualElement)>();
        private IVisualElementScheduledItem _debounce;

        private EditorThemeKitSettingsProvider() : base("Project/Editor Theme Kit", SettingsScope.Project)
        {
            label = "Editor Theme Kit";
        }

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new EditorThemeKitSettingsProvider
            {
                keywords = new HashSet<string>(new[]
                {
                    "theme", "editor", "color", "skin", "dark", "light", "preset",
                    "toolbar", "background", "accent", "customize", "appearance", "swatch",
                }),
            };
        }

        public override void OnActivate(string searchContext, VisualElement root)
        {
            _root = root;
            _working = (EditorThemeApplier.Current ?? ThemeStorage.LoadOrDefault()).Clone();

            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;

            var title = new Label("Editor Theme Kit");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 18;
            root.Add(title);

            var help = new Label(
                "Pick a theme below, or open Customize to edit colors. Changes apply " +
                "instantly (no recompile) and are saved per-user in UserSettings.");
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.opacity = 0.75f;
            help.style.marginBottom = 8;
            root.Add(help);

            var themesHeader = new Label("Themes");
            themesHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            themesHeader.style.marginTop = 4;
            themesHeader.style.marginBottom = 4;
            root.Add(themesHeader);

            _gallery = new VisualElement();
            _gallery.style.flexDirection = FlexDirection.Column;
            root.Add(_gallery);
            BuildGallery();

            // IMGUI toggle.
            _imguiToggle = new Toggle("Also tint IMGUI buttons / dropdowns / headers") { value = _working.applyImguiPass };
            _imguiToggle.tooltip =
                "USS colors the chrome; this adds a gradient-preserving tint for legacy IMGUI " +
                "controls USS can't reach (inspector buttons, dropdowns, component headers).";
            _imguiToggle.style.marginTop = 10;
            _imguiToggle.RegisterValueChangedCallback(evt =>
            {
                _working.applyImguiPass = evt.newValue;
                ScheduleApply();
            });
            root.Add(_imguiToggle);

            // Collapsible color editor.
            _customize = new Foldout
            {
                text = "Customize colors",
                value = !ThemePresets.IsPreset(_working.presetId),
            };
            _customize.style.marginTop = 8;

            _skinField = new EnumField("Base skin", _working.baseSkin);
            _skinField.tooltip =
                "Which Unity base skin the theme builds on. Choose Light for light themes so " +
                "default text stays readable — applying switches Unity's skin to match.";
            _skinField.RegisterValueChangedCallback(evt =>
            {
                _working.baseSkin = (EditorThemeSkin)evt.newValue;
                MarkCustom();
                ScheduleApply();
            });
            _customize.Add(_skinField);

            _colorFields.Clear();
            foreach (var row in ColorRows)
            {
                var field = new ColorField(row.label) { value = _working.Get(row.key, Color.gray), showAlpha = true };
                var key = row.key;
                field.RegisterValueChangedCallback(evt => OnColorChanged(key, evt.newValue));
                _colorFields[key] = field;
                _customize.Add(field);
            }
            root.Add(_customize);

            // Save-as-custom row.
            var saveRow = new VisualElement();
            saveRow.style.flexDirection = FlexDirection.Row;
            saveRow.style.marginTop = 8;
            saveRow.style.alignItems = Align.Center;
            var nameField = new TextField("Save as") { value = "" };
            nameField.style.flexGrow = 1;
            saveRow.Add(nameField);
            var saveBtn = new Button(() =>
            {
                var n = string.IsNullOrWhiteSpace(nameField.value) ? "" : nameField.value.Trim();
                if (n.Length == 0)
                {
                    EditorUtility.DisplayDialog("Editor Theme Kit", "Enter a name for the custom theme.", "OK");
                    return;
                }
                if (ThemeStorage.CustomThemeExists(n) &&
                    !EditorUtility.DisplayDialog("Overwrite theme?", $"A custom theme named '{n}' already exists. Overwrite it?", "Overwrite", "Cancel"))
                    return;
                ThemeStorage.SaveCustomTheme(_working, n);
                _working.presetId = ThemePresets.CustomId;
                _working.displayName = n;
                nameField.value = "";
                BuildGallery();
                ApplyNow(); // persist active with the new name
            })
            { text = "Save Theme" };
            saveRow.Add(saveBtn);
            root.Add(saveRow);

            // Actions.
            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.marginTop = 12;
            buttons.Add(new Button(ResetToPreset) { text = "Reset to Preset" });
            buttons.Add(new Button(() =>
            {
                EditorThemeApplier.RevertToDefault();
                _working = ThemePresets.Default().Clone();
                RefreshColorFields();
                UpdateSelectionHighlight();
            })
            { text = "Revert to Unity Default" });
            root.Add(buttons);

            // Debug: apply the rainbow diagnostic theme (each key a distinct hue).
            var debugRow = new VisualElement();
            debugRow.style.flexDirection = FlexDirection.Row;
            debugRow.style.alignItems = Align.Center;
            debugRow.style.marginTop = 10;
            var debugBtn = new Button(ApplyDebugTheme) { text = "🐞 Apply Debug (Rainbow) Theme" };
            debugBtn.tooltip =
                "Applies a diagnostic theme where every color key is a distinct hue, so you can " +
                "map each editor region to the key that drives it. Not saved — restored on restart.";
            debugRow.Add(debugBtn);
            root.Add(debugRow);

            var debugLegend = new Label(
                "Window=blue  Header=red  Toolbar=orange  Tab=purple  Tab(Sel)=magenta  " +
                "Input=teal  Button=brown  Border=yellow  Accent=green  Scrollbar=pink");
            debugLegend.style.whiteSpace = WhiteSpace.Normal;
            debugLegend.style.opacity = 0.6f;
            debugLegend.style.fontSize = 11;
            debugLegend.style.marginTop = 3;
            root.Add(debugLegend);

            var pathNote = new Label("Saved to: UserSettings/EditorThemeKit/  (ActiveTheme.json + Themes/)");
            pathNote.style.opacity = 0.6f;
            pathNote.style.marginTop = 8;
            pathNote.style.fontSize = 11;
            root.Add(pathNote);

            // Move everything into a ScrollView so a tall gallery + color list scrolls instead
            // of overflowing the settings panel (Project Settings doesn't auto-scroll UITK
            // content). Pin each block to its natural height so nothing collapses/overlaps.
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            foreach (var child in new List<VisualElement>(root.Children()))
            {
                root.Remove(child);
                child.style.flexShrink = 0;
                scroll.Add(child);
            }
            root.style.flexGrow = 1;
            root.Add(scroll);
        }

        // ---- gallery ---------------------------------------------------------------

        private void BuildGallery()
        {
            _gallery.Clear();
            _cards.Clear();

            var presets = new List<(string id, string disp, EditorThemeData data, bool custom)>();
            foreach (var p in ThemePresets.All)
                presets.Add((p.Id, p.DisplayName, ThemePresets.Create(p.Id), false));

            var customs = new List<(string id, string disp, EditorThemeData data, bool custom)>();
            foreach (var name in ThemeStorage.ListCustomThemes())
            {
                var data = ThemeStorage.LoadCustomTheme(name);
                if (data != null)
                    customs.Add(("custom:" + name, name, data, true));
            }

            // User's custom themes first, at the top.
            var star = new Label("★");
            star.style.fontSize = 12;
            star.style.color = new Color(0.95f, 0.80f, 0.35f);
            AddSection("Your", star, customs);
            AddSection("Dark", MakeSkinIcon(false, 14), BySkin(presets, EditorThemeSkin.Dark));
            AddSection("Light", MakeSkinIcon(true, 14), BySkin(presets, EditorThemeSkin.Light));

            UpdateSelectionHighlight();
        }

        private static List<(string id, string disp, EditorThemeData data, bool custom)> BySkin(
            List<(string id, string disp, EditorThemeData data, bool custom)> items, EditorThemeSkin skin)
        {
            var group = new List<(string id, string disp, EditorThemeData data, bool custom)>();
            foreach (var it in items)
                if (it.data.baseSkin == skin)
                    group.Add(it);
            return group;
        }

        private void AddSection(string title, VisualElement icon,
            List<(string id, string disp, EditorThemeData data, bool custom)> group)
        {
            if (group.Count == 0)
                return;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginTop = 6;
            header.style.marginBottom = 3;
            header.style.flexShrink = 0;
            if (icon != null)
                header.Add(icon);
            var hl = new Label(" " + title + " themes");
            hl.style.unityFontStyleAndWeight = FontStyle.Bold;
            hl.style.opacity = 0.85f;
            header.Add(hl);
            _gallery.Add(header);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.flexShrink = 0; // don't collapse — keeps wrapped height so siblings don't overlap
            _gallery.Add(row);

            foreach (var it in group)
                AddCard(row, it.id, it.disp, it.data, it.custom);
        }

        // Sun (light) / moon (dark) glyph, color-tinted so it reads even if the glyph font
        // lacks the symbol.
        private static Label MakeSkinIcon(bool light, int fontSize)
        {
            var icon = new Label(light ? "☀" : "☾"); // ☀ / ☾
            icon.tooltip = light ? "Light theme" : "Dark theme";
            icon.style.fontSize = fontSize;
            icon.style.unityFontStyleAndWeight = FontStyle.Bold;
            icon.style.color = light ? new Color(0.96f, 0.76f, 0.24f) : new Color(0.72f, 0.80f, 0.98f);
            return icon;
        }

        private void AddCard(VisualElement parent, string id, string display, EditorThemeData data, bool isCustom)
        {
            var card = new VisualElement();
            card.style.width = 104;
            card.style.marginRight = 6;
            card.style.marginBottom = 6;
            card.style.paddingTop = 4;
            card.style.paddingBottom = 4;
            card.style.paddingLeft = 4;
            card.style.paddingRight = 4;
            card.style.borderTopWidth = 2;
            card.style.borderBottomWidth = 2;
            card.style.borderLeftWidth = 2;
            card.style.borderRightWidth = 2;
            SetCardBorder(card, false);

            var strip = new VisualElement();
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.height = 26;
            strip.style.marginBottom = 3;
            foreach (var key in SwatchKeys)
            {
                var box = new VisualElement();
                box.style.flexGrow = 1;
                box.style.backgroundColor = data.Get(key, Color.gray);
                strip.Add(box);
            }
            card.Add(strip);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            var nameLabel = new Label(display);
            nameLabel.style.fontSize = 10;
            nameLabel.style.flexGrow = 1;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            row.Add(nameLabel);

            // Sun / moon type icon.
            var badge = MakeSkinIcon(data.baseSkin == EditorThemeSkin.Light, 11);
            badge.style.marginLeft = 2;
            badge.style.marginRight = 1;
            row.Add(badge);

            if (isCustom)
            {
                var del = new Button(() =>
                {
                    var themeName = display;
                    if (EditorUtility.DisplayDialog("Delete theme?", $"Delete custom theme '{themeName}'?", "Delete", "Cancel"))
                    {
                        ThemeStorage.DeleteCustomTheme(themeName);
                        BuildGallery();
                    }
                })
                { text = "✕" };
                del.style.fontSize = 9;
                del.style.paddingLeft = 3;
                del.style.paddingRight = 3;
                del.style.marginLeft = 2;
                row.Add(del);
            }
            card.Add(row);

            var captured = data;
            card.RegisterCallback<ClickEvent>(_ => SelectTheme(captured));

            parent.Add(card);
            _cards.Add((id, card));
        }

        private void SetCardBorder(VisualElement card, bool selected)
        {
            var c = selected ? new Color(0.4f, 0.6f, 1f) : new Color(0f, 0f, 0f, 0.25f);
            card.style.borderTopColor = c;
            card.style.borderBottomColor = c;
            card.style.borderLeftColor = c;
            card.style.borderRightColor = c;
        }

        private string CurrentSelectionId()
        {
            if (ThemePresets.IsPreset(_working.presetId))
                return _working.presetId;
            if (_working.presetId == ThemePresets.CustomId && ThemeStorage.CustomThemeExists(_working.displayName))
                return "custom:" + _working.displayName;
            return null; // unsaved custom
        }

        private void UpdateSelectionHighlight()
        {
            var selId = CurrentSelectionId();
            foreach (var (id, card) in _cards)
                SetCardBorder(card, id == selId);
        }

        // ---- selection / editing ---------------------------------------------------

        private void SelectTheme(EditorThemeData data)
        {
            var keepImgui = _working.applyImguiPass;
            _working = data.Clone();
            _working.applyImguiPass = keepImgui;
            RefreshColorFields();
            _customize.value = !ThemePresets.IsPreset(_working.presetId);
            UpdateSelectionHighlight();
            ApplyNow();
        }

        private void OnColorChanged(ThemeColorKey key, Color value)
        {
            _working.Set(key, value);
            MarkCustom();
            ScheduleApply();
        }

        private void MarkCustom()
        {
            if (_working.presetId != ThemePresets.CustomId)
            {
                _working.presetId = ThemePresets.CustomId;
                _working.displayName = "Custom";
            }
            UpdateSelectionHighlight();
        }

        private void ResetToPreset()
        {
            var basis = ThemePresets.IsPreset(_working.presetId) ? _working.presetId : "unity_dark";
            var keepImgui = _working.applyImguiPass;
            _working = ThemePresets.Create(basis);
            _working.applyImguiPass = keepImgui;
            RefreshColorFields();
            UpdateSelectionHighlight();
            ApplyNow();
        }

        private void RefreshColorFields()
        {
            foreach (var row in ColorRows)
                if (_colorFields.TryGetValue(row.key, out var f))
                    f.SetValueWithoutNotify(_working.Get(row.key, Color.gray));
            if (_imguiToggle != null)
                _imguiToggle.SetValueWithoutNotify(_working.applyImguiPass);
            if (_skinField != null)
                _skinField.SetValueWithoutNotify(_working.baseSkin);
        }

        private void ScheduleApply()
        {
            if (_debounce != null)
            {
                _debounce.ExecuteLater(90);
                return;
            }
            _debounce = _root.schedule.Execute(ApplyNow);
            _debounce.ExecuteLater(90);
        }

        private void ApplyNow()
        {
            EditorThemeApplier.Apply(_working.Clone(), persist: true);
        }

        // ---- debug -----------------------------------------------------------------

        /// <summary>
        /// A diagnostic theme where every semantic color key is a distinct, easily-named hue,
        /// so each editor region can be visually mapped to the key that drives it (and any
        /// region that stays default-gray is one no key currently reaches).
        /// </summary>
        internal static EditorThemeData BuildDebugTheme()
        {
            var t = new EditorThemeData { displayName = "Debug (Rainbow)", presetId = ThemePresets.CustomId, baseSkin = EditorThemeSkin.Dark };
            // Per-tag mode: every individual token/selector gets its own unique color (see the
            // generator), so regions that share a semantic key no longer collapse to one color.
            t.debugUniqueTags = true;
            t.Set(ThemeColorKey.WindowBackground,      new Color32(30, 30, 90, 255));    // deep blue
            t.Set(ThemeColorKey.HeaderBackground,      new Color32(200, 40, 40, 255));   // red
            t.Set(ThemeColorKey.ToolbarBackground,     new Color32(230, 130, 20, 255));  // orange
            t.Set(ThemeColorKey.TabBackground,         new Color32(120, 40, 160, 255));  // purple
            t.Set(ThemeColorKey.TabBackgroundSelected, new Color32(230, 40, 200, 255));  // magenta
            t.Set(ThemeColorKey.InputBackground,       new Color32(20, 140, 140, 255));  // teal
            t.Set(ThemeColorKey.ButtonBackground,      new Color32(120, 80, 40, 255));   // brown
            t.Set(ThemeColorKey.Border,                new Color32(240, 220, 20, 255));  // yellow
            t.Set(ThemeColorKey.Text,                  new Color32(245, 245, 245, 255)); // white
            t.Set(ThemeColorKey.TextSelected,          new Color32(10, 10, 10, 255));    // black
            t.Set(ThemeColorKey.Accent,                new Color32(40, 200, 80, 255));   // green
            t.Set(ThemeColorKey.ScrollbarThumb,        new Color32(230, 120, 160, 255)); // pink
            return t;
        }

        private void ApplyDebugTheme()
        {
            var keepImgui = _working.applyImguiPass;
            _working = BuildDebugTheme();
            _working.applyImguiPass = keepImgui;
            RefreshColorFields();
            _customize.value = true;
            UpdateSelectionHighlight();
            // Apply live but do NOT persist — it's a throwaway diagnostic, restored on restart.
            EditorThemeApplier.Apply(_working.Clone(), persist: false);
            // Print the color→tag legend so any observed region color can be traced to its tag.
            if (!string.IsNullOrEmpty(UssThemeGenerator.LastDebugLegend))
                Debug.Log("[Editor Theme Kit] Debug tag colors (color = tag):\n" + UssThemeGenerator.LastDebugLegend);
        }
    }
}
