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
/// 拖拽期间「捕获丢了」不等于「用户取消了」。
/// </summary>
/// <remarks>
/// 回归：<c>PointerCaptureLost</c> 在两个平台上语义不同 —— Windows 的捕获是 OS 级的
/// （<c>SetCapture</c>），丢了基本就是真被打断；macOS 没有对应 API，Avalonia 的 mac 后端
/// 在主窗口不再是 key window 时就会发它，而拖影窗口 <c>Show()</c> 正好会造成这件事。
/// 原实现把它一律当打断，mac 上表现为「拖影一出现，拖拽就停在那儿动不了」。
/// <para>
/// 这条用例不依赖真实平台：直接把信号打进去，断言手势仍然活着 ——
/// mac 专有的时序问题只能这么钉。
/// </para>
/// </remarks>
public class StripeDragCaptureLostTests
{
    [AvaloniaFact]
    public void 捕获丢了不终止拖拽()
    {
        var (window, frame, shell) = Build();
        var button = frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>().First(x => x.Title == "a");
        var press = Center(button, window);
        var far = new Point(window.Width - 20, window.Height / 2);

        window.MouseDown(press, MouseButton.Left);
        window.MouseMove(press + new Vector(0, 12));
        Pump(window);

        RaiseCaptureLost(frame);
        Pump(window);

        // 拖拽还活着：继续拖到右条带、在那儿松手，应当照常换边。
        window.MouseMove(far);
        window.MouseUp(far, MouseButton.Left);
        Pump(window);

        Assert.Equal(ToolWindowAnchor.Right, shell.Manager.Layout.GetInfo("a")!.Anchor);
        window.Close();
    }

    [AvaloniaFact]
    public void Esc取消拖拽()
    {
        var (window, frame, shell) = Build();
        var button = frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>().First(x => x.Title == "a");
        var press = Center(button, window);
        var far = new Point(window.Width - 20, window.Height / 2);

        window.MouseDown(press, MouseButton.Left);
        window.MouseMove(press + new Vector(0, 12));
        window.MouseMove(far);
        Pump(window);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump(window);

        window.MouseUp(far, MouseButton.Left);
        Pump(window);

        Assert.Equal(ToolWindowAnchor.Left, shell.Manager.Layout.GetInfo("a")!.Anchor);
        window.Close();
    }

    private static void RaiseCaptureLost(Control target)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        target.RaiseEvent(new PointerCaptureLostEventArgs(target, pointer));
    }

    private static Point Center(Control c, Window w)
        => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;

    private static (Window Window, ForgeControl Frame, ToolWindowShellViewModel Shell) Build()
    {
        var infos = new List<WindowInfo>
        {
            new() { Id = "a", Anchor = ToolWindowAnchor.Left, Order = 0, IsVisible = true, Weight = 0.3f },
            new() { Id = "b", Anchor = ToolWindowAnchor.Left, Order = 1, Weight = 0.3f },
        };

        var shell = new ToolWindowShellViewModel(new ToolWindowManager(new DesktopLayout(infos)));
        var frame = new ForgeControl { Palette = ForgePalette.ExpUiDark, Appearance = ForgeAppearance.Default with { FollowThemeVariant = false } };
        frame.Attach(shell);

        var window = new Window { Width = 700, Height = 400, WindowDecorations = WindowDecorations.None, Content = frame };
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
