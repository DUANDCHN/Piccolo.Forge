using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Piccolo.Forge.Tests;

/// <summary>
/// 从渲出来的一帧里读一个像素。
/// </summary>
/// <remarks>
/// 字节序按 <see cref="ILockedFramebuffer.Format"/> 走，别写死 ——
/// headless 后端给的是 <c>Rgba8888</c>，桌面后端多是 <c>Bgra8888</c>，
/// 写死一种会得到红蓝互换的结论。
/// </remarks>
internal static class PixelProbe
{
    /// <summary>读 (x, y) 处的颜色。</summary>
    public static Color At(WriteableBitmap bitmap, int x, int y)
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
