using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 一条边被两个窗口分掉时，中间那条可拖的分割线。
/// </summary>
/// <remarks>
/// 对应 <c>ToolWindowPane.kt:617 · MySplitter</c>（OnePixelSplitter），拖出来的是 <c>sideWeight</c>。
/// <para>
/// 回归：本仓原来只给**四条边与编辑区之间**造了热区（每个 anchor 一个），
/// 格内那条分割线压根没有控件 —— 下面左右两块都打开时，中间那条线拖不动。
/// </para>
/// </remarks>
public class SplitDividerTests
{
    private static (ForgeControl Frame, ToolWindowShellViewModel Shell) Show()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var frame = new ForgeControl();
        frame.Attach(shell);

        var window = new Window { Width = 900, Height = 600, Content = frame };
        window.Show();
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (frame, shell);
    }

    private static IEnumerable<ForgeDockResizer> SplitDividers(ForgeControl frame)
        => frame.DockPane.Children
            .OfType<ForgeDockResizer>()
            .Where(x => x.IsSplitDivider);

    [AvaloniaFact]
    public void No_divider_until_both_halves_are_open()
    {
        var (frame, shell) = Show();

        // 只开非 split 的那半
        var id = shell.ToolWindows.First().Id;
        shell.Manager.MoveTo(id, ToolWindowAnchor.Bottom, -1, isSplit: false);
        shell.Manager.Activate(id);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Empty(SplitDividers(frame));
    }

    [AvaloniaFact]
    public void Opening_both_halves_creates_a_draggable_divider()
    {
        var (frame, shell) = Show();
        var ids = shell.ToolWindows.Select(x => x.Id).ToList();

        shell.Manager.MoveTo(ids[0], ToolWindowAnchor.Bottom, -1, isSplit: false);
        shell.Manager.Activate(ids[0]);
        shell.Manager.MoveTo(ids[1], ToolWindowAnchor.Bottom, -1, isSplit: true);
        shell.Manager.Activate(ids[1]);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var divider = Assert.Single(SplitDividers(frame));

        // 横边左右分 → 分割线是**竖**的一条，宽度是鼠标热区 6
        Assert.Equal(ForgeDockResizer.MouseZone, divider.Bounds.Width);
        Assert.True(divider.Bounds.Height > divider.Bounds.Width, "横边的分割线应当是竖着的一条");

        // 它得落在底边那一格里，而不是整个 pane 的正中
        Assert.NotNull(divider.SlotBounds);
        Assert.True(
            divider.Bounds.Center.X > divider.SlotBounds!.Value.X,
            "分割线跑到了它所在那一格的外面");
    }

    [AvaloniaTheory]
    [InlineData(ForgeThemeFamily.ExpUi, true)]
    [InlineData(ForgeThemeFamily.Islands, false)]
    public void 非岛配色下这条线要画出来(ForgeThemeFamily family, bool expectLine)
    {
        // 上游那条是 OnePixelSplitter（ToolWindowPane.kt:617）——**看得见的一条线**。
        // 岛配色例外：两座岛之间本来就有缝，再画一条等于把缝糊死。
        // 回归：expUI 下这条线一直没画，实机表现是"下面左右两个面板打开时中间没有分割线"。
        var (frame, shell) = Show();
        frame.Appearance = frame.Appearance with { Family = family };

        var ids = shell.ToolWindows.Select(x => x.Id).ToList();
        shell.Manager.MoveTo(ids[0], ToolWindowAnchor.Bottom, -1, isSplit: false);
        shell.Manager.Activate(ids[0]);
        shell.Manager.MoveTo(ids[1], ToolWindowAnchor.Bottom, -1, isSplit: true);
        shell.Manager.Activate(ids[1]);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var divider = Assert.Single(SplitDividers(frame));
        Assert.Equal(expectLine, divider.LineBrush is not null);

        // 颜色必须是**面板四周那圈边**的同一个键（WindowBorder / 上游 Borders.color）——
        // 取成 EditorTabsBorder 会明显偏浅，实机一眼看得出"这条是灰的、旁边那些是黑的"。
        if (expectLine)
        {
            var expected = ForgePalette.ForTheme(family, frame.ActualThemeVariant).WindowBorder;
            Assert.Equal(expected, Assert.IsType<SolidColorBrush>(divider.LineBrush).Color);
        }
    }
}
