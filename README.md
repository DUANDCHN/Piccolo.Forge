# Piccolo.Forge

IntelliJ-style tool windows, docking and editor tabs for [Avalonia](https://avaloniaui.net/).
[中文说明](#中文说明)

## Overview

Piccolo.Forge provides an Avalonia desktop application with the workbench layout of
IntelliJ IDEA and Rider:

- **Tool windows** anchored to the four edges of the window, each edge divided into two
  halves, presented through the vertical icon *stripes* introduced by the IntelliJ new UI.
- **View modes** — docked (pinned or unpinned), undocked, floating and separate window —
  with the original visibility and auto-hide rules.
- An **editor area** with tabs, pin / close / tab-limit policies, and a binary split tree
  that can be rearranged by dragging tabs.
- Two visual families delivered as separate packages, **expUI** and **Islands**, each with
  light and dark variants.
- Layout persistence, context menus, an overflow ("More") button, stripe-button badges,
  accessibility names, and user-interface text in 13 languages.

## Design

The layout model follows the IntelliJ Platform rather than a generic docking tree. A tool
window is described by an *anchor* (Left, Right, Top, Bottom) and an *isSplit* flag
(first or second half of that edge). The position of its stripe button is derived from
these two values by the same algorithm as the upstream `ToolWindowManager`. Because
button placement, auto-hide behaviour and the "one visible window per slot" rule are all
consequences of this model, they remain consistent under any sequence of user operations.

All behaviour and all metrics — stripe widths, header heights, corner radii, theme
colours — are taken from the IntelliJ Platform sources.

The model layer has no UI dependency and no MVVM framework dependency. View models are
available in a framework-neutral package and in a CommunityToolkit.Mvvm variant. Panel
content is an ordinary `object` resolved by the host's own `ViewLocator` or
`DataTemplate`; opening and closing panels never recreates existing controls.

## Packages

| Package | Purpose |
|---|---|
| `Piccolo.Forge.Avalonia` | The controls. References `Model` and `Mvvm`. |
| `Piccolo.Forge.Themes.ExpUi` | The expUI visual family. Reference this package or Islands. |
| `Piccolo.Forge.Themes.Islands` | The Islands visual family. |
| `Piccolo.Forge.Mvvm.CommunityToolkit` | Optional panel base classes for CommunityToolkit.Mvvm. |
| `Piccolo.Forge.Model`, `Piccolo.Forge.Mvvm` | Model and view models; referenced transitively. |

All packages must be referenced at the same version. Targets `net8.0` and `net10.0`
with Avalonia 12; compatible with AOT compilation and trimming.

## Usage

Install one theme in `Application.Styles`. Changing the theme requires an application
restart.

```xml
<Application.Styles>
    <FluentTheme />
    <themes:ForgeExpUiTheme />   <!-- xmlns:themes="using:Piccolo.Forge.Themes.ExpUi" -->
</Application.Styles>
```

Alternatively, install the theme in code after the application settings have been read:

```csharp
ForgeThemeManager.Apply(this, new ForgeIslandsTheme());
ForgeThemeManager.ApplyLocale(this, CultureInfo.CurrentUICulture);   // user-interface text
```

Compose the shell:

```csharp
// Register panels. Only the docking position is specified; the stripe button
// position is derived from it.
var shell = new ToolWindowShellViewModel();
shell.Add(new ProjectPanel(),   ToolWindowAnchor.Left);
shell.Add(new StructurePanel(), ToolWindowAnchor.Left, isSplit: true);
shell.Add(new TerminalPanel(),  ToolWindowAnchor.Bottom);

// Editor area: tabs and the split tree.
var editor = new EditorAreaViewModel();
editor.Open("readme", "README.md", content: new ReadmeView());
var editorControl = new ForgeEditorControl();
editorControl.Attach(editor);

// The frame combines both.
var frame = new ForgeControl { EditorContent = editorControl };
frame.Attach(shell);
window.Content = frame;

shell.Open("Project");
```

A panel is any class derived from `ToolWindowPanel` (or its CommunityToolkit variant)
that provides `Id`, `Title`, `Icon` and `Content`.

Appearance is configured once on the frame and inherited by the editor area:

```csharp
frame.Appearance = ForgeAppearance.Default with
{
    Family = ForgeThemeFamily.Islands,   // must match the installed theme package
    HeaderHeight = 32,
};
```

Layouts are saved and restored with `ToolWindowLayoutSerializer` and
`EditorLayoutSerializer`, using the same XML structure as IntelliJ's `window.xml`. Any
built-in string can be replaced by redefining the corresponding `Forge.Strings.*`
resource key in the application resources.

A complete example is provided in `samples/Piccolo.Forge.Sample` in the repository.

## Acknowledgements

- **IntelliJ Platform** ([JetBrains/intellij-community](https://github.com/JetBrains/intellij-community), Apache License 2.0).
  The tool-window model, layout rules, metrics and the expUI / Islands colour schemes are
  re-implemented from its sources. Piccolo.Forge contains no JetBrains source code or icon assets.
- **Dock** ([wieslawsoltes/Dock](https://github.com/wieslawsoltes/Dock), MIT License).
  The transactional tab-move semantics (detach, attach, commit) and the drag lifecycle of
  the editor area follow Dock's design.

Full notices are provided in `THIRD-PARTY-NOTICES.md`.

## License

MIT License.

---

# 中文说明

Piccolo.Forge 为 Avalonia 桌面应用程序提供 IntelliJ IDEA 与 Rider 的工作台布局：

- **工具窗口**停靠于窗口四边，每边划分为两个半区，以 IntelliJ 新版界面的竖向图标**条带**呈现。
- **视图模式**：停靠（固定或不固定）、取消停靠、浮动、独立窗口，显示与自动隐藏规则与原版一致。
- **编辑区**：标签页、固定 / 关闭 / 数量上限策略，以及可通过拖拽标签重排的二叉分屏树。
- 两种视觉风格以独立包提供：**expUI** 与 **Islands**，各含明、暗两种变体。
- 布局持久化、上下文菜单、溢出（「更多」）按钮、条带按钮角标、无障碍名称，以及 13 种语言的界面文案。

## 设计

布局模型遵循 IntelliJ Platform，而非通用的停靠树。一个工具窗口由 *anchor*（左、右、上、下）与
*isSplit*（该边的前半区或后半区）两个属性描述；其条带按钮的位置由这两个值按上游
`ToolWindowManager` 的算法推导得出。按钮落位、自动隐藏以及「每个槽位仅显示一个窗口」的约束
均为该模型的推论，因此在任意用户操作序列下均保持一致。

全部行为与度量——条带宽度、标题条高度、圆角半径、主题颜色——均取自 IntelliJ Platform 源码。

模型层不依赖 UI，也不依赖任何 MVVM 框架；视图模型提供框架中立版本与 CommunityToolkit.Mvvm 版本。
面板内容为普通 `object`，由宿主自身的 `ViewLocator` 或 `DataTemplate` 解析；打开或关闭面板不会重建已有控件。

## 包

| 包 | 用途 |
|---|---|
| `Piccolo.Forge.Avalonia` | 控件。引用 `Model` 与 `Mvvm`。 |
| `Piccolo.Forge.Themes.ExpUi` | expUI 视觉风格。与 Islands 二选一。 |
| `Piccolo.Forge.Themes.Islands` | Islands 视觉风格。 |
| `Piccolo.Forge.Mvvm.CommunityToolkit` | 可选：面向 CommunityToolkit.Mvvm 的面板基类。 |
| `Piccolo.Forge.Model`、`Piccolo.Forge.Mvvm` | 模型与视图模型，由传递引用引入。 |

所有包须引用同一版本。目标框架 `net8.0` 与 `net10.0`，Avalonia 12；兼容 AOT 编译与裁剪。

## 使用方法

在 `Application.Styles` 中安装一种主题。更换主题需重启应用程序。

```xml
<Application.Styles>
    <FluentTheme />
    <themes:ForgeExpUiTheme />   <!-- xmlns:themes="using:Piccolo.Forge.Themes.ExpUi" -->
</Application.Styles>
```

亦可在读取应用程序配置之后以代码安装：

```csharp
ForgeThemeManager.Apply(this, new ForgeIslandsTheme());
ForgeThemeManager.ApplyLocale(this, CultureInfo.CurrentUICulture);   // 界面文案
```

组装外壳：

```csharp
// 注册面板。仅指定停靠位置；条带按钮位置由此推导。
var shell = new ToolWindowShellViewModel();
shell.Add(new ProjectPanel(),   ToolWindowAnchor.Left);
shell.Add(new StructurePanel(), ToolWindowAnchor.Left, isSplit: true);
shell.Add(new TerminalPanel(),  ToolWindowAnchor.Bottom);

// 编辑区：标签页与分屏树。
var editor = new EditorAreaViewModel();
editor.Open("readme", "README.md", content: new ReadmeView());
var editorControl = new ForgeEditorControl();
editorControl.Attach(editor);

// 外框将二者组合。
var frame = new ForgeControl { EditorContent = editorControl };
frame.Attach(shell);
window.Content = frame;

shell.Open("Project");
```

面板为任意派生自 `ToolWindowPanel`（或其 CommunityToolkit 版本）的类，提供 `Id`、`Title`、`Icon` 与 `Content`。

外观在外框上配置一次，由编辑区继承：

```csharp
frame.Appearance = ForgeAppearance.Default with
{
    Family = ForgeThemeFamily.Islands,   // 须与已安装的主题包一致
    HeaderHeight = 32,
};
```

布局通过 `ToolWindowLayoutSerializer` 与 `EditorLayoutSerializer` 保存与恢复，XML 结构与 IntelliJ 的
`window.xml` 相同。任何内置文案均可通过在应用程序资源中重定义对应的 `Forge.Strings.*` 资源键予以替换。

完整示例见仓库中的 `samples/Piccolo.Forge.Sample`。

## 致谢

- **IntelliJ Platform**（[JetBrains/intellij-community](https://github.com/JetBrains/intellij-community)，Apache License 2.0）。
  工具窗口模型、布局规则、度量以及 expUI / Islands 配色方案均依据其源码重新实现。Piccolo.Forge 不包含 JetBrains 的源代码与图标资产。
- **Dock**（[wieslawsoltes/Dock](https://github.com/wieslawsoltes/Dock)，MIT License）。
  编辑区标签搬移的事务语义（分离、附加、提交）与拖拽生命周期遵循 Dock 的设计。

完整声明见 `THIRD-PARTY-NOTICES.md`。

## 许可证

MIT License。
