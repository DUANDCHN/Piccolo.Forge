using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using System.Linq;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 编辑区标签页的拖拽重排。
/// </summary>
/// <remarks>
/// 对应 <c>DragHelper.java</c> + <c>JBTabsImpl.kt:3331 · reallocate</c>。
/// 上游<b>不画插入线</b>，拖动中直接换位；所以这里测的是"次序有没有当场变对"，
/// 而不是"提示框画在哪"。
/// </remarks>
public class EditorTabDragTests
{
    // ---- 换位方向 ----------------------------------------------------------

    [Fact]
    public void Dragging_leftwards_lands_before_the_target()
    {
        var area = New("a", "b", "c", "d");

        Assert.True(area.ReorderTab("d", "b"));

        // d 原本在 b 右边 → 越过 b，落到它**前面**
        Assert.Equal(["a", "d", "b", "c"], Order(area));
    }

    [Fact]
    public void Dragging_rightwards_lands_after_the_target()
    {
        var area = New("a", "b", "c", "d");

        Assert.True(area.ReorderTab("a", "c"));

        // ⚠ 判别力所在：索引在移除**之前**取（JBTabsImpl.kt:3336-3338）。
        //    a 原本在 c 左边，移除 a 让 c 左移一格，插回旧索引 2 就落到 c **后面**。
        //    若改成移除后再取索引，这里会得到 b a c d —— 往右永远差一格、拖不过去。
        Assert.Equal(["b", "c", "a", "d"], Order(area));
    }

    [Fact]
    public void Reordering_across_groups_is_refused()
    {
        // 跨组是"拖出去"那条路（DropTab），不是 reallocate。
        // 这里不该"就近凑合一下"，该原样退回。
        var area = New("a", "b");
        area.Area.DropTab("b", area.Area.Groups[0], EditorDropSide.Right);

        Assert.Equal(2, area.Area.Groups.Count);
        Assert.False(area.ReorderTab("a", "b"));
    }

    [Fact]
    public void Reordering_announces_the_change()
    {
        // 老坑第四次：改了次序但没经过持有事件的那一层，视图就不会重画。
        var area = New("a", "b", "c");
        var announced = 0;
        area.Area.Changed += (_, _) => announced++;

        area.ReorderTab("a", "c");

        Assert.Equal(1, announced);
    }

    // ---- 几何 --------------------------------------------------------------

    [Fact]
    public void The_dragged_tab_cannot_leave_the_row()
    {
        var row = new Rect(0, 0, 200, 28);

        Assert.Equal(0, ForgeEditorTabDragTarget.MoveToFit(new Rect(-50, 0, 60, 28), row).X);
        Assert.Equal(140, ForgeEditorTabDragTarget.MoveToFit(new Rect(300, 0, 60, 28), row).X);

        // 纵向同理：往下拖不会把标签画到正文上
        Assert.Equal(0, ForgeEditorTabDragTarget.MoveToFit(new Rect(0, 90, 60, 28), row).Y);
    }

    [Fact]
    public void A_neighbour_wider_than_the_distance_dragged_is_left_alone()
    {
        // DragHelper.java:273 那道防抖闸：没拖够一整个邻居的宽度就别换位，
        // 否则鼠标停在两页交界处微动会来回跳。
        var tabs = new[] { new Rect(0, 0, 60, 28), new Rect(60, 0, 60, 28) };

        // 才挪了 10px，邻居宽 60 —— 10 * 1.3 远不够，不换
        var timid = new Rect(10, 0, 60, 28);
        Assert.Equal(-1, ForgeEditorTabDragTarget.ResolveTarget(timid, tabs[0], tabs, sourceIndex: 0));

        // 挪够了就换
        var committed = new Rect(55, 0, 60, 28);
        Assert.Equal(1, ForgeEditorTabDragTarget.ResolveTarget(committed, tabs[0], tabs, sourceIndex: 0));
    }

    [Fact]
    public void The_most_overlapped_neighbour_wins()
    {
        // 拖动矩形同时压着 b 和 c 时，取重叠面积大的那个（DragHelper.java:275-280）
        var tabs = new[]
        {
            new Rect(0, 0, 60, 28), new Rect(60, 0, 60, 28), new Rect(120, 0, 60, 28),
        };

        var dragRec = new Rect(65, 0, 60, 28);   // 压 b 55px、压 c 5px
        Assert.Equal(1, ForgeEditorTabDragTarget.ResolveTarget(dragRec, tabs[0], tabs, sourceIndex: 0));
    }

    [Fact]
    public void Only_vertical_movement_counts_as_dragging_out_of_a_horizontal_row()
    {
        // SingleRowLayoutStrategy.java:100-105 把基类那条 |dX| > 宽 × 倍率 **重写掉了**。
        // 两条同时开着的话手一动就判成拖出，行内根本排不了 —— 这正是要钉住的。
        var label = new Rect(0, 0, 60, 28);

        Assert.False(ForgeEditorTabDragTarget.IsDragOut(label, rowWidth: 300, deltaX: 120, deltaY: 0));
        Assert.True(ForgeEditorTabDragTarget.IsDragOut(label, rowWidth: 300, deltaX: 0, deltaY: 10));
    }

    // ---- 端到端 ------------------------------------------------------------

    [AvaloniaFact]
    public void 手往下带一点仍然是行内重排_不会变成分屏()
    {
        // 回归：判"拖出"曾经用上游那条 |dY| > 标签高 × 0.1。本软件的标签行只有 31 高，
        // 阈值 ≈ 3px —— 拖的时候手往下带一点就进了"拖出"（而且是单向闩，回不来），
        // 实机表现是"文档标签只能拖分屏、换不了序"。现在按**指针在不在这一行里**判。
        var (editor, window, _, a, b) = Shown();

        var press = GrabPoint(a, window);
        window.MouseDown(press, MouseButton.Left);

        // 横着拖过邻居，同时**往下带 6px**（真人拖动几乎不可能是纯水平的）
        var release = new Point(press.X + b.Bounds.Width + 5, press.Y + 6);
        window.MouseMove(release);
        window.MouseUp(release, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["b", "a", "c"], Order(editor));
        Assert.Single(editor.Area.Groups);   // 没有被当成分屏
    }

    [AvaloniaFact]
    public void 拖下去再拖回标签行_还是换序不是分屏()
    {
        // 回归：两种模式曾经是**单向闩**（照 MouseDragHelper 的 myDetachingMode 做的）——
        // 往下拖一次就永远是分屏，再拖回标签行也换不了序。
        // 实机反馈"拖下去可以、拖上去就不行"，而上游是按区域来回切的。
        var (editor, window, _, a, b) = Shown();

        var press = GrabPoint(a, window);
        window.MouseDown(press, MouseButton.Left);

        // ① 先拖到标签行下面（进入分屏那条路）
        window.MouseMove(new Point(press.X + 40, press.Y + 120));
        Dispatcher.UIThread.RunJobs();

        // ② 再拖回标签行里，越过邻居
        var back = new Point(press.X + b.Bounds.Width + 5, press.Y);
        window.MouseMove(back);
        window.MouseUp(back, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(editor.Area.Groups);                     // 没分屏
        Assert.Equal(["b", "a", "c"], Order(editor));          // 换序生效了
    }

    [AvaloniaFact]
    public void 被拖的标签跟着光标走_不被夹在标签行里()
    {
        // ★ 分工：**拖的那个不吸附，落点那块投影才吸附。**
        // 回归：早先被拖的标签被 MoveToFit 夹在行内、纵向又钉在格子上 ——
        // 往下带就顶住不动，人的感受是"被吸住了"。
        var (editor, window, _, a, _) = Shown();
        _ = editor;

        var press = GrabPoint(a, window);
        window.MouseDown(press, MouseButton.Left);
        window.MouseMove(new Point(press.X + 30, press.Y + 9));   // 往下带一点（还没出这一行）
        Dispatcher.UIThread.RunJobs();

        var row = window.GetVisualDescendants().OfType<HeaderTabsPanel>().First();
        Assert.NotNull(row.DraggedChild);
        Assert.False(double.IsNaN(row.DraggedY), "纵向应当跟手，而不是钉在格子的 y 上");
        Assert.True(row.DraggedY > 0, "往下带了就该往下走");

        window.MouseUp(new Point(press.X + 30, press.Y + 9), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(double.IsNaN(row.DraggedY), "手势结束要还原");
    }

    [AvaloniaFact]
    public void 文档标签拖动时也要画落点投影()
    {
        // 回归：空档与投影早先只写在**不换行**那条排布分支里，而文档标签走的是**换行**那条
        //（WrapRows=true），于是"投影矩形"在文档区一点效果都没有。
        var (editor, window, _, a, b) = Shown();
        _ = editor;

        var press = GrabPoint(a, window);
        window.MouseDown(press, MouseButton.Left);
        window.MouseMove(new Point(press.X + b.Bounds.Width + 5, press.Y));
        Dispatcher.UIThread.RunJobs();

        var row = window.GetVisualDescendants().OfType<HeaderTabsPanel>().First();
        Assert.NotNull(row.DraggedChild);
        Assert.NotNull(row.DropRect);          // ← 落点那一格
        Assert.Equal(HeaderTabsPanel.DraggedOpacity, row.DraggedChild!.Opacity);

        window.MouseUp(new Point(press.X + b.Bounds.Width + 5, press.Y), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(row.DropRect);
    }

    [AvaloniaFact]
    public void Dragging_a_tab_past_its_neighbour_actually_reorders_it()
    {
        var (editor, window, _, a, b) = Shown();

        var press = GrabPoint(a, window);
        window.MouseDown(press, MouseButton.Left);

        // 挪过整个 b 的宽度再多一点：既过了起拖阈值，也过了那道防抖闸
        var release = press.WithX(press.X + b.Bounds.Width + 5);
        window.MouseMove(release);
        Dispatcher.UIThread.RunJobs();

        // Dock.Avalonia ItemDragHelper 只做位移预览，Released() 才调用
        // Factory.MoveDockable；手势中途绝不能重建 TabStrip 容器。
        Assert.Equal(["a", "b", "c"], Order(editor));

        window.MouseUp(release, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["b", "a", "c"], Order(editor));

        var strip = window.GetVisualDescendants().OfType<HeaderTabsPanel>().First();
        Assert.Null(strip.DraggedChild);
        var visualOrder = strip.Children
            .OfType<ForgeEditorTab>()
            .OrderBy(tab => strip.SlotOf(tab)!.Value.X)
            .Select(tab => tab.Title);
        Assert.Equal(["Bravo.cs", "Alpha.cs", "Charlie.cs"], visualOrder);
    }

    [AvaloniaFact]
    public void A_click_that_does_not_travel_far_enough_never_starts_a_drag()
    {
        // 起拖阈值 DRAG_START_DEADZONE = 7（MouseDragHelper.java:32）。
        //
        // ⚠ 这里必须看 DraggedChild，不能看次序。
        //    最初我写的是"次序没变"—— 但把阈值整段删掉那条测试**照样绿**：
        //    3px 位移压根过不了 findMostOverlapping 那道 freeSpace 闸
        //    （邻居宽 70，3 × 1.3 差得远），换位本来就不会发生。
        //    也就是说它测的是防抖闸，不是阈值，对阈值零判别力。
        //    阈值真正挡住的是**视觉**：没有它，点一下标签就会跟手跳 3px。
        var (_, window, _, a, _) = Shown();
        var strip = window.GetVisualDescendants().OfType<HeaderTabsPanel>().First();

        var press = GrabPoint(a, window);
        window.MouseDown(press, MouseButton.Left);
        window.MouseMove(press.WithX(press.X + 3));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(strip.DraggedChild);

        // 过了阈值就该浮起来
        window.MouseMove(press.WithX(press.X + 12));
        Dispatcher.UIThread.RunJobs();

        Assert.Same(a, strip.DraggedChild);

        // 松手收摊：不收的话标签会一直飘着
        window.MouseUp(press, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(strip.DraggedChild);
    }

    // ---- 拖出去分屏 --------------------------------------------------------

    [Fact]
    public void The_drop_area_does_not_include_the_tab_row()
    {
        // JBTabs.java:101-115：标签在上方时 insets.top = 标签高，从整块里扣掉。
        // 不扣的话，「拖到上边缘 → 往上切一刀」的判定带会盖在标签行上，
        // 而那一带正是行内重排要用的 —— 两件事在同一片像素上打架。
        var area = ForgeEditorTabDragTarget.DropArea(new Rect(0, 0, 600, 400), tabRowHeight: 40);

        Assert.Equal(new Rect(0, 40, 600, 360), area);
    }

    [Fact]
    public void Each_side_highlights_exactly_half_and_the_centre_highlights_all()
    {
        // TabsUtil.java:95-111 · updateBoundsWithDropSide
        var area = new Rect(0, 40, 600, 360);

        Assert.Equal(new Rect(300, 40, 300, 360), ForgeEditorTabDragTarget.HighlightFor(area, EditorDropSide.Right));
        Assert.Equal(new Rect(0, 40, 300, 360), ForgeEditorTabDragTarget.HighlightFor(area, EditorDropSide.Left));
        Assert.Equal(new Rect(0, 220, 600, 180), ForgeEditorTabDragTarget.HighlightFor(area, EditorDropSide.Bottom));
        Assert.Equal(new Rect(0, 40, 600, 180), ForgeEditorTabDragTarget.HighlightFor(area, EditorDropSide.Top));
        Assert.Equal(area, ForgeEditorTabDragTarget.HighlightFor(area, EditorDropSide.Center));
        Assert.Null(ForgeEditorTabDragTarget.HighlightFor(area, EditorDropSide.None));
    }

    [AvaloniaFact]
    public void Dragging_a_tab_out_to_the_right_edge_splits_the_editor()
    {
        var (editor, window, control, a, _) = Shown();
        var overlay = control.GetVisualDescendants().OfType<ForgeDragOverlay>().First();

        window.MouseDown(GrabPoint(a, window), MouseButton.Left);

        var drop = control.TranslatePoint(
            new Point(control.Bounds.Width * 0.93, control.Bounds.Height * 0.6), window)!.Value;
        window.MouseMove(drop);
        Dispatcher.UIThread.RunJobs();

        // 松手之前先有高亮：右半边，且不含标签行
        var highlight = Assert.NotNull(overlay.AreaRect);
        Assert.True(highlight.X >= (control.Bounds.Width / 2) - 1, $"高亮该在右半边，实际 {highlight}");
        Assert.True(highlight.Y > 0, $"高亮不该盖到标签行上，实际 {highlight}");

        Assert.Single(editor.Area.Groups);
        window.MouseUp(drop, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, editor.Area.Groups.Count);
        Assert.Null(overlay.AreaRect);
    }

    [Fact]
    public void Splitting_a_group_away_from_its_only_tab_is_refused()
    {
        // 用户看到的"中间空了"就是这一条漏掉的后果：
        // 只有一个文档时把它拖到自己那一组的边上，原组会被切成一个**空窗格**留在树里。
        // 上游两头堵死这件事 —— EditorWindow.kt:546-548（split 在 tabCount == 0 时
        // return null）+ EditorTabbedContainer.kt:505-514（先关源，空窗口随即销毁）。
        var area = New("a");

        Assert.False(area.Area.DropTab("a", area.Area.Groups[0], EditorDropSide.Right));
        Assert.Single(area.Area.Groups);
        Assert.NotEmpty(area.Area.Groups[0].Tabs);
    }

    [Fact]
    public void An_empty_group_never_survives_a_notification()
    {
        // 架构层的那条不变量：**广播之前树里不许有空组**（唯一一组除外）。
        // 它兜的是所有路径 —— 包括绕过 EditorArea 直接动 EditorGroup 的那些
        // （读档时按内容工厂的结果逐个丢弃标签页就是这么干的）。
        var area = New("a", "b");
        area.Area.DropTab("b", area.Area.Groups[0], EditorDropSide.Right);
        Assert.Equal(2, area.Area.Groups.Count);

        // 绕过 EditorArea，直接把其中一组清空
        var victim = area.Area.Groups[0];
        victim.Close(victim.Tabs[0].Id);
        Assert.Equal(2, area.Area.Groups.Count);   // 还没广播，树里确实躺着一个空组

        area.Area.Refresh();

        Assert.Single(area.Area.Groups);

        // 第二条不变量：CurrentGroup 不能还指着被摘掉的那一组
        Assert.Same(area.Area.Groups[0], area.Area.CurrentGroup);
    }

    [Fact]
    public void A_drop_that_changes_nothing_keeps_the_very_same_group_object()
    {
        // 组身份不能为一个净效果为零的手势而丢：视图按组缓存宿主与内容控件，
        // 换一个组对象就等于把宿主塞进去的实时画面重新挂载一遍。
        var area = New("a");
        var before = area.Area.Groups[0];

        Assert.False(area.Area.DropTab("a", before, EditorDropSide.Right));

        Assert.Same(before, area.Area.Groups[0]);
    }

    [Fact]
    public void No_sequence_of_drops_ever_leaves_an_empty_group()
    {
        // 空组会累积：漏掉一次就多一块空白，反复拖越攒越多。
        // 这条兜住的是"任何落点组合之后，树里都不该有空组"。
        var area = New("a", "b", "c");

        area.Area.DropTab("a", area.Area.Groups[0], EditorDropSide.Right);
        area.Area.DropTab("b", area.Area.Groups[0], EditorDropSide.Bottom);
        area.Area.DropTab("c", area.Area.Groups[^1], EditorDropSide.Left);
        area.Area.DropTab("a", area.Area.Groups[0], EditorDropSide.Right);

        Assert.All(area.Area.Groups, g => Assert.NotEmpty(g.Tabs));
    }

    [AvaloniaFact]
    public void Dragging_the_only_document_out_does_not_blank_the_editor()
    {
        var editor = new EditorAreaViewModel();
        editor.Open("only", "Only.cs");

        var control = new ForgeEditorControl();
        control.Attach(editor);
        var window = new Window { Width = 800, Height = 500, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var tab = control.GetVisualDescendants().OfType<ForgeEditorTab>().Single();
        window.MouseDown(GrabPoint(tab, window), MouseButton.Left);

        var drop = control.TranslatePoint(
            new Point(control.Bounds.Width * 0.93, control.Bounds.Height * 0.6), window)!.Value;
        window.MouseMove(drop);
        window.MouseUp(drop, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(editor.Area.Groups);
        Assert.Single(control.GetVisualDescendants().OfType<ForgeEditorTab>());
    }

    [AvaloniaFact]
    public void Splitting_while_the_keyboard_focus_is_inside_the_editor_still_works()
    {
        // ★ 「拖动分屏后中间空了、再打开一个标签又全出来」的根因就在这一条。
        //
        //   重建要给缓存的组宿主换父容器，而换父容器会带走键盘焦点；
        //   IsKeyboardFocusWithin 一变，它的处理器过去直接调 Sync ——
        //   于是在外层建到一半时重入。内层建完一棵树、外层拿着过期的局部变量接着建，
        //   结果视图与模型对不上：模型两组，界面上只剩一个标签页。
        //   后来那次 Sync 没被焦点打断，就"全出来了"。
        //
        //   ⚠ 之前所有测试都测不出来：headless 里没人碰过**真实焦点**。
        //     所以这条一定要显式把焦点放进编辑区里面。
        var editor = new EditorAreaViewModel();
        editor.Open("a", "Alpha.cs", new Button { Content = "A" });
        editor.Open("b", "Bravo.cs", new Button { Content = "B" });

        var control = new ForgeEditorControl();
        control.Attach(editor);
        var window = new Window { Width = 800, Height = 500, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // ⚠ 按内容挑，别拿 OfType<Button>().First() —— 标签条右端那个 ⋮（ForgeHeaderButton）
        //   也是 Button，排在前面，焦点会跑到它身上，这条就白测了。
        // ⚠ 挑**宿主内容**里那个按钮，别拿 OfType<Button>().First() —— 标签条右端那个 ⋮
        //   （ForgeHeaderButton）也是 Button 且排在前面，焦点跑到它身上这条就白测了。
        control.GetVisualDescendants().OfType<Button>().First(x => x is not ForgeHeaderButton).Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.True(control.IsKeyboardFocusWithin, "前置条件：焦点得真在编辑区里，否则这条测不到东西");

        editor.Area.DropTab("b", editor.Area.Groups[0], EditorDropSide.Right);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, editor.Area.Groups.Count);
        Assert.Equal(
            ["Alpha.cs", "Bravo.cs"],
            control.GetVisualDescendants().OfType<ForgeEditorTab>().Select(x => x.Title).Order());
    }

    [AvaloniaFact]
    public void Moving_a_tab_whose_icon_is_a_control_does_not_blank_the_editor()
    {
        // ★ 真机复现出来的那条。宿主给的 Icon 是**控件实例**（LDVision 给的是 MaterialIcon），
        //   ForgeEditorTab 把它收成 VisualChildren。标签页搬到另一组时会新建一个
        //   ForgeEditorTab 去收养同一个图标，而旧的那个还攥着它 —— 当场抛
        //   「已有父容器」，异常打断 Sync，整块编辑区变空白。
        //
        //   ⚠ 我此前所有用例的 Icon 要么是 null 要么是 Geometry（数据，画多少遍都行），
        //     **一次都没用过控件当图标** —— 所以headless 全绿、真机一拖就白。
        var editor = new EditorAreaViewModel();
        var first = editor.Open("a", "Alpha.cs");
        first.Icon = new Border { Width = 12, Height = 12 };
        editor.Open("b", "Bravo.cs");

        var control = new ForgeEditorControl();
        control.Attach(editor);
        var window = new Window { Width = 800, Height = 500, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        editor.Area.DropTab("a", editor.Area.Groups[0], EditorDropSide.Right);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, editor.Area.Groups.Count);
        Assert.Equal(
            ["Alpha.cs", "Bravo.cs"],
            control.GetVisualDescendants().OfType<ForgeEditorTab>().Select(x => x.Title).Order());
    }

    [AvaloniaFact]
    public void An_icon_factory_gives_every_tab_its_own_instance()
    {
        // 「拆分」会把同一页同时开在两组里，一个图标控件不可能有两个父。
        // 宿主传 Func<Control> 工厂，两边就各拿一份，都能显示。
        var editor = new EditorAreaViewModel();
        var made = 0;
        var vm = editor.Open("a", "Alpha.cs");
        vm.Icon = new Func<Control>(() => { made++; return new Border { Width = 12, Height = 12 }; });

        var control = new ForgeEditorControl();
        control.Attach(editor);
        var window = new Window { Width = 800, Height = 500, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var group = editor.Area.Groups[0];
        editor.Area.Split(group, isVertical: false, tabId: "a");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, editor.Area.Groups.Count);
        Assert.Equal(2, control.GetVisualDescendants().OfType<ForgeEditorTab>().Count());

        // 两个标签页各一份图标，互不相同
        var icons = control.GetVisualDescendants().OfType<ForgeEditorTab>().Select(x => x.Icon).ToList();
        Assert.Equal(2, icons.Distinct().Count());
        Assert.Equal(2, made);
    }

    [AvaloniaFact]
    public void Moving_a_tab_between_groups_releases_the_old_content_host()
    {
        // 按 (组, id) 做键的缓存过去只增不减，两个后果：搬组时新宿主收养不了旧内容
        // （宿主塞控件就当场抛「已有父容器」），以及关掉的页 / 摘掉的组一直被强引用着放不掉。
        var editor = new EditorAreaViewModel();
        editor.Open("a", "Alpha.cs", new Button { Content = "A" });
        editor.Open("b", "Bravo.cs", new Button { Content = "B" });

        var control = new ForgeEditorControl();
        control.Attach(editor);
        var window = new Window { Width = 800, Height = 500, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        editor.Area.DropTab("b", editor.Area.Groups[0], EditorDropSide.Right);
        Dispatcher.UIThread.RunJobs();

        // 内容控件只能有一个父：搬过去之后它必须挂在**新**组底下
        var moved = control.GetVisualDescendants().OfType<Button>().Single(x => (string?)x.Content == "B");
        Assert.NotNull(moved.Parent);

        editor.Close("b");
        Dispatcher.UIThread.RunJobs();

        // 关掉之后不该再留在可视树里 —— 留着就是宿主的相机会话 / 图像帧回收不掉
        Assert.DoesNotContain(
            control.GetVisualDescendants().OfType<Button>(), x => (string?)x.Content == "B");
    }

    [AvaloniaFact]
    public void Merging_a_split_group_back_keeps_both_visual_tabs()
    {
        var editor = new EditorAreaViewModel();
        editor.Open("a", "Alpha.cs").Icon = new Border { Width = 12, Height = 12 };
        editor.Open("b", "Bravo.cs").Icon = new Border { Width = 12, Height = 12 };

        var control = new ForgeEditorControl();
        control.Attach(editor);
        var window = new Window { Width = 800, Height = 500, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var left = editor.Area.Groups[0];
        editor.Area.DropTab("b", left, EditorDropSide.Right);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, editor.Area.Groups.Count);

        editor.Area.DropTab("b", left, EditorDropSide.Center);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(editor.Area.Groups);
        Assert.Equal(["a", "b"], editor.Area.Groups[0].Tabs.Select(x => x.Id));
        Assert.Equal(2, control.GetVisualDescendants().OfType<ForgeEditorTab>().Count());
        Assert.Equal(
            ["Alpha.cs", "Bravo.cs"],
            control.GetVisualDescendants().OfType<ForgeEditorTab>()
                .OrderBy(x => x.Bounds.X)
                .Select(x => x.Title));
    }

    [AvaloniaFact]
    public void Dragging_a_copied_split_tab_back_to_the_original_group_merges_it()
    {
        var editor = new EditorAreaViewModel();
        editor.Open("a", "Alpha.cs");
        editor.Open("b", "Bravo.cs");
        var control = new ForgeEditorControl();
        control.Attach(editor);
        var window = new Window { Width = 800, Height = 500, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var original = editor.Area.Groups[0];
        editor.Area.Split(original, isVertical: false, tabId: "b");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(3, control.GetVisualDescendants().OfType<ForgeEditorTab>().Count());

        var copied = control.GetVisualDescendants().OfType<ForgeEditorTab>()
            .Where(x => x.Title == "Bravo.cs")
            .OrderBy(x => x.TranslatePoint(default, control)!.Value.X)
            .Last();
        var originalStrip = control.GetVisualDescendants().OfType<TabStrip>()
            .OrderBy(x => x.TranslatePoint(default, control)!.Value.X)
            .First();
        var press = GrabPoint(copied, window);
        var originalX = originalStrip.TranslatePoint(
            new Point(originalStrip.Bounds.Width / 2, originalStrip.Bounds.Height / 2), window)!.Value.X;
        var drop = new Point(originalX, press.Y);
        window.MouseDown(press, MouseButton.Left);
        window.MouseMove(drop);
        Dispatcher.UIThread.RunJobs();
        window.MouseUp(drop, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(editor.Area.Groups);
        Assert.Equal(
            ["Alpha.cs", "Bravo.cs"],
            control.GetVisualDescendants().OfType<ForgeEditorTab>()
                .OrderBy(x => x.Bounds.X)
                .Select(x => x.Title));
    }

    [AvaloniaFact]
    public void Dropping_a_split_tab_on_the_other_tab_row_merges_the_groups()
    {
        var (editor, window, control, _, _) = Shown();
        var left = editor.Area.Groups[0];
        editor.Area.DropTab("b", left, EditorDropSide.Right);
        Dispatcher.UIThread.RunJobs();

        var rightTab = control.GetVisualDescendants().OfType<ForgeEditorTab>()
            .Single(x => x.Title == "Bravo.cs");
        var leftStrip = control.GetVisualDescendants().OfType<TabStrip>()
            .OrderBy(x => x.TranslatePoint(default, control)!.Value.X)
            .First();
        var press = GrabPoint(rightTab, window);
        var leftX = leftStrip.TranslatePoint(
            new Point(leftStrip.Bounds.Width / 2, leftStrip.Bounds.Height / 2), window)!.Value.X;
        // 严格保持同一 Y：用户最自然的动作是沿标签行横着拖回去。
        // 若只靠 SingleRowLayoutStrategy 的纵向拖出判据，这次不会进入跨组拖放。
        var drop = new Point(leftX, press.Y);

        window.MouseDown(press, MouseButton.Left);
        window.MouseMove(drop);
        Dispatcher.UIThread.RunJobs();
        window.MouseUp(drop, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(editor.Area.Groups);
        Assert.Equal(["a", "c", "b"], editor.Area.Groups[0].Tabs.Select(x => x.Id));
        Assert.Equal(3, control.GetVisualDescendants().OfType<ForgeEditorTab>().Count());
    }

    [AvaloniaFact]
    public void Leaving_the_source_strip_horizontally_starts_cross_group_dragging()
    {
        var (editor, window, control, _, _) = Shown();
        var left = editor.Area.Groups[0];
        editor.Area.DropTab("b", left, EditorDropSide.Right);
        Dispatcher.UIThread.RunJobs();

        // 源组仍有 a/c 两页，a 不在标签条边缘。旧实现只看纵向位移，
        // 沿同一标签行平拖到右窗格时永远停在行内重排状态。
        var sourceTab = control.GetVisualDescendants().OfType<ForgeEditorTab>()
            .Single(x => x.Title == "Alpha.cs");
        var targetStrip = control.GetVisualDescendants().OfType<TabStrip>()
            .OrderBy(x => x.TranslatePoint(default, control)!.Value.X)
            .Last();
        var overlay = control.GetVisualDescendants().OfType<ForgeDragOverlay>().Single();
        var press = GrabPoint(sourceTab, window);
        var drop = targetStrip.TranslatePoint(
            new Point(targetStrip.Bounds.Width / 2, targetStrip.Bounds.Height / 2), window)!.Value;
        drop = new Point(drop.X, press.Y);

        window.MouseDown(press, MouseButton.Left);
        window.MouseMove(drop);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(overlay.ThumbImage);
        Assert.NotNull(overlay.AreaRect);

        window.MouseUp(drop, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, editor.Area.Groups.Count);
        Assert.Equal(["c"], editor.Area.Groups[0].Tabs.Select(x => x.Id));
        Assert.Equal(["b", "a"], editor.Area.Groups[1].Tabs.Select(x => x.Id));
    }

    [AvaloniaFact]
    public void Releasing_over_the_editor_body_always_clears_the_drag_session()
    {
        var (_, window, control, a, _) = Shown();
        var overlay = control.GetVisualDescendants().OfType<ForgeDragOverlay>().Single();
        var press = GrabPoint(a, window);
        var release = control.TranslatePoint(
            new Point(control.Bounds.Width * 0.55, control.Bounds.Height * 0.45), window)!.Value;

        window.MouseDown(press, MouseButton.Left);
        window.MouseMove(release);
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(overlay.ThumbImage);

        // 松手位置不在源 TabStrip 的事件路由上；拖拽若归标签条所有，这里就收不了尾。
        window.MouseUp(release, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(overlay.ThumbImage);
        Assert.Null(overlay.ThumbRect);
        Assert.Null(overlay.AreaRect);
        Assert.All(
            control.GetVisualDescendants().OfType<HeaderTabsPanel>(),
            strip => Assert.Null(strip.DraggedChild));
    }

    [AvaloniaFact]
    public void Drag_session_is_finished_before_a_merge_changes_the_split_tree()
    {
        var (editor, window, control, _, _) = Shown();
        var left = editor.Area.Groups[0];
        editor.Area.DropTab("b", left, EditorDropSide.Right);
        Dispatcher.UIThread.RunJobs();

        var overlay = control.GetVisualDescendants().OfType<ForgeDragOverlay>().Single();
        var rightTab = control.GetVisualDescendants().OfType<ForgeEditorTab>()
            .Single(x => x.Title == "Bravo.cs");
        var leftStrip = control.GetVisualDescendants().OfType<TabStrip>()
            .OrderBy(x => x.TranslatePoint(default, control)!.Value.X)
            .First();
        var press = GrabPoint(rightTab, window);
        var drop = leftStrip.TranslatePoint(
            new Point(leftStrip.Bounds.Width / 2, leftStrip.Bounds.Height / 2), window)!.Value;

        var observedMerge = false;
        editor.Area.Changed += (_, _) =>
        {
            if (editor.Area.Groups.Count != 1)
            {
                return;
            }

            observedMerge = true;
            Assert.Null(overlay.ThumbImage);
            Assert.Null(overlay.ThumbRect);
            Assert.Null(overlay.AreaRect);
            Assert.All(
                control.GetVisualDescendants().OfType<HeaderTabsPanel>(),
                panel => Assert.Null(panel.DraggedChild));
        };

        window.MouseDown(press, MouseButton.Left);
        window.MouseMove(drop);
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(overlay.ThumbImage);
        window.MouseUp(drop, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(observedMerge);
        Assert.Single(editor.Area.Groups);
    }

    [AvaloniaFact]
    public void The_control_tree_keeps_matching_the_model_through_a_whole_sequence()
    {
        // 「中间空了」的**通用**判据：界面上的标签集合必须始终等于模型里的。
        //
        // 之前是漏一条路径、修一条、再补一条回归测试 —— 永远不知道还剩几条没堵。
        // 这一条换个方向：不管做了什么结构变更，两边必须对得上。
        // 新增改动路径若忘了维护不变量，这里会红，不必等用户看见空白。
        var editor = new EditorAreaViewModel();
        editor.Open("a", "Alpha.cs");
        editor.Open("b", "Bravo.cs");
        editor.Open("c", "Charlie.cs");

        var control = new ForgeEditorControl();
        control.Attach(editor);
        var window = new Window { Width = 900, Height = 600, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        void Check(string step)
        {
            Dispatcher.UIThread.RunJobs();
            var model = editor.Area.Groups.SelectMany(g => g.Tabs.Select(t => t.Title)).Order().ToList();
            var shown = control.GetVisualDescendants().OfType<ForgeEditorTab>()
                .Select(x => x.Title ?? string.Empty).Order().ToList();

            Assert.Equal(model, shown);
            Assert.All(editor.Area.Groups, g => Assert.NotEmpty(g.Tabs));
            Assert.True(model.Count > 0, $"{step}：模型被清空了");
        }

        Check("初始");

        editor.Area.DropTab("a", editor.Area.Groups[0], EditorDropSide.Right);
        Check("拖 a 到右边分屏");

        editor.Area.DropTab("b", editor.Area.Groups[^1], EditorDropSide.Bottom);
        Check("拖 b 到下边分屏");

        editor.ReorderTab("a", "b");
        Check("重排");

        editor.Close("b");
        Check("关掉 b");

        editor.Close("c");
        Check("关掉 c —— 组会空，必须被摘掉");

        editor.Area.DropTab("a", editor.Area.Groups[0], EditorDropSide.Right);
        Check("只剩一页时再拖一次");
    }

    [AvaloniaFact]
    public void Dropping_back_onto_the_middle_of_the_same_group_changes_nothing()
    {
        // 拖出去 ≠ 一定分屏：落在中心是"并进那一组"，而源就是目标 —— 什么都不该发生。
        // 少了这条，改中心分支时很容易变成"落回原处也切一刀"。
        var (editor, window, control, a, _) = Shown();
        var overlay = control.GetVisualDescendants().OfType<ForgeDragOverlay>().First();

        window.MouseDown(GrabPoint(a, window), MouseButton.Left);

        var middle = control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height * 0.6), window)!.Value;
        window.MouseMove(middle);
        Dispatcher.UIThread.RunJobs();

        // 中心：整片落点区都高亮（不含标签行）
        var highlight = Assert.NotNull(overlay.AreaRect);
        Assert.Equal(control.Bounds.Width, highlight.Width, 1);

        window.MouseUp(middle, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(editor.Area.Groups);
        Assert.Equal(["a", "b", "c"], Order(editor));
    }

    /// <summary>
    /// 抓在标签页的<b>左侧文字区</b>，不是正中。
    /// </summary>
    /// <remarks>
    /// ⚠ 别改成 <c>Bounds.Width / 2</c>。标签页窄的时候关闭按钮的命中区
    /// （<see cref="ForgeEditorTab.TabIconHitRect"/>，图标外还放大了 3px）会一直吃到中线左边 ——
    /// 按正中等于按了关闭，测出来是"一按下标签就没了"，和拖拽毫无关系。
    /// </remarks>
    private static Point GrabPoint(ForgeEditorTab tab, Window window) =>
        tab.TranslatePoint(new Point(10, tab.Bounds.Height / 2), window)!.Value;

    /// <summary>
    /// 把标签拖出整个编辑区 → 浮成一个独立的编辑窗口，内容对象**原样搬过去**。
    /// </summary>
    /// <remarks>
    /// 对位上游 <c>DockManagerImpl</c>：拖离所有容器就新开一个窗口装着
    /// （<c>DockableEditorTabbedContainer</c>）。浮窗里是一整个编辑区，不是"单文档窗口"。
    /// <para>
    /// 内容与图标是宿主的实例（相机实时画面就在里头），必须搬同一个对象 ——
    /// 关掉再开一个等于把相机断了重连，所以走 Detach/Attach 而不是 Close/Open。
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void 把标签拖出编辑区就浮成独立窗口()
    {
        var (editor, window, control, a, _) = Shown();
        var content = new object();
        editor.Find("a")!.Content = content;

        var press = GrabPoint(a, window);
        window.MouseDown(press, MouseButton.Left);

        // 竖着拖出标签行，再远远离开整个编辑区
        window.MouseMove(press.WithY(press.Y + 40));
        Dispatcher.UIThread.RunJobs();
        window.MouseMove(new Point(press.X, window.Height + 200));
        Dispatcher.UIThread.RunJobs();
        window.MouseUp(new Point(press.X, window.Height + 200), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["b", "c"], Order(editor));

        var floating = Assert.Single(control.FloatingWindows);
        var floatedArea = floating.GetVisualDescendants().OfType<ForgeEditorControl>().Single();
        var floatedTab = floatedArea.Editor!.Find("a");

        Assert.NotNull(floatedTab);
        Assert.Same(content, floatedTab!.Content);

        floating.Close();
        window.Close();
    }

    /// <summary>
    /// 一页都没开时不摆标签行 —— 空的标签行只剩底下那条分隔线，凭空在编辑区顶上画一道。
    /// </summary>
    [AvaloniaFact]
    public void 空的编辑区不画标签行那条线()
    {
        var editor = new EditorAreaViewModel();
        var control = new ForgeEditorControl();
        control.Attach(editor);

        var window = new Window { Width = 600, Height = 400, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(
            window.GetVisualDescendants().OfType<Border>(),
            x => x.IsEffectivelyVisible && x.BorderThickness.Bottom > 0);

        // 开一页，线跟着标签行一起回来
        editor.Open("a", "Alpha.cs");
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            window.GetVisualDescendants().OfType<Border>(),
            x => x.IsEffectivelyVisible && x.BorderThickness.Bottom > 0);

        window.Close();
    }

    /// <summary>
    /// 浮窗里只有一份文档时不摆标签条，文档名写在窗口标题上。
    /// </summary>
    /// <remarks>一个窗口一份文档，还挂个标签条是重复的（与 Rider 一致）。再开一页标签条自然回来。</remarks>
    [AvaloniaFact]
    public void 单文档的浮窗不摆标签条而是写在标题上()
    {
        var (_, window, control, a, _) = Shown();

        var press = GrabPoint(a, window);
        window.MouseDown(press, MouseButton.Left);
        window.MouseMove(press.WithY(press.Y + 40));
        Dispatcher.UIThread.RunJobs();
        window.MouseMove(new Point(press.X, window.Height + 200));
        Dispatcher.UIThread.RunJobs();
        window.MouseUp(new Point(press.X, window.Height + 200), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        var floating = Assert.Single(control.FloatingWindows);
        Assert.Equal("Alpha.cs", floating.Title);

        // 标签条整条（含底下那条分割线的宿主）被收起来了
        Assert.Empty(floating.GetVisualDescendants().OfType<ForgeEditorTab>());

        floating.Close();
        window.Close();
    }

    /// <summary>关掉浮窗，里面还开着的文档回到主编辑区 —— 别把宿主的内容跟着窗口一起扔了。</summary>
    [AvaloniaFact]
    public void 关掉浮窗文档回到主编辑区()
    {
        var (editor, window, control, a, _) = Shown();

        var press = GrabPoint(a, window);
        window.MouseDown(press, MouseButton.Left);
        window.MouseMove(press.WithY(press.Y + 40));
        Dispatcher.UIThread.RunJobs();
        window.MouseMove(new Point(press.X, window.Height + 200));
        Dispatcher.UIThread.RunJobs();
        window.MouseUp(new Point(press.X, window.Height + 200), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        var floating = Assert.Single(control.FloatingWindows);
        floating.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("a", Order(editor));
        Assert.Empty(control.FloatingWindows);

        window.Close();
    }

    private static (EditorAreaViewModel Editor, Window Window, ForgeEditorControl Control, ForgeEditorTab A, ForgeEditorTab B) Shown()
    {
        // 标题给真实长度：一个字母的标签只有 49px 宽，几何上不足以演示重排。
        // ⚠ 标题在 Open 时给，不能事后赋 —— 改 VM 的 Title 不发 Area.Changed，
        //   视图不会回头再读（和"新开文档没图标"是同一个坑）。
        var editor = new EditorAreaViewModel();
        editor.Open("a", "Alpha.cs");
        editor.Open("b", "Bravo.cs");
        editor.Open("c", "Charlie.cs");

        var control = new ForgeEditorControl();
        control.Attach(editor);

        var window = new Window { Width = 600, Height = 400, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var tabs = control.GetVisualDescendants().OfType<ForgeEditorTab>().ToList();
        return (editor, window, control, tabs.First(x => x.Title == "Alpha.cs"), tabs.First(x => x.Title == "Bravo.cs"));
    }

    private static EditorAreaViewModel New(params string[] ids)
    {
        var area = new EditorAreaViewModel();
        foreach (var id in ids)
        {
            area.Open(id, id);
        }

        return area;
    }

    private static string[] Order(EditorAreaViewModel area) =>
        [.. area.Area.Groups[0].Tabs.Select(t => t.Id)];
}
