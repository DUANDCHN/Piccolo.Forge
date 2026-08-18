using Avalonia;
using Avalonia.Controls;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 面板标题条模板里那个「放标签条与按钮」的槽。
/// </summary>
/// <remarks>
/// <b>为什么要它</b>（REQ-067）：标题条自己管着一堆子控件（内容标签条 + 停靠/选项/收起
/// 三个按钮 + 宿主追加的按钮），而 <c>ApplyTemplate</c> 会把 <c>VisualChildren</c> 清空 ——
/// 挂在控件身上就每套一次模板被扫一次。这正是早先把这个控件挡在模板之外的理由。
/// 改成"子控件住在模板产出的这个槽里"，那条就不成立了。
/// <para>
/// <b>布局算式一个数都没动</b>（需求的「不做」边界）：槽不自己算，
/// 而是<b>回头问 <see cref="StyledElement.TemplatedParent"/></b> ——
/// 标题条那套「按钮靠右、标签排在标题与按钮之间」的算式仍写在标题条里，
/// 只是改由槽来触发。这与 <see cref="ForgeIconHost"/> 问按钮要图标位置是同一手法：
/// XAML 里传不了方法组，能传的只有"你去问你爹"。
/// </para>
/// <para>
/// <b>公开的</b>：它出现在 <c>ControlTheme</c> 的模板里（XAML 只认得到公开类型）。宿主不该直接 new 它。
/// </para>
/// </remarks>
[ForgeThemeless("标题条里的**槽**：只回头问标题条要布局，不画、不取色。")]
public sealed class ForgeHeaderContentHost : Panel
{
    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
        => TemplatedParent is ForgeToolWindowHeader header
            ? header.MeasureContent(availableSize)
            : base.MeasureOverride(availableSize);

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
        => TemplatedParent is ForgeToolWindowHeader header
            ? header.ArrangeContent(finalSize)
            : base.ArrangeOverride(finalSize);
}
