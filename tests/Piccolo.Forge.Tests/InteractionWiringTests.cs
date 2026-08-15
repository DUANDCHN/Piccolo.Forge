using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 右键菜单接线。
public class ForgeMenuBuilderTests
{
    [Theory]
    // 名字取自位置图标：竖边的另一半在下，横边的另一半在右
    // （ToolWindowManagerImpl.kt:770-786 · getLocationIcon）
    [InlineData(ToolWindowAnchor.Left, false, "左上")]
    [InlineData(ToolWindowAnchor.Left, true, "左下")]
    [InlineData(ToolWindowAnchor.Bottom, false, "下左")]
    [InlineData(ToolWindowAnchor.Bottom, true, "下右")]
    public void Move_items_are_named_after_the_eight_slots(ToolWindowAnchor anchor, bool split, string expected)
        => Assert.Equal(expected, ForgeMenuBuilder.DescribeMove(anchor, split));

    [AvaloniaFact]
    public void The_stripe_button_menu_maps_one_to_one_onto_the_model()
    {
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        var menu = ForgeMenuBuilder.ForStripeButton(manager, "Project", () => { });

        Assert.Equal(ToolWindowContextMenu.ForStripeButton().Count, menu.Items.Count);
        Assert.Equal(2, menu.Items.OfType<Separator>().Count());
    }

    [AvaloniaFact]
    public void The_background_menu_only_has_the_names_toggle()
    {
        var menu = ForgeMenuBuilder.ForStripeBackground(() => { });
        var item = Assert.Single(menu.Items.OfType<MenuItem>());

        Assert.Equal("显示工具窗口名字", item.Header);
    }

    [AvaloniaFact]
    public void The_frame_hangs_a_menu_on_every_stripe_button()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var manager = shell.Manager;
        var frame = new ForgeToolWindowFrame();
        frame.Attach(shell);

        Assert.All(
            frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>(),
            x => Assert.NotNull(x.ContextMenu));
        Assert.NotNull(frame.LeftToolbar.ContextMenu);
    }
}

/// 「更多」弹窗。
public class ForgeMorePopupTests
{
    [Fact]
    public void It_lists_the_windows_whose_stripe_button_was_removed()
    {
        // 就是被 removeFromStripe 摘掉的那批（SquareStripeButton.kt:380-388）
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        Assert.Empty(ForgeMorePopup.ResolveItems(manager.Layout));

        ToolWindowContextMenu.Hide(manager, "Maven");

        Assert.Equal(["Maven"], ForgeMorePopup.ResolveItems(manager.Layout));
    }

    [Fact]
    public void A_visible_window_never_shows_up_there()
    {
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        manager.Activate("Project");

        Assert.DoesNotContain("Project", ForgeMorePopup.ResolveItems(manager.Layout));
    }

    [AvaloniaFact]
    public void Nothing_to_show_means_no_popup()
        => Assert.Null(ForgeMorePopup.Create(new ToolWindowManager(DefaultToolWindowLayout.CreateV2())));

    [AvaloniaFact]
    public void The_popup_is_at_least_300_wide()
    {
        // MoreSquareStripeButton.kt:106
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        ToolWindowContextMenu.Hide(manager, "Maven");

        var flyout = ForgeMorePopup.Create(manager);

        Assert.NotNull(flyout);
        Assert.Equal(ForgeMetrics.MorePopupMinWidth, ((StackPanel)flyout.Content!).MinWidth);
    }
}

/// 浮动窗口淡出的定时器。
public class ForgeFadeControllerTests
{
    [AvaloniaFact]
    public void It_starts_fully_opaque_and_idle()
    {
        using var fade = new ForgeFadeController(new Window());

        Assert.Equal(0f, fade.CurrentRatio);
        Assert.False(fade.IsRunning);
    }

    [AvaloniaFact]
    public void Activating_starts_the_animation_right_away()
    {
        // FloatingDecorator.java:289-296 —— 激活分支没有延迟
        using var fade = new ForgeFadeController(new Window());

        fade.OnActivated();

        Assert.True(fade.IsRunning);
    }

    [AvaloniaFact]
    public void Deactivating_waits_before_it_starts()
    {
        // :298-310 —— 先等 alphaModeDelay(1500ms)
        using var fade = new ForgeFadeController(new Window());

        fade.OnDeactivated();

        Assert.False(fade.IsRunning);
    }

    [AvaloniaFact]
    public void The_target_ratio_defaults_to_the_upstream_value()
        => Assert.Equal(ForgeMetrics.FloatingFadeRatio, new ForgeFadeController(new Window()).TargetRatio);
}

/// 拖动工具窗口时的视觉反馈（AbstractDroppableStripe.kt:627-660 + ToolWindowDragHelper.kt:126-134）。
public class ForgeDragFeedbackTests
{
    private static (ForgeToolWindowFrame Frame, Window Window) Show()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var frame = new ForgeToolWindowFrame { Palette = ForgePalette.ExpUiDark };
        frame.Attach(shell);

        var window = new Window { Width = 900, Height = 600, Content = frame };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (frame, window);
    }

    private static ForgeStripeButton FirstButton(ForgeToolWindowFrame frame)
        => frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>().First();

    [AvaloniaFact]
    public void Dragging_a_stripe_button_shows_the_drop_feedback()
    {
        // 回归：拖拽本身早就能用（松手会真的挪过去），但**过程中什么都不画** ——
        // 用户看不到自己拖到哪儿了。
        var (frame, window) = Show();
        var button = FirstButton(frame);
        var start = button.TranslatePoint(new Point(20, 20), frame)!.Value;

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Vector(0, 200));   // 远超死区 7

        Assert.False(frame.DragOverlay.IsIdle);
        Assert.NotNull(frame.DragOverlay.DropRect);
        Assert.NotNull(frame.DragOverlay.AreaRect);
    }

    [AvaloniaFact]
    public void The_feedback_is_gone_after_the_drop()
    {
        var (frame, window) = Show();
        var button = FirstButton(frame);
        var start = button.TranslatePoint(new Point(20, 20), frame)!.Value;

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Vector(0, 200));
        window.MouseUp(start + new Vector(0, 200), MouseButton.Left);

        Assert.True(frame.DragOverlay.IsIdle);
    }

    [AvaloniaFact]
    public void A_click_that_never_leaves_the_deadzone_paints_nothing()
    {
        // 死区 7 之内是「点击」不是「拖拽」（MouseDragHelper.java:32）
        var (frame, window) = Show();
        var button = FirstButton(frame);
        var start = button.TranslatePoint(new Point(20, 20), frame)!.Value;

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Vector(0, 3));

        Assert.True(frame.DragOverlay.IsIdle);
    }

    [AvaloniaFact]
    public void The_overlay_never_eats_the_mouse()
    {
        // 它盖在所有控件之上，一旦参与命中测试，条带就再也点不动了
        var (frame, _) = Show();
        Assert.False(frame.DragOverlay.IsHitTestVisible);
    }
}

/// 自绘控件的整块可点（Avalonia 的命中测试基于渲染内容）。
public class ForgeHitAreaTests
{
    [AvaloniaFact]
    public void A_stripe_button_reacts_in_the_corner_not_just_on_the_icon()
    {
        // 回归：平时不画背景的控件，只有画了图标的那 20×20 收得到鼠标 ——
        // 表现为「鼠标只有经过图标才能点」。
        var shell = new ToolWindowShellViewModel();
        shell.Add("A", "甲", ToolWindowAnchor.Left, false, ForgeIcons.Camera);

        var frame = new ForgeToolWindowFrame();
        frame.Attach(shell);

        var window = new Window { Width = 600, Height = 400, Content = frame };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var button = frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>().First();
        Assert.False(shell.Find("A")!.IsOpen);

        // 按钮 40×40，图标只占正中 20×20；这里点左上角 (3,3)，图标区之外
        var corner = button.TranslatePoint(new Point(3, 3), frame)!.Value;
        window.MouseDown(corner, MouseButton.Left);
        window.MouseUp(corner, MouseButton.Left);

        Assert.True(shell.Find("A")!.IsOpen);
    }

    [AvaloniaFact]
    public void An_unselected_editor_tab_reacts_off_the_text()
    {
        // 同一个坑：未选中的标签页平时只画文字。
        var editor = new EditorAreaViewModel();
        editor.Open("a", "A");
        editor.Open("b", "B");          // b 选中，a 未选中

        var control = new ForgeEditorArea();
        control.Attach(editor);

        var window = new Window { Width = 600, Height = 400, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var tab = control.GetVisualDescendants().OfType<ForgeEditorTab>().First(x => x.Title == "A");
        Assert.False(editor.Find("a")!.IsSelected);

        // 标签页高 28，文字大约在中间；这里点最上面一行
        var top = tab.TranslatePoint(new Point(tab.Bounds.Width / 2, 2), control)!.Value;
        window.MouseDown(top, MouseButton.Left);
        window.MouseUp(top, MouseButton.Left);

        Assert.True(editor.Find("a")!.IsSelected);
    }
}

/// 按钮换组时的搬迁（跨面板）。
public class ForgeStripeRegroupTests
{
    [AvaloniaFact]
    public void Moving_a_button_from_the_bottom_group_to_the_top_group_does_not_throw()
    {
        // 回归：逐组处理时，轮到目标组插入，它还挂在原来那组上 ——
        // Avalonia 抛「already has a visual parent」。而且只在一个方向上出事
        // （下→上崩、上→下不崩），能潜伏很久。启动时恢复布局就会踩到。
        var shell = new ToolWindowShellViewModel();
        shell.Add("A", "甲", ToolWindowAnchor.Left, false, ForgeIcons.Camera);
        shell.Add("B", "乙", ToolWindowAnchor.Bottom, false, ForgeIcons.Terminal);

        var frame = new ForgeToolWindowFrame();
        frame.Attach(shell);

        var window = new Window { Width = 600, Height = 400, Content = frame };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // 底边的按钮画在左条带的**下**组；挪到左边之后它属于**上**组
        Assert.Contains(
            frame.LeftToolbar.BottomGroup.Children.OfType<ForgeStripeButton>(),
            x => x.Title == "乙");

        shell.MoveTo("B", ToolWindowAnchor.Left, isSplit: false);

        Assert.Contains(
            frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>(),
            x => x.Title == "乙");
        Assert.DoesNotContain(
            frame.LeftToolbar.BottomGroup.Children.OfType<ForgeStripeButton>(),
            x => x.Title == "乙");
    }

    [AvaloniaFact]
    public void Restoring_a_layout_that_regroups_buttons_does_not_throw()
    {
        // 真实触发路径：启动时套用上次退出的布局，一口气挪好几个。
        var saved = new ToolWindowShellViewModel();
        saved.Add("A", "甲", ToolWindowAnchor.Left, false);
        saved.Add("B", "乙", ToolWindowAnchor.Bottom, false);
        saved.MoveTo("B", ToolWindowAnchor.Left, isSplit: false);
        saved.MoveTo("A", ToolWindowAnchor.Bottom, isSplit: false);
        var xml = saved.SaveLayout();

        var shell = new ToolWindowShellViewModel();
        shell.Add("A", "甲", ToolWindowAnchor.Left, false);
        shell.Add("B", "乙", ToolWindowAnchor.Bottom, false);

        var frame = new ForgeToolWindowFrame();
        frame.Attach(shell);

        var window = new Window { Width = 600, Height = 400, Content = frame };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        shell.RestoreLayout(xml);

        Assert.Contains(
            frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>(),
            x => x.Title == "乙");
    }
}
