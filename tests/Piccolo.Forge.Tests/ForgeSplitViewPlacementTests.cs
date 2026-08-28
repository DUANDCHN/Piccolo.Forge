using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// <see cref="SplitView.PanePlacement"/> 由**模板**落实，本用例钉住它真的被落实了。
/// </summary>
/// <remarks>
/// <b>这条用例的由来（2026-08-28）。</b>控件侧对 PanePlacement 只做一件事：把方向记成
/// <c>:left</c> / <c>:right</c> 伪类，自己不挪任何东西。模板要是不响应这两个伪类，
/// 宿主写 <c>PanePlacement="Right"</c> 就是**静默失效** —— 不报错、不警告、连绑定错误都没有，
/// 面板安安静静待在左边。LDVision 的快照文档正是这么中的招：它的属性面板一直写着 Right，
/// 换到本主题之后跑去了左边，排查时最先怀疑的还是页面自己摆错了位置。
/// <para>
/// 断言取 <b>面板与内容实际落在窗口的哪一侧、各占多宽</b>，不取"模板里是不是用了某个容器"。
/// 前者是宿主真正依赖的行为，换个实现照样成立；后者换个写法就得跟着改，守不住东西。
/// </para>
/// </remarks>
public class ForgeSplitViewPlacementTests
{
    /// <summary>默认（左置）：面板贴左缘，内容排在它右边。</summary>
    [AvaloniaFact]
    public void 默认左置时面板贴左缘()
    {
        var (window, split) = Host(SplitViewPanePlacement.Left);

        Assert.Equal(0, PaneRoot(split).Bounds.X);
        Assert.Equal(PaneWidth, ContentHost(split).Bounds.X);

        window.Close();
    }

    /// <summary>右置：面板贴右缘，内容从左缘开始。</summary>
    [AvaloniaFact]
    public void 右置时面板贴右缘()
    {
        var (window, split) = Host(SplitViewPanePlacement.Right);

        Assert.Equal(WindowWidth - PaneWidth, PaneRoot(split).Bounds.X);
        Assert.Equal(0, ContentHost(split).Bounds.X);

        window.Close();
    }

    /// <summary>
    /// 换边之后**内容仍然吃掉剩余宽度**，面板仍然是它自己那个宽。
    /// <para>
    /// 单列一条是因为"换边"最容易做成半吊子：位置挪对了、宽度分配却反过来——
    /// 面板被撑满整窗、内容缩成一条。那种状态同样不报错，只有肉眼能发现。
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void 右置后宽度分配不变()
    {
        var (window, split) = Host(SplitViewPanePlacement.Right);

        Assert.Equal(PaneWidth, PaneRoot(split).Bounds.Width);
        Assert.Equal(WindowWidth - PaneWidth, ContentHost(split).Bounds.Width);

        window.Close();
    }

    /// <summary>运行期改方向也要跟着挪 —— 伪类是活的，模板的响应不能只在首次套模板时成立。</summary>
    [AvaloniaFact]
    public void 运行期改方向面板跟着挪()
    {
        var (window, split) = Host(SplitViewPanePlacement.Left);
        Assert.Equal(0, PaneRoot(split).Bounds.X);

        split.PanePlacement = SplitViewPanePlacement.Right;
        Relayout(window);
        Assert.Equal(WindowWidth - PaneWidth, PaneRoot(split).Bounds.X);

        split.PanePlacement = SplitViewPanePlacement.Left;
        Relayout(window);
        Assert.Equal(0, PaneRoot(split).Bounds.X);

        window.Close();
    }

    private const double WindowWidth = 400;
    private const double PaneWidth = 120;

    private static Control PaneRoot(SplitView split) => Part(split, "PART_PaneRoot");

    private static Control ContentHost(SplitView split) => Part(split, "PART_ContentPresenter");

    private static Control Part(SplitView split, string name) =>
        split.GetVisualDescendants().OfType<Control>().First(c => c.Name == name);

    private static (Window Window, SplitView Split) Host(SplitViewPanePlacement placement)
    {
        var split = new SplitView
        {
            DisplayMode = SplitViewDisplayMode.Inline,
            IsPaneOpen = true,
            OpenPaneLength = PaneWidth,
            PanePlacement = placement,
            Pane = new Border(),
            Content = new Border(),
        };

        var window = new Window
        {
            Width = WindowWidth,
            Height = 200,
            Content = split,
        };

        window.Show();
        Relayout(window);
        return (window, split);
    }

    private static void Relayout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }
}
