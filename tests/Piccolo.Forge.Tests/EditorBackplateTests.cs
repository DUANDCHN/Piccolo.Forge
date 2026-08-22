using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Mvvm;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 编辑区背板：<see cref="ForgeEditorControl.BackplateFactory"/> 交出来的那一层。
/// </summary>
/// <remarks>
/// 背板画在正文**之下**，但只在这一组一张文档都没开时才可见 —— 文档内容允许透明底
/// （编辑区底色本来就是给它们当底的），一直画会从正文底下透出来。
/// <para>
/// 必须真渲一帧：不 realize 就建不出组宿主，用例会假绿。
/// </para>
/// </remarks>
public class EditorBackplateTests
{
    private const string Marker = "backplate";

    [AvaloniaFact]
    public void 空编辑区露出背板_开了文档就不画_关掉又回来()
    {
        var (window, control, vm) = Build();

        var backplate = Assert.Single(Backplates(control));
        Assert.True(backplate.IsVisible);

        vm.Open("d0", "相机 0", new TextBlock { Text = "0" });
        Render(window);
        Assert.False(Assert.Single(Backplates(control)).IsVisible);

        vm.Area.Close("d0");
        Render(window);
        Assert.True(Assert.Single(Backplates(control)).IsVisible);
    }

    [AvaloniaFact]
    public void 分屏后每个叶子各有一份()
    {
        var (window, control, vm) = Build();

        vm.Open("d0", "相机 0", new TextBlock { Text = "0" });
        vm.Open("d1", "相机 1", new TextBlock { Text = "1" });
        vm.Area.ArrangeGrid(["d0", "d1"], 0, 0);
        Render(window);

        // 一个控件只能有一个父 —— 工厂给的必须是两个不同实例，否则第二个叶子当场抛
        var backplates = Backplates(control);
        Assert.Equal(2, backplates.Count);
        Assert.NotSame(backplates[0], backplates[1]);
    }

    [AvaloniaFact]
    public void 接上VM之后再配工厂也补得上()
    {
        var vm = new EditorAreaViewModel();
        var control = new ForgeEditorControl();
        var window = new Window { Content = control, Width = 800, Height = 600 };
        control.Attach(vm);
        window.Show();
        Render(window);

        // 宿主的外观都是**建好控件才补**的（同 ExternalWindowFactory），
        // 那时第一轮同步早跑完了：只在建组那一刻取工厂的话，背板永远不会出现。
        Assert.Empty(Backplates(control));

        control.BackplateFactory = () => new Border { Tag = Marker };
        Render(window);

        Assert.True(Assert.Single(Backplates(control)).IsVisible);
    }

    [AvaloniaFact]
    public void 背板不接鼠标()
    {
        var (_, control, _) = Build();

        // 点在编辑区空白处仍然只是"点进编辑区"，不能被背板吃掉
        Assert.False(Assert.Single(Backplates(control)).IsHitTestVisible);
    }

    private static (Window Window, ForgeEditorControl Control, EditorAreaViewModel Vm) Build()
    {
        var vm = new EditorAreaViewModel();
        var control = new ForgeEditorControl { BackplateFactory = () => new Border { Tag = Marker } };
        var window = new Window { Content = control, Width = 800, Height = 600 };
        control.Attach(vm);
        window.Show();
        Render(window);
        return (window, control, vm);
    }

    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }

    private static List<Border> Backplates(ForgeEditorControl area) =>
        [.. area.GetVisualDescendants().OfType<Border>().Where(b => (string?)b.Tag == Marker)];
}
