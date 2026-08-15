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
    [InlineData("dark")]
    [InlineData("light")]
    public void Render_the_default_layout(string themeName)
    {
        var palette = themeName == "dark" ? ForgePalette.ExpUiDark : ForgePalette.ExpUiLight;
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());

        var frame = new ForgeToolWindowFrame
        {
            Palette = palette,
            EditorContent = new Border
            {
                Background = new SolidColorBrush(themeName == "dark"
                    ? ForgeColor.Parse("#1E1F22")
                    : ForgeColor.Parse("#FFFFFF")),
            },
        };
        frame.Attach(manager);

        manager.Activate("Project");
        manager.Activate("Terminal");

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
