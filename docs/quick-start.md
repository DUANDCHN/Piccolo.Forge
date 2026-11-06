# 快速开始：通过 NuGet 创建应用

本教程从空目录创建一个可运行的 Avalonia 应用，**不需要克隆 Forge 仓库，不使用 ProjectReference**。
已有 Avalonia 应用可从第 2 步开始，将主题和窗口内容整合到自己的工程。

## 1. 新建应用

安装 .NET 10 SDK，在终端运行：

```bash
dotnet new console -n ForgeDemo --framework net10.0
cd ForgeDemo
```

这里用空的 .NET 工程手动添加 Avalonia，不需要另外安装 Avalonia 项目模板。

## 2. 安装 NuGet 包

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

## 3. 配置程序入口和主题

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

## 4. 创建工具面板和编辑器标签页

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

## 5. 运行

```bash
dotnet run
```

启动后应出现左侧「项目」工具窗口、中间「欢迎」标签页，以及侧栏「日志」入口。
可以点击侧栏开关面板，通过面板菜单尝试浮动与独立窗口，拖动面板改变停靠位置。

## 已有项目如何选择

- 完整控件与外壳：安装主包和资源族，使用 `ForgeTheme`，如本例。
- 只要控件主题：使用 `ForgeControlsTheme`，不需要创建 `ForgeControl`。
- 保留已有基础主题、只用外壳：使用 `ForgeShellTheme`，安装在已有基础主题之后。
- 改用经典风格：安装同版本 `Piccolo.Forge.Themes.ExpUi`，将主题和外观的 `Family` 一起改为 `ExpUi`。
- DataGrid/ColorPicker：另装同版本的主题附加包，并在 `Application.Styles` 安装对应主题入口。

主题族切换需重启；明暗由 Avalonia 的主题变体控制。布局持久化及宿主职责见
[接入指南](conventions/宿主接入指南.md)和 [README](../README.md#layout-persistence)。

## 进一步学习与源码示例

以上教程全部通过 NuGet 接入。想研究 MVVM、视图定位与完整示例时，再克隆仓库并阅读
[示例说明](../samples/README.md)；参与库本身开发见 [贡献指南](../CONTRIBUTING.md)。
