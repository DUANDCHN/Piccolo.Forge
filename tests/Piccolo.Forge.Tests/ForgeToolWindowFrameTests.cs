using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 条带 + 停靠区 + manager 三者接起来之后的整体行为。
public class ForgeToolWindowFrameTests
{
    [AvaloniaFact]
    public void Default_layout_puts_every_bottom_button_on_the_left_stripe()
    {
        var (frame, _) = Build();

        // 默认布局：左上组 Project/Commit/分隔符/Structure；左下组是 5 个 BOTTOM 的
        Assert.Equal(4, frame.LeftToolbar.TopGroup.Children.Count);
        Assert.Equal(5, frame.LeftToolbar.BottomGroup.Children.Count);

        // 右条带下组是空的 —— 默认布局里没有 isSplit=true 的 BOTTOM 窗口
        Assert.Empty(frame.RightToolbar.BottomGroup.Children);
        Assert.Equal(5, frame.RightToolbar.TopGroup.Children.Count);
    }

    [AvaloniaFact]
    public void Left_top_group_has_a_separator_before_the_split_window()
    {
        var (frame, _) = Build();

        // Structure 是左边唯一的 split（defaultToolWindowlayoutProvider.kt:263-266）
        Assert.IsType<ForgeStripeSeparator>(frame.LeftToolbar.TopGroup.Children[2]);
    }

    [AvaloniaFact]
    public void Bottom_group_is_ordered_bottom_to_top()
    {
        // AbstractDroppableStripe.kt:63-67
        var (frame, _) = Build();

        var texts = frame.LeftToolbar.BottomGroup.Children
            .OfType<ForgeStripeButton>()
            .Select(x => x.Text)
            .ToList();

        // 默认次序是 Version Control, Problems, Problems View, Terminal, Services（order 0..4），
        // 倒序后首字母是 S, T, P, P, V
        Assert.Equal(["S", "T", "P", "P", "V"], texts);
    }

    [AvaloniaFact]
    public void Showing_a_window_adds_it_to_the_dock_pane()
    {
        var (frame, manager) = Build();
        Assert.Empty(DockedWindows(frame));

        manager.Activate("Project");

        var window = Assert.Single(DockedWindows(frame));
        Assert.Equal(ToolWindowAnchor.Left, ForgeDockPane.GetAnchor(window));
    }

    [AvaloniaFact]
    public void Showing_the_other_window_in_the_same_slot_replaces_it()
    {
        var (frame, manager) = Build();

        manager.Activate("Project");
        manager.Activate("Commit");

        Assert.Single(DockedWindows(frame));
    }

    [AvaloniaFact]
    public void Both_halves_of_the_left_anchor_can_be_open_at_once()
    {
        var (frame, manager) = Build();

        manager.Activate("Project");
        manager.Activate("Structure");   // isSplit=true，是另一格

        Assert.Equal(2, DockedWindows(frame).Count);
    }

    [AvaloniaFact]
    public void The_button_of_a_visible_window_reports_it()
    {
        var (frame, manager) = Build();
        manager.Activate("Project");

        var project = frame.LeftToolbar.TopGroup.Children
            .OfType<ForgeStripeButton>()
            .First(x => x.Text == "P");

        Assert.True(project.IsToolWindowVisible);
        Assert.True(project.IsToolWindowActive);
        Assert.Equal(project.Palette.ButtonSelectedBackground, project.ResolveBackground());
    }

    [AvaloniaFact]
    public void Activating_replaces_the_layout_weight_with_the_unified_anchor_weight()
    {
        // Project 在默认布局里是 0.25（defaultToolWindowlayoutProvider.kt:258-261），
        // 但新 UI 默认走统一权重，激活时会被该边的统一权重（默认 0.33）覆盖
        // （ToolWindowManagerImpl.kt:606-609）。所以量出来是 0.33 而不是 0.25。
        var (frame, manager) = Build();
        manager.Activate("Project");

        frame.Measure(new Size(1000, 800));
        frame.Arrange(new Rect(0, 0, 1000, 800));

        var window = DockedWindows(frame)[0];
        Assert.Equal(WindowInfo.DefaultWeight, manager.Layout.GetInfo("Project")!.Weight);
        Assert.Equal(
            Math.Floor(frame.DockPane.Bounds.Width * WindowInfo.DefaultWeight),
            window.Bounds.Width);
    }

    [AvaloniaFact]
    public void With_unified_sizes_off_the_layout_weight_survives()
    {
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2())
        {
            IsUnifiedToolWindowSizesEnabled = false,
        };
        var frame = new ForgeToolWindowFrame();
        frame.Attach(manager);

        manager.Activate("Project");
        frame.Measure(new Size(1000, 800));
        frame.Arrange(new Rect(0, 0, 1000, 800));

        var window = DockedWindows(frame)[0];
        Assert.Equal(0.25f, manager.Layout.GetInfo("Project")!.Weight);
        Assert.Equal(Math.Floor(frame.DockPane.Bounds.Width * 0.25f), window.Bounds.Width);
    }

    // 停靠区里除了工具窗口，还有编辑区和几条拖拽热区
    private static List<Border> DockedWindows(ForgeToolWindowFrame frame)
        => frame.DockPane.Children.OfType<Border>().ToList();

    private static (ForgeToolWindowFrame Frame, ToolWindowManager Manager) Build()
    {
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        var frame = new ForgeToolWindowFrame();
        frame.Attach(manager);
        return (frame, manager);
    }
}
