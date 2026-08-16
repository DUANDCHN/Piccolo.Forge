using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Mvvm;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 宿主把标签图标交成**控件实例**时的归属。
/// </summary>
/// <remarks>
/// 一个 Avalonia 控件只能有一个父，而同一页可以先后（甚至同时）落在两个标签容器上。
/// 收养前不先摘的话，第二个容器当场抛「already has a visual parent」——
/// 而这一次收养发生在标签集合变化的**同步回调**里，异常会炸穿编辑区 VM 的同步：
/// 表现不是「图标没了」，是**点了没反应**，再点一次少一个标签页。
/// <para>
/// 必须真渲一帧：不realize 容器就碰不到收养这一步，用例会假绿。
/// </para>
/// </remarks>
public class EditorTabIconOwnershipTests
{
    [AvaloniaFact]
    public void 宫格收回单栏时图标控件跟着换容器()
    {
        var (window, control, vm) = Build();

        vm.Area.ArrangeGrid(["d0", "d1"], 0, 0);
        Render(window);
        Assert.Equal(2, Strips(control).Count);

        var ex = Record.Exception(() => vm.Area.CollapseToSingleGroup());
        Render(window);

        Assert.Null(ex);
        var strip = Assert.Single(Strips(control));
        Assert.Equal(2, strip.ItemCount);

        // 两个标签都真的排上了（宽度非 0、不重叠）——少一个的话第二个会停在 x=0
        var tabs = strip.GetVisualDescendants().OfType<TabStripItem>().ToList();
        Assert.Equal(2, tabs.Count);
        Assert.All(tabs, t => Assert.True(t.Bounds.Width > 0));
        Assert.True(tabs[1].Bounds.X >= tabs[0].Bounds.Right);
    }

    [AvaloniaFact]
    public void 反复分屏与收拢不丢标签()
    {
        var (window, control, vm) = Build();

        for (var i = 0; i < 3; i++)
        {
            vm.Area.ArrangeGrid(["d0", "d1"], 0, 0);
            Render(window);
            Assert.Equal(2, Strips(control).Count);

            vm.Area.CollapseToSingleGroup();
            Render(window);
            Assert.Equal(2, Assert.Single(Strips(control)).ItemCount);
        }
    }

    private static (Window Window, ForgeEditorArea Control, EditorAreaViewModel Vm) Build()
    {
        var vm = new EditorAreaViewModel();
        var control = new ForgeEditorArea();
        var window = new Window { Content = control, Width = 800, Height = 600 };
        control.Attach(vm);
        window.Show();

        // 宿主交的是控件**实例**（不是 Func<Control> 工厂）——这条路必须也稳
        var d0 = vm.Open("d0", "相机 0", new TextBlock { Text = "0" });
        d0.Icon = new Border { Width = 16, Height = 16 };
        var d1 = vm.Open("d1", "相机 1", new TextBlock { Text = "1" });
        d1.Icon = new Border { Width = 16, Height = 16 };

        Render(window);
        return (window, control, vm);
    }

    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }

    private static List<TabStrip> Strips(ForgeEditorArea area) =>
        [.. area.GetVisualDescendants().OfType<TabStrip>()];
}
