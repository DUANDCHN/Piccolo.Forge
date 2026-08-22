using Avalonia.Styling;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 一套 Forge 主题（一个 Styles 单元）自报属于哪一族，<see cref="ForgeThemeManager"/> 据此识别装了哪套。
/// 主题包（Piccolo.Forge.Themes.ExpUi / .Islands）各实现一个。
/// </summary>
public interface IForgeTheme : IStyle
{
    /// <summary>这套主题的族。</summary>
    ForgeThemeFamily Family { get; }
}
