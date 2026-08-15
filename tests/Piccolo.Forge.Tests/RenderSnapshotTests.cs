using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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

        var control = new ForgeEditorArea
        {
            Palette = palette,
            Background = new SolidColorBrush(themeName == "light"
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

        var frame = new ForgeToolWindowFrame
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
    public void Render_the_default_layout(string themeName, bool showNames)
    {
        var palette = themeName == "light" ? ForgePalette.ExpUiLight : ForgePalette.ExpUiDark;
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var manager = shell.Manager;

        var frame = new ForgeToolWindowFrame
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
            Background = new SolidColorBrush(palette.Background),
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
    /// 读一个像素。
    /// </summary>
    /// <remarks>
    /// 字节序按 <see cref="ILockedFramebuffer.Format"/> 走，别写死 ——
    /// headless 后端给的是 <c>Rgba8888</c>，桌面后端多是 <c>Bgra8888</c>，
    /// 写死一种会得到红蓝互换的结论。
    /// </remarks>
    private static Color PixelAt(WriteableBitmap bitmap, int x, int y)
    {
        using var buffer = bitmap.Lock();
        var offset = (y * buffer.RowBytes) + (x * 4);
        var bytes = new byte[4];
        System.Runtime.InteropServices.Marshal.Copy(buffer.Address + offset, bytes, 0, 4);

        return buffer.Format == PixelFormat.Rgba8888
            ? Color.FromArgb(bytes[3], bytes[0], bytes[1], bytes[2])
            : Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
    }
}
