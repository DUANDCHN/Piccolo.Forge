using Avalonia;
using Avalonia.Controls;

namespace Piccolo.Forge.Avalonia;

/// <summary>展开标记（chevron）画在标题行的哪一侧。</summary>
public enum ForgeChevronPlacement
{
    /// <summary>标题左边（默认）——与树的展开语汇一致：标记在前，内容在后。</summary>
    Left,

    /// <summary>标题右边——标题贴左对齐，标记推到行尾。</summary>
    Right,
}

/// <summary>
/// <see cref="Expander"/> 的可调项。目前只有一件：展开标记画在哪一侧。
/// </summary>
/// <remarks>
/// <para>
/// 为什么做成**附加属性**而不是让宿主自己换模板：换模板意味着把整份标题行的布局、
/// 悬停叠加层、chevron 的旋转与对齐全抄一遍，抄完还要跟着库升级对一遍 ——
/// 而宿主真正要表达的只是"这一片面板的标记摆右边"。
/// </para>
/// <para>
/// ⚠ <b>可继承</b>（<c>inherits: true</c>）：设在任一祖先上即可覆盖其下所有折叠面板。
/// 典型用法是设在装着一列面板的容器上，而不是逐个设。这也是本属性能被
/// 标题行内部那颗 ToggleButton 读到的原因 —— 值是从 <see cref="Expander"/> 继承下去的。
/// </para>
/// </remarks>
public static class ForgeExpander
{
    /// <summary>展开标记画在标题行的哪一侧；不设就是左边。</summary>
    public static readonly AttachedProperty<ForgeChevronPlacement> ChevronPlacementProperty =
        AvaloniaProperty.RegisterAttached<Control, ForgeChevronPlacement>(
            "ChevronPlacement", typeof(ForgeExpander), inherits: true);

    /// <summary>读 <see cref="ChevronPlacementProperty"/>。</summary>
    /// <param name="element">控件。</param>
    /// <returns>标记摆在哪一侧。</returns>
    public static ForgeChevronPlacement GetChevronPlacement(Control element)
        => element.GetValue(ChevronPlacementProperty);

    /// <summary>写 <see cref="ChevronPlacementProperty"/>。</summary>
    /// <param name="element">控件。</param>
    /// <param name="value">标记摆在哪一侧。</param>
    public static void SetChevronPlacement(Control element, ForgeChevronPlacement value)
        => element.SetValue(ChevronPlacementProperty, value);
}
