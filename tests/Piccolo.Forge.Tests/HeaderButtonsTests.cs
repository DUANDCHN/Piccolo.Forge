using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    public void 面板变窄时标题省略且不侵入按钮区域()
    {
        var actions = Enumerable.Range(0, 4)
            .Select(_ => (Control)new ForgeHeaderButton { Icon = ForgeIcons.Find })
            .ToArray();
        var (window, frame, _) = Build(f => f.HeaderButtonsExtension = _ => actions);
        var header = frame.GetVisualDescendants().OfType<ForgeToolWindowHeader>().Single();
        header.Title = "标题很长的相机列表面板";
        Render(window);

        var title = header.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "PART_Title");
        var buttonsLeft = Buttons(frame)
            .Select(button => button.TranslatePoint(default, header)!.Value.X)
            .Min();
        var titleRight = title.TranslatePoint(default, header)!.Value.X + title.Bounds.Width;

        Assert.Equal(global::Avalonia.Media.TextTrimming.CharacterEllipsis, title.TextTrimming);
        Assert.True(titleRight <= buttonsLeft + 0.5,
            $"标题侵入按钮区域（标题右边缘 {titleRight}，按钮从 {buttonsLeft} 开始）");

        window.Close();
    }

    /// <summary>裁切模式保留完整字形，视口每缩小一像素就只遮去一像素，不能退回逐字省略或重排。</summary>
    [AvaloniaFact]
    public void 标题连续裁切时保留完整排版且动态切回省略号()
    {
        var actions = Enumerable.Range(0, 4)
            .Select(_ => (Control)new ForgeHeaderButton { Icon = ForgeIcons.Find }).ToArray();
        var (window, frame, _) = Build(f =>
        {
            f.HeaderButtonsExtension = _ => actions;
            f.Appearance = f.Appearance with { HeaderTitleTextTrimming = global::Avalonia.Media.TextTrimming.None };
        });
        try
        {
            var header = frame.GetVisualDescendants().OfType<ForgeToolWindowHeader>().Single();
            header.Title = "解决方案与项目资源管理器标题内容";
            header.Width = 240;
            Render(window);
            var title = header.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "PART_Title");
            Assert.Same(global::Avalonia.Media.TextTrimming.None, title.TextTrimming);
            Assert.True(title.ClipToBounds);
            var start = title.TranslatePoint(default, header)!.Value.X;
            var width = title.Bounds.Width;
            var fullLineWidth = title.TextLayout.TextLines.Single().Width;
            Assert.True(fullLineWidth > width, "必须真的溢出才能验证裁切");
            for (var step = 1; step <= 12; step++)
            {
                header.Width = 240 - step;
                Render(window);
                Assert.Equal(width - step, title.Bounds.Width, 2);
                Assert.Equal(start, title.TranslatePoint(default, header)!.Value.X, 2);
                Assert.Equal(fullLineWidth, title.TextLayout.TextLines.Single().Width, 2);
                Assert.False(title.TextLayout.TextLines.Single().HasCollapsed);
                Assert.True(start + title.Bounds.Width <= Buttons(frame).Min(button => button.Bounds.X));
            }

            // 同一份外观接口可动态切回省略号，保留库默认行为以及其他宿主的选择。
            frame.Appearance = frame.Appearance with { HeaderTitleTextTrimming = global::Avalonia.Media.TextTrimming.CharacterEllipsis };
            Render(window);
            Assert.Same(global::Avalonia.Media.TextTrimming.CharacterEllipsis, title.TextTrimming);
            Assert.True(title.TextLayout.TextLines.Single().HasCollapsed);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>分隔线必须释放空槽位；宽度变化、隐藏再显示后，标题和按钮仍使用同一份宽度预算。</summary>
    [AvaloniaFact]
    public void 分隔线按实际宽度排列且首轮布局就释放标题空间()
    {
        var left = new ForgeHeaderButton { Icon = ForgeIcons.Find };
        var right = new ForgeHeaderButton { Icon = ForgeIcons.Find };
        var separator = new Border { Width = 1, Height = 14, Margin = new Thickness(3, 0) };
        var (window, frame, _) = Build(f => f.HeaderButtonsExtension = _ => [left, separator, right]);
        try
        {
            var header = frame.GetVisualDescendants().OfType<ForgeToolWindowHeader>().Single();
            // 两颗宿主按钮加两颗内置按钮，分隔线只占 1 + 3 + 3，不再占 24。
            Assert.Equal(12 + 4 * 24 + 7 + 8, header.ButtonsWidth);
            Assert.Equal(7, right.Bounds.X - left.Bounds.Right, 1);
            Assert.Equal(1, separator.Bounds.Width);
            Assert.Equal(3, separator.Bounds.X - left.Bounds.Right, 1);

            separator.Margin = new Thickness(2, 0);
            Render(window);
            Assert.Equal(12 + 4 * 24 + 5 + 8, header.ButtonsWidth);
            Assert.Equal(5, right.Bounds.X - left.Bounds.Right, 1);

            frame.ShowHeaderButtons = false;
            Render(window);
            Assert.Equal(0, header.ButtonsWidth);
            frame.ShowHeaderButtons = true;
            Render(window);
            Assert.Equal(5, right.Bounds.X - left.Bounds.Right, 1);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>外观切换应同时更新标题截断预算、标签起点和工具栏位置，保持标准按钮命中尺寸。</summary>
    [AvaloniaFact]
    public void 紧凑间距使长标题靠近按钮且动态切换不重叠()
    {
        var actions = Enumerable.Range(0, 4)
            .Select(_ => (Control)new ForgeHeaderButton { Icon = ForgeIcons.Find }).ToArray();
        var (window, frame, _) = Build(f => f.HeaderButtonsExtension = _ => actions);
        try
        {
            var header = frame.GetVisualDescendants().OfType<ForgeToolWindowHeader>().Single();
            header.Title = "标题很长的相机列表面板";
            Render(window);
            var title = header.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "PART_Title");
            var previousMaxWidth = title.MaxWidth;

            frame.Appearance = frame.Appearance with
            {
                HeaderTitleInsetRight = 4,
                HeaderToolbarInsetLeft = 2,
                HeaderToolbarInsetRight = 4,
            };
            Render(window);

            var buttons = Buttons(frame).OrderBy(b => b.Bounds.X).ToArray();
            var titleRight = title.TranslatePoint(default, header)!.Value.X + title.Bounds.Width;
            // 省略号排版可能留下不到一个字的空白；校验预算边界，不把字形余量误认为额外内边距。
            var titleLimit = title.TranslatePoint(default, header)!.Value.X + title.MaxWidth;
            Assert.Equal(6, buttons[0].Bounds.X - titleLimit, 1);
            Assert.True(titleRight <= titleLimit + 0.5);
            Assert.Equal(4, header.Bounds.Width - buttons[^1].Bounds.Right, 1);
            Assert.Equal(previousMaxWidth + 26, title.MaxWidth, 1);
            Assert.All(buttons, button => Assert.Equal(new Size(24, 24), button.Bounds.Size));

            frame.Appearance = frame.Appearance with { HeaderButtonWidth = 22 };
            Render(window);
            Assert.All(buttons, button => Assert.Equal(new Size(22, 24), button.Bounds.Size));
            frame.Appearance = frame.Appearance with { HeaderButtonWidth = 28 };
            Render(window);
            Assert.All(buttons, button => Assert.Equal(new Size(28, 24), button.Bounds.Size));

        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>独立标题条给系统按钮预留空间时，仍按宿主间距约束长标题。</summary>
    [AvaloniaFact]
    public void 紧凑标题栏保留系统按钮区域()
    {
        var header = new ForgeToolWindowHeader
        {
            Title = "标题很长的相机列表面板",
            CaptionButtonsWidth = 72,
            TitleInsetRight = 4,
            ToolbarInsetLeft = 2,
            ToolbarInsetRight = 4,
            ButtonWidth = 22,
        };
        var window = new Window { Width = 240, Height = 100, Content = header };
        try
        {
            window.Show();
            Render(window);
            var buttons = header.GetVisualDescendants().OfType<ForgeHeaderButton>()
                .Where(b => b.IsVisible).OrderBy(b => b.Bounds.X).ToArray();
            var title = header.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "PART_Title");
            Assert.Equal(76, header.Bounds.Width - buttons[^1].Bounds.Right, 1);
            var titleLimit = title.TranslatePoint(default, header)!.Value.X + title.MaxWidth;
            Assert.Equal(6, buttons[0].Bounds.X - titleLimit, 1);
        }
        finally
        {
            window.Close();
        }
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

    /// <summary>
    /// 装了多块内容的面板，⋮ 菜单**最前面**排出那几块 + 一条分隔线。
    /// </summary>
    /// <remarks>
    /// 由来：标题条上那排 tab 在面板窄的时候会被挤掉一部分，而 ⋮ 是"这块面板还能干什么"的
    /// 兜底入口 —— 它原先只有布局三项，**唯独没有"切到哪一块"**，而编辑区那边的标签菜单一直有。
    /// 两处形态不一致，用户会以为这块面板就只有当前这一块内容。
    /// </remarks>
    [AvaloniaFact]
    public void 多块内容时菜单最前面列出那几块()
    {
        var (window, _, shell) = Build();

        var contents = new ToolWindowContentManager();
        contents.Add(new ToolWindowContent("basic", "基础统计", IsCloseable: false));
        contents.Add(new ToolWindowContent("quality", "质量指标", IsCloseable: false));
        contents.Select("quality");

        var menu = ForgeMenuBuilder.ForHeaderOptions(
            shell.Manager, "相机列表", viewModes: ForgeControl.SupportedViewModes, contents: contents);

        var headers = menu.Items.OfType<MenuItem>().Select(x => x.Header as string).ToList();
        Assert.Equal(["基础统计", "质量指标", "视图模式", "移至", "从边栏中移除"], headers);

        // 分隔线把"内容"和"布局"分开：第 3 项（下标 2）就是它
        Assert.IsType<Separator>(menu.Items[2]);

        // 单选带 ✓，且勾的是当前选中那块
        var checkedItems = menu.Items.OfType<MenuItem>().Where(x => x.IsChecked).ToList();
        Assert.Single(checkedItems);
        Assert.Equal("质量指标", checkedItems[0].Header);

        window.Close();
    }

    /// <summary>
    /// 只有一块内容时不出这一段 —— 那时"切到哪一块"没有选项可言，多一条分隔线只是噪音。
    /// </summary>
    [AvaloniaFact]
    public void 只有一块内容时不列()
    {
        var (window, _, shell) = Build();

        var contents = new ToolWindowContentManager();
        contents.Add(new ToolWindowContent("only", "唯一一块", IsCloseable: false));

        var headers = ForgeMenuBuilder.ForHeaderOptions(shell.Manager, "相机列表", contents: contents)
            .Items.OfType<MenuItem>()
            .Select(x => x.Header as string)
            .ToList();

        Assert.Equal(["视图模式", "移至", "从边栏中移除"], headers);
        window.Close();
    }

    /// <summary>点菜单里的一块内容 = 切到那一块。</summary>
    [AvaloniaFact]
    public void 点菜单里的内容会切过去()
    {
        var (window, _, shell) = Build();

        var contents = new ToolWindowContentManager();
        contents.Add(new ToolWindowContent("basic", "基础统计", IsCloseable: false));
        contents.Add(new ToolWindowContent("frame", "帧信息", IsCloseable: false));
        contents.Select("basic");

        var menu = ForgeMenuBuilder.ForHeaderOptions(shell.Manager, "相机列表", contents: contents);
        // 菜单项挂的是 Click（ForgeMenuBuilder.Item），不是 Command —— 直接触发点击
        var frame = menu.Items.OfType<MenuItem>().First(x => (string?)x.Header == "帧信息");
        frame.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        Assert.Equal("frame", contents.SelectedContent?.Id);
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
