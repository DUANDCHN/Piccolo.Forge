using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
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
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        var frame = new ForgeToolWindowFrame();
        frame.Attach(manager);

        var contents = frame.GetContentManager("Version Control");
        contents.Add(new ToolWindowContent("local", "Local Changes"));
        contents.Add(new ToolWindowContent("shelf", "Shelf"));

        manager.Activate("Version Control");

        var header = frame.DockPane.GetVisualDescendants().OfType<ForgeToolWindowHeader>().Single();
        Assert.Equal(2, header.Tabs.Children.Count);
        Assert.Equal(["Local Changes", "Shelf"], header.Tabs.Children.OfType<ForgeContentTab>().Select(x => x.Title));
        Assert.True(header.Tabs.Children.OfType<ForgeContentTab>().First().IsSelected);
    }

    [AvaloniaFact]
    public void A_combo_window_shows_only_the_selected_content()
    {
        // 默认布局里 Project 是 COMBO（defaultToolWindowlayoutProvider.kt:258-261）
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        var frame = new ForgeToolWindowFrame();
        frame.Attach(manager);

        var contents = frame.GetContentManager("Project");
        contents.Add(new ToolWindowContent("a", "Project"));
        contents.Add(new ToolWindowContent("b", "Packages"));

        manager.Activate("Project");

        var header = frame.DockPane.GetVisualDescendants().OfType<ForgeToolWindowHeader>().Single();
        Assert.Equal(ToolWindowContentUiType.Combo, header.ContentUiType);
        Assert.Single(header.Tabs.Children);
    }
}
