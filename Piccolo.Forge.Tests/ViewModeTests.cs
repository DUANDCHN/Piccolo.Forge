using Avalonia;
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
/// 四种视图模式（停靠固定 / 停靠不固定 / 浮动 / 窗口）都得画得出来。
/// </summary>
/// <remarks>
/// 对应上游的 <c>TW.ViewModeGroup</c>：<c>ToolWindowType</c> 的四个值。
/// SLIDING 是滑出**覆盖在编辑区上**（<c>ToolWindowPane.kt:432-459</c> 的 PALETTE_LAYER 那一支），
/// FLOATING / WINDOWED 各有自己的宿主窗口（<c>FloatingDecorator</c> 是 JDialog、
/// <c>WindowedDecorator</c> 是 JFrame）。
/// </remarks>
public class ViewModeTests
{
    [AvaloniaFact]
    public void 停靠固定占掉编辑区的地方()
    {
        var (window, frame, shell, editor) = Build();

        Assert.Equal(ToolWindowType.Docked, shell.Manager.Layout.GetInfo("相机列表")!.Type);
        Assert.True(editor.Bounds.X > 0, "停靠的面板该把编辑区往右推");

        window.Close();
    }

    [AvaloniaFact]
    public void 停靠不固定盖在编辑区上不占地方()
    {
        var (window, frame, shell, editor) = Build();
        var docked = editor.Bounds;

        shell.Manager.SetType("相机列表", ToolWindowType.Sliding);
        Pump(window);

        Assert.Equal(0, editor.Bounds.X, 1.0);
        Assert.True(editor.Bounds.Width > docked.Width, "滑出之后编辑区应当变宽（那块不再占地方）");

        // 面板还在，盖在编辑区左边
        var panel = frame.DockPane.Children.OfType<Border>().First(x => x != editor && x.Bounds.Width > 0);
        Assert.Equal(0, panel.Bounds.X, 1.0);
        Assert.True(panel.ZIndex > editor.ZIndex, "盖不住编辑区就没意义");

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(ToolWindowType.Floating, false)]
    [InlineData(ToolWindowType.Windowed, true)]
    public void 浮动与窗口各自开一个宿主窗口(ToolWindowType type, bool inTaskbar)
    {
        var (window, frame, shell, editor) = Build();

        shell.Manager.SetType("相机列表", type);
        Pump(window);

        // 不再占停靠区
        Assert.Equal(0, editor.Bounds.X, 1.0);
        Assert.DoesNotContain(frame.DockPane.Children.OfType<Border>(), x => x != editor && x.Bounds.Width > 0);

        var external = frame.ExternalWindows.Single();
        Assert.Equal(type, external.Type);
        Assert.Equal(inTaskbar, external.ShowInTaskbar);

        // 切回停靠：窗口收掉，面板回到列里
        shell.Manager.SetType("相机列表", ToolWindowType.Docked);
        Pump(window);

        Assert.Empty(frame.ExternalWindows);
        Assert.True(editor.Bounds.X > 0);

        window.Close();
    }

    [AvaloniaFact]
    public void 关掉浮动窗口就等于收起这个面板()
    {
        var (window, frame, shell, _) = Build();

        shell.Manager.SetType("相机列表", ToolWindowType.Floating);
        Pump(window);

        frame.ExternalWindows.Single().Window.Close();
        Pump(window);

        Assert.False(shell.Manager.Layout.GetInfo("相机列表")!.IsVisible);
        window.Close();
    }

    private static (Window Window, ForgeToolWindowFrame Frame, ToolWindowShellViewModel Shell, Border Editor) Build()
    {
        var infos = new List<WindowInfo>
        {
            new() { Id = "相机列表", Anchor = ToolWindowAnchor.Left, Order = 0, IsVisible = true, Weight = 0.25f },
        };

        var shell = new ToolWindowShellViewModel(new ToolWindowManager(new DesktopLayout(infos)));
        var editor = new Border();
        var frame = new ForgeToolWindowFrame { EditorContent = editor };
        frame.Attach(shell);

        var window = new Window { Width = 900, Height = 500, WindowDecorations = WindowDecorations.None, Content = frame };
        window.Show();
        Pump(window);
        return (window, frame, shell, editor);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }
}
