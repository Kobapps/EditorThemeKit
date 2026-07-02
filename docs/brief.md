# Project Brief — Editor Theme Kit

_BMAD phase: Analyst_

## Problem

Unity ships only two editor skins (Personal / Professional). Teams and individual
developers frequently want to recolor the editor — to reduce eye strain, to match a
studio's brand, or to visually distinguish multiple open projects. Doing this today
means editing Unity's installed USS files (breaks on upgrade, affects all projects) or
buying an asset. The community thread and the free "Editor Themes Plugin" asset show
clear demand, but existing solutions are either fragile or limited.

## Goal

A Unity package that lets a developer restyle the editor from **Project Settings**,
choose from **common presets** or author a **fully custom theme**, with changes applied
**instantly and without triggering script compilation / domain reload**. The active
selection is stored **per-user** (not committed with the project) in the `UserSettings/`
folder.

## Target users

- Solo developers who want a comfortable editor look.
- Teams that keep the *package* in version control but let each member pick their own
  theme locally (hence per-user storage).

## Constraints & principles

- **No recompilation to switch themes.** Applying a theme must not touch `.cs` files or
  force a domain reload. (Importing a `.uss` asset is allowed — it does not recompile C#.)
- **Non-destructive.** Never modify files in the Unity installation. Fully reversible
  (revert to the built-in skin).
- **Per-user setting.** The chosen theme lives in `UserSettings/` (git-ignored by Unity
  convention), so it does not travel with the shared project.
- **Editor-only.** No runtime footprint in player builds.
- **Unity 6+ (UIToolkit editor chrome).** Target 6000.x.

## Non-goals (v1)

- Pixel-perfect reskinning of every legacy IMGUI control (full IMGUI texture swaps are
  fragile across versions). v1 themes the UIToolkit chrome comprehensively and applies a
  light, reversible IMGUI accent pass.
- Per-window theming, font replacement, custom icons.
- Runtime (in-player) theming.

## Success criteria

1. A user opens Project Settings → Editor Theme Kit, picks "Dracula", and the editor
   toolbars/windows/fields/tabs recolor within a frame — no recompile spinner.
2. Restarting the editor restores the same theme (persisted in `UserSettings/`).
3. "Revert to Unity default" cleanly restores the stock look.
4. The theme file is not added to the project's `Assets/` version control surface.
