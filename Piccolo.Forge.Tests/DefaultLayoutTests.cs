using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 默认布局（新 UI / V2）必须与 defaultToolWindowlayoutProvider.kt:254-288 一致。
public class DefaultLayoutTests
{
    [Fact]
    public void V2_left_stripe_matches_upstream()
    {
        var layout = DefaultToolWindowLayout.CreateV2();

        Assert.Equal(
            ["Project", "Commit", "Structure"],
            OnAnchor(layout, ToolWindowAnchor.Left).Select(x => x.Id));

        var project = layout.GetInfo("Project")!;
        Assert.Equal(0.25f, project.Weight);
        Assert.Equal(ToolWindowContentUiType.Combo, project.ContentUiType);
        Assert.False(project.IsSplit);

        // Structure 是左边唯一一个 isSplit=true 的
        Assert.True(layout.GetInfo("Structure")!.IsSplit);
        Assert.False(layout.GetInfo("Commit")!.IsSplit);
    }

    [Fact]
    public void V2_right_stripe_matches_upstream()
    {
        var layout = DefaultToolWindowLayout.CreateV2();

        Assert.Equal(
            ["Notifications", "AIAssistant", "Database", "Gradle", "Maven"],
            OnAnchor(layout, ToolWindowAnchor.Right).Select(x => x.Id));

        Assert.Equal(ToolWindowContentUiType.Combo, layout.GetInfo("Notifications")!.ContentUiType);
        Assert.All(OnAnchor(layout, ToolWindowAnchor.Right), x => Assert.Equal(0.25f, x.Weight));
    }

    [Fact]
    public void V2_bottom_stripe_matches_upstream()
    {
        var layout = DefaultToolWindowLayout.CreateV2();
        var bottom = OnAnchor(layout, ToolWindowAnchor.Bottom).ToList();

        Assert.Equal(
            ["Version Control", "Problems", "Problems View", "Terminal", "Services"],
            bottom.Select(x => x.Id));

        // 没写 weight 的取 builder 默认 0.33（defaultToolWindowlayoutProvider.kt:308）
        Assert.All(bottom, x => Assert.Equal(WindowInfo.DefaultWeight, x.Weight));

        // 默认布局里没有任何窗口落在右条带下组
        Assert.All(bottom, x => Assert.False(x.IsSplit));
    }

    [Fact]
    public void V2_order_is_generated_per_stripe_from_zero()
    {
        var layout = DefaultToolWindowLayout.CreateV2();

        foreach (var anchor in new[] { ToolWindowAnchor.Left, ToolWindowAnchor.Right, ToolWindowAnchor.Bottom })
        {
            Assert.Equal(
                Enumerable.Range(0, OnAnchor(layout, anchor).Count()),
                OnAnchor(layout, anchor).Select(x => x.Order));
        }
    }

    [Fact]
    public void V2_nothing_is_visible_by_default()
    {
        // defaultToolWindowlayoutProvider.kt:307 —— isVisible 默认 false
        Assert.All(DefaultToolWindowLayout.CreateV2().GetSortedList(), x => Assert.False(x.IsVisible));
    }

    [Fact]
    public void Sorted_list_orders_by_anchor_weight_then_order()
    {
        // DesktopLayout.kt:180-193 —— TOP=1 LEFT=2 BOTTOM=3 RIGHT=4
        var sorted = DefaultToolWindowLayout.CreateV2().GetSortedList();

        Assert.Equal(
            [
                ToolWindowAnchor.Left, ToolWindowAnchor.Left, ToolWindowAnchor.Left,
                ToolWindowAnchor.Bottom, ToolWindowAnchor.Bottom, ToolWindowAnchor.Bottom,
                ToolWindowAnchor.Bottom, ToolWindowAnchor.Bottom,
                ToolWindowAnchor.Right, ToolWindowAnchor.Right, ToolWindowAnchor.Right,
                ToolWindowAnchor.Right, ToolWindowAnchor.Right,
            ],
            sorted.Select(x => x.Anchor));
    }

    private static IEnumerable<WindowInfo> OnAnchor(DesktopLayout layout, ToolWindowAnchor anchor)
        => layout.GetSortedList().Where(x => x.Anchor == anchor);
}
