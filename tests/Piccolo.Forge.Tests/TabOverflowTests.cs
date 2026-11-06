using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Mvvm;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 文档标签行放不下时怎么排：<b>换行，一个都不藏</b>。
/// </summary>
/// <remarks>
/// 回归过两轮：
/// <list type="number">
/// <item>原实现只照抄了上游「放不下 → 压成 0 宽」一句，窄窗格里**整排标签消失**；</item>
/// <item>接着试过「起点往后挪、保住选中的」，那会让**左边的标签凭空消失**——同样是丢东西。</item>
/// </list>
/// 文档是用户自己开的，而标签行既没有「更多」弹窗也没有滚动箭头，藏起来就等于找不回来。
/// 所以编辑区这一行改成换行排布。
/// </remarks>
public class TabOverflowTests
{
    /// <summary>文件名超过标签上限时，标题仍必须留在图标与关闭按钮之间。</summary>
    [AvaloniaTheory]
    [InlineData(false, 800, false)]
    [InlineData(true, 800, false)]
    [InlineData(false, 260, true)]
    [InlineData(true, 260, true)]
    public void 长文件名不会覆盖关闭按钮或相邻标签(bool islands, int width, bool pinned)
    {
        using var theme = ForgeThemeScope.Use(islands
            ? Piccolo.Forge.Themes.ForgeThemeFamily.Islands
            : Piccolo.Forge.Themes.ForgeThemeFamily.ExpUi);
        var tab = new ForgeEditorTab
        {
            Title = new string('W', 120) + ".tiff",
            TitleFontWeight = global::Avalonia.Media.FontWeight.Bold,
            Palette = islands ? Piccolo.Forge.Themes.ForgePalette.IslandsLight : Piccolo.Forge.Themes.ForgePalette.ExpUiLight,
            Icon = new TextBlock { Text = "▣" },
            IsSelected = true,
            CanBeClosed = true,
            IsPinned = pinned,
        };
        var panel = new HeaderTabsPanel { WrapRows = true };
        panel.Children.Add(tab);
        panel.Children.Add(new ForgeEditorTab { Title = "next.tiff" });
        var window = new Window { Width = width, Height = 160, Content = panel };
        try
        {
            window.Show();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var title = tab.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PART_Title");
            var close = tab.GetVisualDescendants().OfType<ForgeGeometryIcon>().Single(t => t.Name == "PART_TabIcon");
            Assert.Equal(global::Avalonia.Media.TextTrimming.CharacterEllipsis, title.TextTrimming);
            var titleRight = title.TranslatePoint(new Point(title.Bounds.Width, 0), tab)!.Value.X;
            var closeLeft = close.TranslatePoint(default, tab)!.Value.X;
            Assert.True(titleRight <= closeLeft, "长标题必须为关闭按钮预留空间。");
            Assert.True(tab.Bounds.Width <= Piccolo.Forge.Themes.ForgeMetrics.TabMaxWidth);
            Assert.True(title.TextLayout.WidthIncludingTrailingWhitespace <= title.Bounds.Width);
        }
        finally { window.Close(); }
    }

    private static HeaderTabsPanel Panel(int count, bool wrap = true)
    {
        var panel = new HeaderTabsPanel { WrapRows = wrap };
        for (var i = 0; i < count; i++)
        {
            panel.Children.Add(new ForgeEditorTab { Title = $"文档 {i}", IsSelected = i == 0 });
        }

        panel.Measure(new Size(double.PositiveInfinity, 41));
        return panel;
    }

    [AvaloniaFact]
    public void 放不下就换行而不是藏起来()
    {
        var panel = Panel(4);
        var one = panel.Children[0].DesiredSize.Width;

        // 一行只放得下两个
        panel.Measure(new Size(one * 2, double.PositiveInfinity));
        var rowHeight = panel.Children[0].DesiredSize.Height;
        panel.Arrange(new Rect(0, 0, one * 2, panel.DesiredSize.Height));

        // 四个全在，宽度都是完整的
        Assert.All(panel.Children, x => Assert.Equal(one, x.Bounds.Width, 1.0));
        Assert.Equal(0, panel.HiddenTabCount);

        // 排成两行，面板也跟着长高
        Assert.Equal(2, panel.Children.Select(x => x.Bounds.Y).Distinct().Count());
        Assert.Equal(rowHeight * 2, panel.DesiredSize.Height, 1.0);
    }

    [AvaloniaFact]
    public void 一行放得下就不换行()
    {
        var panel = Panel(3);
        var total = panel.Children.Sum(x => x.DesiredSize.Width);

        panel.Measure(new Size(total + 100, double.PositiveInfinity));
        var rowHeight = panel.Children[0].DesiredSize.Height;
        panel.Arrange(new Rect(0, 0, total + 100, panel.DesiredSize.Height));

        Assert.Single(panel.Children.Select(x => x.Bounds.Y).Distinct());
        Assert.Equal(rowHeight, panel.DesiredSize.Height, 1.0);
    }

    [AvaloniaFact]
    public void 单个标签比整行还宽时收到行宽而不是消失()
    {
        var panel = Panel(2);
        var one = panel.Children[0].DesiredSize.Width;
        var narrow = one / 2;

        panel.Measure(new Size(narrow, double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, narrow, panel.DesiredSize.Height));

        Assert.All(panel.Children, x => Assert.Equal(narrow, x.Bounds.Width, 1.0));
        Assert.Equal(2, panel.Children.Select(x => x.Bounds.Y).Distinct().Count());
    }

    /// <summary>工具窗口的标题条高度固定、不换行，但也不能整排消失。</summary>
    [AvaloniaFact]
    public void 标题条不换行但第一个越界的仍要看得见()
    {
        var panel = Panel(3, wrap: false);
        var one = panel.Children[0].DesiredSize.Width;

        panel.Arrange(new Rect(0, 0, one / 2, 41));

        Assert.True(panel.Children[0].Bounds.Width > 0, "标题条第一个标签也没了");
    }

    /// <summary>真接上编辑区：窄窗格里标签仍然一个不少。</summary>
    [AvaloniaFact]
    public void 编辑区窄窗格里标签一个不少()
    {
        var vm = new EditorAreaViewModel();
        var area = new ForgeEditorControl();
        var window = new Window { Content = area, Width = 260, Height = 400 };
        area.Attach(vm);
        window.Show();

        for (var i = 0; i < 4; i++)
        {
            vm.Open($"d{i}", $"很长很长的文档标题 {i}", new TextBlock());
        }

        Dispatcher.UIThread.RunJobs();

        var tabs = area.GetVisualDescendants().OfType<ForgeEditorTab>().ToList();
        Assert.Equal(4, tabs.Count);
        Assert.All(tabs, t => Assert.True(t.Bounds.Width > 0, $"「{t.Title}」被藏起来了"));
    }
}
