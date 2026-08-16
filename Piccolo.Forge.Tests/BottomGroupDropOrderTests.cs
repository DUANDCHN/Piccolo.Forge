using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// BOTTOM 组是**倒序显示**的，拖放的落点换算容易整个反过来。
/// </summary>
/// <remarks>
/// 倒序出处 <c>AbstractDroppableStripe.kt:63-67 · createButtonLayoutComparator</c>
/// （<c>getOrderForComparator(o2) - getOrderForComparator(o1)</c>）；
/// 校正出处 <c>:250-253 · finishDrop</c> 的 <c>order++</c>。
/// </remarks>
public class BottomGroupDropOrderTests
{
    private const string Pane = WindowInfo.DefaultPaneId;

    /// <summary>建一个 BOTTOM 组，order 0/1/2，**显示次序是 2,1,0**。</summary>
    private static (DesktopLayout Layout, string[] Displayed) NewBottomGroup()
    {
        var layout = new DesktopLayout();
        foreach (var (id, order) in new[] { ("a", 0), ("b", 1), ("c", 2) })
        {
            layout.SetInfo(new WindowInfo { Id = id, PaneId = Pane, Anchor = ToolWindowAnchor.Bottom, Order = order });
        }

        return (layout, Displayed(layout));
    }

    private static string[] Displayed(DesktopLayout layout)
        => StripeGroupLayout
            .Build(
                layout.GetSortedList().Where(x => x.Anchor == ToolWindowAnchor.Bottom),
                ToolWindowAnchor.Bottom,
                useSeparator: false)
            .Where(x => !x.IsSeparator)
            .Select(x => x.Window!.Id)
            .ToArray();

    [Fact]
    public void The_group_really_is_displayed_in_reverse()
        => Assert.Equal(["c", "b", "a"], NewBottomGroup().Displayed);

    /// <summary>
    /// 把**显示在最后**的那个拖到**显示第一位**，结果就该显示在第一位。
    /// </summary>
    /// <remarks>用户实测：拖到第一个，反而跑到了最下面 —— 说明这段换算整个反了。</remarks>
    [Fact]
    public void Dropping_at_the_top_of_the_reversed_group_puts_it_first()
    {
        var (layout, displayed) = NewBottomGroup();
        Assert.Equal(["c", "b", "a"], displayed);

        // 拖 "a"（显示在最后）到显示第一位：排除它之后剩 [c, b]，落在序号 0 之前
        var target = DropAt(layout, dragged: "a", index: 0);
        layout.SetSideToolAndAnchor("a", Pane, target.Anchor, target.Order, target.IsSplit);

        Assert.Equal("a", Displayed(layout)[0]);
    }

    /// <summary>把显示在第一位的拖到末尾，就该显示在最后。</summary>
    [Fact]
    public void Dropping_at_the_bottom_of_the_reversed_group_puts_it_last()
    {
        var (layout, _) = NewBottomGroup();

        // 拖 "c"（显示第一）到末尾：排除后剩 [b, a]，落在序号 2（末尾）
        var target = DropAt(layout, dragged: "c", index: 2);
        layout.SetSideToolAndAnchor("c", Pane, target.Anchor, target.Order, target.IsSplit);

        var displayed = Displayed(layout);
        Assert.Equal("c", displayed[^1]);
    }

    /// <summary>
    /// 把落点序号换算成 <see cref="StripeDropTarget"/>，走的是界面层那条一模一样的路。
    /// </summary>
    private static StripeDropTarget DropAt(DesktopLayout layout, string dragged, int index)
    {
        // 排除被拖项之后的显示序列（对应 AbstractDroppableStripe.kt:601）
        var rest = Displayed(layout).Where(x => x != dragged).ToArray();

        // 序号 → order：取从它起第一个的 order；越界表示接末尾
        var order = index < rest.Length ? layout.GetInfo(rest[index])!.Order : -1;

        var target = StripeDropResolver.Resolve(
            StripeSide.Left, StripeGroup.Bottom, isAfterSeparator: false, order);
        return StripeDropResolver.AdjustForReversedBottomGroup(target);
    }
}
