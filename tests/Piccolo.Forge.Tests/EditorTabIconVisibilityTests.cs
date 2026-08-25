using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 文档标签右边那个图标什么时候画，以及 Islands 那个内缩块的圆角取哪来。
/// </summary>
/// <remarks>
/// 两条都是实机反馈逼出来的（2026-08-25）：
/// <list type="bullet">
/// <item>
/// <b>叉一直画着</b>：一排标签就是一排 ×，噪点盖过标题，且容易误点。
/// 改成**选中或悬停**才出；<b>图钉不受这条管</b> —— 它是状态指示，藏了就看不出标签被固定住。
/// </item>
/// <item>
/// <b>圆角明显比别的东西大一圈</b>：上游取的是主工具栏按钮的 arc（12 直径 → 半径 6），
/// 而全库其余矩形是 <c>Forge.CornerRadius</c>（4）。现在改由资源键喂。
/// </item>
/// </list>
/// </remarks>
public class EditorTabIconVisibilityTests
{
    /// <summary>没选中也没悬停时不画叉；选中就画。</summary>
    [AvaloniaFact]
    public void 叉只在选中或悬停时出()
    {
        var (window, tab) = Host();

        tab.CanBeClosed = true;
        tab.IsSelected = false;
        Render(window);
        Assert.False(TabIcon(tab).IsVisible);

        tab.IsSelected = true;
        Render(window);
        Assert.True(TabIcon(tab).IsVisible);
        Assert.Same(ForgeIcons.Close, TabIcon(tab).Icon);

        window.Close();
    }

    /// <summary>鼠标移上去也画 —— 用真的指针移动验，不是拨伪类。</summary>
    [AvaloniaFact]
    public void 鼠标移到标签上就把叉亮出来()
    {
        var (window, tab) = Host();

        tab.CanBeClosed = true;
        tab.IsSelected = false;
        Render(window);
        Assert.False(TabIcon(tab).IsVisible);

        // 移到标签**左半边**（避开右边那个图标自己的悬停区）
        var p = tab.TranslatePoint(new Point(tab.Bounds.Width / 4, tab.Bounds.Height / 2), window)!.Value;
        window.MouseMove(p);
        Render(window);
        Assert.True(TabIcon(tab).IsVisible);

        window.Close();
    }

    /// <summary>
    /// 图钉不受"选中或悬停"那条管：固定住的标签任何时候都看得见它。
    /// </summary>
    /// <remarks>
    /// 这条与上面那条是**一对**：把叉藏起来时最容易顺手把图钉一起藏了，
    /// 而那样一来"哪几个标签被固定住"就没有任何地方看得出来。
    /// </remarks>
    [AvaloniaFact]
    public void 图钉不跟着藏()
    {
        var (window, tab) = Host();

        tab.CanBeClosed = true;
        tab.IsPinned = true;
        tab.IsSelected = false;
        Render(window);

        Assert.True(TabIcon(tab).IsVisible);
        Assert.Same(ForgeIcons.Pin, TabIcon(tab).Icon);
        Assert.True(TabIcon(tab).IsFilled);

        window.Close();
    }

    /// <summary>
    /// Islands 那个块的圆角跟着资源键走，并补上那半像素。
    /// </summary>
    /// <remarks>
    /// 半像素的来由见 <see cref="ForgeEditorTab.DecorationMargin"/>：块本身外扩了 0.5，
    /// 圆角要跟着加同样的量，画出来才正好是键上写的那个数。
    /// </remarks>
    [AvaloniaFact]
    public void 块圆角跟着资源键并补了那半像素()
    {
        using var theme = ForgeThemeScope.Use(ForgeThemeFamily.Islands);
        var (window, tab) = Host(islands: true);

        // 主题喂的是 Forge.CornerRadius（4），不再是上游那个 12/2
        Assert.Equal(new CornerRadius(4), tab.BlockCornerRadius);
        Assert.Equal(new CornerRadius(4.5), tab.DecorationCornerRadius);
        Assert.NotEqual(ForgeMetrics.IslandsEditorTabArc / 2, tab.BlockCornerRadius.TopLeft);

        window.Close();
    }

    /// <summary>expUI 一族不画那个块，圆角恒为 0（喂了键也一样）。</summary>
    [AvaloniaFact]
    public void 经典档不画块所以圆角恒为零()
    {
        var (window, tab) = Host();

        Assert.Equal(default, tab.DecorationCornerRadius);

        window.Close();
    }

    private static (Window Window, ForgeEditorTab Tab) Host(bool islands = false)
    {
        var vm = new global::Piccolo.Forge.Mvvm.EditorAreaViewModel();
        var control = new ForgeEditorControl();
        var window = new Window { Content = control, Width = 800, Height = 600 };
        control.Attach(vm);
        window.Show();

        vm.Open("d0", "相机 0", new TextBlock { Text = "0" });
        Render(window);

        var tab = control.GetVisualDescendants().OfType<ForgeEditorTab>().First();
        if (islands)
        {
            tab.Palette = ForgePalette.IslandsDark;
            Render(window);
        }

        return (window, tab);
    }

    private static ForgeGeometryIcon TabIcon(ForgeEditorTab tab)
        => tab.GetVisualDescendants().OfType<ForgeGeometryIcon>().Single(x => x.Name == "PART_TabIcon");

    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }
}
