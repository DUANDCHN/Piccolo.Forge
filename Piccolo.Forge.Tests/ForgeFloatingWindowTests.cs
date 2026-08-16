using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 外部形态的宿主窗口。
public class ForgeFloatingWindowTests
{
    private static readonly FloatingBounds Screen = new(0, 0, 1920, 1080);
    private static readonly FloatingBounds Frame = new(100, 100, 1200, 800);

    [Theory]
    // FloatingDecorator.java:92 vs ToolWindowManagerDecorators.kt:103
    [InlineData(ToolWindowType.Floating, "Terminal")]
    [InlineData(ToolWindowType.Windowed, "Terminal - Piccolo")]
    public void The_title_differs_between_the_two_external_types(ToolWindowType type, string expected)
        => Assert.Equal(expected, ForgeFloatingToolWindow.ComposeTitle(type, "Terminal", "Piccolo"));

    [AvaloniaFact]
    public void Only_windowed_shows_up_in_the_taskbar()
    {
        Assert.False(New(ToolWindowType.Floating).ShowInTaskbar);
        Assert.True(New(ToolWindowType.Windowed).ShowInTaskbar);
    }

    [AvaloniaFact]
    public void Internal_types_are_rejected()
        => Assert.Throws<ArgumentException>(() => New(ToolWindowType.Docked));

    [AvaloniaFact]
    public void Missing_bounds_end_up_centered_on_the_ide_frame()
    {
        // ToolWindowManagerDecorators.kt:234-251
        var host = New(ToolWindowType.Floating);

        var bounds = host.ApplyBounds(null, Screen, Frame, (400, 300));

        Assert.Equal(100 + ((1200 - 400) / 2), bounds.X);
        Assert.Equal(400, bounds.Width);
    }

    [AvaloniaFact]
    public void Valid_stored_bounds_are_kept_exactly()
    {
        // :222-226 —— 有效就原样用，不居中
        var host = New(ToolWindowType.Floating);
        var stored = new FloatingBounds(250, 260, 500, 400);

        Assert.Equal(stored, host.ApplyBounds(stored, Screen, Frame, (400, 300)));
    }

    [AvaloniaFact]
    public void Saving_writes_bounds_and_active_flag_back()
    {
        var host = New(ToolWindowType.Floating);
        host.ApplyBounds(new FloatingBounds(300, 200, 500, 400), Screen, Frame, (400, 300));

        var info = host.SaveState(new WindowInfo { Id = "Terminal", Type = ToolWindowType.Floating });

        Assert.Equal(new FloatingBounds(300, 200, 500, 400), info.FloatingBounds);
        Assert.False(info.IsMaximized);
    }

    [AvaloniaFact]
    public void A_floating_window_is_never_reported_as_maximized()
    {
        // isMaximized 只对 WINDOWED 有意义（WindowInfoImpl.kt:56-59）
        var host = New(ToolWindowType.Floating);
        host.Window.WindowState = WindowState.Maximized;

        Assert.False(host.SaveState(new WindowInfo { Id = "T" }).IsMaximized);
    }

    private static ForgeFloatingToolWindow New(ToolWindowType type)
        => new(type, "Terminal", "Piccolo", new Border());
}
