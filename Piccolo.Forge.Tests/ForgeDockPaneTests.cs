using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// ToolWindowPane 的两层分隔器与权重换算。
public class ForgeDockPaneTests
{
    [Fact]
    public void Weight_multiplies_the_whole_pane_not_the_remaining_space()
    {
        // ToolWindowPane.kt:333-339 —— 乘的是 rootPane 的宽/高
        var sizes = ForgeDockPane.ComputeSizes(
            new Size(1000, 800),
            new Dictionary<ToolWindowAnchor, double>
            {
                [ToolWindowAnchor.Left] = 0.25,
                [ToolWindowAnchor.Right] = 0.25,
                [ToolWindowAnchor.Bottom] = 0.33,
            });

        Assert.Equal(250, sizes.Left);
        Assert.Equal(250, sizes.Right);
        Assert.Equal(264, sizes.Bottom);   // 800 * 0.33
        Assert.Equal(0, sizes.Top);
    }

    [Theory]
    // ToolWindowPane.kt:85-90 · normalizeWeight
    // 上限那两档是 669 而不是 670：1f - 0.33f 在 float 里是 0.66999996，
    // 1000 × 它 = 669.99996，上游的 (…).toInt() 与这里的 Math.Floor 都截成 669。
    [InlineData(0.0, 330)]      // <= 0 → 0.33
    [InlineData(-1.0, 330)]
    [InlineData(1.0, 669)]      // >= 1 → 1 - 0.33
    [InlineData(2.0, 669)]
    [InlineData(0.5, 500)]
    public void Weights_are_normalized_before_use(double weight, double expected)
    {
        var sizes = ForgeDockPane.ComputeSizes(
            new Size(1000, 1000),
            new Dictionary<ToolWindowAnchor, double> { [ToolWindowAnchor.Left] = weight });

        Assert.Equal(expected, sizes.Left);
    }

    [AvaloniaFact]
    public void Default_layout_makes_bottom_span_the_full_width()
    {
        // wideScreenSupport 默认 false（UISettingsState.kt:93）→ 竖分隔器在外，TOP/BOTTOM 通栏
        var (pane, left, bottom, center) = BuildPane(isWideScreen: false);

        Assert.Equal(new Rect(0, 0, 1000, 1000), new Rect(pane.Bounds.Size));
        Assert.Equal(1000, bottom.Bounds.Width);          // 通栏
        Assert.Equal(670, left.Bounds.Height);            // 1000 - 330（下边）
        Assert.Equal(new Rect(250, 0, 750, 670), center.Bounds);
    }

    [AvaloniaFact]
    public void Wide_screen_makes_left_and_right_span_the_full_height()
    {
        var (_, left, bottom, center) = BuildPane(isWideScreen: true);

        Assert.Equal(1000, left.Bounds.Height);           // 通栏
        Assert.Equal(750, bottom.Bounds.Width);           // 1000 - 250（左边）
        Assert.Equal(new Rect(250, 0, 750, 670), center.Bounds);
    }

    [AvaloniaFact]
    public void Hidden_slots_take_no_space()
    {
        var (_, left, _, center) = BuildPane(isWideScreen: false);

        left.IsVisible = false;
        var pane = (ForgeDockPane)left.Parent!;
        pane.InvalidateMeasure();
        pane.Measure(new Size(1000, 1000));
        pane.Arrange(new Rect(0, 0, 1000, 1000));

        Assert.Equal(0, center.Bounds.X);
        Assert.Equal(1000, center.Bounds.Width);
    }

    [AvaloniaFact]
    public void Two_windows_on_the_left_split_it_top_and_bottom()
    {
        // ToolWindowPane.kt:611-741 —— isSplit=false 在前（上），isSplit=true 在后（下）；
        // 左右边默认竖着切（ToolWindowAnchor.java:63-66 + leftHorizontalSplit 默认 false）
        var (pane, first, second) = BuildSplitPane(ToolWindowAnchor.Left, sideWeight: 0.5);

        Assert.Equal(0, first.Bounds.Y);
        Assert.Equal(335, first.Bounds.Height);      // 670 * 0.5
        Assert.Equal(335, second.Bounds.Y);
        Assert.Equal(335, second.Bounds.Height);
        Assert.Equal(first.Bounds.Width, second.Bounds.Width);
        Assert.Equal(250, pane.Children.Count > 0 ? first.Bounds.Width : 0);
    }

    [AvaloniaFact]
    public void The_split_proportion_comes_from_the_non_split_window()
    {
        // ToolWindowPane.kt:722 —— proportion = normalizeWeight(非 split 那个的 sideWeight)
        var (_, first, second) = BuildSplitPane(ToolWindowAnchor.Left, sideWeight: 0.25);

        Assert.Equal(167, first.Bounds.Height);      // floor(670 * 0.25)
        Assert.Equal(503, second.Bounds.Height);
    }

    [AvaloniaFact]
    public void Two_windows_on_the_bottom_split_it_left_and_right()
    {
        // 横边是左右分
        var (_, first, second) = BuildSplitPane(ToolWindowAnchor.Bottom, sideWeight: 0.5);

        Assert.Equal(0, first.Bounds.X);
        Assert.Equal(500, first.Bounds.Width);
        Assert.Equal(500, second.Bounds.X);
        Assert.Equal(first.Bounds.Height, second.Bounds.Height);
    }

    [Fact]
    public void Split_proportion_is_normalized()
    {
        // 0 和 1 都要过 normalizeWeight
        Assert.Equal(WindowInfo.DefaultWeight, ForgeDockPane.ComputeSplitProportion(0), 5);
        Assert.Equal(0.5, ForgeDockPane.ComputeSplitProportion(0.5), 5);
    }

    private static (ForgeDockPane Pane, Control First, Control Second) BuildSplitPane(
        ToolWindowAnchor anchor,
        double sideWeight)
    {
        var pane = new ForgeDockPane();

        var first = new Border();
        ForgeDockPane.SetAnchor(first, anchor);
        ForgeDockPane.SetWeight(first, 0.25);
        ForgeDockPane.SetSideWeight(first, sideWeight);

        var second = new Border();
        ForgeDockPane.SetAnchor(second, anchor);
        ForgeDockPane.SetWeight(second, 0.25);
        ForgeDockPane.SetIsSplit(second, true);

        var bottom = new Border();
        ForgeDockPane.SetAnchor(bottom, ToolWindowAnchor.Bottom);
        ForgeDockPane.SetWeight(bottom, 0.33);

        pane.Children.Add(first);
        pane.Children.Add(second);
        if (anchor != ToolWindowAnchor.Bottom)
        {
            pane.Children.Add(bottom);
        }

        pane.Measure(new Size(1000, 1000));
        pane.Arrange(new Rect(0, 0, 1000, 1000));
        return (pane, first, second);
    }

    private static (ForgeDockPane Pane, Control Left, Control Bottom, Control Center) BuildPane(bool isWideScreen)
    {
        var pane = new ForgeDockPane { IsWideScreen = isWideScreen };

        var left = new Border();
        ForgeDockPane.SetAnchor(left, ToolWindowAnchor.Left);
        ForgeDockPane.SetWeight(left, 0.25);

        var bottom = new Border();
        ForgeDockPane.SetAnchor(bottom, ToolWindowAnchor.Bottom);
        ForgeDockPane.SetWeight(bottom, 0.33);

        var center = new Border();

        pane.Children.Add(left);
        pane.Children.Add(bottom);
        pane.Children.Add(center);

        pane.Measure(new Size(1000, 1000));
        pane.Arrange(new Rect(0, 0, 1000, 1000));

        return (pane, left, bottom, center);
    }
}
