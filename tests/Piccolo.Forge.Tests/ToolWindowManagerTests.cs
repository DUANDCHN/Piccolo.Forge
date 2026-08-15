using Piccolo.Forge.Model;
using Xunit;

namespace Piccolo.Forge.Tests;

/// ToolWindowManagerImpl 的显示/隐藏/激活规则。
public class ToolWindowManagerTests
{
    [Fact]
    public void Showing_hides_the_other_window_in_the_same_slot()
    {
        // ToolWindowManagerImpl.kt:962-966 —— (pane, anchor, isSplit, type) 全同的可见窗口会被挤掉
        var m = NewManager();
        m.Show("Project");
        m.Show("Commit");

        Assert.False(m.Layout.GetInfo("Project")!.IsVisible);
        Assert.True(m.Layout.GetInfo("Commit")!.IsVisible);
    }

    [Fact]
    public void Showing_does_not_touch_the_other_half_of_the_same_anchor()
    {
        // Structure 的 isSplit=true，跟 Project 不是一格
        var m = NewManager();
        m.Show("Project");
        m.Show("Structure");

        Assert.True(m.Layout.GetInfo("Project")!.IsVisible);
        Assert.True(m.Layout.GetInfo("Structure")!.IsVisible);
    }

    [Fact]
    public void Showing_forces_the_stripe_button_on_and_assigns_an_order()
    {
        // ToolWindowManagerImpl.kt:927-931
        var layout = new DesktopLayout([
            new WindowInfo { Id = "X", Anchor = ToolWindowAnchor.Left, IsShowStripeButton = false, Order = -1 },
        ]);
        var m = new ToolWindowManager(layout);

        m.Show("X");

        var info = layout.GetInfo("X")!;
        Assert.True(info.IsShowStripeButton);
        Assert.Equal(0, info.Order);
    }

    [Fact]
    public void Hiding_with_hideSide_hides_everything_on_that_anchor()
    {
        // ToolWindowManagerImpl.kt:867-880
        var m = NewManager();
        m.Show("Project");
        m.Show("Structure");

        m.Hide("Project", hideSide: true);

        Assert.False(m.Layout.GetInfo("Project")!.IsVisible);
        Assert.False(m.Layout.GetInfo("Structure")!.IsVisible);
    }

    [Fact]
    public void Hiding_with_removeFromStripe_resets_the_pane_id()
    {
        // ToolWindowManagerImpl.kt:845-848
        var m = NewManager();
        m.Show("Project");

        m.Hide("Project", removeFromStripe: true);

        var info = m.Layout.GetInfo("Project")!;
        Assert.False(info.IsShowStripeButton);
        Assert.Equal(WindowInfo.DefaultPaneId, info.PaneId);
    }

    [Fact]
    public void Side_stack_is_off_by_default_so_nothing_comes_back()
    {
        // registry.properties:1020 —— ide.enable.toolwindow.stack=false
        var m = NewManager();
        m.Show("Project");
        m.Show("Commit");   // Project 被挤掉

        m.Hide("Commit");

        Assert.False(m.Layout.GetInfo("Project")!.IsVisible);
    }

    [Fact]
    public void Side_stack_restores_the_displaced_window_when_enabled()
    {
        var m = NewManager(isStackEnabled: true);
        m.Show("Project");
        m.Show("Commit");

        m.Hide("Commit");

        Assert.True(m.Layout.GetInfo("Project")!.IsVisible);
    }

    [Fact]
    public void Activate_uses_the_unified_anchor_weight()
    {
        // ToolWindowManagerImpl.kt:606-609，新 UI 默认启用
        var m = NewManager();
        m.Layout.SetUnifiedAnchorWeight(ToolWindowAnchor.Left, 0.4f);

        m.Activate("Project");

        Assert.Equal(0.4f, m.Layout.GetInfo("Project")!.Weight);
    }

    [Fact]
    public void Activate_keeps_the_window_weight_when_unified_sizes_are_off()
    {
        var m = new ToolWindowManager(DefaultToolWindowLayout.CreateV2()) { IsUnifiedToolWindowSizesEnabled = false };
        m.Layout.SetUnifiedAnchorWeight(ToolWindowAnchor.Left, 0.4f);

        m.Activate("Project");

        Assert.Equal(0.25f, m.Layout.GetInfo("Project")!.Weight);
    }

    [Fact]
    public void Recent_list_puts_the_latest_first()
    {
        // ToolWindowManagerImpl.kt:615-616
        var m = NewManager();
        m.Activate("Project");
        m.Activate("Terminal");
        m.Activate("Project");

        Assert.Equal(["Project", "Terminal"], m.RecentToolWindows);
    }

    [Fact]
    public void Focus_falls_back_to_the_persistent_stack_after_the_editor_takes_focus()
    {
        // ToolWindowManagerImpl.kt:1921-1941 + ToolWindowManagerLifecycle.kt:85-92
        var m = NewManager();
        m.Activate("Project");
        m.Activate("Terminal");

        Assert.Equal("Terminal", m.FindToolWindowToFocus());

        m.OnEditorFocused();

        // 易失栈空了，但持久栈还在，两个都还可见
        Assert.Equal("Terminal", m.FindToolWindowToFocus());
        Assert.Equal("Terminal", m.LastActiveToolWindowId);
    }

    [Fact]
    public void Hidden_windows_are_not_focus_candidates()
    {
        var m = NewManager();
        m.Activate("Project");
        m.Activate("Terminal");
        m.Hide("Terminal");

        Assert.Equal("Project", m.FindToolWindowToFocus());
    }

    [Theory]
    // ToolWindowManagerLifecycle.kt:116-118 —— SLIDING 无论 isAutoHide 都自动隐藏
    [InlineData(ToolWindowType.Docked, false, false)]
    [InlineData(ToolWindowType.Docked, true, true)]
    [InlineData(ToolWindowType.Sliding, false, true)]
    [InlineData(ToolWindowType.Sliding, true, true)]
    public void Auto_hide_covers_sliding_regardless_of_the_flag(
        ToolWindowType type,
        bool isAutoHide,
        bool expected)
    {
        var info = new WindowInfo { Id = "X", Type = type, IsAutoHide = isAutoHide, IsVisible = true };
        Assert.Equal(expected, ToolWindowManager.ShouldAutoHideOnFocusLost(info));
    }

    [Fact]
    public void Auto_hide_does_not_restore_from_the_side_stack()
    {
        // 走 deactivateToolWindow 而不是 executeHide，所以不触发恢复
        var layout = DefaultToolWindowLayout.CreateV2();
        layout.SetInfo(layout.GetInfo("Commit")! with { IsAutoHide = true });
        var m = new ToolWindowManager(layout, isStackEnabled: true);

        m.Show("Project");
        m.Show("Commit");
        m.AutoHideOnFocusLost("Commit");

        Assert.False(layout.GetInfo("Project")!.IsVisible);
    }

    [Fact]
    public void MoveTo_displaces_the_occupant_of_the_target_slot()
    {
        // 拖拽落到已有住户的格子上：借互斥把旧住户挤走
        var m = NewManager();
        m.Show("Project");
        m.Show("Terminal");

        m.MoveTo("Terminal", ToolWindowAnchor.Left, order: -1, isSplit: false);

        Assert.True(m.Layout.GetInfo("Terminal")!.IsVisible);
        Assert.False(m.Layout.GetInfo("Project")!.IsVisible);
        Assert.Equal(ToolWindowAnchor.Left, m.Layout.GetInfo("Terminal")!.Anchor);
    }

    [Fact]
    public void MoveTo_keeps_an_invisible_window_invisible()
    {
        var m = NewManager();

        m.MoveTo("Terminal", ToolWindowAnchor.Right, order: -1, isSplit: false);

        Assert.False(m.Layout.GetInfo("Terminal")!.IsVisible);
        Assert.Equal(ToolWindowAnchor.Right, m.Layout.GetInfo("Terminal")!.Anchor);
    }

    [Fact]
    public void State_changes_are_reported()
    {
        var m = NewManager();
        var seen = new List<(ToolWindowChangeKind Kind, string Id)>();
        m.StateChanged += (_, e) => seen.Add((e.Kind, e.Id));

        m.Activate("Project");
        m.Hide("Project");

        Assert.Contains((ToolWindowChangeKind.Shown, "Project"), seen);
        Assert.Contains((ToolWindowChangeKind.Activated, "Project"), seen);
        Assert.Contains((ToolWindowChangeKind.Hidden, "Project"), seen);
    }

    private static ToolWindowManager NewManager(bool isStackEnabled = false)
        => new(DefaultToolWindowLayout.CreateV2(), isStackEnabled);
}
