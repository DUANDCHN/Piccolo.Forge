using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// DesktopLayout.kt 的次序规则。
public class DesktopLayoutTests
{
    [Fact]
    public void GetNextOrder_returns_zero_for_empty_stripe()
    {
        // DesktopLayout.kt:33-35
        Assert.Equal(0, new DesktopLayout().GetNextOrder(WindowInfo.DefaultPaneId, ToolWindowAnchor.Left));
    }

    [Fact]
    public void GetNextOrder_returns_max_plus_one()
    {
        var layout = DefaultToolWindowLayout.CreateV2();
        Assert.Equal(3, layout.GetNextOrder(WindowInfo.DefaultPaneId, ToolWindowAnchor.Left));
        Assert.Equal(5, layout.GetNextOrder(WindowInfo.DefaultPaneId, ToolWindowAnchor.Right));
    }

    [Fact]
    public void SetAnchor_with_minus_one_appends_to_the_end()
    {
        // DesktopLayout.kt:81-84
        var layout = DefaultToolWindowLayout.CreateV2();
        var affected = layout.SetAnchor("Terminal", WindowInfo.DefaultPaneId, ToolWindowAnchor.Left, -1);

        Assert.Empty(affected);
        var terminal = layout.GetInfo("Terminal")!;
        Assert.Equal(ToolWindowAnchor.Left, terminal.Anchor);
        Assert.Equal(3, terminal.Order);
    }

    [Fact]
    public void SetAnchor_with_explicit_order_shifts_others_right()
    {
        // DesktopLayout.kt:86-95
        var layout = DefaultToolWindowLayout.CreateV2();
        var affected = layout.SetAnchor("Terminal", WindowInfo.DefaultPaneId, ToolWindowAnchor.Left, 1);

        Assert.Equal(["Commit", "Structure"], affected.Select(x => x.Id).Order());
        Assert.Equal(1, layout.GetInfo("Terminal")!.Order);
        Assert.Equal(2, layout.GetInfo("Commit")!.Order);
        Assert.Equal(3, layout.GetInfo("Structure")!.Order);
        Assert.Equal(0, layout.GetInfo("Project")!.Order);
    }

    [Fact]
    public void NormalizeOrder_renumbers_per_anchor_and_skips_minus_one()
    {
        // DesktopLayout.kt:198-214
        var layout = new DesktopLayout(
        [
            new WindowInfo { Id = "a", Anchor = ToolWindowAnchor.Left, Order = 5 },
            new WindowInfo { Id = "b", Anchor = ToolWindowAnchor.Left, Order = 9 },
            new WindowInfo { Id = "c", Anchor = ToolWindowAnchor.Right, Order = 7 },
            new WindowInfo { Id = "d", Anchor = ToolWindowAnchor.Right, Order = -1 },
        ]);

        layout.NormalizeOrder();

        Assert.Equal(0, layout.GetInfo("a")!.Order);
        Assert.Equal(1, layout.GetInfo("b")!.Order);
        Assert.Equal(0, layout.GetInfo("c")!.Order);
        Assert.Equal(-1, layout.GetInfo("d")!.Order);
    }

    [Fact]
    public void SetSideToolAndAnchor_is_a_no_op_when_nothing_changes()
    {
        // ToolWindowManagerImpl.kt:1665-1669 的提前返回
        var layout = DefaultToolWindowLayout.CreateV2();
        var before = layout.GetInfo("Terminal")!;

        var affected = layout.SetSideToolAndAnchor(
            "Terminal", before.PaneId, before.Anchor, before.Order, before.IsSplit);

        Assert.Empty(affected);
        Assert.Equal(before, layout.GetInfo("Terminal"));
    }

    [Fact]
    public void SetSideToolAndAnchor_moves_a_bottom_window_to_the_right_stripe_group()
    {
        // 拖拽落到右条带下组 = anchor 不变、isSplit 变 true（AbstractDroppableStripe.kt:242-258）
        var layout = DefaultToolWindowLayout.CreateV2();

        layout.SetSideToolAndAnchor(
            "Terminal", WindowInfo.DefaultPaneId, ToolWindowAnchor.Bottom, -1, isSplit: true);

        var terminal = layout.GetInfo("Terminal")!;
        Assert.True(terminal.IsSplit);
        Assert.Equal(ToolWindowAnchor.Bottom, terminal.Anchor);
        Assert.Equal(
            new StripePlacement(StripeSide.Right, StripeGroup.Bottom),
            StripePlacementResolver.Resolve(terminal.Anchor, terminal.IsSplit));
    }

    [Fact]
    public void SetSideToolAndAnchor_shifts_others_when_an_order_is_given()
    {
        var layout = DefaultToolWindowLayout.CreateV2();

        var affected = layout.SetSideToolAndAnchor(
            "Terminal", WindowInfo.DefaultPaneId, ToolWindowAnchor.Left, 0, isSplit: false);

        Assert.Equal(["Commit", "Project", "Structure"], affected.Select(x => x.Id).Order());
        Assert.Equal(0, layout.GetInfo("Terminal")!.Order);
        Assert.Equal(1, layout.GetInfo("Project")!.Order);
    }

    [Fact]
    public void Unified_weights_default_to_DEFAULT_WEIGHT()
    {
        // UnifiedToolWindowWeights.kt:20-23
        var layout = new DesktopLayout();
        foreach (var anchor in Enum.GetValues<ToolWindowAnchor>())
        {
            Assert.Equal(WindowInfo.DefaultWeight, layout.GetUnifiedAnchorWeight(anchor));
        }
    }

    [Fact]
    public void Weight_is_clamped_to_unit_range()
    {
        // WindowInfoImpl.kt:87、:89 —— max(0f, min(1f, it))
        Assert.Equal(1f, new WindowInfo { Id = "x", Weight = 4f }.Weight);
        Assert.Equal(0f, new WindowInfo { Id = "x", SideWeight = -1f }.SideWeight);
    }
}
