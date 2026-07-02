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
            _gallery.style.flexDirection = FlexDirection.Row;
            _gallery.style.flexWrap = Wrap.Wrap;
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

            var pathNote = new Label("Saved to: UserSettings/EditorThemeKit/  (ActiveTheme.json + Themes/)");
            pathNote.style.opacity = 0.6f;
            pathNote.style.marginTop = 8;
            pathNote.style.fontSize = 11;
            root.Add(pathNote);
        }

        // ---- gallery ---------------------------------------------------------------

        private void BuildGallery()
        {
            _gallery.Clear();
            _cards.Clear();

            foreach (var p in ThemePresets.All)
                AddCard(p.Id, p.DisplayName, ThemePresets.Create(p.Id), isCustom: false);

            foreach (var name in ThemeStorage.ListCustomThemes())
            {
                var data = ThemeStorage.LoadCustomTheme(name);
                if (data != null)
                    AddCard("custom:" + name, name, data, isCustom: true);
            }

            UpdateSelectionHighlight();
        }

        private void AddCard(string id, string display, EditorThemeData data, bool isCustom)
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

            _gallery.Add(card);
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
            if (_working.presetId != ThemePresets.CustomId)
            {
                _working.presetId = ThemePresets.CustomId;
                _working.displayName = "Custom";
            }
            UpdateSelectionHighlight();
            ScheduleApply();
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
    }
}
