using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 窗口变大时，面板保持像素宽度、多出来的地方归编辑区
/// （<see cref="ForgeDockPane.KeepAnchorSizeOnResize"/>）。
/// </summary>
/// <remarks>
/// 库的默认是上游那套按比例（weight 乘整个 pane），窗口一变宽两侧面板跟着一起变宽，
/// 中间那块的占比原地不动。本软件中间是一幅要尽量大的画面，所以宿主把这个开关打开。
/// </remarks>
public class DockPaneFixedSizeTests
{
    private static (ForgeDockPane Pane, Border Panel, Border Editor) Build(bool keep)
    {
        var panel = new Border();
        ForgeDockPane.SetAnchor(panel, ToolWindowAnchor.Left);
        ForgeDockPane.SetWeight(panel, 0.25);

        var editor = new Border();
        ForgeDockPane.SetAnchor(editor, null);

        var pane = new ForgeDockPane { KeepAnchorSizeOnResize = keep };
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
    public void 打开时面板宽度不随窗口变大()
    {
        var (pane, panel, editor) = Build(keep: true);

        LayoutAt(pane, 1000, 500);
        Assert.Equal(250, panel.Bounds.Width);
        Assert.Equal(750, editor.Bounds.Width);

        // 窗口变宽 1000：面板一动不动，多出来的全给编辑区
        LayoutAt(pane, 2000, 500);
        Assert.Equal(250, panel.Bounds.Width);
        Assert.Equal(1750, editor.Bounds.Width);
    }

    [AvaloniaFact]
    public void 关着时仍按比例缩放()
    {
        var (pane, panel, _) = Build(keep: false);

        LayoutAt(pane, 1000, 500);
        Assert.Equal(250, panel.Bounds.Width);

        LayoutAt(pane, 2000, 500);
        Assert.Equal(500, panel.Bounds.Width);
    }

    [AvaloniaFact]
    public void 用户改了权重之后按新权重重新锁一次()
    {
        var (pane, panel, _) = Build(keep: true);

        LayoutAt(pane, 1000, 500);
        Assert.Equal(250, panel.Bounds.Width);

        // 拖拽 / 读档 / 恢复默认布局都是改 weight —— 锁要作废
        ForgeDockPane.SetWeight(panel, 0.4);
        LayoutAt(pane, 1000, 500);
        Assert.Equal(400, panel.Bounds.Width);

        LayoutAt(pane, 2000, 500);
        Assert.Equal(400, panel.Bounds.Width);
    }

    /// <summary>取消停靠（覆盖层）走的是另一条排布分支，同样得锁像素。</summary>
    /// <remarks>
    /// 回归：第一版只改了停靠那条路（<c>ComputeSizes</c>），滑出的面板照旧按比例长 ——
    /// 而"取消停靠"恰恰是最容易看出这毛病的形态，它铺满整条边、宽度一眼可见。
    /// </remarks>
    [AvaloniaFact]
    public void 取消停靠的面板也不随窗口变大()
    {
        var (pane, panel, _) = Build(keep: true);
        ForgeDockPane.SetIsOverlay(panel, true);

        LayoutAt(pane, 1000, 500);
        Assert.Equal(250, panel.Bounds.Width);

        LayoutAt(pane, 2000, 500);
        Assert.Equal(250, panel.Bounds.Width);
    }

    [AvaloniaFact]
    public void 窗口比面板还窄时面板跟着缩()
    {
        var (pane, panel, _) = Build(keep: true);

        LayoutAt(pane, 2000, 500);
        Assert.Equal(500, panel.Bounds.Width);

        // 500px 的面板塞进 400px 的窗口：夹住，不能把编辑区挤成负数
        LayoutAt(pane, 400, 500);
        Assert.True(panel.Bounds.Width <= 400, $"面板 {panel.Bounds.Width} 比 pane 还宽");
    }
}
