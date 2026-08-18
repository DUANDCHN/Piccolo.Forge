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
    private static (ForgeControl Frame, Window Window) Show()
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var frame = new ForgeControl { Palette = ForgePalette.ExpUiDark };
        frame.Attach(shell);

        var window = new Window { Width = 900, Height = 600, Content = frame };
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
