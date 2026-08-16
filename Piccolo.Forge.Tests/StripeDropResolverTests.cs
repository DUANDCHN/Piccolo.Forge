using Avalonia;
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
    [InlineData(0, 0)]
    [InlineData(19, 0)]
    [InlineData(21, 1)]
    [InlineData(61, 2)]
    [InlineData(200, 3)]    // 序号版的"末尾"是长度，不是 -1
    public void Insert_index_is_the_position_not_the_order(double offset, int expected)
        => Assert.Equal(expected, StripeDropResolver.ResolveInsertIndex(offset, [40d, 40d, 40d]));

    /// <summary>
    /// **被拖的那个必须先从序列里拿掉**，否则它后面每一项的累计坐标都多算一个按钮高。
    /// </summary>
    /// <remarks>
    /// 对应 <c>AbstractDroppableStripe.kt:601 · tools.remove(dragButton)</c>。
    /// 回归：本仓原来按含被拖项的原始 bounds 算落点，于是拖到某个位置时
    /// 蓝框提示的位置比真正会落到的位置**偏上一格**（用户实测报的就是这个）。
    /// </remarks>
    [Fact]
    public void Removing_the_dragged_item_shifts_the_later_positions_up()
    {
        // 三个 40 高的按钮，正在拖第一个。排除之后只剩两个。
        double[] withDragged = [40, 40, 40];
        double[] withoutDragged = [40, 40];

        // 落点 50：排除后落在第二个（序号 1）；不排除的话会被算成序号 1 的**另一个**按钮，
        // 两者对应的 order 差一格。这里直接比序号在两种序列下的语义差异。
        Assert.Equal(1, StripeDropResolver.ResolveInsertIndex(50, withoutDragged));
        Assert.Equal(1, StripeDropResolver.ResolveInsertIndex(50, withDragged));

        // 关键在末尾：排除后只有两格，落点 90 已经是末尾（2）；
        // 不排除则还有第三格可落（2 而非 3），于是"接到末尾"这件事在两种序列下结论不同。
        Assert.Equal(2, StripeDropResolver.ResolveInsertIndex(90, withoutDragged));
        Assert.Equal(2, StripeDropResolver.ResolveInsertIndex(90, withDragged));
        Assert.Equal(2, StripeDropResolver.ResolveInsertIndex(200, withoutDragged));
        Assert.Equal(3, StripeDropResolver.ResolveInsertIndex(200, withDragged));
    }

    /// <summary>
    /// 上组的落区是整列的**上半**，与按钮占了多高无关。
    /// </summary>
    /// <remarks>
    /// <c>ToolWindowToolbar.kt:232-241</c>：<c>bounds = 条带矩形; bounds.height /= 2;
    /// bounds.width += toolWindowWidth</c>（右条带同时左移 x）。
    /// </remarks>
    [Fact]
    public void The_top_zone_is_the_upper_half_of_the_column()
    {
        var stripe = new Rect(0, 100, 38, 600);
        var zone = StripeDropResolver.ResolveTopZone(stripe, isRight: false, toolWindowSize: 300);

        Assert.Equal(new Rect(0, 100, 338, 300), zone);

        // 右条带：向**左**扩，落区盖住条带内侧
        var right = StripeDropResolver.ResolveTopZone(new Rect(962, 100, 38, 600), isRight: true, 300);
        Assert.Equal(new Rect(662, 100, 338, 300), right);
    }

    /// <summary>
    /// 下组的落区是**整窗底部**一条带，半屏宽，与鼠标在哪根条带上无关。
    /// </summary>
    /// <remarks><c>ToolWindowToolbar.kt:243-252</c>。</remarks>
    [Theory]
    [InlineData(false, 0d)]
    [InlineData(true, 500d)]
    public void The_bottom_zone_is_a_band_across_the_window(bool isSplit, double expectedX)
    {
        var zone = StripeDropResolver.ResolveBottomZone(
            new Size(1000, 800), stripeHeight: 120, toolWindowSize: 100, isSplit);

        // 高取 max(工具窗 100, 条带 120 + 40) = 160
        Assert.Equal(new Rect(expectedX, 640, 500, 160), zone);
    }

    /// <summary>
    /// **落在第一个图标上方仍属于上组**，但那是因为它在上半区里，
    /// 不是因为"什么都不中就算上组"。
    /// </summary>
    /// <remarks>
    /// 回归：本仓原先按「按钮的内容矩形」判组，都不中就兜底成上组。于是
    /// 上组按钮少的时候，整整半根条带都落到那条兜底上——把下组的按钮往上拖一点点
    /// 就被判成上停靠（用户实测报的就是这个）。
    /// 现在两个落区都是**几何定义的**，不中就是不中（返回 null，不给放）。
    /// </remarks>
    [Fact]
    public void A_point_below_the_upper_half_is_not_the_top_zone()
    {
        var stripe = new Rect(0, 0, 38, 600);
        var zone = StripeDropResolver.ResolveTopZone(stripe, isRight: false, toolWindowSize: 300);

        Assert.True(zone.Contains(new Point(19, 10)));    // 第一个图标上方 —— 在上半区，算上组
        Assert.True(zone.Contains(new Point(19, 299)));   // 上半区的下沿
        Assert.False(zone.Contains(new Point(19, 301)));  // 过了中线就不再是上组
    }

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
