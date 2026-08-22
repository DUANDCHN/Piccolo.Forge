# Piccolo.Forge

English · [中文](README.zh-CN.md)

IntelliJ-style tool windows, docking and editor tabs for **Avalonia** — a faithful
re-implementation of the IntelliJ Platform's tool-window layout (stripes, anchors,
split halves, view modes) and its two modern looks, **expUI** and **Islands**.

Not a port of Java source: every behaviour and every number traces back to a
specific class or field in `intellij-community` (see `docs/upstream/`, Chinese).

> Status: **0.1.0-preview**. Used in production by one host application; public API
> is tracked with `PublicAPI.*.txt` and will stabilise at 1.0.

## Packages

| Package | What's inside | Depends on |
|---|---|---|
| `Piccolo.Forge.Model` | Pure model: `WindowInfo`, `DesktopLayout`, `ToolWindowManager`, editor groups / splits / tab rules, layout serialisation | — |
| `Piccolo.Forge.Mvvm` | Observable view-models for tool windows and editor content. No Avalonia, no MVVM framework | Model |
| `Piccolo.Forge.Mvvm.CommunityToolkit` | Panel base classes on top of CommunityToolkit.Mvvm (`[ObservableProperty]` keeps working) | Model, CommunityToolkit.Mvvm |
| `Piccolo.Forge.Avalonia` | Controls: stripe buttons, stripes, dock panes, headers, editor tabs, drag & drop, floating windows; metrics, palettes, 13 locales | Avalonia 12 |
| `Piccolo.Forge.Themes.ExpUi` | The classic expUI look (`ForgeExpUiTheme`) | Avalonia |
| `Piccolo.Forge.Themes.Islands` | The Islands look — rounded panels floating on a "sea" (`ForgeIslandsTheme`) | Avalonia |

Reference **exactly one** theme package (or both and let the user choose; a theme
change requires a restart — see the host guide).

Targets `net8.0` and `net10.0`; AOT/trim compatible.

## Quick start

```xml
<Application.Styles>
    <FluentTheme />
    <themes:ForgeExpUiTheme />   <!-- xmlns:themes="using:Piccolo.Forge.Themes.ExpUi" -->
</Application.Styles>
```

```csharp
// 1. the shell view-model is the host's entry point
var shell = new ToolWindowShellViewModel();

// 2. register panels: where they dock is the only thing you say; where the
//    stripe button lands is computed, as in IntelliJ
shell.Add(new ProjectPanel(), ToolWindowAnchor.Left);
shell.Add(new StructurePanel(), ToolWindowAnchor.Left, isSplit: true);
shell.Add(new TerminalPanel(), ToolWindowAnchor.Bottom);

// 3. editor area (tabs + binary split tree)
var editor = new EditorAreaViewModel();
editor.Open("readme", "README.md", content: new ReadmeView());
var editorControl = new ForgeEditorControl();
editorControl.Attach(editor);

// 4. the frame
var frame = new ForgeControl { EditorContent = editorControl };
frame.Attach(shell);
window.Content = frame;

shell.Open("Project");
```

Panel content is an `object`; resolve views with your own `ViewLocator` /
`DataTemplate`. Opening and closing panels never recreates existing controls.

Localisation follows Semi.Avalonia's model: call
`ForgeThemeManager.ApplyLocale(app, CultureInfo.CurrentUICulture)` and the
right dictionary (en, zh-CN, zh-Hant, ja, ko, de, fr, es, it, nl, pl, ru, uk)
is merged; override any `Forge.Strings.*` key in your own resources.

A complete runnable example is in [`samples/Piccolo.Forge.Sample`](samples/Piccolo.Forge.Sample).
The host integration guide (what the library does, what stays with you) is
[`docs/conventions/宿主接入指南.md`](docs/conventions/宿主接入指南.md) (Chinese).

## What works

- V2 default layout: left/right vertical stripes, top/bottom groups, group separators
- One visible window per slot; two windows on the same side split by `sideWeight`
- Resizable dock panes (0 px divider, 6 px hot zone); drag stripe buttons between stripes
- View modes: dock pinned / unpinned, undock, float, window — with bounds persistence
- Light/dark expUI palettes, compact metrics, `JBUI.scale` rounding rules
- **Islands** theme: rounded panels with gaps, orthogonal to light/dark
- Layout save/load (same XML shape as upstream)
- "Show names" stripe mode, content tabs in the header row, pin/close, "More" button
- Context menus, "More" popup, floating-window fade timer, Alt-double-click state machine
- **Editor area**: tabs (select / pin / close / limit eviction), binary split tree, full
  "what to select after closing" rules, drag tabs between groups, empty-area backdrop
- Badges on stripe buttons, accessible names on every interactive control

Deliberately not done (reasons in `docs/upstream/工具窗口模型.md` §17): JetBrains icon
assets (we draw our own at the audited sizes), nested splits *inside* a tool window,
the global Alt keyboard hook (the state machine is there; hook it up in your host).

## Building

```bash
dotnet test Piccolo.Forge.slnx
```

xunit v3 + Avalonia headless with Skia render snapshots (`FORGE_SNAPSHOT_DIR` sets
the output folder). Warnings are errors; the public surface is guarded by
`Microsoft.CodeAnalysis.PublicApiAnalyzers`.

## License

MIT — see [LICENSE](LICENSE). Behaviour and metrics are derived from the IntelliJ
Platform (Apache-2.0) and tab-move semantics from Dock (MIT); see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
