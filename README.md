# Piccolo.Forge

Tool windows, docking and editor tabs for [Avalonia](https://avaloniaui.net/), with a layout similar to the IntelliJ Platform.
[中文说明](#中文说明)

## Overview

Piccolo.Forge provides an Avalonia desktop application with a workbench layout similar
to that of IntelliJ IDEA and Rider:

- **Tool windows** anchored to the four edges of the window, each edge divided into two
  halves, presented through the vertical icon *stripes* introduced by the IntelliJ new UI.
- **View modes** — docked (pinned or unpinned), undocked, floating and separate window —
  with visibility and auto-hide rules modelled on the IntelliJ Platform.
- An **editor area** with tabs, pin / close / tab-limit policies, and a binary split tree
  that can be rearranged by dragging tabs.
- A **complete control theme** — all 89 concrete Avalonia controls (buttons, inputs, lists,
  trees, menus, pickers…) in the IntelliJ visual language, usable as a full base theme that
  replaces Fluent or Semi; DataGrid and ColorPicker themes ship as add-on packages.
- Two visual families, **expUI** and **Islands**, each with light and dark variants; the
  family is selected by a single `Family` property.
- Layout persistence, context menus, an overflow ("More") button, stripe-button badges,
  accessibility names, and user-interface text in 13 languages.

## Design

The layout model is based on the IntelliJ Platform's tool-window model rather than on a
generic docking tree. A tool window is described by an *anchor* (Left, Right, Top,
Bottom) and an *isSplit* flag (first or second half of that edge); the position of its
stripe button is derived from these two values. Because button placement, auto-hide
behaviour and the "one visible window per slot" rule are consequences of this model,
they remain consistent under any sequence of user operations.

Behaviour and metrics — stripe widths, header heights, corner radii, theme colours —
were determined with reference to the IntelliJ Platform sources.

The model layer has no UI dependency and no MVVM framework dependency. View models are
available in a framework-neutral package and in a CommunityToolkit.Mvvm variant. Panel
content is an ordinary `object` resolved by the host's own `ViewLocator` or
`DataTemplate`; opening and closing panels never recreates existing controls.

## Packages

| Package | Purpose |
|---|---|
| `Piccolo.Forge.Avalonia` | Shell controls, control templates and the three theme entry classes. |
| `Piccolo.Forge.Themes.ExpUi` | expUI resources (palettes + shell templates). Resource-only. |
| `Piccolo.Forge.Themes.Islands` | Islands resources. Reference one family or both. |
| `Piccolo.Forge.Avalonia.DataGrid` | DataGrid theme add-on (only if you use DataGrid). |
| `Piccolo.Forge.Avalonia.ColorPicker` | ColorPicker theme add-on. |
| `Piccolo.Forge.Mvvm.CommunityToolkit` | Optional panel base classes for CommunityToolkit.Mvvm. |
| `Piccolo.Forge.Model`, `Piccolo.Forge.Mvvm` | Model and view models; referenced transitively. |

**Packages define what is available; the `Family` property picks what is used.** The theme
entry classes live in `Piccolo.Forge.Avalonia`; family resources are resolved at runtime,
so selecting a family whose package is not referenced fails at startup with a clear message.

All packages must be referenced at the same version. Targets `net8.0` and `net10.0`
with Avalonia 12; compatible with AOT compilation and trimming.

## Usage

Install one theme entry in `Application.Styles` — pick the layer you need
(`xmlns:forge="using:Piccolo.Forge.Avalonia"`). Changing the family requires a restart.

```xml
<Application.Styles>
    <forge:ForgeTheme Family="ExpUi" />           <!-- everything: controls + shell -->
</Application.Styles>
```

```xml
<forge:ForgeControlsTheme Family="ExpUi" />       <!-- controls only: a full base theme,
                                                       no docking — replaces Fluent/Semi -->
```

```xml
<FluentTheme />                                   <!-- your own base theme, first -->
<forge:ForgeShellTheme Family="ExpUi" />          <!-- shell only, on top of it -->
```

Hosts that ship both families reference both resource packages and bind the property to
configuration — evaluated once at startup: `Family="{x:Static local:Settings.Family}"`.
The same entry can also be installed in code after settings have been read:

```csharp
ForgeThemeManager.Apply(this, new ForgeTheme { Family = settings.Family });
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
    Family = ForgeThemeFamily.Islands,   // must match the Family on the theme entry
    HeaderHeight = 32,
};
```

Layouts are saved and restored with `ToolWindowLayoutSerializer` and
`EditorLayoutSerializer`, using an XML structure modelled on IntelliJ's `window.xml`. Any
built-in string can be replaced by redefining the corresponding `Forge.Strings.*`
resource key in the application resources.

Three samples are provided in `samples/`: `Piccolo.Forge.Sample` (the control catalog),
`Piccolo.Forge.Sample.ExpUi` and `.Islands` (the shell layout, one family each).

## Acknowledgements

- **IntelliJ Platform** ([JetBrains/intellij-community](https://github.com/JetBrains/intellij-community), Apache License 2.0).
  The tool-window model, layout rules, metrics and the expUI / Islands colour schemes were
  developed with reference to its sources. Piccolo.Forge contains no JetBrains source code or icon assets.
- **Dock** ([wieslawsoltes/Dock](https://github.com/wieslawsoltes/Dock), MIT License).
  The tab-move semantics of the editor area (detach, attach, commit) and its drag lifecycle
  were developed with reference to Dock.

Full notices are provided in `THIRD-PARTY-NOTICES.md`.

## License

Proprietary. The packages may be referenced and redistributed in binary form as part of an
application; the source code is not open source. See the `LICENSE` file in the package.

---

# 中文说明

Piccolo.Forge 为 Avalonia 桌面应用程序提供与 IntelliJ IDEA、Rider 类似的工作台布局：

- **工具窗口**停靠于窗口四边，每边划分为两个半区，以 IntelliJ 新版界面的竖向图标**条带**呈现。
- **视图模式**：停靠（固定或不固定）、取消停靠、浮动、独立窗口，显示与自动隐藏规则参考 IntelliJ Platform。
- **编辑区**：标签页、固定 / 关闭 / 数量上限策略，以及可通过拖拽标签重排的二叉分屏树。
- **完整的控件主题**：Avalonia 全部 89 个具体控件（按钮、输入、列表、树、菜单、选择器…）
  的 IntelliJ 视觉语言实现，可作为完整基础主题直接替代 Fluent / Semi；
  DataGrid 与 ColorPicker 的主题以附加包提供。
- 两种视觉风格：**expUI** 与 **Islands**，各含明、暗两种变体；族由一个 `Family` 属性选定。
- 布局持久化、上下文菜单、溢出（「更多」）按钮、条带按钮角标、无障碍名称，以及 13 种语言的界面文案。

## 设计

布局模型参考 IntelliJ Platform 的工具窗口模型，而非通用的停靠树。一个工具窗口由 *anchor*（左、右、上、下）与
*isSplit*（该边的前半区或后半区）两个属性描述，其条带按钮的位置由这两个值推导得出。按钮落位、
自动隐藏以及「每个槽位仅显示一个窗口」的约束均为该模型的推论，因此在任意用户操作序列下均保持一致。

行为与度量——条带宽度、标题条高度、圆角半径、主题颜色——参考 IntelliJ Platform 源码确定。

模型层不依赖 UI，也不依赖任何 MVVM 框架；视图模型提供框架中立版本与 CommunityToolkit.Mvvm 版本。
面板内容为普通 `object`，由宿主自身的 `ViewLocator` 或 `DataTemplate` 解析；打开或关闭面板不会重建已有控件。

## 包

| 包 | 用途 |
|---|---|
| `Piccolo.Forge.Avalonia` | 外壳控件、控件模板与三个主题入口类。 |
| `Piccolo.Forge.Themes.ExpUi` | expUI 资源（色板 + 外壳模板）。纯资源包。 |
| `Piccolo.Forge.Themes.Islands` | Islands 资源。按需引一族或两族。 |
| `Piccolo.Forge.Avalonia.DataGrid` | DataGrid 主题附加包（用到才引）。 |
| `Piccolo.Forge.Avalonia.ColorPicker` | ColorPicker 主题附加包。 |
| `Piccolo.Forge.Mvvm.CommunityToolkit` | 可选：面向 CommunityToolkit.Mvvm 的面板基类。 |
| `Piccolo.Forge.Model`、`Piccolo.Forge.Mvvm` | 模型与视图模型，由传递引用引入。 |

**包管"有哪些可用"，`Family` 属性管"用哪个"。** 主题入口类在 `Piccolo.Forge.Avalonia` 里；
族的资源在运行期解析，`Family` 选了没被引用的族，启动即抛，异常写明缺哪个包。

所有包须引用同一版本。目标框架 `net8.0` 与 `net10.0`，Avalonia 12；兼容 AOT 编译与裁剪。

## 使用方法

在 `Application.Styles` 中安装一个主题入口，按需要的层挑
（`xmlns:forge="using:Piccolo.Forge.Avalonia"`）。更换族需重启应用程序。

```xml
<Application.Styles>
    <forge:ForgeTheme Family="ExpUi" />           <!-- 全套：控件 + 外壳 -->
</Application.Styles>
```

```xml
<forge:ForgeControlsTheme Family="ExpUi" />       <!-- 只要控件：完整基础主题，
                                                       不含停靠，直接替代 Fluent / Semi -->
```

```xml
<FluentTheme />                                   <!-- 自己的基础主题在前 -->
<forge:ForgeShellTheme Family="ExpUi" />          <!-- 只要外壳，叠在其上 -->
```

两档都要的宿主两个资源包都引，属性从配置来（启动时求值一次）：
`Family="{x:Static local:Settings.Family}"`。也可在读取配置之后以代码安装同一个入口：

```csharp
ForgeThemeManager.Apply(this, new ForgeTheme { Family = settings.Family });
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
    Family = ForgeThemeFamily.Islands,   // 须与主题入口上的 Family 一致
    HeaderHeight = 32,
};
```

布局通过 `ToolWindowLayoutSerializer` 与 `EditorLayoutSerializer` 保存与恢复，XML 结构参考 IntelliJ 的
`window.xml`。任何内置文案均可通过在应用程序资源中重定义对应的 `Forge.Strings.*` 资源键予以替换。

示例见仓库 `samples/`：`Piccolo.Forge.Sample`（控件目录）、
`Piccolo.Forge.Sample.ExpUi` 与 `.Islands`（外壳布局，各演示一族）。

## 致谢

- **IntelliJ Platform**（[JetBrains/intellij-community](https://github.com/JetBrains/intellij-community)，Apache License 2.0）。
  工具窗口模型、布局规则、度量以及 expUI / Islands 配色方案参考其源码实现。Piccolo.Forge 不包含 JetBrains 的源代码与图标资产。
- **Dock**（[wieslawsoltes/Dock](https://github.com/wieslawsoltes/Dock)，MIT License）。
  编辑区标签搬移的语义（分离、附加、提交）与拖拽生命周期参考 Dock 实现。

完整声明见 `THIRD-PARTY-NOTICES.md`。

## 许可证

专有许可。允许在应用程序中引用并以二进制形式分发；源代码未开源。详见包内 `LICENSE` 文件。
