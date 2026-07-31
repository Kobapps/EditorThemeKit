# Editor Theme Kit

Customize the Unity Editor's colors from **Project Settings** — toolbars, window and
field backgrounds, tabs, text, and selection accent. Ships common presets, supports fully
custom themes, applies **instantly with no script recompilation**, and stores your choice
**per-user** in the project's `UserSettings/` folder (so it never travels with the shared
project).

Requires **Unity 6000.0+** (the editor chrome must be UIToolkit-based).

## Install

Embedded (this repo): the package lives in `Packages/com.kobapps.editorthemekit`; Unity picks
it up automatically.

Via Package Manager → *Add package from git URL…* (if hosted), or *Add package from
disk…* pointing at `Packages/com.kobapps.editorthemekit/package.json`.

## Use

1. Open **Edit → Project Settings → Editor Theme Kit**.
2. Pick a **Preset** (Dracula, Monokai, Solarized Dark, Nord, Gruvbox Dark, High Contrast,
   or Unity Dark/Light).
3. Or edit any color swatch to build a **Custom** theme. Changes apply live.
4. **Reset to Preset** re-loads the current preset's colors. **Revert to Unity Default**
   removes all theming and restores the stock skin.

Your active theme is saved to `UserSettings/EditorThemeKit/ActiveTheme.json` and restored
on the next editor launch.

## How it works

Unity 6's editor is a **hybrid**: the top toolbar and many controls are UIToolkit, while
window bodies (Inspector, Hierarchy, Project, Console) are still IMGUI. Editor Theme Kit
generates a `.uss` sheet and injects it into each **`GUIView` host-view panel** (via
reflection) — the panels that own the editor chrome. Unity 6 sets many chrome colors
*directly* in its built-in sheet (not via `var(--unity-colors-*)`), so the sheet wins the
cascade with high-specificity (doubled-class) selectors on Unity's real class names (e.g.
`.unity-editor-main-toolbar`). It also sets the `--unity-colors-*` tokens for controls that
do read them. **Importing a USS asset does not recompile C# or trigger a domain reload**,
so switching themes is instant.

> Earlier versions injected into `EditorWindow.rootVisualElement`, which only reaches a
> window's content, not the chrome. The `Assets/Editor/StyleSheets/Extensions/` trick from
> older Unity threads no longer drives the chrome in Unity 6.

**Window backgrounds** are painted by setting each `EditorWindow.rootVisualElement`
background directly (their UITK content is transparent over the dock area, and Unity 6
doesn't color them via tokens). **Title-bar tabs** are drawn by `DockArea`/`HostView` with
their own *cached* `GUIStyle`s, so a reversible IMGUI pass (on by default, *"Also tint
IMGUI"* toggle) retints those cached styles plus label text. For fuller coverage, enable
*"Deep IMGUI reskin (experimental)"* — it also recolors text across all built-in styles and
flattens box/field/button backgrounds. Everything is reversible; disable to undo instantly.

## Notes & limits

- The generated sheet lives at `Assets/EditorThemeKit.Generated/ActiveTheme.uss`. It is a
  build artifact — safe to git-ignore (`Assets/EditorThemeKit.Generated/`).
- Two theming layers: **USS** into the `GUIView` panels (UITK toolbar + controls) and
  **IMGUI** style retinting (window bodies, dock tabs, title bars). Flat solid fills replace
  Unity's subtly-gradiented/9-sliced textures, so gradients and some borders are lost — the
  trade for arbitrary theme colors.
- The IMGUI pass mutates shared built-in styles, so it can clash with other editor
  extensions. Turn off *"Also tint IMGUI"* if anything looks wrong.
- Nothing in the Unity installation is modified; theming is fully reversible.

See `docs/` for the BMAD planning artifacts (brief, PRD, architecture, stories).
