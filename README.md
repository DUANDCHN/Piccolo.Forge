# Piccolo.Forge

An [Avalonia](https://avaloniaui.net/) implementation of the layout and visual styles of
JetBrains IDEs, including IntelliJ IDEA and Rider, providing tool windows, docking,
editor tabs and control themes.
[中文说明](#中文说明)

Piccolo.Forge is an independent project, not affiliated with or endorsed by JetBrains.
Upstream implementation references and third-party terms are documented in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

**License:** [MIT](LICENSE) for project-owned code; third-party material retains its own terms.
Previously published packages retain the license included in each package.

## Start here

Use Forge from NuGet; cloning this repository is not required. In your Avalonia project:

```bash
dotnet add package Piccolo.Forge.Avalonia --version 12.1.1.11
dotnet add package Piccolo.Forge.Themes.Islands --version 12.1.1.11
```

Then install `<forge:ForgeTheme Family="Islands" />` in `Application.Styles` and compose
your window with `ForgeControl` and `ForgeEditorControl`. The complete NuGet installation and runnable application are included below in
[使用方法](#使用方法), including project creation, full source files and `dotnet run`. All Forge package references must use the same version.

| Goal | Documentation |
|---|---|
| Install NuGet packages and write a complete application | [Installation and complete code below](#使用方法) |
| Explore the four runnable examples | [Samples](samples/README.md) |
| Integrate panels, themes and persistence | [Host integration](docs/conventions/宿主接入指南.md) |
| Build, test or contribute | [Contributing](CONTRIBUTING.md) |
| Find design notes and reference material | [Documentation index](docs/README.md) |
| Check licenses and upstream sources | [Third-party notices](THIRD-PARTY-NOTICES.md) |
| Report a vulnerability | [Security policy](SECURITY.md) |

The release workflow builds and tests on Windows, Linux and macOS. Native window decoration,
popup positioning and display scaling should also be checked in the target desktop environment.

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
  replaces the default base theme; DataGrid and ColorPicker themes ship as add-on packages.
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

All packages must be referenced at the same version. Targets `net8.0` and `net10.0`;
requires **Avalonia ≥ 12.1.0** (the control themes cover the `TableView` family, which
is new in 12.1). The CommunityToolkit variant requires **CommunityToolkit.Mvvm ≥ 8.2.2**.
Compatible with AOT compilation and trimming.

## Usage

Install the packages from NuGet in your Avalonia project (no source checkout required):

```bash
dotnet add package Piccolo.Forge.Avalonia --version 12.1.1.11
dotnet add package Piccolo.Forge.Themes.Islands --version 12.1.1.11
```

For the expUI snippets below, install `Piccolo.Forge.Themes.ExpUi` at the same version instead.
A complete application, including its project file, entry point and window code, is included
in this README under [使用方法](#使用方法).

Install one theme entry in `Application.Styles` — pick the layer you need
(`xmlns:forge="using:Piccolo.Forge.Avalonia"`). Changing the family requires a restart.

```xml
<Application.Styles>
    <forge:ForgeTheme Family="ExpUi" />           <!-- everything: controls + shell -->
</Application.Styles>
```

```xml
<forge:ForgeControlsTheme Family="ExpUi" />       <!-- controls only: a full base theme,
                                                       no docking — a base theme on its own -->
```

```xml
<FluentTheme />                                   <!-- your own base theme, first -->
<forge:ForgeShellTheme Family="ExpUi" />          <!-- shell only, on top of it -->
```

If you use DataGrid or ColorPicker, add the matching add-on package and install its entry
alongside the theme entry — the theme supplies the colour keys, and **without it** those
controls render but stay uncoloured, with no error. Their relative order does not matter
(colour keys are resolved via `DynamicResource`, and the two sets of implicit control
themes target disjoint types). The add-ons have no `Family` — templates are shared between
families and the colours follow whatever family the theme entry selected:

```xml
<Application.Styles>
    <forge:ForgeTheme Family="ExpUi" />
    <forgedg:ForgeDataGridTheme />    <!-- xmlns:forgedg="using:Piccolo.Forge.Avalonia.DataGrid" -->
    <forgecpk:ForgeColorPickerTheme /><!-- xmlns:forgecpk="using:Piccolo.Forge.Avalonia.ColorPicker" -->
</Application.Styles>
```

Hosts that ship both families reference both resource packages and pick the family from
configuration. Two equivalent ways to install the entry — use one, not both:

**In XAML** — `{x:Static}` reads the configuration when the XAML loads at startup
(one-time; not a live binding):

```xml
<Application.Styles>
    <forge:ForgeTheme Family="{x:Static local:Settings.Family}" />
</Application.Styles>
```

**In code** — for hosts that need logic around it (read settings first, migrate old
values, etc.). Call it in `Application.Initialize`, before any window is created:

```csharp
public override void Initialize()
{
    AvaloniaXamlLoader.Load(this);
    ForgeThemeManager.Apply(this, new ForgeTheme { Family = settings.Family });
}
```

Either way, changing the family requires a restart. `ForgeThemeManager.ApplyLocale(this,
CultureInfo.CurrentUICulture)` installs the user-interface strings; it is independent of
the theme and works with both approaches.

Compose the shell. Building the view models is the same either way:

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

shell.Open("Project");
```

Then connect them to the controls — **two equivalent ways, pick one**:

**In XAML, via bindings** (`Shell` and `Editor` are styled properties; this is the
counterpart of Dock's `<DockControl Layout="{Binding Layout}"/>` — see the
`Sample.Mvvm` sample for the full pattern):

```xml
<forge:ForgeControl Shell="{Binding Shell}">
    <forge:ForgeControl.EditorContent>
        <forge:ForgeEditorControl Editor="{Binding Editor}" />
    </forge:ForgeControl.EditorContent>
</forge:ForgeControl>
```

**In code, via `Attach`** (equivalent to setting the properties):

```csharp
var editorControl = new ForgeEditorControl();
editorControl.Attach(editor);

var frame = new ForgeControl { EditorContent = editorControl };
frame.Attach(shell);
window.Content = frame;
```

A panel is any class derived from `ToolWindowPanel` (or its CommunityToolkit variant)
that provides `Id`, `Title` and `Icon`. The panel object itself becomes the content,
resolved by a host data template; the `Content` property belongs to the registered `ToolWindowViewModel`.

Appearance is configured once on the frame and inherited by the editor area:

```csharp
frame.Appearance = ForgeAppearance.Default with
{
    Family = ForgeThemeFamily.Islands,   // must match the Family on the theme entry
    HeaderHeight = 32,
};
```

### Layout persistence

Panel positions, widths, open states and the editor split tree survive a restart with
`ForgeLayoutSerializer` (the counterpart of Dock's `DockSerializer`). The archive stores
**how things are laid out**; **what exists** always comes from your `Add` calls — that is
why a layout file from an older version of your app can never break the UI.

The complete wiring, in the application entry point:

```csharp
public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        var vm = new MainViewModel();               // all panels are Add-ed in the ctor

        // Restore AFTER all Add calls. A missing, corrupt or foreign file returns
        // false instead of throwing — fall back to your default layout and move on.
        if (!ForgeLayoutSerializer.TryRead(LayoutPath, vm.Shell, vm.Editor, CreateContent))
            vm.OpenDefaultLayout();

        // Save on exit. ShutdownRequested covers normal close; it does NOT cover a
        // killed process — offer a "save layout" action too if that matters to you.
        desktop.ShutdownRequested += (_, _) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath)!);
            ForgeLayoutSerializer.Write(vm.Shell, vm.Editor).Save(LayoutPath);
        };

        desktop.MainWindow = new MainWindow { DataContext = vm };
    }
    base.OnFrameworkInitializationCompleted();
}

private static string LayoutPath => Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "YourApp", "layout.xml");
```

Editor tabs are restored by identity: the archive holds tab ids and the split tree,
the **content** is yours — rebuild it in the factory. Return `null` for an id you no
longer recognise and that tab is dropped (and reported back), never failing the restore:

```csharp
private static object? CreateContent(string id) => id switch
{
    "readme" => new ReadmePanelViewModel(),
    var file when File.Exists(file) => OpenDocument(file),
    _ => null,   // unknown → drop this tab, keep everything else
};
```

Three rules, all enforced by the API shape rather than by convention:

- **Restore after every `Add`.** Restoring earlier does not throw — archive ids simply
  match nothing and are ignored (the classic "restore did nothing" symptom).
- **`TryRead` returning `false` is the only failure mode** — first start, corrupt file
  and foreign file all take the same exit. Never let an archive block startup.
- **Version upgrades are absorbed**: ids in the archive that no longer exist are
  ignored; new panels missing from the archive keep their default placement.

To embed the layout into your own settings file instead of a standalone one, use the
`XElement` overloads (`Write(...)` returns the element, `TryRead(element, ...)` reads it).
The per-part serializers (`ToolWindowLayoutSerializer`, `EditorLayoutSerializer`) remain
public for hosts that store the two halves separately. The XML structure is modelled on
IntelliJ's `window.xml` and only writes non-default values, so archives stay small and
human-readable. A full working example is `samples/Piccolo.Forge.Sample.Mvvm`.

Any
built-in string can be replaced by redefining the corresponding `Forge.Strings.*`
resource key in the application resources.

Four runnable samples are provided in `samples/`: `Piccolo.Forge.Sample` (the control catalog),
`Piccolo.Forge.Sample.ExpUi` and `.Islands` (the shell layout, one family each), and
`Piccolo.Forge.Sample.Mvvm` (view-model composition and persistence).
`Piccolo.Forge.Sample.Shared` is a content library, not an executable.

## Acknowledgements

- **IntelliJ Platform** ([JetBrains/intellij-community](https://github.com/JetBrains/intellij-community), Apache License 2.0).
  The tool-window model, layout rules, metrics and the expUI / Islands colour schemes were
  developed with reference to its sources. Generated palette data retains the upstream attribution
  and Apache-2.0 terms; see the file mapping in the third-party notices. Forge is not affiliated
  with or endorsed by JetBrains.
- **Dock** ([wieslawsoltes/Dock](https://github.com/wieslawsoltes/Dock), MIT License).
  The tab-move semantics of the editor area (detach, attach, commit) and its drag lifecycle
  were developed with reference to Dock.
- **Semi.Avalonia** ([irihitech/Semi.Avalonia](https://github.com/irihitech/Semi.Avalonia), MIT License).
  The locale mechanism (`ApplyLocale`), the add-on package layout (DataGrid / ColorPicker as
  separate packages) and a number of control-templating framework facts were developed with
  reference to Semi.Avalonia. Piccolo.Forge contains no Semi source code; visual structure
  and colours follow the IntelliJ Platform, not Semi.

Full notices are provided in `THIRD-PARTY-NOTICES.md`.

## License

Project-owned code is licensed under the [MIT License](LICENSE). Third-party material retains
its original terms; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and `licenses/`.
This change does not alter the license of packages already published before it.

---

# 中文说明

导航：[安装与使用](#使用方法) · [文档目录](docs/README.md) · [源码示例](samples/README.md) ·
[参与开发](CONTRIBUTING.md) · [第三方来源](THIRD-PARTY-NOTICES.md) · [安全反馈](SECURITY.md)。

Piccolo.Forge 是 JetBrains IDE（包括 IntelliJ IDEA、Rider）布局与视觉风格的 Avalonia 实现，
提供工具窗口、停靠、编辑器标签页和控件主题。
这是独立项目，与 JetBrains 无隶属或背书关系；源码参考依据及第三方条款见
[第三方声明](THIRD-PARTY-NOTICES.md)。

主要能力：

- **工具窗口**停靠于窗口四边，每边划分为两个半区，以 IntelliJ 新版界面的竖向图标**条带**呈现。
- **视图模式**：停靠（固定或不固定）、取消停靠、浮动、独立窗口，显示与自动隐藏规则参考 IntelliJ Platform。
- **编辑区**：标签页、固定 / 关闭 / 数量上限策略，以及可通过拖拽标签重排的二叉分屏树。
- **完整的控件主题**：Avalonia 全部 89 个具体控件（按钮、输入、列表、树、菜单、选择器…）
  的 IntelliJ 视觉语言实现，可作为完整基础主题独立使用（无需再装其它基础主题）；
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

所有包须引用同一版本。目标框架 `net8.0` 与 `net10.0`；要求 **Avalonia ≥ 12.1.0**
（控件主题覆盖 `TableView` 一族，12.1 新增）。CommunityToolkit 变体要求
**CommunityToolkit.Mvvm ≥ 8.2.2**。兼容 AOT 编译与裁剪。

## 使用方法

本教程从空目录创建一个可运行的 Avalonia 应用，**不需要克隆 Forge 仓库，不使用 ProjectReference**。
已有 Avalonia 应用可从第 2 步开始，将主题和窗口内容整合到自己的工程。

### 1. 新建应用

安装 .NET 10 SDK，在终端运行：

```bash
dotnet new console -n ForgeDemo --framework net10.0
cd ForgeDemo
```

这里用空的 .NET 工程手动添加 Avalonia，不需要另外安装 Avalonia 项目模板。

### 2. 安装 NuGet 包

在 `ForgeDemo.csproj` 所在目录执行：

```bash
dotnet add package Avalonia --version 12.1.0
dotnet add package Avalonia.Desktop --version 12.1.0
dotnet add package Piccolo.Forge.Avalonia --version 12.1.1.11
dotnet add package Piccolo.Forge.Themes.Islands --version 12.1.1.11
```

本例固定使用一组已发布、兼容的版本，便于复现。所有 `Piccolo.Forge.*` 包使用相同版本；
这里选择 Islands 风格，只需安装 Islands 资源包。`Model` 和 `Mvvm` 由传递依赖带入。

将工程输出类型改为 `WinExe`；完成后的 `ForgeDemo.csproj` 如下：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <!-- Avalonia 与桌面平台启动入口。 -->
    <PackageReference Include="Avalonia" Version="12.1.0" />
    <PackageReference Include="Avalonia.Desktop" Version="12.1.0" />
    <!-- Forge 主包与所选主题资源使用同一个版本。 -->
    <PackageReference Include="Piccolo.Forge.Avalonia" Version="12.1.1.11" />
    <PackageReference Include="Piccolo.Forge.Themes.Islands" Version="12.1.1.11" />
  </ItemGroup>
</Project>
```

包许可以所安装版本内的 LICENSE 为准。12.1.1.11 的包仍带原有许可，当前仓库源码的 MIT
变更不追溯修改旧包；此教程也不代表发布了一个新的 MIT 包版本。

### 3. 配置程序入口和主题

用以下完整内容替换 `Program.cs`：

```csharp
using Avalonia;

namespace ForgeDemo;

internal static class Program
{
    // 桌面 UI 使用 STA 主线程，并按系统选择 Avalonia 平台后端。
    [STAThread]
    public static void Main(string[] args) =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
}
```

新建 `App.axaml`：

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:forge="using:Piccolo.Forge.Avalonia"
             x:Class="ForgeDemo.App">
  <Application.Styles>
    <!-- ForgeTheme 已包含基础控件和外壳主题，不再同时安装 FluentTheme。 -->
    <forge:ForgeTheme Family="Islands" />
  </Application.Styles>
</Application>
```

新建 `App.axaml.cs`：

```csharp
using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Piccolo.Forge.Avalonia;

namespace ForgeDemo;

public partial class App : Application
{
    public override void Initialize()
    {
        // 加载主题，再安装随系统语言选择的菜单与面板文案。
        AvaloniaXamlLoader.Load(this);
        ForgeThemeManager.ApplyLocale(this, CultureInfo.CurrentUICulture);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 由桌面生命周期管理主窗口。
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
```

### 4. 创建工具面板和编辑器标签页

新建 `MainWindow.cs`。它是完整的代码窗口，不需要 `MainWindow.axaml`：

```csharp
using Avalonia.Controls;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;

namespace ForgeDemo;

public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Forge NuGet Demo";
        Width = 1000;
        Height = 700;

        // 固定 Id 用于代码和布局存档；标题是显示给用户的名称。
        var shell = new ToolWindowShellViewModel();
        shell.Add("Project", "项目", ToolWindowAnchor.Left,
            contentFactory: () => new TextBlock { Text = "这里放项目树或其他面板内容。" });
        shell.Add("Log", "日志", ToolWindowAnchor.Bottom,
            contentFactory: () => new TextBlock { Text = "应用已启动。" });
        shell.Open("Project");

        // 中间编辑区通过标签页承载内容，内容可以是控件或由宿主解析的 VM。
        var editor = new EditorAreaViewModel();
        editor.Open("welcome", "欢迎", new TextBlock { Text = "这是编辑器标签页。" });
        var editorControl = new ForgeEditorControl();
        editorControl.Attach(editor);

        // 外壳外观与 App.axaml 安装的主题族保持一致。
        var frame = new ForgeControl
        {
            Appearance = ForgeAppearance.Default with { Family = ForgeThemeFamily.Islands },
            EditorContent = editorControl,
        };
        frame.Attach(shell);
        Content = frame;
    }
}
```

项目现在包含：

```text
ForgeDemo/
  ForgeDemo.csproj
  Program.cs
  App.axaml
  App.axaml.cs
  MainWindow.cs
```

### 5. 运行

```bash
dotnet run
```

启动后应出现左侧「项目」工具窗口、中间「欢迎」标签页，以及侧栏「日志」入口。
可以点击侧栏开关面板，通过面板菜单尝试浮动与独立窗口，拖动面板改变停靠位置。

### 已有项目如何选择

- 完整控件与外壳：安装主包和资源族，使用 `ForgeTheme`，如本例。
- 只要控件主题：使用 `ForgeControlsTheme`，不需要创建 `ForgeControl`。
- 保留已有基础主题、只用外壳：使用 `ForgeShellTheme`，安装在已有基础主题之后。
- 改用经典风格：安装同版本 `Piccolo.Forge.Themes.ExpUi`，将主题和外观的 `Family` 一起改为 `ExpUi`。
- DataGrid/ColorPicker：另装同版本的主题附加包，并在 `Application.Styles` 安装对应主题入口。

主题族切换需重启；明暗由 Avalonia 的主题变体控制。布局持久化及宿主职责见
[接入指南](docs/conventions/宿主接入指南.md)和 [本页布局持久化](#布局持久化)。

### 布局持久化

面板位置、宽窄、开合与编辑区分屏树用 `ForgeLayoutSerializer` 存读（对位 Dock 的
`DockSerializer`）。存档只描述**摆成什么样**；**有什么**永远以宿主的 `Add` 为准 ——
所以旧版本应用留下的存档不可能弄坏界面。

完整接线（应用入口处）：

```csharp
public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        var vm = new MainViewModel();               // 全部面板在构造里 Add 完

        // 恢复必须在**所有 Add 之后**。没有存档、存档坏掉、别家的文件都返回
        // false 而不抛 —— 走你的默认摆放即可，存档出任何问题都不该挡住启动。
        if (!ForgeLayoutSerializer.TryRead(LayoutPath, vm.Shell, vm.Editor, CreateContent))
            vm.OpenDefaultLayout();

        // 退出时存。ShutdownRequested 覆盖正常关闭；**覆盖不了进程被杀** ——
        // 在意就再提供一个"保存布局"入口，调的是同一个 Write。
        desktop.ShutdownRequested += (_, _) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath)!);
            ForgeLayoutSerializer.Write(vm.Shell, vm.Editor).Save(LayoutPath);
        };

        desktop.MainWindow = new MainWindow { DataContext = vm };
    }
    base.OnFrameworkInitializationCompleted();
}

private static string LayoutPath => Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "你的应用名", "layout.xml");
```

编辑区按身份恢复：存档里只有标签 id 与分屏树，**内容是宿主的** —— 在 factory 里按 id
重建。认不出的 id 返回 `null`，那一页被丢掉并报回，绝不让整个恢复失败：

```csharp
private static object? CreateContent(string id) => id switch
{
    "readme" => new ReadmePanelViewModel(),
    var file when File.Exists(file) => OpenDocument(file),
    _ => null,   // 认不出 → 丢这一页，其余照常
};
```

三条规矩，都由 API 的形状保证而不是靠自觉：

- **恢复在所有 `Add` 之后。** 调早了不报错 —— 存档里的 id 一个都对不上、全被忽略
  （"恢复了但什么都没变"这个经典症状的来源）。
- **`TryRead` 返回 `false` 是唯一的失败形态** —— 首次启动、坏档、别家文件同一个出口。
- **版本升级被吸收**：存档里已删面板的 id 被忽略；存档里没有的新面板保持默认摆放。

要把布局埋进自己的设置文件而不单独落盘，用 `XElement` 重载（`Write(...)` 返回元素、
`TryRead(element, ...)` 读它）。分块的 `ToolWindowLayoutSerializer` /
`EditorLayoutSerializer` 照旧公开，给两块分开存的宿主。XML 结构参考 IntelliJ 的
`window.xml` 且只写非默认值，存档小而可读。完整可运行的例子见
`samples/Piccolo.Forge.Sample.Mvvm`。

任何内置文案均可通过在应用程序资源中重定义对应的 `Forge.Strings.*` 资源键予以替换。

示例见仓库 `samples/`：`Piccolo.Forge.Sample`（控件目录）、
`Piccolo.Forge.Sample.ExpUi` 与 `.Islands`（外壳布局，各演示一族），以及
`Piccolo.Forge.Sample.Mvvm`（MVVM 接入与布局持久化）。`.Shared` 是内容类库，不可单独运行。

## 致谢

- **IntelliJ Platform**（[JetBrains/intellij-community](https://github.com/JetBrains/intellij-community)，Apache License 2.0）。
  工具窗口模型、布局规则、度量以及 expUI / Islands 配色方案参考其源码实现。
  生成色板数据保留上游署名与 Apache-2.0 条款，具体范围见第三方声明。Forge 与 JetBrains 无隶属或背书关系。
- **Dock**（[wieslawsoltes/Dock](https://github.com/wieslawsoltes/Dock)，MIT License）。
  编辑区标签搬移的语义（分离、附加、提交）与拖拽生命周期参考 Dock 实现。
- **Semi.Avalonia**（[irihitech/Semi.Avalonia](https://github.com/irihitech/Semi.Avalonia)，MIT License）。
  语言文案机制（`ApplyLocale`）、附加包拆分惯例（DataGrid / ColorPicker 独立成包）
  及若干控件模板化的框架事实参考 Semi.Avalonia。Piccolo.Forge 不包含 Semi 的源代码；
  视觉结构与颜色以 IntelliJ Platform 为依据，而非 Semi。

完整声明见 `THIRD-PARTY-NOTICES.md`。

## 许可证

项目自有代码采用 [MIT 许可证](LICENSE)，允许商用、修改及再分发，分发时需保留版权和许可声明。
第三方内容仍遵循其原有许可证，详见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) 与 `licenses/`。
此次变更不追溯修改已发布包附带的许可。
