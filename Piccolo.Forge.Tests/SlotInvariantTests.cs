using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 「一格最多一个可见」这条不变量。
/// </summary>
/// <remarks>
/// 回归：<c>Show</c> / <c>MoveTo</c> 一直维护它，唯独 <c>SetLayout</c>（读档）
/// 是直接把存档写进台账、一条都不判 —— 存档里只要有「同一格两个都可见」，
/// 这状态就永久留着：那一格两个条带按钮**都亮着背板**，两块面板叠在同一个位置、
/// 后画的盖住前面的，看起来就是「选中的是甲、开着的却是乙」，关掉乙甲又冒出来。
/// </remarks>
public class SlotInvariantTests
{
    private static DesktopLayout Broken() => new(
    [
        new WindowInfo { Id = "相机列表", Anchor = ToolWindowAnchor.Left, Order = 0, IsVisible = true, Weight = 0.3f },
        new WindowInfo { Id = "本地文件", Anchor = ToolWindowAnchor.Left, Order = 1, IsVisible = true, Weight = 0.3f },
    ]);

    [Fact]
    public void 读档时同一格只留一个可见()
    {
        var manager = new ToolWindowManager(Broken());
        manager.SetLayout(Broken());

        var left = manager.Layout.GetSortedList().Where(x => x.Anchor == ToolWindowAnchor.Left).ToList();
        Assert.Equal(["相机列表"], left.Where(x => x.IsVisible).Select(x => x.Id));
    }

    [Fact]
    public void 不同格的两个可以同时可见()
    {
        var layout = new DesktopLayout(
        [
            new WindowInfo { Id = "相机列表", Anchor = ToolWindowAnchor.Left, Order = 0, IsVisible = true, Weight = 0.3f },
            new WindowInfo { Id = "日志", Anchor = ToolWindowAnchor.Left, Order = 1, IsVisible = true, IsSplit = true, Weight = 0.3f },
        ]);

        var manager = new ToolWindowManager(layout);
        manager.SetLayout(layout);

        Assert.Equal(2, manager.Layout.GetSortedList().Count(x => x.IsVisible));
    }

    /// <summary>界面上的表现：条带上只有一个按钮亮着，面板也只有一块。</summary>
    [AvaloniaFact]
    public void 界面上不会两个按钮都亮着()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(Broken()));
        shell.RestoreLayout(ToolWindowLayoutSerializer.Write(Broken()));

        var frame = new ForgeToolWindowFrame { Palette = ForgePalette.ExpUiDark, FollowThemeVariant = false };
        frame.Attach(shell);

        var window = new Window { Width = 700, Height = 400, WindowDecorations = WindowDecorations.None, Content = frame };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();

        var lit = frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>()
            .Count(x => x.ResolveBackground() is not null);

        Assert.Equal(1, lit);
        Assert.Equal(1, frame.DockPane.Children.OfType<Border>().Count(x => x.Bounds.Width > 0));
    }
}
