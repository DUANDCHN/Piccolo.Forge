using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 停靠区的边尺寸**只有像素这一套语义**：面板拉多宽就一直多宽，
/// 窗口变大时多出来的地方全归编辑区。
/// </summary>
/// <remarks>
/// <c>weight</c> 只剩两处用场：没有记录时播一次种，以及反算回去写台账（存档格式仍是它）。
/// 排布不再经过它 —— 像素→weight→floor 回像素的那一来一回带着量化误差，
/// 窄尺寸下足够让边界每帧跳一格（实机："拖到小一点时标题栏一跳一跳"）。
/// </remarks>
public class DockPaneFixedSizeTests
{
    private static (ForgeDockPane Pane, Border Panel, Border Editor) Build()
    {
        var panel = new Border();
        ForgeDockPane.SetAnchor(panel, ToolWindowAnchor.Left);
        ForgeDockPane.SetWeight(panel, 0.25);

        var editor = new Border();
        ForgeDockPane.SetAnchor(editor, null);

        var pane = new ForgeDockPane();
        pane.Children.Add(panel);
        pane.Children.Add(editor);

        return (pane, panel, editor);
    }

    private static void LayoutAt(ForgeDockPane pane, double width, double height)
    {
        pane.Measure(new Size(width, height));
        pane.Arrange(new Rect(0, 0, width, height));
    }

    [AvaloniaFact]
    public void 面板宽度不随窗口变大()
    {
        var (pane, panel, editor) = Build();

        LayoutAt(pane, 1000, 500);
        Assert.Equal(250, panel.Bounds.Width);
        Assert.Equal(750, editor.Bounds.Width);

        // 窗口变宽 1000：面板一动不动，多出来的全给编辑区
        LayoutAt(pane, 2000, 500);
        Assert.Equal(250, panel.Bounds.Width);
        Assert.Equal(1750, editor.Bounds.Width);
    }

    /// <summary>拖分隔条给的是像素，直接落地，不再经过权重换算。</summary>
    [AvaloniaFact]
    public void 拖出来的像素直接生效()
    {
        var (pane, panel, editor) = Build();
        LayoutAt(pane, 1000, 500);

        pane.SetAnchorSize(ToolWindowAnchor.Left, 137);
        LayoutAt(pane, 1000, 500);

        // 137 就是 137 —— 不是 floor(1000 * (137/1000)) 之后可能差一格的那个数
        Assert.Equal(137, panel.Bounds.Width);
        Assert.Equal(863, editor.Bounds.Width);

        // 窗口再变，这个宽度照样不动
        LayoutAt(pane, 1600, 500);
        Assert.Equal(137, panel.Bounds.Width);
    }

    /// <summary>取消停靠（覆盖层）走的是另一条排布分支，用的是同一份像素记录。</summary>
    [AvaloniaFact]
    public void 取消停靠的面板也不随窗口变大()
    {
        var (pane, panel, _) = Build();
        ForgeDockPane.SetIsOverlay(panel, true);

        LayoutAt(pane, 1000, 500);
        Assert.Equal(250, panel.Bounds.Width);

        LayoutAt(pane, 2000, 500);
        Assert.Equal(250, panel.Bounds.Width);
    }

    [AvaloniaFact]
    public void 作废之后按新权重重新播种()
    {
        var (pane, panel, _) = Build();

        LayoutAt(pane, 1000, 500);
        Assert.Equal(250, panel.Bounds.Width);

        // 读档 / 恢复默认布局 / 换边 / 开合 → 宿主调 InvalidateAnchorSizes，之后按新 weight 播种
        ForgeDockPane.SetWeight(panel, 0.4);
        pane.InvalidateAnchorSizes();
        LayoutAt(pane, 1000, 500);
        Assert.Equal(400, panel.Bounds.Width);

        LayoutAt(pane, 2000, 500);
        Assert.Equal(400, panel.Bounds.Width);
    }

    [AvaloniaFact]
    public void 窗口比面板还窄时面板跟着缩且拉回来能复原()
    {
        var (pane, panel, _) = Build();

        LayoutAt(pane, 2000, 500);
        Assert.Equal(500, panel.Bounds.Width);

        // 500px 的面板塞进 400px 的窗口：夹住，不能把编辑区挤成负数
        LayoutAt(pane, 400, 500);
        Assert.True(panel.Bounds.Width <= 400, $"面板 {panel.Bounds.Width} 比 pane 还宽");

        // ⚠ 夹取不写回记录：窗口拉回来，面板要回到用户拉的那个宽度
        LayoutAt(pane, 2000, 500);
        Assert.Equal(500, panel.Bounds.Width);
    }
}
