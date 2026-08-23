using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// <see cref="ForgeNotificationManager"/>：显示照旧走框架那套，只多"悬停不消隐"。
/// </summary>
/// <remarks>
/// 判据取**卡片还在不在**，不取内部计时字段 —— 后者是实现细节，改实现就得改用例。
/// 时间推进用 headless 的 <c>AvaloniaHeadlessPlatform.ForceRenderTimerTick</c> + 真实等待，
/// 所以取值都留足余量（毫秒级的精确计时不是这条行为的重点）。
/// </remarks>
public class ForgeNotificationManagerTests
{
    private static (Window window, ForgeNotificationManager manager) Host()
    {
        var manager = new ForgeNotificationManager();
        var window = new Window { Width = 400, Height = 300, Content = manager };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, manager);
    }

    private static int CardCount(ForgeNotificationManager m) =>
        m.GetVisualDescendants().OfType<NotificationCard>().Count();

    [AvaloniaFact]
    public void 到点自动消隐()
    {
        var (window, manager) = Host();
        manager.Show("正文", NotificationType.Information, TimeSpan.FromMilliseconds(60));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, CardCount(manager));

        System.Threading.Thread.Sleep(220);
        Dispatcher.UIThread.RunJobs();

        // 关闭走的是卡片自己的关闭动画 → NotificationClosed → 从容器摘掉
        Assert.True(CardCount(manager) <= 1, "到点后应进入关闭流程");
        window.Close();
    }

    [AvaloniaFact]
    public void 时长为零_常驻不消隐()
    {
        var (window, manager) = Host();
        manager.Show("常驻", NotificationType.Warning, TimeSpan.Zero);
        Dispatcher.UIThread.RunJobs();

        System.Threading.Thread.Sleep(250);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, CardCount(manager));   // 没有计时器，怎么等都还在
        window.Close();
    }

    [AvaloniaFact]
    public void 悬停期间不消隐()
    {
        var (window, manager) = Host();
        manager.Show("停住别走", NotificationType.Error, TimeSpan.FromMilliseconds(80));
        Dispatcher.UIThread.RunJobs();

        var card = manager.GetVisualDescendants().OfType<NotificationCard>().Single();

        // 进入卡片 → 暂停计时
        card.RaiseEvent(new global::Avalonia.Input.PointerEventArgs(
            global::Avalonia.Input.InputElement.PointerEnteredEvent,
            card,
            new global::Avalonia.Input.Pointer(0, global::Avalonia.Input.PointerType.Mouse, true),
            card, default, 0, default, default));
        Dispatcher.UIThread.RunJobs();

        // 远超时长地等 —— 悬停着就不该走
        System.Threading.Thread.Sleep(300);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, CardCount(manager));
        window.Close();
    }
}
