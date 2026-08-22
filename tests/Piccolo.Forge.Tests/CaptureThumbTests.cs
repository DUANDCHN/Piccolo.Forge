using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Piccolo.Forge.Avalonia;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 拖影快照：拍**带字形的控件**（条带图标就是字体图标）不能带出黑块。
public class CaptureThumbTests
{
    /// <summary>
    /// 拍一个红色字形，位图里不能有**不透明纯黑**的像素。
    /// </summary>
    /// <remarks>
    /// 默认的 LCD 子像素抗锯齿假定目标有不透明底，画进透明位图会把一部分笔画落成
    /// (0,0,0,255) —— 实机表现就是"拖侧边栏图标时图标带一圈黑"。
    /// 对照那一半（不改文本渲染模式直接 Render）先证明这条路真的会黑，
    /// 否则这个用例就是一条永远绿的假守卫。
    /// </remarks>
    [AvaloniaFact]
    public void A_glyph_thumb_carries_no_opaque_black()
    {
        var icon = new TextBlock
        {
            Text = "Wg",
            FontSize = 16,
            Foreground = Brushes.Red,
        };

        var window = new Window
        {
            Width = 100,
            Height = 100,
            WindowDecorations = WindowDecorations.None,
            Content = icon,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var raw = RenderWithoutCare(icon);
        var thumb = ForgeDragOverlay.CaptureThumb(icon);
        Assert.NotNull(thumb);

        // 对照组不黑（macOS 的 Skia 文本默认就是灰度抗锯齿）：这台机器守不住任何东西，跳过而不是装绿
        Assert.SkipWhen(OpaqueBlackCount(raw) == 0, "对照组不黑：这台机器的文本渲染没走子像素抗锯齿，本用例在此无意义");
        Assert.Equal(0, OpaqueBlackCount(thumb!));

        raw.Dispose();
        thumb!.Dispose();
    }

    /// <summary>
    /// 拍快照可以临时换个前景，拍完控件仍旧继承原来的颜色。
    /// </summary>
    /// <remarks>
    /// 条带图标要这条：开着的面板，它的按钮是激活态、图标是白的，而拖影背板取的是悬停
    /// 那一档底色 —— 白图标落在浅色主题的浅灰背板上几乎看不见（用户实机提的）。
    /// 拖影画的是"这颗按钮被拎起来"的常规样子，前景该跟着背板一起退回常规那一档。
    /// </remarks>
    [AvaloniaFact]
    public void A_thumb_can_be_taken_in_another_colour()
    {
        var icon = new TextBlock { Text = "Wg", FontSize = 16 };

        // 前景从上一层继承下来 —— 宿主塞进来的图标控件自己不设色，真实情况就是这样
        var host = new Border { Child = icon };
        TextElement.SetForeground(host, Brushes.White);

        var window = new Window
        {
            Width = 100,
            Height = 100,
            WindowDecorations = WindowDecorations.None,
            Content = host,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var asIs = ForgeDragOverlay.CaptureThumb(icon);
        var tinted = ForgeDragOverlay.CaptureThumb(icon, Brushes.Red);

        Assert.NotNull(asIs);
        Assert.NotNull(tinted);

        Assert.True(WhiteCount(asIs!) > 0 && RedCount(asIs!) == 0, "原样拍应该是白的");
        Assert.True(RedCount(tinted!) > 0 && WhiteCount(tinted!) == 0, "换色那一张应该是红的");

        // 换的那个色不能留在控件上：撤成"没设过"，前景继续由上一层继承下来
        Assert.False(icon.IsSet(TextElement.ForegroundProperty), "拍完没还原：控件被钉在换的那个色上了");

        // ⚠ 屏幕上要等下一轮排布才回到白 —— TextBlock 的文本排版是缓存的，
        //   紧接着**同一帧**再拍一张拿到的仍是红。真实拖动里这不成问题（下一帧就重画了），
        //   但用例得把这一步排明白，否则读起来像"还原没生效"。
        icon.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var afterwards = ForgeDragOverlay.CaptureThumb(icon);
        Assert.NotNull(afterwards);
        Assert.True(WhiteCount(afterwards!) > 0 && RedCount(afterwards!) == 0, "还原之后重排一次，应该又是白的");

        asIs!.Dispose();
        tinted!.Dispose();
        afterwards!.Dispose();
    }

    /// <summary>
    /// 激活态的前景与常规那一档**确实不是一个色** —— 上一条用例的前提。
    /// </summary>
    /// <remarks>四套主题的 <c>ButtonSelectedForeground</c> 都是白；拖影用的是 <c>IdleForeground</c>。</remarks>
    [AvaloniaFact]
    public void An_active_button_knows_its_idle_colour_too()
    {
        var button = new ForgeStripeButton { IsToolWindowActive = true };
        var window = new Window
        {
            Width = 100,
            Height = 100,
            WindowDecorations = WindowDecorations.None,
            Content = button,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(button.Palette.ButtonSelectedForeground, button.ResolveForeground());
        Assert.Equal(button.Palette.ButtonForeground, button.IdleForeground);
        Assert.NotEqual(button.ResolveForeground(), button.IdleForeground);

        window.Close();
    }

    /// 数「看得出是白」的像素：按颜色判，不看覆盖率。
    /// 位图是预乘 alpha 的，半透明像素的通道值已经乘过 alpha，所以与 alpha 比而不是与 255 比 ——
    /// 灰度抗锯齿下 16px 字形可能没有一个整像素是满覆盖的（macOS 实测），但颜色这条事实不受覆盖率影响。
    private static int WhiteCount(RenderTargetBitmap bitmap) => CountPixels(bitmap, (b, g, r, a) => b >= a * 0.8 && g >= a * 0.8 && r >= a * 0.8);

    /// 数「看得出是红」的像素。
    private static int RedCount(RenderTargetBitmap bitmap) => CountPixels(bitmap, (b, g, r, a) => r >= a * 0.8 && g <= a * 0.2 && b <= a * 0.2);

    private static int CountPixels(RenderTargetBitmap bitmap, Func<byte, byte, byte, byte, bool> match)
    {
        var bytes = ReadPixels(bitmap);
        var count = 0;

        for (var i = 0; i < bytes.Length; i += 4)
        {
            if (bytes[i + 3] >= 32 && match(bytes[i], bytes[i + 1], bytes[i + 2], bytes[i + 3]))
            {
                count++;
            }
        }

        return count;
    }

    /// 旧写法：不碰文本渲染模式，直接把控件画进透明位图。
    private static RenderTargetBitmap RenderWithoutCare(Control control)
    {
        var size = control.Bounds.Size;
        var bitmap = new RenderTargetBitmap(
            new PixelSize((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height)));
        bitmap.Render(control);
        return bitmap;
    }

    /// 数一数有多少个像素是「不透明纯黑」—— 这一条必须精确，它就是黑块本身。
    private static int OpaqueBlackCount(RenderTargetBitmap bitmap)
    {
        var bytes = ReadPixels(bitmap);
        var count = 0;

        for (var i = 0; i < bytes.Length; i += 4)
        {
            if (bytes[i] == 0 && bytes[i + 1] == 0 && bytes[i + 2] == 0 && bytes[i + 3] == 255)
            {
                count++;
            }
        }

        return count;
    }

    /// 把位图读成 Bgra8888 的字节。
    /// RenderTargetBitmap 的像素格式随平台：Windows 是 Bgra8888，macOS 是 Rgba8888 ——
    /// 不看 Format 直接按 BGRA 数，红在 mac 上会被数成蓝（曾经的"mac 上恒红两条"就是这个）。
    private static byte[] ReadPixels(RenderTargetBitmap bitmap)
    {
        var bytes = ReadRaw(bitmap);
        if (bitmap.Format == PixelFormat.Rgba8888)
        {
            for (var i = 0; i < bytes.Length; i += 4)
            {
                (bytes[i], bytes[i + 2]) = (bytes[i + 2], bytes[i]);
            }
        }

        return bytes;
    }

    private static byte[] ReadRaw(RenderTargetBitmap bitmap)
    {
        var w = bitmap.PixelSize.Width;
        var h = bitmap.PixelSize.Height;
        var stride = w * 4;
        var bytes = new byte[stride * h];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(bytes, System.Runtime.InteropServices.GCHandleType.Pinned);

        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, w, h), handle.AddrOfPinnedObject(), bytes.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        return bytes;
    }
}
