# 快速开始

## 先运行示例

安装 .NET 10 SDK 及 .NET 8 runtime。在仓库根目录运行：

```bash
dotnet run --project samples/Piccolo.Forge.Sample.Islands
```

经典主题、MVVM 接入和控件目录见 [示例说明](../samples/README.md)。
普通构建不需要 `third_party/` 或上位机工程。

## 在自己的应用中安装

使用 Avalonia 12.1 或更新的兼容版本，目标框架为 `net8.0` 或 `net10.0`。
安装 `Piccolo.Forge.Avalonia` 和至少一个资源族：

```bash
dotnet add package Piccolo.Forge.Avalonia
dotnet add package Piccolo.Forge.Themes.Islands
```

生产工程应将全部 `Piccolo.Forge.*` 引用固定为同一个已发布版本；不要混用资源包版本。
许可按所安装包内的 LICENSE 为准；当前源码的 MIT 变更不追溯改变旧包。

在 `App.axaml` 声明命名空间并安装一个主题入口：

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:forge="using:Piccolo.Forge.Avalonia"
             x:Class="YourApp.App">
    <Application.Styles>
        <!-- ForgeTheme 提供控件与外壳，不与另一套完整基础主题同时安装。 -->
        <forge:ForgeTheme Family="Islands" />
    </Application.Styles>
</Application>
```

保留应用原有的数据模板、资源和生命周期代码。只需要控件可选择 `ForgeControlsTheme`；
已有基础主题且只需外壳时使用 `ForgeShellTheme`。换主题族需重启，明暗变体可由宿主切换。

## 最小工具窗口

下面的代码可放进宿主窗口构造函数；窗口、内容和视图定位由应用自己管理：

```csharp
using Avalonia.Controls;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;

// 面板身份固定，便于布局保存后恢复。
var shell = new ToolWindowShellViewModel();
shell.Add("Project", "Project", ToolWindowAnchor.Left,
    contentFactory: () => new TextBlock { Text = "Panel content" });
shell.Open("Project");

// 编辑区和面板内容都使用普通控件；真实 MVVM 应用可改用 DataTemplate 解析 VM。
var frame = new ForgeControl
{
    // 外壳外观与 Application 中安装的主题族一致。
    Appearance = ForgeAppearance.Default with { Family = ForgeThemeFamily.Islands },
    EditorContent = new TextBlock { Text = "Editor area" },
};
frame.Attach(shell);
Content = frame;
```

代码中的 `Content = frame` 属于宿主窗口。这里使用内容工厂；完整 MVVM 模式可继承
`Piccolo.Forge.Mvvm.Controls.ToolWindowPanel`，面板对象本身就是内容 VM，再由宿主 DataTemplate 解析。
完整 MVVM 写法可直接参照 [MainViewModel](../samples/Piccolo.Forge.Sample.Mvvm/ViewModels/MainViewModel.cs)。

## 下一步

- 布局保存：先注册全部面板，再恢复布局；见 [README](../README.md#layout-persistence)。
- 图标：由宿主选择图标库；需要多处显示时通过 `IconFactory` 创建独立控件实例。
- DataGrid/ColorPicker：另装对应主题附加包，包版本与主包相同。
- 自定义窗体、AOT 与资源覆盖：见 [宿主接入指南](conventions/宿主接入指南.md)。
