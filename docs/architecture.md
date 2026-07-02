# Architecture — Editor Theme Kit

_BMAD phase: Architect_

## How Unity editor theming works (and our approach)

Unity 6's editor is a **hybrid**. The top toolbar and many controls are **UIToolkit**;
window bodies (Inspector, Hierarchy, Project, Console) and dock tabs are still **IMGUI**.

**Empirically established in Unity 6000.5** (via live experiments over the MCP bridge):

- Injecting into `EditorWindow.rootVisualElement` only reaches a window's *content*, not
  the chrome. The chrome lives in the **`GUIView` host-view panels** (reachable via the
  internal `GUIView.visualTree` property). Injecting there is what reaches the toolbar/dock
  panels.
- Overriding `--unity-colors-*` variables is **not sufficient**: Unity 6 sets many chrome
  colors directly in its built-in sheet, not via `var(--unity-colors-*)`. Verified the
  toolbar ignores the variable override.
- A stylesheet added to a `GUIView` panel with a **plain class selector loses** to Unity's
  built-in rule (order/specificity). A **doubled-class selector** (`.x.x`, specificity
  0,2,0) **wins**. Verified: `.unity-editor-main-toolbar.unity-editor-main-toolbar` recolors
  the toolbar.
- The `Assets/Editor/StyleSheets/Extensions/{common,dark,light}.uss` mechanism from older
  Unity threads has **no effect** in Unity 6, even after a domain reload.

So we generate a `.uss` with (1) `:root` `--unity-colors-*` overrides for controls that do
read them, plus (2) high-specificity chrome rules on Unity's real class names, and inject
it into every `GUIView.visualTree` (and `EditorWindow.rootVisualElement` for custom
windows). **Importing a `.uss` asset does not recompile C# or trigger a domain reload**, so
switching is instant.

The IMGUI surface (window bodies, dock tabs) is not reached by USS. A reversible IMGUI pass
recolors label text by default and, opt-in ("deep"), text across all built-in styles plus
flat backgrounds via `GUIStyle`. Broader IMGUI-body theming is a planned follow-up.

## Data flow

```
UserSettings/EditorThemeKit/ActiveTheme.json
        │  (load on [InitializeOnLoad])
        ▼
   EditorThemeData  ──edit in──►  Project Settings UI
        │                              │
        │  UssThemeGenerator            │ live/debounced
        ▼                              ▼
   generated USS text ──write──► Assets/EditorThemeKit.Generated/ActiveTheme.uss
        │  AssetDatabase.ImportAsset (no domain reload)
        ▼
   StyleSheet asset ──inject──► every EditorWindow.rootVisualElement.styleSheets
        │
        └──► IMGUI accent pass (EditorStyles), reversible
```

## Components

- **`EditorThemeData`** (`Model/`) — `[Serializable]` plain class: a set of named
  `Color` semantic values + `presetId` + `displayName`. Serialized with `JsonUtility`
  (Color round-trips natively). No ScriptableObject asset needed → nothing to import for
  the *setting* itself.
- **`ThemePresets`** (`Presets/`) — static factory returning the built-in read-only
  themes by id.
- **`ThemeStorage`** (`IO/`) — reads/writes `ActiveTheme.json` under the project's
  `UserSettings/EditorThemeKit/` folder (resolved from `Application.dataPath/../
  UserSettings`). Pure file I/O + `JsonUtility`.
- **`UssThemeGenerator`** (`Generation/`) — maps semantic colors → concrete
  `--unity-colors-*` declarations and emits a `:root { … }` USS string. Central mapping
  table is the only version-sensitive surface; extra/unknown vars are harmless.
- **`EditorThemeApplier`** (`Core/`) — `[InitializeOnLoad]` entry point. Owns the current
  `StyleSheet`, writes+imports the generated USS, injects it into all open
  `EditorWindow`s (`Resources.FindObjectsOfTypeAll<EditorWindow>()`), keeps new windows
  covered (polls/hook), runs & reverts the IMGUI pass, and exposes
  `Apply(EditorThemeData)` / `RevertToDefault()`.
- **`EditorThemeKitSettingsProvider`** (`Settings/`) — `SettingsProvider` (UIToolkit)
  rendering the preset dropdown + color fields; calls `EditorThemeApplier.Apply` live and
  `ThemeStorage.Save` on save.

## Key decisions & trade-offs

- **Generated USS asset vs. runtime-built StyleSheet.** Unity exposes no stable public
  API to parse a USS string into a `StyleSheet` at runtime. Generating a `.uss` file and
  importing it uses only public, stable APIs and still avoids domain reload. Cost: one
  generated asset in the project. Mitigation: keep it in a dedicated
  `Assets/EditorThemeKit.Generated/` folder and document git-ignoring it.
- **Semantic colors vs. raw tokens.** Editing ~10 semantic colors is approachable;
  exposing 100+ raw tokens is not. The generator fans out semantics to tokens.
- **Injection scope.** `rootVisualElement` covers in-window chrome. The container/dock
  frame above it may retain stock colors; acceptable for v1 and the dominant surface is
  covered.
- **Reflection policy.** Only the IMGUI accent pass touches internals, all guarded in
  `try/catch`; failure degrades gracefully (chrome still themes).

## Assembly / layout

```
Packages/com.editorthemekit.core/
  package.json
  Editor/
    EditorThemeKit.Editor.asmdef        (Editor platform only)
    Model/EditorThemeData.cs
    Presets/ThemePresets.cs
    IO/ThemeStorage.cs
    Generation/UssThemeGenerator.cs
    Core/EditorThemeApplier.cs
    Settings/EditorThemeKitSettingsProvider.cs
  docs/ (brief, prd, architecture, stories)
```
