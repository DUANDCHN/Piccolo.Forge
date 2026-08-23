using Avalonia.Styling;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 一套 Forge 主题（一个 Styles 单元）自报属于哪一族，<see cref="ForgeThemeManager"/> 据此识别装了哪套。
/// 三个入口（ForgeTheme / ForgeControlsTheme / ForgeShellTheme）都实现它，族从 Family 属性来。
/// </summary>
public interface IForgeTheme : IStyle
{
    /// <summary>这套主题的族。</summary>
    ForgeThemeFamily Family { get; }
}
