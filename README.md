# Super Dark Editor

A local Unity package (`com.blind.superdark`) that darkens the
Unity 6 dark editor theme with the same color rule. Built and verified on Unity 6000.3.16f1.

Nothing is hand-written: the package reads Unity's own stylesheets and generates darkened copies of
every dark-grey surface color it finds.

### How Unity colors its editor

| System | Used by | Where colors live | How the package overrides them |
|---|---|---|---|
| UI Toolkit | Inspector fields, toolbars, overlays, newer windows | ~730 built-in `.uss` sheets (plus package sheets), attached to specific elements | One generated override per source sheet, attached right after it on the same element |
| IMGUI style catalog | Window backgrounds, Hierarchy, Project, Console, Timeline | USS files compiled into a catalog; packages extend it via `…/StyleSheets/Extensions/dark.uss` | One generated `Extensions/Generated/dark.uss`, merged into the catalog by Unity |
| IMGUI textures / C# colors | Game view background, Animator grid, Animation dopesheet, Audio Mixer strips, IMGUI reorderable lists | Bitmaps in the editor skin or hard-coded in Unity's C# | Not covered |

### Files

| Path | Purpose |
|---|---|
| `Editor/OverrideRegeneration.cs` | On editor load, compares a fingerprint (generator version, Unity version, installed packages) stored in the project's `Library/SuperDarkEditor/`. When it changed, regenerates all overrides and refreshes. |
| `Editor/UnityThemeSources.cs` | Access to Unity internals (via reflection): built-in sheets, catalog sources, USS export rule by rule. |
| `Editor/DarkOverrideBuilder.cs` | Picks surface color properties (`background`, `border`, `separator`, `hover`, `selected`, custom `--*color*`; never `text`/`font`/`tint`) and darkens their values. |
| `Editor/GreyDarkening.cs` | The color rule. |
| `Editor/UssRuleSet.cs` | Minimal USS parse / merge / write. |
| `Editor/SuperDarkEditorTheme.cs` | Every 0.5 s walks open editor windows (~1 ms) and attaches each generated override right after its source sheet. |
| `Editor/Generated/UIToolkit/*.uss` | Generated UI Toolkit overrides, one per Unity sheet. |
| `Editor/StyleSheets/Extensions/Generated/dark.uss` | Generated IMGUI catalog overrides. |

Generated files are merged, not replaced, so projects with different packages (e.g. with/without Timeline)
can share the package without fighting over the files.

### Color rule

Only dark neutral colors change; everything else keeps Unity's value.

- Pure grey `1…110`: halve, R − 1 and G − 1, B = R + 4 (e.g. `#383838` → `#1B1B1F`).
- Slightly tinted grey (channel spread ≤ 6): halve every channel.
- Unchanged: accents (selection blue, warning, error), greys above 110 (slider thumbs, checkmarks), black, text colors.

## Install

Package Manager → **+** → **Install package from disk** → `package.json` in this repository.
This adds a `file:` reference to the project's `Packages/manifest.json`; repeat per project.
Unity reads the package from this folder on every load, so keep the clone in place.

Do not install it from a Git URL: the package writes its generated overrides into its own folder,
which must stay writable. Generated files are git-ignored and rebuilt automatically when missing.
To turn the theme off, remove the package.

## Limits

- Relies on Unity internals (`EditorResources`, `StyleSheetToUss`, `UIElementsEditorUtility`, the extension-sheet
  mechanism). A Unity upgrade can break regeneration; the package then logs a warning and keeps the last generated files.
- Sheets Unity creates at runtime (e.g. `OverlayPreferences`) are not read.
- The Windows menu bar (File, Edit, …) is native and cannot be styled.
