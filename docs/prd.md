# PRD — Editor Theme Kit

_BMAD phase: PM_

## Overview

Editor Theme Kit (ETK) is an editor-only Unity package that recolors the Unity Editor
from Project Settings using generated Unity Style Sheets (USS) that override Unity's
built-in `--unity-colors-*` design tokens, plus a light IMGUI accent pass. Theme
selection is stored per-user under `UserSettings/`.

## Functional requirements

- **FR1 — Project Settings UI.** A settings page under
  `Project Settings → Editor Theme Kit` with: a preset dropdown, editable color swatches
  for each semantic color, an "Apply" (live), "Save", "Revert to Unity default", and
  "Duplicate preset to custom" action.
- **FR2 — Presets.** Ship built-in presets: `Unity Default (Dark)`, `Unity Default
  (Light)`, `Dracula`, `Monokai`, `Solarized Dark`, `Nord`, `Gruvbox Dark`, `High
  Contrast`. Presets are read-only; editing one starts a custom theme.
- **FR3 — Custom theme.** The user can edit any semantic color; the result is a "Custom"
  theme.
- **FR4 — Instant apply, no recompile.** Applying regenerates a USS asset and re-injects
  it into open editor windows. Must not modify `.cs` files or trigger a domain reload.
- **FR5 — Per-user persistence.** The active theme (preset id or full custom color set)
  is serialized to `UserSettings/EditorThemeKit/ActiveTheme.json`. Loaded on editor
  startup and applied automatically.
- **FR6 — Revert.** One action removes injected stylesheets and restores IMGUI styles,
  returning to the stock Unity skin without restart.
- **FR7 — New-window coverage.** Windows opened after apply are themed automatically.
- **FR8 — Live edit.** Editing a color in the settings updates the editor immediately
  (debounced), before an explicit Save.

## Semantic color model

The user edits a small set of *semantic* colors; the generator expands each to the
concrete Unity tokens it drives:

| Semantic key         | Purpose                                             |
|----------------------|-----------------------------------------------------|
| `WindowBackground`   | Main window / view backgrounds                      |
| `HeaderBackground`   | App toolbar, inspector title bars                   |
| `ToolbarBackground`  | In-window toolbars                                  |
| `TabBackground`      | Dock tab backgrounds (+ selected variant)           |
| `InputBackground`    | Text/object/number field backgrounds                |
| `ButtonBackground`   | Buttons                                             |
| `Border`             | Separators / borders                                |
| `Text`               | Default label text (+ hover/selected)               |
| `Accent`             | Selection / highlight / focus ring                  |
| `ScrollbarThumb`     | Scrollbar thumbs                                    |

## Non-functional requirements

- **NFR1** Apply latency < 1 frame perceptually; no domain reload.
- **NFR2** Editor-only assembly; excluded from player builds.
- **NFR3** Robust to Unity version drift: unknown tokens are additive (extra USS vars are
  harmless); all reflection is guarded and non-fatal.
- **NFR4** No writes into the Unity installation directory. Generated USS lives in a
  dedicated, git-ignorable project folder.
- **NFR5** Zero third-party dependencies.

## Acceptance criteria

- Selecting a preset recolors editor chrome within one frame, no compile bar (AC for
  FR1/FR2/FR4).
- Killing and relaunching the editor restores the last theme (FR5).
- The active-theme file resides in `UserSettings/`, not `Assets/` (FR5, NFR4).
- "Revert to Unity default" restores the stock skin live (FR6).
- Opening a new window after apply shows the theme (FR7).
