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

/// <summary>
/// 岛的裁剪与留缝，读**真实像素**。
/// </summary>
/// <remarks>
/// 面板内容几乎总有自己的底色（宿主的视图不可能都是透明的），
/// 所以「圆角对不对」这件事只能在"content 铺满且不透明"的前提下验——
/// 拿一个透明内容去测，圆角看起来永远是对的。
/// </remarks>
public class IslandDecorTests
{
    private static readonly Color Sea = Colors.Lime;      // 窗口底，代表"海"/云母
    private static readonly Color Content = Colors.Red;   // 面板内容自带的不透明底

    [AvaloniaFact]
    public void Opaque_panel_content_does_not_square_off_the_island_corner()
    {
        var bitmap = RenderIsland();

        // 岛中心：内容自己的颜色
        Assert.Equal(Content, PixelAt(bitmap, 60, 60));

        // 最外圈那 3px 是缝（Island.borderWidth / 2），必须露出海色
        Assert.Equal(Sea, PixelAt(bitmap, 1, 1));

        // 圆角内侧：岛的矩形是从 (3,3) 起，圆角半径 10（Island.arc = 20 是直径），
        // 所以 (5,5) 落在圆弧**外面** —— 这里若是内容色，就说明内容没被裁、把角盖方了。
        Assert.NotEqual(Content, PixelAt(bitmap, 5, 5));
    }

    [AvaloniaFact]
    public void The_gap_between_islands_is_the_border_width()
    {
        var bitmap = RenderIsland();

        // 缝宽 = Island.borderWidth / 2 = 3：x=0..2 是海，x>=3 起进岛
        Assert.Equal(Sea, PixelAt(bitmap, 0, 60));
        Assert.Equal(Sea, PixelAt(bitmap, 2, 60));
        Assert.NotEqual(Sea, PixelAt(bitmap, 4, 60));
    }

    /// <summary>
    /// 透底开关：海与条带底都不画，露出窗口背后的东西（宿主的云母）；岛自己仍不透明。
    /// </summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_transparent_backdrop_only_stops_painting_the_sea(bool transparent)
    {
        var shell = new ToolWindowShellViewModel(new ToolWindowManager(DefaultToolWindowLayout.CreateV2()));
        var frame = new ForgeControl
        {
            Appearance = ForgeAppearance.Default with { Family = ForgeThemeFamily.Islands },
            TransparentBackdrop = transparent,
        };
        frame.Attach(shell);

        var window = new Window
        {
            Width = 400,
            Height = 300,
            WindowDecorations = WindowDecorations.None,
            Background = new SolidColorBrush(Sea),
            Content = frame,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);

        // 条带右边那一列是停靠区的空白处（默认布局下右上角没开面板）——它就是「海」。
        var sea = PixelAt(bitmap!, 380, 8);

        if (transparent)
        {
            Assert.Equal(Sea, sea);
        }
        else
        {
            Assert.Equal(ForgePalette.IslandsLight.MainBackground, sea);
        }
    }

    /// <summary>渲一个装了不透明内容的岛，窗口底是 <see cref="Sea"/>。</summary>
    private static WriteableBitmap RenderIsland()
    {
        var host = new Border { Child = new Border { Background = new SolidColorBrush(Content) } };
        ForgeIslandDecor.Apply(host, ForgePalette.IslandsLight, ToolWindowAnchor.Left);

        var window = new Window
        {
            Width = 120,
            Height = 120,
            WindowDecorations = WindowDecorations.None,
            Background = new SolidColorBrush(Sea),
            Content = host,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        return bitmap!;
    }

    /// <summary>读一个像素（字节序按 framebuffer 的 Format 走，同 RenderSnapshotTests）。</summary>
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
