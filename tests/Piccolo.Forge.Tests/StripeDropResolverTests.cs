using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 拖拽落点的判定。
public class StripeDropResolverTests
{
    private static readonly (int Order, double Size)[] ThreeButtons = [(0, 40), (1, 40), (2, 40)];

    [Theory]
    // AbstractDroppableStripe.kt:374-401 —— 落点距当前累计位置不足半个按钮就插在它前面
    [InlineData(0, 0)]      // 第一个按钮的上半
    [InlineData(19, 0)]
    [InlineData(21, 1)]     // 越过第一个按钮的中线
    [InlineData(59, 1)]
    [InlineData(61, 2)]
    [InlineData(200, -1)]   // 都没命中 → 末尾（:437-442）
    public void Insert_order_uses_the_half_button_rule(double offset, int expected)
        => Assert.Equal(expected, StripeDropResolver.ResolveInsertOrder(offset, ThreeButtons));

    [Fact]
    public void An_empty_group_always_means_the_end()
        => Assert.Equal(-1, StripeDropResolver.ResolveInsertOrder(0, []));

    [Theory]
    // ToolWindowPaneNewButtonManager.kt:228-233 的逆
    [InlineData(StripeSide.Left, StripeGroup.Bottom, false, ToolWindowAnchor.Bottom, false)]
    [InlineData(StripeSide.Right, StripeGroup.Bottom, false, ToolWindowAnchor.Bottom, true)]
    [InlineData(StripeSide.Left, StripeGroup.Top, false, ToolWindowAnchor.Left, false)]
    [InlineData(StripeSide.Right, StripeGroup.Top, false, ToolWindowAnchor.Right, false)]
    public void Side_and_group_translate_back_to_anchor_and_split(
        StripeSide side,
        StripeGroup group,
        bool afterSeparator,
        ToolWindowAnchor expectedAnchor,
        bool expectedSplit)
    {
        var target = StripeDropResolver.Resolve(side, group, afterSeparator, order: 0);

        Assert.Equal(expectedAnchor, target.Anchor);
        Assert.Equal(expectedSplit, target.IsSplit);
    }

    [Fact]
    public void Dropping_after_the_separator_makes_it_a_split_window()
    {
        // AbstractDroppableStripe.kt:463-469 —— 新 UI 竖条带按落点在分隔符哪一侧算 isSplit
        var target = StripeDropResolver.Resolve(StripeSide.Left, StripeGroup.Top, isAfterSeparator: true, order: 1);

        Assert.Equal(ToolWindowAnchor.Left, target.Anchor);
        Assert.True(target.IsSplit);
    }

    [Fact]
    public void Bottom_group_order_is_bumped_by_one()
    {
        // AbstractDroppableStripe.kt:250-253 —— 那一组倒序排，插入位置的含义反了
        var target = new StripeDropTarget(ToolWindowAnchor.Bottom, 2, IsSplit: false);
        Assert.Equal(3, StripeDropResolver.AdjustForReversedBottomGroup(target).Order);

        var left = new StripeDropTarget(ToolWindowAnchor.Left, 2, IsSplit: false);
        Assert.Equal(2, StripeDropResolver.AdjustForReversedBottomGroup(left).Order);
    }

    [Fact]
    public void Dragging_a_bottom_window_to_the_right_stripe_makes_it_split()
    {
        // 走一遍完整链路：默认布局里 Terminal 在左条带下组，拖到右条带下组
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        var target = StripeDropResolver.Resolve(StripeSide.Right, StripeGroup.Bottom, false, order: -1);

        manager.MoveTo("Terminal", target.Anchor, target.Order, target.IsSplit);

        var info = manager.Layout.GetInfo("Terminal")!;
        Assert.True(info.IsSplit);
        Assert.Equal(
            new StripePlacement(StripeSide.Right, StripeGroup.Bottom),
            StripePlacementResolver.Resolve(info.Anchor, info.IsSplit));
    }
}
