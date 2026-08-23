using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Themes.Islands;

/// <summary>
/// Forge 的 Islands 主题：全部控件的 ControlTheme + 两套明暗颜色，一个 <see cref="Styles"/> 单元。
/// </summary>
/// <remarks>
/// 用法与 Avalonia 自带的 FluentTheme / SimpleTheme 一样：<c>&lt;themes:ForgeIslandsShellTheme/&gt;</c> 放进
/// <c>Application.Styles</c>，或代码里 <c>ForgeThemeManager.Apply(app, new ForgeIslandsShellTheme())</c>。
/// 引用哪个主题包就是哪套长相；两个包互不依赖，都只依赖 Piccolo.Forge.Avalonia。
/// <para>⚠ 换主题要重启（运行中换 Styles 单元不会让已挂树的控件重取模板）。</para>
/// </remarks>
public sealed class ForgeIslandsShellTheme : Styles, IForgeTheme
{
    /// <summary>装载 XAML 里的那份 Styles。</summary>
    public ForgeIslandsShellTheme() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public ForgeThemeFamily Family => ForgeThemeFamily.Islands;
}
