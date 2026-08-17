using Avalonia;
using System.Linq;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 把整机渲成 PNG，用来肉眼复核（不做像素断言，断言在各自的度量用例里）。
public class RenderSnapshotTests
{
    private static string OutputDirectory =>
        Environment.GetEnvironmentVariable("FORGE_SNAPSHOT_DIR") ?? Path.Combine(Path.GetTempPath(), "forge-snapshots");

    /// 编辑区：两个标签页 + 一次竖分屏。
    private static Control BuildEditor(ForgePalette palette, string themeName)
    {
        var editor = new EditorAreaViewModel();

        // Content 放**数据**不放控件：分屏之后同一个标签页会同时开在两个组里，
        // 一个控件不能挂在两个父容器下。放数据则由模板各渲染一份（上游同理）。
        editor.Open("Program.cs", "Program.cs", "Program.cs 的内容");
        editor.Open("Readme.md", "Readme.md", "Readme.md 的内容");
        editor.Area.SetPinned("Program.cs", pinned: true);

        // 竖着切一刀，把选中那页带过去
        editor.Area.Split(editor.Area.CurrentGroup, isVertical: true);

        var control = new ForgeEditorControl
        {
            Palette = palette,
            // 编辑区正文的底色。islands-light 也是浅色，所以按「名字里带 light」判，
            // 不是 == "light"。
            Background = new SolidColorBrush(themeName.Contains("light", StringComparison.Ordinal)
                ? ForgeColor.Parse("#FFFFFF")
                : ForgeColor.Parse("#1E1F22")),
            Foreground = new SolidColorBrush(palette.Foreground),
        };
        control.Attach(editor);
        return control;
    }

    /// 按 LDVision 的登记表渲一张：10 个面板全都有按钮，只有 3 个是打开的。
    [AvaloniaFact]
    public void Render_an_ldvision_like_layout()
    {
        var palette = ForgePalette.ExpUiDark;
        var shell = new ToolWindowShellViewModel();

        // 左：相机列表 / 图像库；右：相机参数；下左：日志 / 统计 / 测量表格；下右：四个分析面板
        (string Id, string Title, ToolWindowAnchor Anchor, bool Split, Geometry Icon)[] definitions =
        [
            ("CamList", "相机列表", ToolWindowAnchor.Left, false, ForgeIcons.Camera),
            ("ImageLibrary", "图像库", ToolWindowAnchor.Left, false, ForgeIcons.Project),
            ("CamParam", "相机参数", ToolWindowAnchor.Right, false, ForgeIcons.Settings),
            ("DisplayLog", "日志显示", ToolWindowAnchor.Bottom, false, ForgeIcons.Terminal),
            ("ImageStats", "图像统计", ToolWindowAnchor.Bottom, false, ForgeIcons.Chart),
            ("MeasureTable", "测量表格", ToolWindowAnchor.Bottom, false, ForgeIcons.TodoList),
            ("Histogram", "直方图", ToolWindowAnchor.Bottom, true, ForgeIcons.Chart),
            ("ProfileLine", "剖面线", ToolWindowAnchor.Bottom, true, ForgeIcons.Structure),
            ("PixelInspector", "像素查看器", ToolWindowAnchor.Bottom, true, ForgeIcons.Find),
            ("FocusMeasure", "清晰度因子", ToolWindowAnchor.Bottom, true, ForgeIcons.Debug),
        ];

        foreach (var (id, title, anchor, split, icon) in definitions)
        {
            // 登记只要 id/标题/图标 —— 不需要面板实例，按钮却已经在了
            shell.Add(id, title, anchor, split, icon);
        }

        var frame = new ForgeControl
        {
            Palette = palette,
            EditorContent = new Border { Background = new SolidColorBrush(ForgeColor.Parse("#1E1F22")) },
        };
        frame.Attach(shell);

        // 每格开第一个
        shell.Open("CamList");
        shell.Open("CamParam");
        shell.Open("DisplayLog");

        var window = new Window
        {
            Width = 900,
            Height = 560,
            WindowDecorations = WindowDecorations.None,
            Background = new SolidColorBrush(palette.Background),
            Content = frame,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);

        Directory.CreateDirectory(OutputDirectory);
        bitmap.Save(Path.Combine(OutputDirectory, "forge-ldvision.png"), new PngBitmapEncoderOptions());
    }

    [AvaloniaTheory]
    [InlineData("dark", false)]
    [InlineData("light", false)]
    [InlineData("names", true)]
    [InlineData("islands-dark", false)]
    [InlineData("islands-light", false)]
    public void Render_the_default_layout(string themeName, bool showNames)
    {
        var palette = themeName switch
        {
            "light" => ForgePalette.ExpUiLight,
            "islands-dark" => ForgePalette.IslandsDark,
            "islands-light" => ForgePalette.IslandsLight,
            _ => ForgePalette.ExpUiDark,
        };
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var manager = shell.Manager;

        var frame = new ForgeControl
        {
            Palette = palette,
            ShowToolWindowNames = showNames,
            EditorContent = BuildEditor(palette, themeName),
        };
        frame.Attach(shell);

        // Version Control 装三块内容，标题条上排成 tab（§13.1）
        var vcs = frame.GetContentManager("Version Control");
        vcs.Add(new ToolWindowContent("local", "Local Changes"));
        vcs.Add(new ToolWindowContent("shelf", "Shelf"));
        vcs.Add(new ToolWindowContent("console", "Console", IsPinned: true));
        vcs.Select("shelf");

        // 摘掉一个条带按钮，好让「更多」按钮有东西可挑
        manager.Hide("Maven", removeFromStripe: true);

        // 内容由**宿主**塞进去（Forge 不编占位内容）
        foreach (var vm in shell.ToolWindows)
        {
            vm.Content = new TextBlock
            {
                Text = $"{vm.Title} 的内容",
                Margin = new Thickness(12, 8, 0, 0),
                Foreground = new SolidColorBrush(palette.Foreground),
            };
        }

        manager.Activate("Project");
        manager.Activate("Structure");        // isSplit=true —— 与 Project 分掉左边这一格
        manager.Activate("Version Control");  // 下边这一格，带三个 tab

        var window = new Window
        {
            Width = 900,
            Height = 560,
            WindowDecorations = WindowDecorations.None,
            // 窗口底色用「海」：岛形态下缝里露出来的是它。expUI 下它等于 Background，无差别。
            Background = new SolidColorBrush(palette.MainBackground),
            Content = frame,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);

        Directory.CreateDirectory(OutputDirectory);
        var path = Path.Combine(OutputDirectory, $"forge-{themeName}.png");
        bitmap.Save(path, new PngBitmapEncoderOptions());

        Assert.True(new FileInfo(path).Length > 0);
    }


    /// <summary>按 LDVision 的样子渲一张浅色整机图（工具窗口 + 编辑区标签页），用来肉眼对照。</summary>
    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("dark")]
    public void Render_an_ldvision_like_layout_with_documents(string themeName)
    {
        var palette = themeName == "light" ? ForgePalette.ExpUiLight : ForgePalette.ExpUiDark;
        var shell = new ToolWindowShellViewModel();

        (string Id, string Title, ToolWindowAnchor Anchor, bool Split, Geometry Icon)[] definitions =
        [
            ("CamList", "相机列表", ToolWindowAnchor.Left, false, ForgeIcons.Camera),
            ("ImageLibrary", "本地文件", ToolWindowAnchor.Left, false, ForgeIcons.Project),
            ("CamParam", "相机参数", ToolWindowAnchor.Right, false, ForgeIcons.Settings),
            ("DisplayLog", "日志管理", ToolWindowAnchor.Bottom, false, ForgeIcons.Terminal),
            ("ImageStats", "图像统计", ToolWindowAnchor.Bottom, false, ForgeIcons.Chart),
            ("Histogram", "直方图", ToolWindowAnchor.Bottom, true, ForgeIcons.Chart),
            ("PixelInspector", "像素查看器", ToolWindowAnchor.Bottom, true, ForgeIcons.Find),
        ];

        foreach (var (id, title, anchor, split, icon) in definitions)
        {
            shell.Add(id, title, anchor, split, icon);
        }

        var editor = new EditorAreaViewModel();
        editor.Open("cam-1", "彩条测试图 1280×1024", "实时画面");
        editor.Open("snap-1", "快照 #12", "静态图");
        editor.Find("cam-1")!.Icon = ForgeIcons.Camera;
        editor.Find("snap-1")!.Icon = ForgeIcons.Bookmarks;

        var editorControl = new ForgeEditorControl();
        editorControl.Attach(editor);

        var frame = new ForgeControl { EditorContent = editorControl };
        frame.Attach(shell);

        shell.Open("ImageLibrary");
        shell.Open("CamParam");
        shell.Open("DisplayLog");

        var window = new Window
        {
            Width = 1100,
            Height = 700,
            WindowDecorations = WindowDecorations.None,
            RequestedThemeVariant = themeName == "light" ? ThemeVariant.Light : ThemeVariant.Dark,
            Content = frame,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        // 跟随变体：宿主没设 Palette，两个控件都应自己切到这一套
        Assert.Same(palette, frame.Palette);
        Assert.Same(palette, editorControl.Palette);

        var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);

        Directory.CreateDirectory(OutputDirectory);
        bitmap!.Save(Path.Combine(OutputDirectory, $"forge-ldvision-{themeName}.png"), new PngBitmapEncoderOptions());
    }


    /// <summary>拖动中的样子：目标区域高亮 + 条带落点方块（明暗各一，肉眼复核用）。</summary>
    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("dark")]
    public void Render_a_drag_in_progress(string themeName)
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var frame = new ForgeControl
        {
            EditorContent = new Border(),
        };
        frame.Attach(shell);

        var window = new Window
        {
            Width = 900,
            Height = 600,
            WindowDecorations = WindowDecorations.None,
            RequestedThemeVariant = themeName == "light" ? ThemeVariant.Light : ThemeVariant.Dark,
            Content = frame,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var button = frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>().First();
        var start = button.TranslatePoint(new Point(20, 20), frame)!.Value;

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Vector(0, 260));
        Dispatcher.UIThread.RunJobs();

        Assert.False(frame.DragOverlay.IsIdle);

        var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);

        Directory.CreateDirectory(OutputDirectory);
        bitmap!.Save(Path.Combine(OutputDirectory, $"forge-drag-{themeName}.png"), new PngBitmapEncoderOptions());
    }

    /// <summary>
    /// 宿主塞控件当图标：真的画出来了吗。
    /// </summary>
    /// <remarks>
    /// 这条是回归：其余快照用例给的都是 <c>Geometry</c>，所以「Icon 是 Control 时
    /// 一片空白」在快照里看不出来——LDVision 用的恰恰是控件（Material.Icons），
    /// 一整排条带按钮是空的，而库自己的快照张张好看。
    /// 这里读**真实像素**，不是看有没有子控件。
    /// </remarks>
    [AvaloniaFact]
    public void A_control_icon_is_actually_painted()
    {
        var button = new ForgeStripeButton
        {
            Palette = ForgePalette.ExpUiDark,
            Icon = new Border
            {
                Width = 20,
                Height = 20,
                Background = new SolidColorBrush(Colors.Red),
            },
        };

        var window = new Window
        {
            Width = 40,
            Height = 40,
            WindowDecorations = WindowDecorations.None,
            Background = new SolidColorBrush(ForgePalette.ExpUiDark.Background),
            Content = button,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);

        Assert.Equal(Colors.Red, PixelAt(bitmap!, 20, 20));
    }

    /// <summary>
    /// 把条带按钮放大 4 倍渲出来，用来肉眼核对图标与高亮的位置关系。
    /// </summary>
    /// <remarks>
    /// 1× 下这些差异只有一两个像素（Islands 的图标居中基准错了会偏 1.5px），
    /// 在整机快照里根本看不出来，只能靠放大看。
    /// </remarks>
    [AvaloniaTheory]
    [InlineData("islands-left", true)]
    [InlineData("islands-right", false)]
    public void Render_the_stripe_buttons_zoomed(string name, bool isLeft)
    {
        var palette = ForgePalette.IslandsDark;

        // ⚠ 必须钉宽 + 不拉伸：StackPanel 默认把子控件横向拉满，
        // 按钮会拿到窗口宽而不是它自己的 37，高亮铺到最右、图标被推出画面。
        var stack = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Vertical,
            Width = 37,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top,
        };

        (Geometry Icon, bool Selected)[] items =
        [
            (ForgeIcons.Project, true),
            (ForgeIcons.Camera, false),
            (ForgeIcons.Chart, false),
        ];

        foreach (var (icon, selected) in items)
        {
            stack.Children.Add(new ForgeStripeButton
            {
                Palette = palette,
                IsLeftStripe = isLeft,
                Icon = icon,
                IsToolWindowVisible = selected,
                IsToolWindowActive = selected,
            });
        }

        const double zoom = 4;
        var scaled = new Border
        {
            Child = stack,
            Width = 37,
            Height = 40 * items.Length,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top,
            RenderTransform = new ScaleTransform(zoom, zoom),
            RenderTransformOrigin = RelativePoint.TopLeft,
        };

        var window = new Window
        {
            Width = 37 * zoom,
            Height = 40 * items.Length * zoom,
            WindowDecorations = WindowDecorations.None,
            // 条带底 = 海，与实机的透底一致地留出对比
            Background = new SolidColorBrush(palette.StripeBackground),
            Content = scaled,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);

        Directory.CreateDirectory(OutputDirectory);
        bitmap!.Save(Path.Combine(OutputDirectory, $"forge-stripe-{name}.png"), new PngBitmapEncoderOptions());
    }

    /// <summary>读一个像素（实现见 <see cref="PixelProbe"/>）。</summary>
    private static Color PixelAt(WriteableBitmap bitmap, int x, int y) => PixelProbe.At(bitmap, x, y);
}
