# Epic & Stories — Editor Theme Kit

_BMAD phase: Scrum Master → Dev_

## Epic: Configurable, no-recompile editor theming

### Story 1 — Theme data model & presets
- `EditorThemeData` serializable semantic-color model.
- `ThemePresets` with built-in read-only themes.
- **DoD:** presets constructible by id; data round-trips through `JsonUtility`.

### Story 2 — Per-user persistence (UserSettings)
- `ThemeStorage` load/save to `UserSettings/EditorThemeKit/ActiveTheme.json`.
- **DoD:** save writes under `UserSettings/`; load returns saved theme or default.

### Story 3 — USS generation
- `UssThemeGenerator` maps semantics → `--unity-colors-*`, emits `:root {}` USS.
- **DoD:** output is valid USS; every semantic color drives ≥1 token.

### Story 4 — Applier (inject + revert, no domain reload)
- `EditorThemeApplier` `[InitializeOnLoad]`: write+import USS, inject into all windows,
  cover new windows, IMGUI accent pass, revert.
- **DoD:** apply recolors chrome with no compile; revert restores stock; new windows
  covered; startup auto-applies persisted theme.

### Story 5 — Project Settings UI
- `EditorThemeKitSettingsProvider`: preset dropdown + color fields + apply/save/revert.
- **DoD:** editing live-applies; save persists; revert restores default.

### Story 6 — Packaging & docs
- `package.json`, asmdef (editor-only), README, CHANGELOG, `.gitignore` guidance for the
  generated folder.
- **DoD:** package resolves in Unity 6; assembly is editor-only.

## Status
- [x] Story 1  - [x] Story 2  - [x] Story 3  - [x] Story 4  - [x] Story 5  - [x] Story 6
