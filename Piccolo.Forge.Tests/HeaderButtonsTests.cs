using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 面板标题条与编辑区标签条右端的那排按钮。
/// </summary>
/// <remarks>
/// 对应上游标题条右侧那条 <c>ActionToolbar</c>
/// （<c>ToolWindowHeader.kt:119</c>：<c>ShowOptionsAction</c> + <c>HideAction</c>，
/// 加在 <c>BorderLayout.EAST</c>），以及标签条右端的动作区。
/// 宿主可以关掉，也可以塞自己的按钮（上游的 <c>setTitleActions</c>）。
/// </remarks>
public class HeaderButtonsTests
{
    [AvaloniaFact]
    public void 面板标题条默认有选项与隐藏两个按钮()
    {
        var (window, frame, _) = Build();

        var buttons = Buttons(frame);
        Assert.Equal(2, buttons.Count);
        Assert.All(buttons, b => Assert.True(b.Bounds.Width > 0, "按钮没排上"));

        // 靠右排，且在标题条那一行里
        var header = frame.GetVisualDescendants().OfType<ForgeToolWindowHeader>().Single();
        Assert.All(buttons, b => Assert.True(
            b.TranslatePoint(default, header)!.Value.X > header.Bounds.Width / 2,
            "按钮没靠右"));

        window.Close();
    }

    [AvaloniaFact]
    public void 关掉开关就没有按钮()
    {
        var (window, frame, _) = Build();
        frame.ShowHeaderButtons = false;
        Render(window);

        Assert.DoesNotContain(Buttons(frame), b => b.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void 点隐藏收起面板但条带按钮还在()
    {
        var (window, frame, shell) = Build();
        Assert.True(shell.Manager.Layout.GetInfo("相机列表")!.IsVisible);

        // 两个按钮里靠右那个是「隐藏」
        var hide = Buttons(frame).OrderBy(b => b.TranslatePoint(default, frame)!.Value.X).Last();
        ClickCenter(window, frame, hide);

        Assert.False(shell.Manager.Layout.GetInfo("相机列表")!.IsVisible);

        // 条带按钮还在（「隐藏」不等于「从条带上摘掉」——那是右键菜单里那条）
        Assert.Contains(
            frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>(),
            x => x.Title == "相机列表");

        window.Close();
    }

    [AvaloniaFact]
    public void 宿主的按钮排在内置那两个左边()
    {
        var mine = new ForgeHeaderButton { Icon = ForgeIcons.Find };
        var (window, frame, _) = Build(f => f.HeaderButtonsExtension = _ => [mine]);

        var buttons = Buttons(frame);
        Assert.Equal(3, buttons.Count);
        Assert.Same(mine, buttons.OrderBy(b => b.TranslatePoint(default, frame)!.Value.X).First());

        window.Close();
    }

    [AvaloniaFact]
    public void 编辑区每组标签条右端有一个更多按钮()
    {
        var (window, _, editor) = BuildWithEditor();

        var buttons = editor.GetVisualDescendants().OfType<ForgeHeaderButton>().ToList();
        Assert.Single(buttons);

        editor.ShowTabActions = false;
        Render(window);
        Assert.Empty(editor.GetVisualDescendants().OfType<ForgeHeaderButton>());

        window.Close();
    }

    /// <summary>⋮ 的菜单里应当有这一组的每一页，外加关闭系列。</summary>
    /// <remarks>
    /// 直接建菜单来断言内容 —— 弹出层在 headless 下建不出来
    /// （<c>Unable to create IPopupImpl</c>），所以「建」与「弹」在实现里就是分开的。
    /// 顺带把宿主扩展点真的会被调用也一并钉住。
    /// </remarks>
    [AvaloniaFact]
    public void 更多菜单列出这一组的标签页()
    {
        var (window, _, editor) = BuildWithEditor();

        string? seenAnchor = null;
        editor.TabActionsMenuExtension = (menu, anchor) =>
        {
            seenAnchor = anchor;
            menu.Items.Add(new MenuItem { Header = "我自己的项" });
        };

        var group = editor.Editor!.Groups[0];
        var headers = editor.BuildTabActionsMenu(group).Items.OfType<MenuItem>()
            .Select(x => x.Header as string)
            .ToList();

        Assert.Equal(["移动横线测试图", "高斯光斑", "关闭其他标签页", "关闭所有标签页", "我自己的项"], headers);
        Assert.Equal("d0", seenAnchor);

        window.Close();
    }

    /// <summary>菜单里点某一页 = 切到那一页。</summary>
    [AvaloniaFact]
    public void 更多菜单里点一页就切过去()
    {
        var (window, _, editor) = BuildWithEditor();
        var vm = editor.Editor!;
        vm.Select("d0");

        var item = editor.BuildTabActionsMenu(vm.Groups[0]).Items.OfType<MenuItem>()
            .First(x => (string?)x.Header == "高斯光斑");
        item.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));

        Assert.Equal("d1", vm.ActiveTab?.Id);
        window.Close();
    }

    /// <summary>
    /// ⋮ 的菜单结构照上游的 gear popup（<c>ToolWindowImpl.kt:855-888</c>）：
    /// 视图模式 ▸ / 移至 ▸ / 分隔线 / 从边栏中移除。
    /// </summary>
    /// <remarks>
    /// 与**条带按钮**的右键菜单是两份，别混：那份是「这个按钮」的（隐藏 / 移动 / 显示名字），
    /// 这份是「这个面板」的。
    /// </remarks>
    [AvaloniaFact]
    public void 选项菜单是上游那套结构()
    {
        var (window, _, shell) = Build();

        var menu = ForgeMenuBuilder.ForHeaderOptions(
            shell.Manager, "相机列表", viewModes: ForgeControl.SupportedViewModes);
        var top = menu.Items.OfType<MenuItem>().Select(x => x.Header as string).ToList();

        Assert.Equal(["视图模式", "移至", "从边栏中移除"], top);
        Assert.Contains(menu.Items, x => x is Separator);

        // 视图模式：只列库画得出来的那些（另见「视图模式只列画得出来的形态」），当前那种打勾
        var modes = menu.Items.OfType<MenuItem>().First(x => (string?)x.Header == "视图模式");
        Assert.Equal(
            ["停靠固定", "停靠不固定", "取消停靠", "浮动", "窗口"],
            modes.Items.OfType<MenuItem>().Select(x => x.Header as string));
        Assert.Single(modes.Items.OfType<MenuItem>(), x => x.IsChecked);

        window.Close();
    }

    /// <summary>视图模式里列的，必须是视图**真画得出来**的那些。</summary>
    /// <remarks>
    /// 回归：曾经把四种都摆上去而一种都没实现 —— 用户一点「停靠不固定」，视图仍把它当
    /// 停靠面板塞进同一列，而模型认为 SLIDING 与 DOCKED 是**两个格**
    /// （`ToolWindowSlot` 的键含形态）、互斥判定放行，于是同一列里两块面板叠着画，
    /// 还写进了存档、重启也回不来。现在四种都实现了，这条钉的是"菜单与实现同步"。
    /// </remarks>
    [AvaloniaFact]
    public void 视图模式列的就是视图支持的()
    {
        var (window, _, shell) = Build();

        var menu = ForgeMenuBuilder.ForHeaderOptions(
            shell.Manager, "相机列表", viewModes: ForgeControl.SupportedViewModes);
        var modes = menu.Items.OfType<MenuItem>().First(x => (string?)x.Header == "视图模式");

        Assert.Equal(ForgeControl.SupportedViewModes.Count, modes.Items.Count);

        window.Close();
    }

    /// <summary>菜单原样交给宿主：Add 就追加在后面，想插到最前自己 Insert(0)。</summary>
    /// <remarks>
    /// 与 <c>TabMenuExtension</c> / <c>TabActionsMenuExtension</c> 同形 ——
    /// 三个扩展点行为一致，宿主不用记「哪个会把我的项搬走」。
    /// </remarks>
    [AvaloniaFact]
    public void 宿主的菜单项按自己加的位置来()
    {
        var (window, _, shell) = Build();

        var appended = ForgeMenuBuilder.ForHeaderOptions(
            shell.Manager, "相机列表", (m, _) => m.Items.Add(new MenuItem { Header = "刷新设备列表" }));
        Assert.Equal("刷新设备列表", (appended.Items[^1] as MenuItem)?.Header);

        var prepended = ForgeMenuBuilder.ForHeaderOptions(
            shell.Manager, "相机列表", (m, _) => m.Items.Insert(0, new MenuItem { Header = "从编辑器自动滚动" }));
        Assert.Equal("从编辑器自动滚动", (prepended.Items[0] as MenuItem)?.Header);

        window.Close();
    }

    /// <summary>库自己只放布局相关的三项 —— 别的给了也用不上。</summary>
    [AvaloniaFact]
    public void 默认只有布局相关的项()
    {
        var (window, _, shell) = Build();

        var headers = ForgeMenuBuilder.ForHeaderOptions(shell.Manager, "相机列表")
            .Items.OfType<MenuItem>()
            .Select(x => x.Header as string)
            .ToList();

        Assert.Equal(["视图模式", "移至", "从边栏中移除"], headers);
        window.Close();
    }

    /// <summary>标题条上**看得见**的那几个按钮。</summary>
    /// <remarks>
    /// 「停靠回去」那个恒在树上、只在外部形态时可见（同上游：DockToolWindowAction 只有
    /// 浮动 / 独立窗口时才出现），所以这里按可见性过滤。
    /// </remarks>
    private static List<ForgeHeaderButton> Buttons(ForgeControl frame) =>
        [.. frame.GetVisualDescendants().OfType<ForgeToolWindowHeader>()
            .SelectMany(h => h.GetVisualDescendants().OfType<ForgeHeaderButton>())
            .Where(x => x.IsVisible)];

    private static (Window Window, ForgeControl Frame, ToolWindowShellViewModel Shell) Build(
        Action<ForgeControl>? configure = null)
    {
        var infos = new List<WindowInfo>
        {
            new() { Id = "相机列表", Anchor = ToolWindowAnchor.Left, Order = 0, IsVisible = true, Weight = 0.3f },
        };

        var shell = new ToolWindowShellViewModel(new ToolWindowManager(new DesktopLayout(infos)));
        var frame = new ForgeControl { Palette = ForgePalette.ExpUiDark, Appearance = ForgeAppearance.Default with { FollowThemeVariant = false } };
        configure?.Invoke(frame);
        frame.Attach(shell);

        var window = new Window { Width = 700, Height = 300, WindowDecorations = WindowDecorations.None, Content = frame };
        window.Show();
        Render(window);
        return (window, frame, shell);
    }

    private static (Window Window, ForgeControl Frame, ForgeEditorControl Editor) BuildWithEditor()
    {
        var (window, frame, _) = Build();

        var vm = new EditorAreaViewModel();
        var editor = new ForgeEditorControl { Palette = ForgePalette.ExpUiDark, Appearance = ForgeAppearance.Default with { FollowThemeVariant = false } };
        editor.Attach(vm);
        frame.EditorContent = editor;

        vm.Open("d0", "移动横线测试图", new TextBlock());
        vm.Open("d1", "高斯光斑", new TextBlock());
        Render(window);

        return (window, frame, editor);
    }

    private static void ClickCenter(Window window, Visual root, Control target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Render(window);
    }

    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }
}
