using System;
using System.Linq;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 内容区的 tab 与 combo。
public class ContentTabTests
{
    [Fact]
    public void The_first_content_is_selected_automatically()
    {
        var contents = new ToolWindowContentManager();
        contents.Add(new ToolWindowContent("a", "Local Changes"));
        contents.Add(new ToolWindowContent("b", "Shelf"));

        Assert.Equal("a", contents.SelectedContent!.Value.Id);
        Assert.False(contents.IsSoleContent);
    }

    [Fact]
    public void Removing_the_selected_content_moves_the_selection_back()
    {
        var contents = new ToolWindowContentManager();
        contents.Add(new ToolWindowContent("a", "A"));
        contents.Add(new ToolWindowContent("b", "B"));
        contents.Select("b");

        contents.Remove("b");

        Assert.Equal("a", contents.SelectedContent!.Value.Id);
        Assert.True(contents.IsSoleContent);
    }

    [Fact]
    public void Removing_the_last_content_clears_the_selection()
    {
        var contents = new ToolWindowContentManager();
        contents.Add(new ToolWindowContent("a", "A"));

        contents.Remove("a");

        Assert.Null(contents.SelectedContent);
    }

    [AvaloniaFact]
    public void A_tab_is_as_wide_as_its_text_plus_two_insets()
    {
        // ContentLabel.java:265-269 —— 新 UI 下左右内边距是 12
        var tab = new ForgeContentTab { Title = "Shelf" };
        tab.Measure(new Size(double.PositiveInfinity, 41));

        Assert.Equal(12, ForgeContentTab.Inset);
        Assert.True(tab.DesiredSize.Width > 24);
        Assert.True(tab.DesiredSize.Width <= ForgeMetrics.TabMaxWidth);
    }

    [AvaloniaFact]
    public void A_very_long_tab_is_capped_at_400()
    {
        // ContentTabLabel.java:48 · MAX_WIDTH
        var tab = new ForgeContentTab { Title = new string('W', 500) };
        tab.Measure(new Size(double.PositiveInfinity, 41));

        Assert.Equal(ForgeMetrics.TabMaxWidth, tab.DesiredSize.Width);
    }

    [Fact]
    public void The_underline_sits_flush_with_the_bottom_of_the_tab()
    {
        // JBDefaultTabPainter.kt:92-96 —— (x, y + height - thickness, width, thickness)
        // ToolWindowTabPainter.kt:35-36 先 y += 1、height -= 1，
        // 两下抵消：**底边还在 41**，被抬高的是画背景那块的顶边。
        var rect = ForgeContentTab.ComputeUnderlineRect(new Rect(0, 0, 100, 41));

        Assert.Equal(ForgeMetrics.HeaderTabUnderlineHeight, rect.Height);
        Assert.Equal(100, rect.Width);
        Assert.Equal(41, rect.Bottom);
        Assert.Equal(41 - ForgeMetrics.HeaderTabUnderlineHeight, rect.Y);
    }

    [AvaloniaFact]
    public void Tabs_start_after_the_title()
    {
        // TabContentLayout.java:178-184 —— TAB_LAYOUT_START(4) + idLabel 宽
        var header = new ForgeToolWindowHeader { Title = "Version Control" };
        header.Measure(new Size(600, double.PositiveInfinity));

        Assert.True(header.ComputeTabsStart() > ForgeMetrics.TabLayoutStart + ForgeMetrics.HeaderLabelInsetLeft);
    }

    [AvaloniaFact]
    public void Tabs_that_do_not_fit_are_squashed_to_zero_width()
    {
        // TabContentLayout.java:161 —— 被藏起来的 tab 就是宽度为 0 的那些
        var panel = new HeaderTabsPanel();
        for (var i = 0; i < 5; i++)
        {
            panel.Children.Add(new ForgeContentTab { Title = $"Content {i}" });
        }

        panel.Measure(new Size(double.PositiveInfinity, 41));
        panel.Arrange(new Rect(0, 0, 120, 41));

        Assert.True(panel.HiddenTabCount > 0);
        Assert.Contains(panel.Children, x => x.Bounds.Width == 0);
    }

    [AvaloniaFact]
    public void The_frame_puts_tabs_into_the_header_row()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var manager = shell.Manager;
        var frame = new ForgeControl();
        frame.Attach(shell);

        var contents = frame.GetContentManager("Version Control");
        contents.Add(new ToolWindowContent("local", "Local Changes"));
        contents.Add(new ToolWindowContent("shelf", "Shelf"));

        manager.Activate("Version Control");

        var header = frame.DockPane.GetVisualDescendants().OfType<ForgeToolWindowHeader>().Single();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, header.Tabs.ItemCount);
        Assert.Equal(
            ["Local Changes", "Shelf"],
            Assert.IsAssignableFrom<IEnumerable<ToolWindowContent>>(header.Tabs.ItemsSource)
                .Select(x => x.DisplayName));
        Assert.Equal(contents.SelectedContent, header.Tabs.SelectedItem);
    }

    [AvaloniaFact]
    public void A_combo_window_shows_only_the_selected_content()
    {
        // 默认布局里 Project 是 COMBO（defaultToolWindowlayoutProvider.kt:258-261）
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var manager = shell.Manager;
        var frame = new ForgeControl();
        frame.Attach(shell);

        var contents = frame.GetContentManager("Project");
        contents.Add(new ToolWindowContent("a", "Project"));
        contents.Add(new ToolWindowContent("b", "Packages"));

        manager.Activate("Project");

        var header = frame.DockPane.GetVisualDescendants().OfType<ForgeToolWindowHeader>().Single();
        Assert.Equal(ToolWindowContentUiType.Combo, header.ContentUiType);
        Assert.Equal(1, header.Tabs.ItemCount);
    }

    // ── 顺序的持久化：拖出来的次序要能存进布局、再套回来 ──────────────

    [Fact]
    public void 内容tab的顺序存得住也套得回来()
    {
        var saved = NewShell();
        var panel = saved.Add("camParam", "相机参数", ToolWindowAnchor.Right);
        panel.Contents.Add(new ToolWindowContent("common", "快捷属性"));
        panel.Contents.Add(new ToolWindowContent("tree", "属性树"));

        // 用户把「属性树」拖到最前
        panel.Contents.Reorder("tree", "common");
        var xml = saved.SaveLayout();

        // 重启：面板与内容按登记时的原始次序重新建起来
        var restored = NewShell();
        var fresh = restored.Add("camParam", "相机参数", ToolWindowAnchor.Right);
        fresh.Contents.Add(new ToolWindowContent("common", "快捷属性"));
        fresh.Contents.Add(new ToolWindowContent("tree", "属性树"));

        restored.RestoreLayout(xml);

        Assert.Equal(["tree", "common"], fresh.Contents.Contents.Select(c => c.Id));
    }

    [Fact]
    public void 存档里没有的那一块留在原地()
    {
        // 存档是老版本写的（那时还没有「图像预处理」这一块），新增的一块不能因此被甩丢
        var saved = NewShell();
        var panel = saved.Add("camParam", "相机参数", ToolWindowAnchor.Right);
        panel.Contents.Add(new ToolWindowContent("tree", "属性树"));
        panel.Contents.Add(new ToolWindowContent("common", "快捷属性"));
        var xml = saved.SaveLayout();

        var restored = NewShell();
        var fresh = restored.Add("camParam", "相机参数", ToolWindowAnchor.Right);
        fresh.Contents.Add(new ToolWindowContent("common", "快捷属性"));
        fresh.Contents.Add(new ToolWindowContent("preprocess", "图像预处理"));
        fresh.Contents.Add(new ToolWindowContent("tree", "属性树"));

        restored.RestoreLayout(xml);

        // 存档里出现过的两块按存档排（tree 在 common 前），没出现过的那块仍在
        var ids = fresh.Contents.Contents.Select(c => c.Id).ToArray();
        Assert.Contains("preprocess", ids);
        Assert.True(Array.IndexOf(ids, "tree") < Array.IndexOf(ids, "common"));
    }

    /// <summary>只要 VM 层 —— 顺序的存取不碰控件，别在这儿造 ForgeControl（那要头less 平台起字体管理器）。</summary>
    private static ToolWindowShellViewModel NewShell()
        => new(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
}
