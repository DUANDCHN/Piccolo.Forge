using System;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Piccolo.Forge.Avalonia.DataGrid;

/// <summary>
/// DataGrid 一族的控件主题（附加包）。
/// </summary>
/// <remarks>
/// 与主题包的入口（<c>ForgeExpUiTheme</c> / <c>ForgeIslandsTheme</c>）**一起**挂进
/// <c>Application.Styles</c>，且主题包在前：色板键由主题包提供，只挂本入口会得到无色的控件。
/// 模板两档共用，故本入口不分档。
/// </remarks>
public sealed class ForgeDataGridTheme : Styles
{
    /// <summary>装载 XAML 里的那份 Styles。</summary>
    public ForgeDataGridTheme(IServiceProvider? serviceProvider = null)
        => AvaloniaXamlLoader.Load(serviceProvider, this);
}
