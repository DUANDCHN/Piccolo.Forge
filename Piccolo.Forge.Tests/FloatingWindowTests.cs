using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 浮动 / 独立窗口形态。
public class FloatingWindowTests
{
    private static readonly FloatingBounds Screen = new(0, 0, 1920, 1080);
    private static readonly FloatingBounds Frame = new(100, 100, 1200, 800);

    [Theory]
    // ToolWindowTypeExtensions.kt:9-10
    [InlineData(ToolWindowType.Docked, true)]
    [InlineData(ToolWindowType.Sliding, true)]
    [InlineData(ToolWindowType.Floating, false)]
    [InlineData(ToolWindowType.Windowed, false)]
    public void Internal_means_docked_or_sliding(ToolWindowType type, bool expected)
        => Assert.Equal(expected, ToolWindowManager.IsInternal(type));

    [Fact]
    public void Switching_to_floating_does_not_write_internal_type()
    {
        // ToolWindowManagerImpl.kt:1770-1772 —— 只有内部形态才写 internalType
        var m = NewManager();
        m.SetType("Project", ToolWindowType.Floating);

        Assert.Equal(ToolWindowType.Floating, m.Layout.GetInfo("Project")!.Type);
        Assert.Equal(ToolWindowType.Docked, m.Layout.GetInfo("Project")!.InternalType);
        Assert.Equal(ToolWindowType.Docked, m.ResolveInternalType("Project"));
    }

    [Fact]
    public void Switching_to_sliding_updates_internal_type()
    {
        var m = NewManager();
        m.SetType("Project", ToolWindowType.Sliding);

        Assert.Equal(ToolWindowType.Sliding, m.Layout.GetInfo("Project")!.InternalType);
    }

    [Fact]
    public void The_same_type_is_a_no_op()
    {
        // ToolWindowManagerImpl.kt:1746-1755
        Assert.False(NewManager().SetType("Project", ToolWindowType.Docked));
    }

    [Fact]
    public void A_floating_window_does_not_displace_the_docked_one()
    {
        // 外部形态不进 pane（ToolWindowManagerImpl.kt:942-947），所以不参与互斥
        var m = NewManager();
        m.Show("Project");
        m.SetType("Commit", ToolWindowType.Floating);
        m.Show("Commit");

        Assert.True(m.Layout.GetInfo("Project")!.IsVisible);
        Assert.True(m.Layout.GetInfo("Commit")!.IsVisible);
    }

    [Fact]
    public void Docking_a_floating_window_back_starts_displacing_again()
    {
        var m = NewManager();
        m.Show("Project");
        m.SetType("Commit", ToolWindowType.Floating);
        m.Show("Commit");

        m.SetType("Commit", m.ResolveInternalType("Commit"));

        Assert.False(m.Layout.GetInfo("Project")!.IsVisible);
        Assert.True(m.Layout.GetInfo("Commit")!.IsVisible);
    }

    [Fact]
    public void An_invisible_window_only_changes_the_field()
    {
        // ToolWindowManagerImpl.kt:1758-1762
        var m = NewManager();
        m.SetType("Project", ToolWindowType.Floating);

        Assert.False(m.Layout.GetInfo("Project")!.IsVisible);
    }

    [Fact]
    public void Valid_stored_bounds_are_used_as_is()
    {
        // ToolWindowManagerDecorators.kt:222-226
        var stored = new FloatingBounds(200, 200, 400, 300);
        var (bounds, center) = FloatingBoundsResolver.Resolve(stored, Screen, (800, 600));

        Assert.Equal(stored, bounds);
        Assert.False(center);
    }

    [Fact]
    public void Offscreen_bounds_are_pulled_back_and_centered()
    {
        // :227-233
        var stored = new FloatingBounds(5000, 5000, 400, 300);
        var (bounds, center) = FloatingBoundsResolver.Resolve(stored, Screen, (800, 600));

        Assert.True(center);
        Assert.True(FloatingBoundsResolver.IsValid(bounds, Screen));
        Assert.Equal(400, bounds.Width);
    }

    [Fact]
    public void Missing_bounds_fall_back_to_the_given_size_and_center()
    {
        // :234-246
        var (bounds, center) = FloatingBoundsResolver.Resolve(null, Screen, (800, 600));

        Assert.True(center);
        Assert.Equal(800, bounds.Width);
        Assert.Equal(600, bounds.Height);
    }

    [Fact]
    public void Zero_sized_stored_bounds_count_as_missing()
    {
        // WindowInfoImpl.kt:53 —— 全零等于未定义
        var (bounds, _) = FloatingBoundsResolver.Resolve(new FloatingBounds(0, 0, 0, 0), Screen, (640, 480));

        Assert.Equal(640, bounds.Width);
    }

    [Fact]
    public void Centering_is_relative_to_the_ide_frame()
    {
        // :248-251
        var centered = FloatingBoundsResolver.CenterOn(new FloatingBounds(0, 0, 400, 300), Frame);

        Assert.Equal(100 + ((1200 - 400) / 2), centered.X);
        Assert.Equal(100 + ((800 - 300) / 2), centered.Y);
    }

    private static ToolWindowManager NewManager() => new(DefaultToolWindowLayout.CreateV2());
}
