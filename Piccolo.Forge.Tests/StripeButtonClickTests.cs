using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 条带按钮的「点一下开合」。
/// </summary>
/// <remarks>
/// 回归：判据原先只有拖拽死区（<c>MouseDragHelper.java:32</c> 的 7px）——
/// 按下时抖过 7px 就一律当成拖拽，而这一下又落不到任何有效槽位，于是什么都不做。
/// 真人按鼠标抖七八个像素再正常不过，表现就是**侧边栏按钮有时要点两下**，
/// 等于把「能点的地方」缩成按下点周围 7px 的一个小圈。
/// <para>
/// 上游没有这个问题：<c>SquareStripeButton</c> 继承 <c>ActionButton</c>，
/// 点击是 Swing 的「释放在组件内即触发」，拖拽助手只在真正拖离之后才接管。
/// </para>
/// </remarks>
public class StripeButtonClickTests
{
    [AvaloniaTheory]
    [InlineData(0)]    // 稳稳一点
    [InlineData(8)]    // 抖过死区，但还在按钮里
    [InlineData(20)]   // 抖得更多，仍在按钮里（40 宽的按钮）
    public void 在同一个按钮上按下松开就是点击(double jitter)
    {
        var (window, frame, shell) = Build();
        var button = frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>().First(x => x.Title == "相机列表");
        var press = Center(button, window);

        window.MouseDown(press, MouseButton.Left);
        if (jitter > 0)
        {
            window.MouseMove(press + new Vector(0, jitter));
        }

        window.MouseUp(press + new Vector(0, jitter), MouseButton.Left);
        Pump(window);

        Assert.False(shell.Manager.Layout.GetInfo("相机列表")!.IsVisible, $"抖了 {jitter}px 就不算点击了");
        window.Close();
    }

    /// <summary>拖到另一条边上仍然是「移动」，不是「点击」。</summary>
    [AvaloniaFact]
    public void 拖到别处不算点击()
    {
        var (window, frame, shell) = Build();
        var button = frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>().First(x => x.Title == "相机列表");
        var press = Center(button, window);
        var far = new Point(window.Width - 20, window.Height / 2);

        window.MouseDown(press, MouseButton.Left);
        window.MouseMove(far);
        window.MouseUp(far, MouseButton.Left);
        Pump(window);

        var info = shell.Manager.Layout.GetInfo("相机列表")!;
        Assert.True(info.IsVisible, "拖动被当成了点击（面板被收起来了）");
        Assert.Equal(ToolWindowAnchor.Right, info.Anchor);

        window.Close();
    }

    private static Point Center(Control button, Window window)
        => button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;

    private static (Window Window, ForgeToolWindowFrame Frame, ToolWindowShellViewModel Shell) Build()
    {
        var infos = new List<WindowInfo>
        {
            new() { Id = "相机列表", Anchor = ToolWindowAnchor.Left, Order = 0, IsVisible = true, Weight = 0.3f },
        };

        var shell = new ToolWindowShellViewModel(new ToolWindowManager(new DesktopLayout(infos)));
        var frame = new ForgeToolWindowFrame { Palette = ForgePalette.ExpUiDark, FollowThemeVariant = false };
        frame.Attach(shell);

        var window = new Window { Width = 700, Height = 300, WindowDecorations = WindowDecorations.None, Content = frame };
        window.Show();
        Pump(window);
        return (window, frame, shell);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }
}
