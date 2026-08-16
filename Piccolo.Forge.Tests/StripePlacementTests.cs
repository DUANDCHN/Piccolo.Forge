using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 「面板停在哪」与「按钮画在哪」是两件事，落位是派生量。
public class StripePlacementTests
{
    [Theory]
    [InlineData(ToolWindowAnchor.Left, false, StripeSide.Left, StripeGroup.Top)]
    [InlineData(ToolWindowAnchor.Left, true, StripeSide.Left, StripeGroup.Top)]
    [InlineData(ToolWindowAnchor.Right, false, StripeSide.Right, StripeGroup.Top)]
    [InlineData(ToolWindowAnchor.Right, true, StripeSide.Right, StripeGroup.Top)]
    [InlineData(ToolWindowAnchor.Bottom, false, StripeSide.Left, StripeGroup.Bottom)]
    [InlineData(ToolWindowAnchor.Bottom, true, StripeSide.Right, StripeGroup.Bottom)]
    public void Resolve_matches_findToolbar(
        ToolWindowAnchor anchor,
        bool isSplit,
        StripeSide expectedSide,
        StripeGroup expectedGroup)
    {
        // ToolWindowPaneNewButtonManager.kt:228-233 + ToolWindowLeftToolbar.kt:11-12
        Assert.Equal(new StripePlacement(expectedSide, expectedGroup), StripePlacementResolver.Resolve(anchor, isSplit));
    }

    [Fact]
    public void Top_anchor_has_no_stripe_in_new_ui()
    {
        // defaultToolWindowlayoutProvider.kt:234 —— "The top stripe is not supported"
        Assert.Throws<ArgumentOutOfRangeException>(
            () => StripePlacementResolver.Resolve(ToolWindowAnchor.Top, false));
    }

    [Fact]
    public void Default_layout_puts_every_bottom_button_on_the_left_stripe()
    {
        var bottom = DefaultToolWindowLayout.CreateV2()
            .GetSortedList()
            .Where(x => x.Anchor == ToolWindowAnchor.Bottom);

        Assert.All(bottom, x =>
            Assert.Equal(
                new StripePlacement(StripeSide.Left, StripeGroup.Bottom),
                StripePlacementResolver.Resolve(x.Anchor, x.IsSplit)));
    }

    [Fact]
    public void Split_buttons_sort_after_non_split_ones()
    {
        // AbstractDroppableStripe.kt:60-62
        var sorted = Sort(
            ToolWindowAnchor.Left,
            [
                new WindowInfo { Id = "split", IsSplit = true, Order = 0 },
                new WindowInfo { Id = "plain", IsSplit = false, Order = 9 },
            ]);

        Assert.Equal(["plain", "split"], sorted.Select(x => x.Id));
    }

    [Fact]
    public void Bottom_group_is_ordered_bottom_to_top()
    {
        // AbstractDroppableStripe.kt:63-67 —— 竖条带的 BOTTOM 组是倒序的
        var sorted = Sort(
            ToolWindowAnchor.Bottom,
            [
                new WindowInfo { Id = "first", Order = 0 },
                new WindowInfo { Id = "second", Order = 1 },
                new WindowInfo { Id = "third", Order = 2 },
            ]);

        Assert.Equal(["third", "second", "first"], sorted.Select(x => x.Id));
    }

    [Fact]
    public void Undefined_order_sorts_last()
    {
        // AbstractDroppableStripe.kt:74-77 —— order == -1 视作 Int.MAX_VALUE
        var sorted = Sort(
            ToolWindowAnchor.Left,
            [
                new WindowInfo { Id = "none", Order = -1 },
                new WindowInfo { Id = "zero", Order = 0 },
            ]);

        Assert.Equal(["zero", "none"], sorted.Select(x => x.Id));
    }

    private static List<WindowInfo> Sort(ToolWindowAnchor anchor, IEnumerable<WindowInfo> infos)
        => infos.Order(StripePlacementResolver.CreateButtonLayoutComparer(anchor)).ToList();
}
