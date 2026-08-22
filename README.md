# Piccolo.Forge

IntelliJ-style tool windows, docking and editor tabs for [Avalonia](https://avaloniaui.net/).
中文说明在下方 ↓ ([中文](#中文))

## What it is

Piccolo.Forge gives an Avalonia desktop app the workbench of IntelliJ IDEA / Rider:

- **Tool windows** on four sides, each side split into two halves, with the vertical
  *stripes* of icon buttons that IntelliJ's new UI uses instead of tabs.
- **View modes** — docked (pinned / unpinned), undocked, floating, separate window —
  with the same rules for what stays visible and what auto-hides.
- An **editor area** with tabs, pin / close / limit rules, and a binary split tree you
  can rearrange by dragging tabs.
- Two looks, shipped as separate packages: the classic **expUI** and **Islands**
  (rounded panels floating on a "sea"), each with light and dark variants.
- Layout persistence, context menus, a "More" overflow button, badges on stripe
  buttons, accessible names, and UI text in 13 languages.

## Design

The layout model is the IntelliJ one, not a generic docking tree. A tool window has an
*anchor* (Left / Right / Top / Bottom) and an *isSplit* flag (first or second half of
that side); **where its stripe button is drawn is computed from those two values**,
the same way `ToolWindowManager` does it upstream. This is what keeps the button
positions, auto-hide behaviour and "one visible window per slot" rule consistent no
matter how the user drags things around.

Every behaviour and every pixel value — stripe widths, header heights, corner radii,
theme colours — is taken from the IntelliJ Platform sources rather than guessed from
screenshots, so the result looks and feels like the original.

The model layer has no UI dependency and no MVVM framework; view-models come in a
neutral package and a CommunityToolkit.Mvvm flavour. Panel content is a plain `object`
resolved by your own `ViewLocator` / `DataTemplate`, and opening or closing a panel
never recreates existing controls.

## Packages

| Package | Purpose |
|---|---|
| `Piccolo.Forge.Avalonia` | The controls. Pulls in `Model` and `Mvvm`. |
| `Piccolo.Forge.Themes.ExpUi` | The expUI look. Reference this **or** Islands. |
| `Piccolo.Forge.Themes.Islands` | The Islands look. |
| `Piccolo.Forge.Mvvm.CommunityToolkit` | Optional: panel base classes for `[ObservableProperty]`. |
| `Piccolo.Forge.Model` / `Piccolo.Forge.Mvvm` | Model and view-models; referenced transitively. |

Keep all packages on the same version. Targets `net8.0` and `net10.0`, Avalonia 12;
AOT / trimming compatible.

## Getting started

Install one theme in `Application.Styles` (a theme change requires an app restart):

```xml
<Application.Styles>
    <FluentTheme />
    <themes:ForgeExpUiTheme />   <!-- xmlns:themes="using:Piccolo.Forge.Themes.ExpUi" -->
</Application.Styles>
```

Or in code, after reading your settings:

```csharp
ForgeThemeManager.Apply(this, new ForgeIslandsTheme());
ForgeThemeManager.ApplyLocale(this, CultureInfo.CurrentUICulture);   // UI text
```

Build the shell:

```csharp
// Panels: say where they dock; the stripe button position is computed.
var shell = new ToolWindowShellViewModel();
shell.Add(new ProjectPanel(),   ToolWindowAnchor.Left);
shell.Add(new StructurePanel(), ToolWindowAnchor.Left, isSplit: true);
shell.Add(new TerminalPanel(),  ToolWindowAnchor.Bottom);

// Editor area: tabs plus a split tree.
var editor = new EditorAreaViewModel();
editor.Open("readme", "README.md", content: new ReadmeView());
var editorControl = new ForgeEditorControl();
editorControl.Attach(editor);

// The frame ties them together.
var frame = new ForgeControl { EditorContent = editorControl };
frame.Attach(shell);
window.Content = frame;

shell.Open("Project");
```

A panel is any class deriving from `ToolWindowPanel` (or the CommunityToolkit
variant) with an `Id`, `Title`, `Icon` and `Content`. Views are resolved by your own
`ViewLocator` / `DataTemplate`.

Appearance is set once on the frame and inherited by the editor area:

```csharp
frame.Appearance = ForgeAppearance.Default with
{
    Family = ForgeThemeFamily.Islands,   // must match the installed theme package
    HeaderHeight = 32,
};
```

Save and restore the layout with `ToolWindowLayoutSerializer` /
`EditorLayoutSerializer` (same XML shape as IntelliJ's `window.xml`). To translate or
reword any built-in string, redefine the `Forge.Strings.*` key in your own resources.

A complete runnable example is in `samples/Piccolo.Forge.Sample` in the repository.

## Credits

- **IntelliJ Platform** ([JetBrains/intellij-community](https://github.com/JetBrains/intellij-community), Apache-2.0) —
  the tool-window model, layout rules, metrics and the expUI / Islands colours are
  re-implemented from its sources. Forge contains no JetBrains code or icon assets.
- **Dock** ([wieslawsoltes/Dock](https://github.com/wieslawsoltes/Dock), MIT) — the
  transactional tab-move semantics (detach → attach → commit) and the drag lifecycle
  of the editor area follow Dock's design.

See `THIRD-PARTY-NOTICES.md` for the full notices.

## License

MIT.

---

# 中文

Piccolo.Forge 为 Avalonia 桌面程序提供 IntelliJ IDEA / Rider 那套工作台：

- **工具窗口**停在四条边，每条边分上下（左右）两半；侧边是 IntelliJ 新 UI 的图标**条带**，不是标签页。
- **视图模式**：停靠（固定 / 不固定）、取消停靠、浮动、独立窗口，显示与自动隐藏的规则与原版一致。
- **编辑区**：标签页、固定 / 关闭 / 上限淘汰规则、可拖拽重排的二叉分屏树。
- 两套外观分装两个包：经典 **expUI** 与 **Islands**（圆角留缝、浮在"海"上），各带明暗两档。
- 布局存档、右键菜单、「更多」溢出按钮、条带角标、无障碍名称、13 种语言的界面文案。

## 设计思想

布局模型照 IntelliJ 建，而不是通用的停靠树：一个工具窗口只有 *anchor*（四条边之一）
和 *isSplit*（这条边的前半还是后半）两个属性，**条带按钮画在哪里由这两个值算出来**，
与上游 `ToolWindowManager` 同一算法。按钮落位、自动隐藏、「一格只显示一个窗口」的互斥，
无论用户怎么拖都不会走样，靠的就是这一层。

每一个行为、每一个像素值——条带宽度、标题条高度、圆角、主题颜色——都取自 IntelliJ Platform
源码，不是看截图估的。

模型层不依赖 UI，也不绑定 MVVM 框架；视图模型有中立包与 CommunityToolkit.Mvvm 两种。
面板内容是普通 `object`，由你的 `ViewLocator` / `DataTemplate` 解析；开关面板不会重建已有控件。

## 包

| 包 | 用途 |
|---|---|
| `Piccolo.Forge.Avalonia` | 控件本体，自动带上 `Model` 与 `Mvvm` |
| `Piccolo.Forge.Themes.ExpUi` | expUI 外观。与 Islands **二选一** |
| `Piccolo.Forge.Themes.Islands` | Islands 外观 |
| `Piccolo.Forge.Mvvm.CommunityToolkit` | 可选：面板基类，继续用 `[ObservableProperty]` |
| `Piccolo.Forge.Model` / `Piccolo.Forge.Mvvm` | 模型与视图模型，传递引用即可 |

所有包保持同一版本。目标 `net8.0` / `net10.0`，Avalonia 12；支持 AOT 与裁剪。

## 怎么用

在 `Application.Styles` 里装一套主题（换主题需要重启程序）：

```xml
<Application.Styles>
    <FluentTheme />
    <themes:ForgeExpUiTheme />   <!-- xmlns:themes="using:Piccolo.Forge.Themes.ExpUi" -->
</Application.Styles>
```

或在读完自己的配置后用代码装：

```csharp
ForgeThemeManager.Apply(this, new ForgeIslandsTheme());
ForgeThemeManager.ApplyLocale(this, CultureInfo.CurrentUICulture);   // 界面文案
```

搭外壳：

```csharp
// 登记面板：只说停在哪条边；按钮画在哪是算出来的
var shell = new ToolWindowShellViewModel();
shell.Add(new ProjectPanel(),   ToolWindowAnchor.Left);
shell.Add(new StructurePanel(), ToolWindowAnchor.Left, isSplit: true);
shell.Add(new TerminalPanel(),  ToolWindowAnchor.Bottom);

// 编辑区：标签页 + 分屏树
var editor = new EditorAreaViewModel();
editor.Open("readme", "README.md", content: new ReadmeView());
var editorControl = new ForgeEditorControl();
editorControl.Attach(editor);

// 外框把两者接起来
var frame = new ForgeControl { EditorContent = editorControl };
frame.Attach(shell);
window.Content = frame;

shell.Open("Project");
```

面板是任何继承 `ToolWindowPanel`（或 CommunityToolkit 版本）的类，带 `Id`、`Title`、`Icon`、`Content`。

外观在外框上设一次，编辑区自动继承：

```csharp
frame.Appearance = ForgeAppearance.Default with
{
    Family = ForgeThemeFamily.Islands,   // 必须与装的主题包一致
    HeaderHeight = 32,
};
```

布局存取用 `ToolWindowLayoutSerializer` / `EditorLayoutSerializer`（与 IntelliJ 的
`window.xml` 同一形状）。要改任何一句内置文案，在自己的资源里重定义同名 `Forge.Strings.*` 键。

完整可运行的示例在仓库的 `samples/Piccolo.Forge.Sample`。

## 参考与致谢

- **IntelliJ Platform**（[JetBrains/intellij-community](https://github.com/JetBrains/intellij-community)，Apache-2.0）——
  工具窗口模型、布局规则、度量与 expUI / Islands 配色均按其源码重新实现。本库不含 JetBrains 的代码与图标资产。
- **Dock**（[wieslawsoltes/Dock](https://github.com/wieslawsoltes/Dock)，MIT）——
  编辑区标签搬移的事务语义（先 detach、再 attach、最后提交）与拖拽生命周期沿用其设计。

完整声明见 `THIRD-PARTY-NOTICES.md`。

## 许可证

MIT。
