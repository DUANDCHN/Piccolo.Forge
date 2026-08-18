using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
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

    /// <summary>
    /// 8 个「移至」收在**一个子菜单**里，不是平铺在一级。
    /// </summary>
    /// <remarks>
    /// 上游那份就是 <c>Move to ▸</c>（用户实测的 Rider 截图亦然）。
    /// 平铺会得到一条 11 项的长菜单，而这 8 项本来就是同一件事的取值。
    /// <para>
    /// ⚠ 所以**顶层项数不再等于模型项数** —— 原来那条断言
    /// <c>ForStripeButton().Count == menu.Items.Count</c> 正是把「平铺」这个错结构钉住了。
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void The_eight_move_targets_live_in_one_submenu()
    {
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        var menu = ForgeMenuBuilder.ForStripeButton(manager, "Project", () => { });

        // 顶层：隐藏 / 分隔 / 移至 / 分隔 / 显示名称
        Assert.Equal(2, menu.Items.OfType<Separator>().Count());
        Assert.Equal(3, menu.Items.OfType<MenuItem>().Count());

        var moveTo = Assert.Single(menu.Items.OfType<MenuItem>(), x => (string?)x.Header == "移至");
        Assert.Equal(
            ToolWindowContextMenu.ForStripeButton().Count(x => x.Kind == ToolWindowMenuItemKind.MoveTo),
            moveTo.Items.OfType<MenuItem>().Count());
    }

    /// <summary>「显示工具窗口名称」是**可勾选**的，勾要反映当前状态。</summary>
    /// <remarks>回归：原来是个普通项，用户看不出当前开没开（上游那项带 ✓）。</remarks>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_names_toggle_shows_its_current_state(bool showNames)
    {
        var menu = ForgeMenuBuilder.ForStripeBackground(() => { }, showNames);
        var item = Assert.Single(menu.Items.OfType<MenuItem>());

        Assert.Equal("显示工具窗口名称", item.Header);
        Assert.Equal(MenuItemToggleType.CheckBox, item.ToggleType);
        Assert.Equal(showNames, item.IsChecked);
    }

    [AvaloniaFact]
    public void The_frame_hangs_a_menu_on_every_stripe_button()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var manager = shell.Manager;
        var frame = new ForgeControl();
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
    private static (ForgeControl Frame, Window Window) Show(double height = 600)
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var frame = new ForgeControl { Palette = ForgePalette.ExpUiDark };
        frame.Attach(shell);

        var window = new Window { Width = 900, Height = height, Content = frame };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (frame, window);
    }

    private static ForgeStripeButton FirstButton(ForgeControl frame)
        => frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>().First();

    /// <summary>
    /// **蓝框指的位置，就是松手后真正落到的位置。**
    /// </summary>
    /// <remarks>
    /// 这是拖拽反馈唯一真正要保证的事。回归：两者原先各算各的 ——
    /// 框「钉在指针那一行」，而插入位置走的是逐按钮的半高判定，且判定时
    /// 还把被拖的那个也算进了序列（上游 <c>AbstractDroppableStripe.kt:601</c> 是先
    /// <c>tools.remove(dragButton)</c> 的）。两处偏差叠起来，用户实测表现为
    /// 「明明放在下面，蓝框提示在上面」。
    /// <para>
    /// 现在框画的就是排布让出来的那个空槽（<see cref="StripeItemsPanel.DropSlotRect"/>），
    /// 一处算、两处用，不可能再对不上。
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void The_drop_indicator_lands_where_the_button_actually_goes()
    {
        var (frame, window) = Show();
        var group = frame.LeftToolbar.TopGroup;
        var buttons = group.Children.OfType<ForgeStripeButton>().ToList();
        Assert.True(buttons.Count >= 3, "默认布局的左上组至少要有三个按钮，否则这条用例证明不了什么");

        var dragged = buttons[0];
        var start = dragged.TranslatePoint(new Point(18, 20), frame)!.Value;

        // 拖到第三个按钮的位置附近
        var drop = buttons[2].TranslatePoint(new Point(18, 20), frame)!.Value;

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(drop);
        Dispatcher.UIThread.RunJobs();

        // 空槽在排布里真的空出来了，且蓝框就画在它身上
        Assert.NotNull(group.DropSlotRect);
        var slot = group.DropSlotRect!.Value;
        var slotInFrame = group.TranslatePoint(slot.Position, frame)!.Value;
        var indicator = frame.DragOverlay.DropRect;
        Assert.NotNull(indicator);

        // 框在槽内（框比槽小一圈 —— 它要扣掉图标内边距，与真按钮的高亮同宽）
        Assert.InRange(indicator!.Value.Y, slotInFrame.Y, slotInFrame.Y + slot.Height);
        Assert.True(indicator.Value.Height <= slot.Height);

        // 被拖的那个已经不占位了：它下面的按钮补了上来
        Assert.Equal(0, buttons[1].Bounds.Y);
    }

    /// <summary>
    /// 松手之后，按钮真的排到了拖去的那个位置——不是永远排最后。
    /// </summary>
    /// <remarks>
    /// 回归：落点解析里判的是 <c>button.Tag is int</c>，而 <c>Tag</c> 装的是
    /// <c>ToolWindowViewModel</c>，**这个条件永远为假**，于是每次算出来的 order 都是 -1。
    /// 上组表现为"拖到哪都排最后"；BOTTOM 组倒序显示，-1 会变成最小 order、
    /// 显示在最下面（用户报的「拖到第一个反而变成最下面」）。
    /// <para>
    /// 类型不匹配的模式匹配编译器不管，而当时**没有任何用例检查落点 order 是对的**——
    /// 拖拽的用例只验了"画没画反馈""松手后反馈消失"。这条补的就是那个缺口。
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void A_dropped_button_lands_where_it_was_dragged_to()
    {
        var (frame, window) = Show();
        var group = frame.LeftToolbar.TopGroup;
        var ids = () => group.Children.OfType<ForgeStripeButton>()
            .Select(x => ((ToolWindowViewModel)x.Tag!).Id)
            .ToList();

        var before = ids();
        Assert.True(before.Count >= 3, "默认布局的左上组至少要三个按钮");

        // 把最后一个拖到第一个的位置
        var buttons = group.Children.OfType<ForgeStripeButton>().ToList();
        var moved = before[^1];
        var from = buttons[^1].TranslatePoint(new Point(18, 20), frame)!.Value;
        var to = buttons[0].TranslatePoint(new Point(18, 2), frame)!.Value;

        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to);
        window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        var after = ids();
        Assert.Equal(before.Count, after.Count);
        Assert.Equal(moved, after[0]);          // 排到了第一位
        Assert.NotEqual(moved, after[^1]);      // 而不是"还在最后"
    }

    /// <summary>
    /// 下组：指针不动，落点就不该自己跑掉。
    /// </summary>
    /// <remarks>
    /// 回归。下组是**贴底**排布（<c>ForgeToolWindowToolbar</c> 里的 <c>Dock.Bottom</c>），
    /// 一留空槽整组就被往上顶一个按钮高。当时算落点用的是这一组**当时的顶边**，
    /// 于是"没槽"那一帧和"有槽"那一帧的换算基准差了一个按钮高——
    /// 同一个指针位置，前后两帧解出两个格子。表现为下组落点恒偏一格，
    /// 且在格子边界上来回翻（翻一次就重排一次，也是拖动发卡的一份）。
    /// <para>
    /// 现在换算基准是「**去掉空槽之后**那条序列的顶」（<c>ForgeControl.ApplyDropOffset</c>），
    /// 与 <see cref="StripeItemsPanel.DropOffset"/> 的口径对齐，指针不动结果就不动。
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void The_bottom_group_keeps_the_same_slot_when_the_pointer_does_not_move()
    {
        var (frame, window) = Show();
        var group = frame.LeftToolbar.BottomGroup;
        var buttons = group.Children.OfType<ForgeStripeButton>().ToList();
        Assert.True(buttons.Count >= 3, "默认布局的下组至少要有三个按钮，否则这条用例证明不了什么");

        var from = buttons[^1].TranslatePoint(new Point(18, 20), frame)!.Value;
        var to = buttons[1].TranslatePoint(new Point(18, 20), frame)!.Value;

        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to);
        Dispatcher.UIThread.RunJobs();

        var first = group.DropIndex;
        Assert.True(first >= 0, "指针落在下组里，就该有空槽");

        // 同一个点再来一次 —— 这一次这一组**已经留着空槽**（顶边被顶高了）
        window.MouseMove(to);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(first, group.DropIndex);

        window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// 拖出去再拖回原位，虚影**照样跟手**。
    /// </summary>
    /// <remarks>
    /// 回归（用户实测）：「往下拖再往上拖，鼠标一进第一个图标那块地，拖着的图标就没了」。
    /// 成因是起拖的两个条件被写成了**每个指针事件都判**——「已经离开这个按钮」这条
    /// 在指针回到原按钮矩形时又成立了，移动处理整个提前 return：虚影停在那儿不再跟手，
    /// 落点也不再更新。它天然方向相关（往下拖离开没事、往上拖回来中招），
    /// 所以表现成「先往下、再往上就很卡」——其实一帧没掉，是反馈被掐了。
    /// <para>
    /// 上游的死区同样只管起拖（<c>MouseDragHelper.java:32</c>），起拖后每个移动事件
    /// 都照常喂给 <c>relocate</c>。
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void The_thumb_keeps_following_after_the_pointer_returns_to_the_source_button()
    {
        var (frame, window) = Show();
        var button = FirstButton(frame);
        var start = button.TranslatePoint(new Point(18, 20), frame)!.Value;

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Vector(0, 260));   // 拖出去：起拖
        Dispatcher.UIThread.RunJobs();

        // 落到别人身上 = 拖影是"按钮的样子"
        Assert.False(frame.StripeDragShowsFloatPreview);

        // 拖回来，落在原按钮那块地里：反馈**不能**被掐掉（这才是本条要守的）
        var back = start + new Vector(0, 4);
        window.MouseMove(back);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(frame.DragOverlay.DropRect);

        window.MouseUp(back, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// 拖到"放不下"的地方，拖影不但要在，还得换成**小窗口预览**。
    /// </summary>
    /// <remarks>
    /// 回归（用户实测）：「拖到侧边栏中间，拖着的图标就没了，继续往上或往下又出现」。
    /// 条带上下两个落区之间本来就有一段谁都不属于的空白（上组只占上半、下组是窗口底部一条），
    /// 落在那儿 <c>ResolveDropTarget</c> 返回 null —— 这是对的，"这里不能放"；
    /// 错的是当时顺手调了 <c>Clear()</c>，把**虚影**也一起清了。
    /// <para>
    /// 上游的虚影是一个独立窗口，跟落点判定毫不相干，全程跟手
    /// （<c>ToolWindowDragHelper.kt:299 · relocateImageView</c>）。
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void The_ghost_turns_into_a_window_preview_where_nothing_can_be_dropped()
    {
        var (frame, window) = Show(height: 1200);
        var button = FirstButton(frame);
        var start = button.TranslatePoint(new Point(18, 20), frame)!.Value;

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Vector(0, 120));
        Dispatcher.UIThread.RunJobs();

        // 落得下去 = 按钮的样子
        Assert.False(frame.StripeDragShowsFloatPreview);

        var dead = FindDeadSpot(frame, window, start);

        // ★ 硬约束：这儿松手会浮出窗口，那拖影现在就得长成小窗口的样子
        Assert.True(frame.StripeDragShowsFloatPreview, "落不下去的地方，拖影必须是小窗口预览");
        Assert.Null(frame.DragOverlay.DropRect);

        window.MouseUp(dead, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        // 手势结束了，形态也该收干净
        Assert.False(frame.StripeDragShowsFloatPreview);
    }

    /// <summary>
    /// 现场找一个"确实放不下"的位置：没有任何落点反馈的那一点。
    /// </summary>
    /// <remarks>
    /// 条带上下两个落区之间那段空白的具体几何随窗口尺寸变（上组占条带上半、
    /// 下组是窗口底部一条），所以别写死坐标。窗口要够高才有这段空白 —— 矮的时候两者会叠上。
    /// 调用前必须已经起拖。
    /// </remarks>
    private static Point FindDeadSpot(ForgeControl frame, Window window, Point start)
    {
        for (var y = frame.Bounds.Height / 2; y < frame.Bounds.Height; y += 10)
        {
            var probe = new Point(start.X, y);
            window.MouseMove(probe);
            Dispatcher.UIThread.RunJobs();

            if (frame.DragOverlay.DropRect is null && frame.DragOverlay.AreaRect is null)
            {
                return probe;
            }
        }

        Assert.Fail("这个窗口尺寸下没有「放不下」的位置，用例前提不成立");
        return default;
    }

    /// <summary>
    /// 在落区之外松手 = 浮出来。
    /// </summary>
    /// <remarks>
    /// 实机 Rider 就是这样（用户实测：拖到条带中间那段空白松手，窗口直接浮了）。
    /// 上游同一条路：<c>ToolWindowDragHelper.kt:457 · processDragFinish</c>
    /// 拿不到目标条带就走浮动分支。本仓原来这里写的是"本阶段不做浮动"，
    /// 于是那段空白成了"松手什么都不发生"的死区。
    /// </remarks>
    [AvaloniaFact]
    public void Releasing_outside_every_drop_zone_floats_the_window()
    {
        // 窗口要够高才有那段空白：上组落区占条带上半、下组占窗口底部一条，
        // 窗口矮的时候两者会叠上，压根没有"放不下"的地方（默认 600 就是这种）。
        var (frame, window) = Show(height: 1200);
        var button = FirstButton(frame);
        var id = ((ToolWindowViewModel)button.Tag!).Id;
        var manager = frame.Shell!.Manager;
        var start = button.TranslatePoint(new Point(18, 20), frame)!.Value;

        Assert.NotEqual(ToolWindowType.Floating, manager.Layout.GetInfo(id)!.Type);

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Vector(0, 120));
        Dispatcher.UIThread.RunJobs();

        var dead = FindDeadSpot(frame, window, start);

        window.MouseUp(dead, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ToolWindowType.Floating, manager.Layout.GetInfo(id)!.Type);
    }

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

        var frame = new ForgeControl();
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

        var control = new ForgeEditorControl();
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

        var frame = new ForgeControl();
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

        var frame = new ForgeControl();
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

/// <summary>
/// 点进编辑区 → 编辑区拿到焦点（标签页"活动那档"配色的前提）。
/// </summary>
/// <remarks>
/// 上游选中标签之后焦点就落在编辑器上，于是选中标签走的是「选中 + 活动」那档
/// （蓝下划线）。宿主的文档内容不一定可获焦 —— 图像视口、只读画布都不是 ——
/// 没有这条接线的话，那一档在这类宿主里**永远到不了**，选中标签的下划线一直是失焦灰。
/// </remarks>
public class ForgeEditorFocusTests
{
    [AvaloniaFact]
    public void 点标签之后编辑区握着焦点()
    {
        var (window, control, outside, _) = Build();

        outside.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.False(control.IsKeyboardFocusWithin);   // 前提：焦点确实在编辑区外

        var tab = control.GetVisualDescendants().OfType<ForgeEditorTab>().Single();
        var point = tab.TranslatePoint(new Point(10, tab.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(control.IsKeyboardFocusWithin);
        Assert.True(tab.IsGroupActive);               // → 选中标签走活动那档（蓝下划线）
    }

    [AvaloniaFact]
    public void 内容自己接管了焦点时不抢()
    {
        // 这一条钉住"只在没人接的时候才收焦点"：改成 Tunnel 就会抢在内容前面，
        // 症状是文档里的输入框点了不进光标。
        // 用一个自己在 PointerPressed 里取焦点的元素代替 TextBox —— headless 测试应用里
        // 没有 Fluent/Semi，TextBox 没有模板、它那条取焦点的路走不到（与本改动无关）。
        var inner = new Border { Focusable = true, Background = Brushes.Transparent };
        inner.PointerPressed += (_, _) => inner.Focus();
        var (window, control, outside, _) = Build(inner);

        outside.Focus();
        Dispatcher.UIThread.RunJobs();

        var point = inner.TranslatePoint(new Point(5, inner.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(inner.IsFocused, $"focused={window.FocusManager?.GetFocusedElement()?.GetType().Name}");
        Assert.True(control.IsKeyboardFocusWithin);
    }

    [AvaloniaFact]
    public void 新开一张文档就把焦点收进编辑区()
    {
        // 上游 openFile 的 focusEditor 默认为真：在项目视图里双击一个文件，焦点跟着进编辑器。
        var (window, control, outside, editor) = Build();

        outside.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.False(control.IsKeyboardFocusWithin);   // 前提：焦点确实在编辑区外

        editor.Open("b", "B.cs");
        Dispatcher.UIThread.RunJobs();

        Assert.True(control.IsKeyboardFocusWithin);
    }

    [AvaloniaFact]
    public void 关掉一张不抢焦点()
    {
        // 关闭会让选中落到左邻居 —— 那不是"打开"，焦点该留在用户当时所在的地方。
        var (window, control, outside, editor) = Build();
        editor.Open("b", "B.cs");
        Dispatcher.UIThread.RunJobs();

        outside.Focus();
        Dispatcher.UIThread.RunJobs();

        editor.Area.Close("b");
        Dispatcher.UIThread.RunJobs();

        Assert.False(control.IsKeyboardFocusWithin);
    }

    /// <summary>一个编辑区 + 一个区外的可获焦控件（焦点的起点）。</summary>
    private static (Window Window, ForgeEditorControl Control, Button Outside, EditorAreaViewModel Editor) Build(object? content = null)
    {
        var editor = new EditorAreaViewModel();
        editor.Open("a", "A.cs", content);

        var control = new ForgeEditorControl();
        control.Attach(editor);

        var outside = new Button { Content = "外面" };
        var root = new DockPanel();
        DockPanel.SetDock(outside, Dock.Top);
        root.Children.Add(outside);
        root.Children.Add(control);

        var window = new Window { Width = 800, Height = 500, Content = root };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, control, outside, editor);
    }
}

/// <summary>
/// 标题条按钮的两条图标路：库自带的 <c>Icon</c>（Geometry）与宿主塞的 <c>IconContent</c>（控件）。
/// </summary>
/// <remarks>
/// 由来：宿主在同一排里混了图标包的图标（字形几乎撑满 em 盒），与库自带那几个
/// （画在 16 视框里、四周留白，⋮ 的墨迹只有 10 高）摆在一起就是一大一小。
/// <c>IconInkScale</c> 让宿主把库自带的抬到同一档；它**只作用于路径那条**。
/// </remarks>
public class ForgeHeaderButtonIconTests
{
    [AvaloniaFact]
    public void 墨迹系数透到画图标那个子元素上()
    {
        var button = new ForgeHeaderButton { Icon = ForgeIcons.MoreVertical, IconInkScale = 1.15 };
        Show(button);

        var icon = button.GetVisualDescendants().OfType<ForgeGeometryIcon>().Single();
        Assert.Equal(1.15, icon.InkScale);
    }

    [AvaloniaFact]
    public void 默认是上游原样()
    {
        var button = new ForgeHeaderButton { Icon = ForgeIcons.MoreVertical };
        Show(button);

        Assert.Equal(1, button.GetVisualDescendants().OfType<ForgeGeometryIcon>().Single().InkScale);
    }

    [AvaloniaFact]
    public void 宿主给了控件就放它而不是画路径()
    {
        // 宿主的图标是控件形态（图标包给的多是这种），大小由它自己说了算，不受 IconInkScale 影响
        var glyph = new Border { Width = 12, Height = 12 };
        var button = new ForgeHeaderButton { Icon = ForgeIcons.MoreVertical, IconContent = glyph };
        Show(button);

        Assert.Contains(glyph, button.GetVisualDescendants());
        Assert.Empty(button.GetVisualDescendants().OfType<ForgeGeometryIcon>());
    }

    [AvaloniaFact]
    public void 图标位比库自带的图标大一圈_给宿主的字体图标留余量()
    {
        // 由来：宿主塞进来的多是字体形态的图标，字形的推进宽度不保证正好 1 em ——
        // 格子与字号等大时右边会被切掉一两个像素（实机撞过两次：条带一次、标题条一次）。
        // 库自带的 Geometry 不跟着放大：那套路径按 16 网格画，1:1 才锐利。
        Assert.Equal(20, ForgeMetrics.HeaderButtonIconSlot);
        Assert.Equal(16, ForgeMetrics.HeaderButtonIconSize);
        Assert.True(ForgeMetrics.HeaderButtonIconSlot < ForgeMetrics.HeaderButtonSize, "图标位不能大过按钮本身");

        var button = new ForgeHeaderButton { Icon = ForgeIcons.MoreVertical };
        Show(button);

        var icon = button.GetVisualDescendants().OfType<ForgeGeometryIcon>().Single();
        Assert.Equal(new Size(16, 16), icon.Bounds.Size);
    }

    [AvaloniaFact]
    public void 禁用时整颗压暗()
    {
        // 标题条上的动作按钮是纯图标、没有文字的，"能不能用"只剩深浅这一条线索。
        // 压的是整颗按钮而不是前景色 —— 图标可能是宿主塞的控件，前景色管不着它。
        var button = new ForgeHeaderButton { Icon = ForgeIcons.MoreVertical, IsEnabled = false };
        Show(button);

        Assert.Equal(0.4, button.Opacity);
    }

    [AvaloniaFact]
    public void 可用时不压暗()
    {
        var button = new ForgeHeaderButton { Icon = ForgeIcons.MoreVertical };
        Show(button);

        Assert.Equal(1, button.Opacity);
    }

    private static void Show(ForgeHeaderButton button)
    {
        var window = new Window { Width = 200, Height = 100, Content = button };
        window.Show();
        Dispatcher.UIThread.RunJobs();
    }
}

/// <summary>
/// 标题条上那排**内容 tab**（上游 ContentManager；Rider 工具窗口标题条上那排）。
/// </summary>
public class ForgeContentTabClickTests
{
    [AvaloniaFact]
    public void 点一下就切过去_并且高亮跟着走()
    {
        // 回归：点 tab 只改模型（Select），而 e.Handled=true 让 TabStrip 自带的选中逻辑不跑；
        // 早先没人把模型的变化刷回视图 —— 实机表现是"点了没反应"（模型其实已经切了）。
        var shell = new ToolWindowShellViewModel();
        var panel = shell.Add("A", "甲", ToolWindowAnchor.Bottom, false);
        panel.Contents.Add(new ToolWindowContent("one", "一", IsCloseable: false));
        panel.Contents.Add(new ToolWindowContent("two", "二", IsCloseable: false));
        shell.Manager.Activate("A");

        var frame = new ForgeControl();
        frame.Attach(shell);
        var window = new Window { Width = 800, Height = 500, Content = frame };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var strip = frame.GetVisualDescendants().OfType<ForgeContentTabStrip>().First();
        var second = Enumerable.Range(0, strip.ItemCount)
            .Select(i => strip.ContainerFromIndex(i))
            .OfType<ForgeContentTab>()
            .Single(x => x.Title == "二");

        var point = second.TranslatePoint(new Point(4, second.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("two", panel.Contents.SelectedContent?.Id);   // 模型切了
        Assert.True(second.IsSelected);                            // 视图也跟着走了
    }
}

/// <summary>
/// 标题条上内容 tab 的**选中形态**要分族：expUI 一条下划线，Islands 一块圆角实底。
/// </summary>
/// <remarks>
/// 回归：<c>:islands</c> 分支早先只换了颜色、没换形状 —— Islands 下标题条上的 tab 还是下划线，
/// 而同屏的文档标签已经是圆角块，一个界面两套语言。
/// </remarks>
public class ForgeContentTabFamilyTests
{
    [AvaloniaTheory]
    [InlineData(ForgeThemeFamily.ExpUi, true)]
    [InlineData(ForgeThemeFamily.Islands, false)]
    public void 选中态的下划线只属于_expUI(ForgeThemeFamily family, bool expectUnderline)
    {
        using var theme = ForgeThemeScope.Use(family);
        var (window, tab) = Build(family);
        _ = window;

        var underline = tab.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "PART_Underline");
        var background = tab.GetVisualDescendants().OfType<Border>().Single(x => x.Name == "PART_Background");

        Assert.Equal(expectUnderline, underline.IsVisible);

        // Islands：改由那块圆角底表达选中（颜色取的与文档标签同一个键）
        Assert.Equal(!expectUnderline, background.IsVisible);
        if (!expectUnderline)
        {
            Assert.True(background.CornerRadius.TopLeft > 0, "Islands 的选中底应当是圆角");
        }
    }

    private static (Window Window, ForgeContentTab Tab) Build(ForgeThemeFamily family)
    {
        var shell = new ToolWindowShellViewModel();
        var panel = shell.Add("A", "甲", ToolWindowAnchor.Bottom, false);
        panel.Contents.Add(new ToolWindowContent("one", "一", IsCloseable: false));
        panel.Contents.Add(new ToolWindowContent("two", "二", IsCloseable: false));
        shell.Manager.Activate("A");

        // ⚠ 族在**建控件之前**就定下：两个开关必须同族（挂树时核对），
        //   而且换单元不会让已挂树的控件重取模板（REQ-068）。
        var frame = new ForgeControl { Appearance = ForgeAppearance.Default with { Family = family } };
        frame.Attach(shell);

        var window = new Window { Width = 800, Height = 500, Content = frame };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var tab = frame.GetVisualDescendants().OfType<ForgeContentTab>().First(x => x.IsSelected);
        return (window, tab);
    }
}

/// <summary>标题条上的内容 tab：拖着换次序。</summary>
/// <remarks>
/// 语义与文档标签同一条（模型里的 Reorder：先取目标索引再移除源，两个方向都是"越过目标"）。
/// 与文档标签不同的是**不跨容器** —— 内容 tab 只属于这一个工具窗口。
/// </remarks>
public class ForgeContentTabReorderTests
{
    [AvaloniaFact]
    public void 拖过去就换位_而且选中的还是原来那块()
    {
        var (window, panel, strip) = Build();

        // 选中第一块，然后把它拖到第三块身上
        panel.Contents.Select("a");
        Dispatcher.UIThread.RunJobs();

        // ⚠ 两个坐标都要在**按下之前**量：一旦开始拖，容器会跟着次序重建，
        //   再去问旧引用要位置就问不到了（TranslatePoint 给 null）。
        var from = Center(TabOf(strip, "甲"), window);
        var to = Center(TabOf(strip, "丙"), window);

        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to);
        Dispatcher.UIThread.RunJobs();

        // 拖动中**先不落位**，只给出"松手会落在哪"的样子：
        // 被拖的那块半透明跟手，行内在落点处留一个空档（与文档标签同一套）
        Assert.Equal(["a", "b", "c"], panel.Contents.Contents.Select(x => x.Id));
        var row = strip.GetVisualDescendants().OfType<HeaderTabsPanel>().Single();
        Assert.NotNull(row.DraggedChild);
        Assert.NotNull(row.DropIndex);
        Assert.NotNull(row.DropRect);   // 投影吸在缝上（被拖的视觉在幽灵窗上，不在行内）

        window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(row.DraggedChild);          // 收掉了
        Assert.Null(row.DropIndex);
        Assert.Equal(["b", "c", "a"], panel.Contents.Contents.Select(x => x.Id));
        Assert.Equal("a", panel.Contents.SelectedContent?.Id);   // 选中跟着**那一块**走，不是跟着位置
    }

    [AvaloniaFact]
    public void 拖出标题条不打断手势_绕回来还能接着排()
    {
        // 回归：早先没捕获指针 —— 鼠标一离开这条标题条就收不到移动事件，
        // 表现是"拖不出去"（其实是画面停在原地）。面板没有分屏那条路，
        // 拖到外面**不做处理**即可，但手势不该被截断：人常从外面绕一圈再拖回来。
        var (window, panel, strip) = Build();

        var from = Center(TabOf(strip, "甲"), window);
        var to = Center(TabOf(strip, "丙"), window);

        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(new Point(from.X + 20, from.Y + 200));   // 拖到标题条外面
        Dispatcher.UIThread.RunJobs();

        var row = strip.GetVisualDescendants().OfType<HeaderTabsPanel>().Single();
        Assert.NotNull(row.DraggedChild);   // 还在跟手
        Assert.Null(row.DropRect);          // 但没有落点

        window.MouseMove(to);               // 绕回来
        window.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["b", "c", "a"], panel.Contents.Contents.Select(x => x.Id));
    }

    [AvaloniaFact]
    public void 手抖不算拖()
    {
        // 死区 7：按下时抖两下不该换位（条带按钮那次"有时要点两下"就是死区没设好）
        var (window, panel, strip) = Build();
        var from = Center(TabOf(strip, "甲"), window);
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(new Point(from.X + 3, from.Y));
        window.MouseUp(new Point(from.X + 3, from.Y), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["a", "b", "c"], panel.Contents.Contents.Select(x => x.Id));
    }

    private static ForgeContentTab TabOf(ForgeContentTabStrip strip, string title)
        => Enumerable.Range(0, strip.ItemCount)
            .Select(strip.ContainerFromIndex)
            .OfType<ForgeContentTab>()
            .Single(x => x.Title == title);

    private static Point Center(ForgeContentTab tab, Window window)
        => tab.TranslatePoint(new Point(tab.Bounds.Width / 2, tab.Bounds.Height / 2), window)!.Value;

    private static (Window Window, ToolWindowViewModel Panel, ForgeContentTabStrip Strip) Build()
    {
        var shell = new ToolWindowShellViewModel();
        var panel = shell.Add("A", "面板", ToolWindowAnchor.Bottom, false);
        panel.Contents.Add(new ToolWindowContent("a", "甲", IsCloseable: false));
        panel.Contents.Add(new ToolWindowContent("b", "乙", IsCloseable: false));
        panel.Contents.Add(new ToolWindowContent("c", "丙", IsCloseable: false));
        shell.Manager.Activate("A");

        var frame = new ForgeControl();
        frame.Attach(shell);

        var window = new Window { Width = 900, Height = 500, Content = frame };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, panel, frame.GetVisualDescendants().OfType<ForgeContentTabStrip>().First());
    }
}
