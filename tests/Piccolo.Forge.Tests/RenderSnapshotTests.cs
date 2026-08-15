using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 把整机渲成 PNG，用来肉眼复核（不做像素断言，断言在各自的度量用例里）。
public class RenderSnapshotTests
{
    private static string OutputDirectory =>
        Environment.GetEnvironmentVariable("FORGE_SNAPSHOT_DIR") ?? Path.Combine(Path.GetTempPath(), "forge-snapshots");

    [AvaloniaTheory]
    [InlineData("dark", false)]
    [InlineData("light", false)]
    [InlineData("names", true)]
    public void Render_the_default_layout(string themeName, bool showNames)
    {
        var palette = themeName == "light" ? ForgePalette.ExpUiLight : ForgePalette.ExpUiDark;
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());

        var frame = new ForgeToolWindowFrame
        {
            Palette = palette,
            ShowToolWindowNames = showNames,
            EditorContent = new Border
            {
                Background = new SolidColorBrush(themeName == "light"
                    ? ForgeColor.Parse("#FFFFFF")
                    : ForgeColor.Parse("#1E1F22")),
            },
        };
        frame.Attach(manager);

        // Version Control 装三块内容，标题条上排成 tab（§13.1）
        var vcs = frame.GetContentManager("Version Control");
        vcs.Add(new ToolWindowContent("local", "Local Changes"));
        vcs.Add(new ToolWindowContent("shelf", "Shelf"));
        vcs.Add(new ToolWindowContent("console", "Console", IsPinned: true));
        vcs.Select("shelf");

        // 摘掉一个条带按钮，好让「更多」按钮有东西可挑
        manager.Hide("Maven", removeFromStripe: true);

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
}
