using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 标签行放不下时怎么排（<c>TabContentLayout.java:224-272 · layout</c>）。
/// </summary>
/// <remarks>
/// 回归：本仓原来只照抄了「放不下 → 压成 0 宽」那一句，漏掉了排布阶段的
/// <c>reachedBounds</c>「第一个越界的用剩余宽度画出来」，也没有「选中的一定看得见」。
/// 后果不是少显示几个，而是**一个都不显示**：窄窗格里第一个标签就放不下，
/// 从它起全被压成 0，标签行整条空白。
/// </remarks>
public class TabOverflowTests
{
    private static HeaderTabsPanel Panel(int count, int selected = 0)
    {
        var panel = new HeaderTabsPanel();
        for (var i = 0; i < count; i++)
        {
            panel.Children.Add(new ForgeEditorTab { Title = $"文档 {i}", IsSelected = i == selected });
        }

        panel.Measure(new Size(double.PositiveInfinity, 41));
        return panel;
    }

    [AvaloniaFact]
    public void 窄到放不下一个标签时也要画出半个()
    {
        var panel = Panel(3);
        var full = panel.Children[0].DesiredSize.Width;
        var narrow = full / 3;

        panel.Arrange(new Rect(0, 0, narrow, 41));

        // 关键：不是全空。至少有一个占着实际宽度（被切掉一截，但看得见）
        var shown = panel.Children.Where(x => x.Bounds.Width > 0).ToList();
        Assert.NotEmpty(shown);
        // 排布会按布局像素取整，比到 1px 就够
        Assert.Equal(narrow, shown.Sum(x => x.Bounds.Width), 1.0);
    }

    [AvaloniaFact]
    public void 第一个越界的被切一半而不是消失()
    {
        var panel = Panel(3);
        var full = panel.Children[0].DesiredSize.Width;

        // 一个半的宽度
        panel.Arrange(new Rect(0, 0, full * 1.5, 41));

        var widths = panel.Children.Select(x => x.Bounds.Width).ToList();
        Assert.Equal(full, widths[0], 3);                    // 第一个整格
        Assert.InRange(widths[1], 0.1, full - 0.1);          // 第二个被切一半
        Assert.Equal(0, widths[2], 3);                       // 再往后才真的没有
    }

    [AvaloniaFact]
    public void 选中的那个一定看得见()
    {
        // 选中最后一个，宽度只够一个 —— 留下来的必须是它
        var panel = Panel(4, selected: 3);
        var full = panel.Children[0].DesiredSize.Width;

        panel.Arrange(new Rect(0, 0, full, 41));

        var selected = panel.Children[3];
        Assert.True(selected.Bounds.Width > 0, "选中的标签被挤出了标签行");
    }

    [AvaloniaFact]
    public void 宽度够时人人整格()
    {
        var panel = Panel(3);
        var full = panel.Children.Sum(x => x.DesiredSize.Width);

        panel.Arrange(new Rect(0, 0, full + 100, 41));

        Assert.Equal(0, panel.HiddenTabCount);
        Assert.All(panel.Children, x => Assert.True(x.Bounds.Width > 0));
    }
}
