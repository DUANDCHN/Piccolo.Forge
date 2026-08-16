using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Piccolo.Forge.Themes;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Xunit;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 四种视图模式（停靠固定 / 停靠不固定 / 浮动 / 窗口）都得画得出来。
/// </summary>
/// <remarks>
/// 对应上游的 <c>TW.ViewModeGroup</c>：<c>ToolWindowType</c> 的四个值。
/// SLIDING 是滑出**覆盖在编辑区上**（<c>ToolWindowPane.kt:432-459</c> 的 PALETTE_LAYER 那一支），
/// FLOATING / WINDOWED 各有自己的宿主窗口（<c>FloatingDecorator</c> 是 JDialog、
/// <c>WindowedDecorator</c> 是 JFrame）。
/// </remarks>
public class ViewModeTests
{
    [AvaloniaFact]
    public void 停靠固定占掉编辑区的地方()
    {
        var (window, frame, shell, editor) = Build();

        Assert.Equal(ToolWindowType.Docked, shell.Manager.Layout.GetInfo("相机列表")!.Type);
        Assert.True(editor.Bounds.X > 0, "停靠的面板该把编辑区往右推");

        window.Close();
    }

    [AvaloniaFact]
    public void 停靠不固定盖在编辑区上不占地方()
    {
        var (window, frame, shell, editor) = Build();
        var docked = editor.Bounds;

        shell.Manager.SetType("相机列表", ToolWindowType.Sliding);
        Pump(window);

        Assert.Equal(0, editor.Bounds.X, 1.0);
        Assert.True(editor.Bounds.Width > docked.Width, "滑出之后编辑区应当变宽（那块不再占地方）");

        // 面板还在，盖在编辑区左边
        var panel = frame.DockPane.Children.OfType<Border>().First(x => x != editor && x.Bounds.Width > 0);
        Assert.Equal(0, panel.Bounds.X, 1.0);
        Assert.True(panel.ZIndex > editor.ZIndex, "盖不住编辑区就没意义");

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(ToolWindowType.Floating, false)]
    [InlineData(ToolWindowType.Windowed, true)]
    public void 浮动与窗口各自开一个宿主窗口(ToolWindowType type, bool inTaskbar)
    {
        var (window, frame, shell, editor) = Build();

        shell.Manager.SetType("相机列表", type);
        Pump(window);

        // 不再占停靠区
        Assert.Equal(0, editor.Bounds.X, 1.0);
        Assert.DoesNotContain(frame.DockPane.Children.OfType<Border>(), x => x != editor && x.Bounds.Width > 0);

        var external = frame.ExternalWindows.Single();
        Assert.Equal(type, external.Type);
        Assert.Equal(inTaskbar, external.ShowInTaskbar);

        // 切回停靠：窗口收掉，面板回到列里
        shell.Manager.SetType("相机列表", ToolWindowType.Docked);
        Pump(window);

        Assert.Empty(frame.ExternalWindows);
        Assert.True(editor.Bounds.X > 0);

        window.Close();
    }

    /// <summary>
    /// 两种外部形态差在**有没有系统标题栏**：独立窗口留着（最小/最大/关闭齐全），浮动去掉。
    /// </summary>
    /// <remarks>
    /// 上游：WINDOWED 是 <c>FrameWrapper</c> 包的普通 JFrame（<c>ToolWindowManagerDecorators.kt:98</c>），
    /// FLOATING 是 JDialog 且 Windows 上 <c>setUndecorated(true)</c>（<c>FloatingDecorator.java:102-108</c>）。
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(ToolWindowType.Floating, WindowDecorations.BorderOnly)]
    [InlineData(ToolWindowType.Windowed, WindowDecorations.Full)]
    public void 只有浮动去掉标题栏(ToolWindowType type, WindowDecorations decorations)
    {
        var (window, frame, shell, _) = Build();

        shell.Manager.SetType("相机列表", type);
        Pump(window);

        var host = frame.ExternalWindows.Single().Window;

        // BorderOnly = 留边框、圆角、拖边改大小，只去掉标题栏那条（也就没有最小/最大/关闭）；
        // Full = 完整装饰，独立窗口该有的三个按钮都在。
        Assert.Equal(decorations, host.WindowDecorations);
        Assert.True(host.CanResize, "两种形态都得能改大小");

        window.Close();
    }

    /// <summary>
    /// 标题条**处处**能拖窗口，只让开按钮和标签 —— 此前落在标签条上的按下被子控件吃掉，
    /// 表现为"标题条有些地方拖不动"。
    /// </summary>
    [AvaloniaFact]
    public void 标题条上除了按钮和标签都能拖窗口()
    {
        var (window, frame, shell, _) = Build();
        shell.Manager.SetType("相机列表", ToolWindowType.Floating);
        Pump(window);

        // 面板已经搬进外部窗口了，标题条要从那边找
        var header = frame.ExternalWindows.Single().Window
            .GetVisualDescendants().OfType<ForgeToolWindowHeader>().Single();

        Assert.True(header.IsWindowDragArea(header), "标题条自身该是把手");
        Assert.True(header.IsWindowDragArea(header.Tabs), "标签条上没被标签占到的地方也是把手");
        Assert.False(header.IsWindowDragArea(
            header.GetVisualDescendants().OfType<ForgeHeaderButton>().First(x => x.IsVisible)));

        var tab = header.GetVisualDescendants().OfType<ForgeContentTab>().FirstOrDefault();
        if (tab is not null)
        {
            Assert.False(header.IsWindowDragArea(tab), "标签是用来切内容和搬标签页的");
        }

        window.Close();
    }

    /// <summary>
    /// 浮动时标题条兼任窗口标题栏 → 按窗口标题栏的高度长；停靠回去仍是面板的 41。
    /// </summary>
    [AvaloniaFact]
    public void 浮动时标题条按窗口标题栏的高度()
    {
        var (window, frame, shell, _) = Build();
        var header = frame.GetVisualDescendants().OfType<ForgeToolWindowHeader>().Single();

        Assert.Equal(ForgeMetrics.HeaderHeight, header.Bounds.Height, 1.0);

        shell.Manager.SetType("相机列表", ToolWindowType.Floating);
        Pump(window);
        Assert.Equal(ForgeMetrics.WindowTitleBarHeight, header.HeaderHeight, 1.0);

        shell.Manager.SetType("相机列表", ToolWindowType.Docked);
        Pump(window);
        Assert.Equal(ForgeMetrics.HeaderHeight, header.Bounds.Height, 1.0);

        window.Close();
    }

    /// <summary>
    /// 独立窗口形态下，标题条与窗口标题栏是同一行：面板的按钮要给右边那三个系统按钮让位。
    /// </summary>
    [AvaloniaFact]
    public void 独立窗口时面板按钮给系统按钮让位()
    {
        var (window, frame, shell, _) = Build();
        frame.CaptionButtonsWidth = 140;

        var header = frame.GetVisualDescendants().OfType<ForgeToolWindowHeader>().Single();
        var dockedRight = header.GetVisualDescendants().OfType<ForgeHeaderButton>()
            .Where(x => x.IsVisible).Max(x => x.Bounds.Right);

        shell.Manager.SetType("相机列表", ToolWindowType.Windowed);
        Pump(window);

        var windowedRight = header.GetVisualDescendants().OfType<ForgeHeaderButton>()
            .Where(x => x.IsVisible).Max(x => x.Bounds.Right);
        Assert.True(
            windowedRight <= header.Bounds.Width - 140,
            $"面板按钮压到系统按钮底下了：右端 {windowedRight}，条宽 {header.Bounds.Width}");

        // 浮动没有系统按钮，不该让位
        shell.Manager.SetType("相机列表", ToolWindowType.Floating);
        Pump(window);
        Assert.Equal(0, header.CaptionButtonsWidth);

        // 停回去更不该让位
        shell.Manager.SetType("相机列表", ToolWindowType.Docked);
        Pump(window);
        var backRight = header.GetVisualDescendants().OfType<ForgeHeaderButton>()
            .Where(x => x.IsVisible).Max(x => x.Bounds.Right);
        Assert.Equal(dockedRight, backRight, 1.0);

        window.Close();
    }

    /// <summary>
    /// 独立窗口的窗体可以由宿主提供 —— 一个应用里所有窗体该是同一副长相，
    /// 那副长相是宿主的事，库只往里塞标题和内容。
    /// </summary>
    [AvaloniaFact]
    public void 宿主可以接管外部窗体()
    {
        var (window, frame, shell, _) = Build();

        var mine = new Window();
        frame.ExternalWindowFactory = type => type == ToolWindowType.Windowed ? mine : null;

        shell.Manager.SetType("相机列表", ToolWindowType.Windowed);
        Pump(window);

        var external = frame.ExternalWindows.Single();
        Assert.Same(mine, external.Window);
        Assert.Contains("相机列表", mine.Title);
        Assert.NotNull(mine.Content);

        // 长相归宿主，但"在不在任务栏里"是形态语义，库说了算
        Assert.True(mine.ShowInTaskbar, "独立窗口该在任务栏里有自己的一项");

        // 浮动那边工厂返回 null → 用库自带的窗口，而且不进任务栏
        shell.Manager.SetType("相机列表", ToolWindowType.Floating);
        Pump(window);

        var floating = frame.ExternalWindows.Single();
        Assert.NotSame(mine, floating.Window);
        Assert.False(floating.ShowInTaskbar, "浮动是挂在主窗口上的，不该在任务栏里另起一项");

        window.Close();
    }

    /// <summary>
    /// 切出去的窗口用**面板停靠时的那块大小**，不按内容长。
    /// </summary>
    /// <remarks>对位 <c>ToolWindowManagerDecorators.kt:237-243</c>；居中在主窗口上（<c>:248-251</c>）。</remarks>
    [AvaloniaFact]
    public void 外部窗口沿用停靠时的尺寸()
    {
        var (window, frame, shell, _) = Build();

        var host = frame.DockPane.Children.OfType<Border>()
            .First(x => x.GetVisualDescendants().OfType<ForgeToolWindowHeader>().Any());
        var docked = host.Bounds.Size;
        Assert.True(docked.Width > 0, "面板还没排过版，这条断言没意义");

        shell.Manager.SetType("相机列表", ToolWindowType.Floating);
        Pump(window);

        var external = frame.ExternalWindows.Single().Window;
        Assert.Equal(docked.Width, external.Width, 1.0);
        Assert.Equal(docked.Height, external.Height, 1.0);

        window.Close();
    }

    /// <summary>
    /// 点到编辑区 = 焦点离开工具窗口 → 「失焦就收」的那些真的收起来。
    /// </summary>
    /// <remarks>
    /// 回归：模型早就有收起的判据（<c>ToolWindowManagerLifecycle.kt:116</c>），
    /// 但视图层没人在焦点变化时调它，于是"停靠不固定 / 取消停靠"点到别处纹丝不动。
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(ToolWindowViewMode.DockUnpinned)]
    [InlineData(ToolWindowViewMode.Undock)]
    public void 点到编辑区会把失焦就收的面板收起(ToolWindowViewMode mode)
    {
        var (window, frame, shell, editor) = Build();

        ToolWindowViewModes.Apply(shell.Manager, "相机列表", mode);
        shell.Manager.Show("相机列表");
        Pump(window);
        Assert.True(shell.Manager.Layout.GetInfo("相机列表")!.IsVisible);

        editor.RaiseEvent(new PointerPressedEventArgs(
            editor,
            new Pointer(0, PointerType.Mouse, isPrimary: true),
            editor,
            default,
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));
        Pump(window);

        Assert.False(shell.Manager.Layout.GetInfo("相机列表")!.IsVisible);

        window.Close();
    }

    /// <summary>
    /// 取消停靠（覆盖层）也要能拖宽窄，而且与被盖住的内容之间有一条分界线。
    /// </summary>
    /// <remarks>
    /// 上游同样给 SLIDING 配了 resizer（<c>ToolWindowPane.kt:565-607 · Resizer.LayeredPane</c>）
    /// 和一条 divider（<c>InternalDecoratorImpl.kt:599-612</c>）。
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(ForgeThemeFamily.ExpUi)]
    [InlineData(ForgeThemeFamily.Islands)]
    public void 取消停靠能拖大小且画分界线(ForgeThemeFamily family)
    {
        var (window, frame, shell, _) = Build();

        // 按宿主的真实配置来：岛形态下面板有外边距和圆角，配色也是另一套
        //（回归：Islands 的 WindowBorder 是全透明，用它画线等于没画）
        frame.ThemeFamily = family;
        frame.IslandMetrics = ForgeIslandMetrics.Default with { BorderWidth = 3 };
        Pump(window);

        // 宿主会把窗口底板色告诉外壳（取消停靠那条缝要露它）
        frame.BackdropBrush = Brushes.Black;

        shell.Manager.SetType("相机列表", ToolWindowType.Sliding);
        Pump(window);

        var panel = frame.DockPane.Children.OfType<Border>()
            .First(x => x.GetVisualDescendants().OfType<ForgeToolWindowHeader>().Any());
        var resizer = frame.DockPane.Children.OfType<ForgeDockResizer>()
            .Single(x => x.Anchor == ToolWindowAnchor.Left && !x.IsSplitDivider);

        // 拖手要骑在覆盖层的内沿上，而不是停靠尺寸（覆盖层不占位，那儿是 0）
        Assert.Equal(panel.Bounds.Right, resizer.Bounds.Center.X, 1.0);

        // 抓得到：覆盖层 ZIndex 1，拖手得更高
        Assert.True(resizer.ZIndex > panel.ZIndex, "拖手被覆盖层盖住了");

        // 怎么分开这两块，**按配色族**走上游：
        //   · 非岛：一条看得见的 1px divider（InternalDecoratorImpl.kt:599-612）；
        //     ⚠ 必须真有颜色 —— 曾经用 WindowBorder，它在岛配色里是 #00000000，画了等于没画。
        //   · 岛：面板自己就是一座岛，圆角 + 四周留缝、缝里透出下面的编辑区
        //     （XNextRoundedBorder.kt:38-47），这时不画线。
        if (family == ForgeThemeFamily.Islands)
        {
            Assert.Null(resizer.LineBrush);
            Assert.True(panel.CornerRadius.TopLeft > 0, "岛该是圆角的");

            // 取消停靠的那块**就是一座岛**，和别的岛一模一样：
            //   · 四周各让半条缝（并排时两边各一半凑成整条 BorderWidth=3）；
            //   · 四角都圆 —— 包括贴着条带的那一侧；
            //   · 不额外加边框、不填海色、不加投影 —— 这三样我都试过，全是画蛇添足。
            var metrics = ForgeIslandMetrics.Default with { BorderWidth = 3 };
            Assert.Equal(new CornerRadius(metrics.CornerRadius), panel.CornerRadius);

            // 缝的**宽度**与停靠的岛一样（各让半条），但缝里要露"底板"：
            // 停靠时缝底下什么都没有，露的就是窗口底板；这座岛底下压着编辑区，
            // 不把底板色填上就露出编辑区（岛配色里两者同色，缝等于不存在）。
            // 宿主给了底板色就用边框占住那条缝，没给就还是留白。
            Assert.Equal(new Thickness(metrics.HalfBorderWidth), panel.BorderThickness);
            Assert.Same(frame.BackdropBrush, panel.BorderBrush);
            Assert.Equal(default, panel.Margin);
        }
        else
        {
            var line = Assert.IsType<SolidColorBrush>(resizer.LineBrush);
            Assert.True(line.Color.A > 0, "分界线是透明的，等于没画");
        }

        // 真的抓得到：命中测试必须落在拖手上，而不是被覆盖层或编辑区吃掉
        var probe = new Point(resizer.Bounds.Center.X, resizer.Bounds.Center.Y);
        Assert.Same(resizer, frame.DockPane.InputHitTest(probe));

        // 而且按下去面板得还在 ——
        // 回归：框架的焦点跟踪是 Tunnel，先于拖手跑，把"按在拖手上"判成了失焦，
        // 面板当场收起，表现就是「取消停靠的边界抓不住」。
        resizer.RaiseEvent(new PointerPressedEventArgs(
            resizer,
            new Pointer(0, PointerType.Mouse, isPrimary: true),
            resizer,
            default,
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));
        Pump(window);

        Assert.True(
            shell.Manager.Layout.GetInfo("相机列表")!.IsVisible,
            "按在拖手上就把面板收了，等于抓不住");

        shell.Manager.SetType("相机列表", ToolWindowType.Docked);
        Pump(window);
        Assert.Null(resizer.LineBrush);

        window.Close();
    }

    /// <summary>独立出去之后，标题条上多一个「停靠回去」；点它就回到原来的内部形态。</summary>
    /// <remarks>上游那组动作的第一个就是 <c>DockToolWindowAction</c>（<c>ToolWindowHeader.kt:119</c>）。</remarks>
    [AvaloniaFact]
    public void 停靠回去的按钮只在独立出去时出现()
    {
        var (window, frame, shell, _) = Build();
        var header = frame.GetVisualDescendants().OfType<ForgeToolWindowHeader>().Single();

        Assert.False(header.ShowDockButton, "停靠着的面板不该有「停靠回去」");

        shell.Manager.SetType("相机列表", ToolWindowType.Floating);
        Pump(window);
        Assert.True(header.ShowDockButton);

        // 点它 → 回到内部形态（模型记着上一次是哪种）
        header.GetVisualDescendants().OfType<ForgeHeaderButton>()
            .First(x => x.IsVisible && ReferenceEquals(x.Icon, ForgeIcons.Dock))
            .RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Pump(window);

        Assert.Equal(ToolWindowType.Docked, shell.Manager.Layout.GetInfo("相机列表")!.Type);
        Assert.Empty(frame.ExternalWindows);

        window.Close();
    }

    [AvaloniaFact]
    public void 关掉浮动窗口就等于收起这个面板()
    {
        var (window, frame, shell, _) = Build();

        shell.Manager.SetType("相机列表", ToolWindowType.Floating);
        Pump(window);

        frame.ExternalWindows.Single().Window.Close();
        Pump(window);

        Assert.False(shell.Manager.Layout.GetInfo("相机列表")!.IsVisible);
        window.Close();
    }

    private static (Window Window, ForgeToolWindowFrame Frame, ToolWindowShellViewModel Shell, Border Editor) Build()
    {
        var infos = new List<WindowInfo>
        {
            new() { Id = "相机列表", Anchor = ToolWindowAnchor.Left, Order = 0, IsVisible = true, Weight = 0.25f },
        };

        var shell = new ToolWindowShellViewModel(new ToolWindowManager(new DesktopLayout(infos)));
        var editor = new Border();
        var frame = new ForgeToolWindowFrame { EditorContent = editor };
        frame.Attach(shell);

        var window = new Window { Width = 900, Height = 500, WindowDecorations = WindowDecorations.None, Content = frame };
        window.Show();
        Pump(window);
        return (window, frame, shell, editor);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }
}
