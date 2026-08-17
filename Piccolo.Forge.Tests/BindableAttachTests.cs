using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 「接哪个 VM」是可绑定属性，于是整块外壳能在 XAML 里声明。
/// </summary>
/// <remarks>
/// 对位 Dock.Avalonia 的 <c>&lt;DockControl Layout="{Binding Layout}"/&gt;</c>。
/// 早先只有 <c>Attach()</c> 方法、<c>Shell</c> / <c>Area</c> 是只读的，
/// 宿主就只能在 code-behind 里建控件再塞进去。
/// </remarks>
public class BindableAttachTests
{
    [AvaloniaFact]
    public void 给外框赋一个外壳VM就等于接上()
    {
        var frame = new ForgeToolWindowFrame();
        var window = new Window { Width = 700, Height = 300, Content = frame };
        window.Show();
        Render(window);

        // 没接之前条带上什么都没有
        Assert.Empty(Buttons(frame));

        // XAML 里 Shell="{Binding …}" 走的就是这一句
        frame.Shell = NewShell();
        Render(window);

        Assert.Contains(Buttons(frame), x => x.Title == "相机列表");
        window.Close();
    }

    /// <summary>属性与方法是同一条路 —— <c>Attach()</c> 现在就是写这个属性。</summary>
    [AvaloniaFact]
    public void Attach与赋值等价()
    {
        var shell = NewShell();
        var frame = new ForgeToolWindowFrame();
        frame.Attach(shell);

        Assert.Same(shell, frame.Shell);
    }

    [AvaloniaFact]
    public void 给编辑区赋一个VM就等于接上()
    {
        var area = new ForgeEditorArea();
        var window = new Window { Width = 700, Height = 300, Content = area };
        window.Show();
        Render(window);

        var vm = new EditorAreaViewModel();
        vm.Open("d0", "cam-1.png", new TextBlock());
        area.Area = vm;
        Render(window);

        Assert.Contains(Tabs(area), x => x.Title == "cam-1.png");
        window.Close();
    }

    /// <summary>
    /// 换一个 VM：画的是新的那份，旧的那份不再影响它。
    /// </summary>
    /// <remarks>
    /// 钉的是"换一份"这件事本身（早先只 Attach 一次，没人试过换）。
    /// <para>
    /// ⚠ 它**测不到**同一次改动里补的那句退订（<c>Attach</c> 原先每次都往
    /// <c>area.Area.Changed</c> 挂新 lambda、从不摘旧的）：漏摘的后果是同一个变更多同步一遍，
    /// 而 <c>Sync</c> 读的是当前 VM，画出来一模一样。那是笔性能/卫生债，不改变可观测行为。
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void 换一个编辑区VM之后旧的不再牵动它()
    {
        var first = new EditorAreaViewModel();
        first.Open("a", "第一份", new TextBlock());

        var area = new ForgeEditorArea { Area = first };
        var window = new Window { Width = 700, Height = 300, Content = area };
        window.Show();
        Render(window);
        Assert.Contains(Tabs(area), x => x.Title == "第一份");

        var second = new EditorAreaViewModel();
        second.Open("b", "第二份", new TextBlock());
        area.Area = second;
        Render(window);

        Assert.Same(second, area.Area);
        Assert.Contains(Tabs(area), x => x.Title == "第二份");
        Assert.DoesNotContain(Tabs(area), x => x.Title == "第一份");

        // 旧的那份再动，画面不该跟着变
        first.Open("c", "旧的又开了一页", new TextBlock());
        Render(window);
        Assert.DoesNotContain(Tabs(area), x => x.Title == "旧的又开了一页");

        window.Close();
    }

    private static ToolWindowShellViewModel NewShell()
    {
        var infos = new List<WindowInfo>
        {
            new() { Id = "相机列表", Anchor = ToolWindowAnchor.Left, Order = 0, IsVisible = true, Weight = 0.3f },
        };

        return new ToolWindowShellViewModel(new ToolWindowManager(new DesktopLayout(infos)));
    }

    private static IEnumerable<ForgeStripeButton> Buttons(ForgeToolWindowFrame frame)
        => frame.GetVisualDescendants().OfType<ForgeStripeButton>();

    private static IEnumerable<ForgeEditorTab> Tabs(ForgeEditorArea area)
        => area.GetVisualDescendants().OfType<ForgeEditorTab>();

    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }
}
