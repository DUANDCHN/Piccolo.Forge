using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 拖分隔条改大小。
public class ForgeDockResizerTests
{
    [Fact]
    public void Weight_uses_the_upstream_adjusted_ratio()
    {
        // ToolWindowManagerImpl.kt:228-232 —— ratio += ((part+1)/total - ratio)/2
        var weight = ForgeDockResizer.ComputeWeight(250, 1000);

        var expected = (250f / 1000) + ((((250f + 1) / 1000) - (250f / 1000)) / 2);
        Assert.Equal(expected, weight, 5);
    }

    [Fact]
    public void Weight_is_clamped_by_the_main_splitter_min_size()
    {
        // registry.properties:160 —— ide.mainSplitter.min.size=30
        var tiny = ForgeDockResizer.ComputeWeight(2, 1000);
        var huge = ForgeDockResizer.ComputeWeight(999, 1000);

        Assert.Equal(ForgeDockResizer.ComputeWeight(30, 1000), tiny, 5);
        Assert.Equal(ForgeDockResizer.ComputeWeight(970, 1000), huge, 5);
    }

    [Fact]
    public void Weight_is_normalized()
    {
        // 退化情形也要过 normalizeWeight
        Assert.Equal(WindowInfo.DefaultWeight, ForgeDockResizer.ComputeWeight(100, 0), 5);
    }

    [AvaloniaFact]
    public void Mouse_zone_is_six_pixels_wide()
    {
        // ToolWindowPane.kt:198 + registry.properties:290
        Assert.Equal(6, ForgeDockResizer.MouseZone);

        var resizer = new ForgeDockResizer { Anchor = ToolWindowAnchor.Left };
        resizer.Measure(Size.Infinity);
        Assert.Equal(6, resizer.DesiredSize.Width);

        resizer.Anchor = ToolWindowAnchor.Bottom;
        resizer.InvalidateMeasure();
        resizer.Measure(Size.Infinity);
        Assert.Equal(6, resizer.DesiredSize.Height);
    }

    [AvaloniaFact]
    public void A_resizer_is_placed_on_every_occupied_edge()
    {
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        var frame = new ForgeToolWindowFrame();
        frame.Attach(manager);

        manager.Activate("Project");    // LEFT
        manager.Activate("Terminal");   // BOTTOM

        var resizers = frame.DockPane.Children
            .OfType<ForgeDockResizer>()
            .Select(x => x.Anchor)
            .Order()
            .ToList();

        Assert.Equal([ToolWindowAnchor.Left, ToolWindowAnchor.Bottom], resizers);
    }

    [AvaloniaFact]
    public void The_resizer_straddles_the_boundary()
    {
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        var frame = new ForgeToolWindowFrame();
        frame.Attach(manager);
        manager.Activate("Project");

        frame.Measure(new Size(1000, 800));
        frame.Arrange(new Rect(0, 0, 1000, 800));

        var resizer = frame.DockPane.Children.OfType<ForgeDockResizer>().Single();
        var window = frame.DockPane.Children.OfType<Border>().Single();

        // 热区中心对准左窗口的右缘
        Assert.Equal(window.Bounds.Right, resizer.Bounds.Center.X, 1);
        Assert.Equal(ForgeMetrics.SplitterMouseZone, resizer.Bounds.Width);
    }
}
