# Changelog

All notable changes to Editor Theme Kit are documented here.
This project adheres to [Semantic Versioning](https://semver.org/).

## [0.6.4] - 2026-07-03

### Fixed
- **Window dividers are now actually visible.** Unity 6 exposes no themeable inter-window
  divider, so each window's content root gets a 1px border in the Border color — outlining
  panels and making the lines between docked windows clear. The divider color is derived to
  be a **darker groove** than the window (lightened only on near-black themes), and re-applies
  to newly opened windows.

## [0.6.3] - 2026-07-02

### Added
- **Optional .gitignore entry**: the first time a theme is applied in a git project, Editor
  Theme Kit offers (once) to add `Assets/EditorThemeKit.Generated/` to `.gitignore`.

### Fixed
- **Window/pane dividers stay visible.** The Border/Separator color is now applied to split
  draglines, and presets derive it to always contrast with the window background (lighter on
  dark themes, darker on light) so it doesn't vanish on some themes.

## [0.6.2] - 2026-07-02

### Added
- **Light/Dark base skin per theme.** Each theme now declares a base skin; applying it
  switches Unity's editor skin (Pro/Personal) to match, so default text stays readable —
  this fixes light themes rendering light-on-light (invisible) text. Set it via the new
  **Base skin** dropdown in Customize; every gallery card shows an **L/D** badge.

### Fixed
- **Selected tab now reads as a different color from the header.** Presets set the selected
  tab to the window (content) color and unselected tabs to the header color — the
  conventional Unity look, so the active tab stands out instead of blending into the strip.

## [0.6.1] - 2026-07-02

### Fixed
- **Dock header bar now colors** and the **selected tab is a distinct shade**. The dock
  header/tabs are painted by `DockArea`/`HostView` shared static `GUIStyle`s that the USS
  name-bridge couldn't fully reach (no header, no selected state). The IMGUI pass now
  retints them directly from `EditorApplication.update` (so it works even with no inspector
  open): header/strip → Header color, unselected tabs → Tab color, selected tab (`on*`
  states) → Tab (Selected) color. Removed the now-redundant `.dockHeader`/`.dragtab-label`
  USS rules.

## [0.6.0] - 2026-07-02

### Changed
- **Adopted the editor stylesheet-extension technique** (as used by the EditorThemes asset):
  generated USS now targets Unity's real internal selectors (`.TabWindowBackground`,
  `.ScrollViewAlt`, `.dockHeader`, `.IN Title`, `.dragtab-label`, `.AppToolbar`, `.Toolbar`,
  hierarchy `.TV Line`/`.OL EntryBack*`, selection `.TV Selection`/`.OL SelectedRow`, UITK
  controls, …) with hover/focus/active/checked states, written to `dark.uss`+`light.uss`
  under `Editor/StyleSheets/Extensions/` + `AssetDatabase.Refresh()`. This colors the full
  chrome — window bodies, tabs, title bars, toolbars, hierarchy/lists, selection.
- IMGUI pass rewritten to **preserve gradients**: copies the original style texture and
  shades per-pixel by luminance (buttons/dropdowns/component headers), instead of flat fills.

### Added
- **Theme gallery UI**: swatch-card grid of built-in presets + saved custom themes; click a
  card to apply.
- **Collapsible color editor**: pickers are hidden until you expand "Customize colors" (auto-
  expanded for custom themes); editing any color forks a live "Custom" theme.
- **Custom themes**: save the current colors as a named theme (per-user, under
  `UserSettings/EditorThemeKit/Themes/`), and delete them from the gallery.

## [0.5.0] - 2026-07-02

### Fixed
- **Window backgrounds now recolor.** In Unity 6 editor-window bodies are transparent UITK
  over the dock area, and Unity does NOT drive their color from `--unity-colors-*`. The
  applier now paints each `EditorWindow.rootVisualElement.style.backgroundColor` directly
  (verified live: Inspector/Hierarchy/Project/Console bodies take the theme's window color).
- **Title-bar tabs now recolor.** The dock tab strip is drawn by `DockArea`/`HostView`
  using their own *cached* `GUIStyle` copies (not the built-in skin styles), so retinting
  the skin had no effect. The IMGUI pass now reflects into those cached styles
  (`DockArea.Styles.background`/`dragTab`/`dragTabFirst`/`dockTitleBarStyle`,
  `HostView.Styles.background`/`tabWindowBackground`) and retints them, reversibly.

### Notes
- Investigated exhaustively against the live editor: USS on window content, the UITK panel
  root, skin `GUIStyle`s, and built-in texture recolor all had NO effect on the window body
  — only painting the window content-root background does. This is the correct Unity 6 path.

## [0.4.0] - 2026-07-02

### Added
- **Window backgrounds and title bars now themed.** In Unity 6 the dock/window bodies and
  title-bar tabs are painted by IMGUI (inside each dock area's `IMGUIContainer`), not USS.
  The IMGUI pass now retints the built-in styles that draw them — `dockarea`, `hostview`,
  `dockareaStandalone`, `TabWindowBackground` (window bg), `dragtab`/`dragtab first` (tabs),
  `dockHeader` + `IN BigTitle`/`IN Title` (title bars), and `Toolbar`. Verified live:
  `dockarea` background became the theme's window color and reverted exactly.
- This chrome retint runs whenever "Also tint IMGUI" is on (default), handles retina
  `scaledBackgrounds`, and is fully reversible.

## [0.3.0] - 2026-07-02

### Fixed
- **Editor chrome now actually recolors** (previously only text changed). The stylesheet
  is injected into each `GUIView` host-view panel (via reflection) instead of
  `EditorWindow.rootVisualElement`, which never reached the chrome. Verified live: the main
  toolbar recolors to the theme's header color and reverts cleanly.

### Changed
- USS generation adds a "Layer 2" of explicit editor-chrome rules using high-specificity
  (doubled-class) selectors on Unity 6's real class names (e.g. `.unity-editor-main-toolbar`,
  `.unity-button`, `.unity-base-text-field__input`). Unity 6 sets many chrome colors
  directly rather than via `var(--unity-colors-*)`, so variable overrides alone had no
  effect; the high-specificity rules win the cascade.

### Notes
- Confirmed the `Assets/Editor/StyleSheets/Extensions/*.uss` approach from older Unity
  threads does **not** drive the chrome in Unity 6 (no effect even after a domain reload).
- IMGUI window *bodies* remain IMGUI-drawn; broader IMGUI-body theming is a follow-up.

## [0.2.0] - 2026-07-02

### Added
- **Deep IMGUI reskin** (opt-in, experimental): recolors text across every built-in IMGUI
  style and flattens key backgrounds (box, window, scroll view, text/object fields,
  buttons, help boxes) to theme colors using generated solid textures. Toggle *"Deep IMGUI
  reskin (experimental)"* in Project Settings. Fully reversible — each mutation records an
  undo step and generated textures are destroyed on revert. Defaults off.

## [0.1.0] - 2026-07-02

### Added
- Project Settings page (`Project Settings → Editor Theme Kit`) with preset dropdown,
  editable semantic color swatches, and revert/reset actions.
- Built-in presets: Unity Default (Dark/Light), Dracula, Monokai, Solarized Dark, Nord,
  Gruvbox Dark, High Contrast.
- USS generation overriding Unity's `--unity-colors-*` tokens, injected into every editor
  window — instant apply with no script recompilation / domain reload.
- Light, reversible IMGUI label-text tinting pass.
- Per-user persistence to `UserSettings/EditorThemeKit/ActiveTheme.json`, auto-applied on
  editor startup.
- "Revert to Unity Default" restores the stock skin live.
