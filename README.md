# Editor Theme Kit

Recolor the **Unity Editor** — window backgrounds, toolbars, dock tabs, title bars,
hierarchy/project lists, fields, buttons and selection — from **Project Settings**. Ships
common presets, lets you build and save your own themes, and applies **instantly with no
script recompilation**. Your active theme is stored **per-user** in the project's
`UserSettings/` folder, so it never travels with the shared project.

> Requires **Unity 6000.0 (Unity 6) or newer** — the editor chrome must be the modern
> UIToolkit-based skin.

## Install

### Package Manager → Add package from git URL

1. Open **Window → Package Manager**.
2. Click **+ → Add package from git URL…**
3. Enter:

   ```
   https://github.com/Kobapps/EditorThemeKit.git
   ```

### Or add it to `Packages/manifest.json`

```json
{
  "dependencies": {
    "com.kobapps.editorthemekit": "https://github.com/Kobapps/EditorThemeKit.git"
  }
}
```

To pin a version, append `#<tag>` (e.g. `…EditorThemeKit.git#v0.6.0`).

## Usage

Open **Edit → Project Settings → Editor Theme Kit**.

- **Pick a theme** from the swatch gallery — one click applies it live.
- **Customize colors**: expand the *Customize colors* section to edit any color. Editing
  forks a live "Custom" theme.
- **Save your own**: type a name and press **Save Theme** — it's saved to
  `UserSettings/EditorThemeKit/Themes/` and appears in the gallery (delete with the ✕ on
  its card).
- **Also tint IMGUI**: adds a gradient-preserving tint to legacy IMGUI buttons/dropdowns/
  component headers that USS can't reach.
- **Revert to Unity Default** restores the stock skin at any time.

Built-in presets: Unity Default (Dark/Light), Dracula, Monokai, Solarized Dark, Nord,
Gruvbox Dark, High Contrast.

## How it works

Unity 6's editor chrome is styled by internal USS classes (`.TabWindowBackground`,
`.ScrollViewAlt`, `.dockHeader`, `.IN Title`, `.dragtab-label`, `.AppToolbar`, `.Toolbar`,
hierarchy `.TV Line`/`.OL EntryBack*`, selection `.TV Selection`/`.OL SelectedRow`, …).
Editor Theme Kit generates a stylesheet overriding those and writes it as
`dark.uss`/`light.uss` under an `Editor/StyleSheets/Extensions/` folder; Unity's editor
skin-extension mechanism merges it and recolors the chrome. Importing a `.uss` asset does
**not** recompile C# or trigger a domain reload, so switching themes is instant. A small,
fully reversible IMGUI pass covers the legacy controls USS can't reach.

Nothing in the Unity installation is modified — theming is entirely project-local and
reversible.

## Notes & limits

- The generated stylesheet lives at
  `Assets/EditorThemeKit.Generated/Editor/StyleSheets/Extensions/`. It's a build artifact —
  safe to git-ignore (`Assets/EditorThemeKit.Generated/`).
- Solid theme fills replace some of Unity's subtle gradients/borders — the trade for
  arbitrary theme colors. The IMGUI pass preserves gradients where it can.
- Only one editor-theme extension should be active at a time; running another editor-theme
  asset alongside it will conflict (Unity merges all extension stylesheets).

## License

MIT — see [LICENSE](LICENSE).
