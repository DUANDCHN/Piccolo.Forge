using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 「恢复默认布局」的做法：**整份台账换成出厂那份**，而不是逐个字段往回改。
/// </summary>
/// <remarks>
/// 宿主（LDVision 的 <c>ShellHost.ResetLayout</c>）就是这么做的：按登记表造一份只带
/// anchor / isSplit / order 的 <see cref="DesktopLayout"/>，其余全取 <see cref="WindowInfo"/> 的默认值，
/// 一次 <see cref="ToolWindowManager.SetLayout"/> 套上去。
/// <para>
/// 回归的是这个：早先宿主只调 <c>MoveTo</c>（只管 anchor 与 isSplit），于是**视图模式、面板大小
/// 这些同样属于布局的字段一个都没回去** —— 用户把面板切成取消停靠 / 浮动、拖宽过、
/// 把条带按钮拖过序、或者「从边栏中移除」过，点了「恢复默认布局」照旧保持原样。
/// 逐字段补是补不完的（<see cref="WindowInfo"/> 每加一个字段就多一处会被忘掉的地方），
/// 所以这里固化的是**整体替换**这条做法本身。
/// </para>
/// </remarks>
public class LayoutResetTests
{
    private const string Panel = "相机参数";

    /// <summary>被用户改得面目全非的台账。</summary>
    private static DesktopLayout Messed() => new(
    [
        new WindowInfo
        {
            Id = Panel,
            Anchor = ToolWindowAnchor.Bottom,   // 挪过边
            IsSplit = true,                     // 挪到了另一半
            Type = ToolWindowType.Sliding,      // 取消停靠
            InternalType = ToolWindowType.Sliding,
            IsAutoHide = true,                  // 停靠不固定
            Weight = 0.8f,                      // 拖得很宽
            SideWeight = 0.9f,
            Order = 7,                          // 条带按钮拖过序
            IsShowStripeButton = false,         // 从边栏中移除过
            FloatingBounds = new FloatingBounds(10, 20, 300, 400),
            ContentOrder = ["乙", "甲"],
        },
    ]);

    /// <summary>出厂台账：只有登记表说了算的三样，其余一律默认值。</summary>
    private static DesktopLayout Factory() => new(
    [
        new WindowInfo { Id = Panel, Anchor = ToolWindowAnchor.Right, IsSplit = false, Order = 0 },
    ]);

    [Fact]
    public void 整份换成出厂台账时形态与大小一起回默认()
    {
        var manager = new ToolWindowManager(Messed());

        manager.SetLayout(Factory());

        var info = manager.Layout.GetInfo(Panel)!;

        // 登记表说了算的三样
        Assert.Equal(ToolWindowAnchor.Right, info.Anchor);
        Assert.False(info.IsSplit);
        Assert.Equal(0, info.Order);

        // ★ 其余一律回 WindowInfo 的默认值 —— 这几条正是早先漏掉的
        Assert.Equal(ToolWindowType.Docked, info.Type);
        Assert.Equal(ToolWindowType.Docked, info.InternalType);
        Assert.False(info.IsAutoHide);
        Assert.Equal(WindowInfo.DefaultWeight, info.Weight);
        Assert.Equal(0.5f, info.SideWeight);
        Assert.True(info.IsShowStripeButton);
        Assert.Null(info.FloatingBounds);
        Assert.Empty(info.ContentOrder);
    }

    /// <summary>每条边的统一宽度也是布局的一部分，同样跟着回默认。</summary>
    [Fact]
    public void 每条边的统一宽度也回默认()
    {
        var manager = new ToolWindowManager(Messed());
        manager.Layout.SetUnifiedAnchorWeight(ToolWindowAnchor.Right, 0.75f);

        manager.SetLayout(Factory());

        Assert.Equal(
            new DesktopLayout().GetUnifiedAnchorWeight(ToolWindowAnchor.Right),
            manager.Layout.GetUnifiedAnchorWeight(ToolWindowAnchor.Right));
    }

    /// <summary>界面上的表现：已经浮出去单开的那个窗口会被收回主窗口。</summary>
    [AvaloniaFact]
    public void 浮出去的面板被收回主窗口()
    {
        // 登记走 Add 这一条（它自己会把 WindowInfo 写进台账），别再预置一份同 id 的 —— 那是两次登记
        var shell = new ToolWindowShellViewModel();
        shell.Add(Panel, Panel, ToolWindowAnchor.Right);

        var frame = new ForgeControl { EditorContent = new Border() };
        frame.Attach(shell);

        var window = new Window { Width = 900, Height = 500, Content = frame };
        window.Show();
        Pump(window);

        shell.Open(Panel);
        shell.Manager.SetType(Panel, ToolWindowType.Floating);
        Pump(window);
        Assert.NotEmpty(frame.ExternalWindows);

        shell.Manager.SetLayout(Factory());
        Pump(window);

        // 台账回了停靠，宿主窗口就该跟着关掉 —— 否则桌面上留一个孤儿浮动窗口
        Assert.Empty(frame.ExternalWindows);
        Assert.Equal(ToolWindowType.Docked, shell.Manager.Layout.GetInfo(Panel)!.Type);

        window.Close();
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }
}
