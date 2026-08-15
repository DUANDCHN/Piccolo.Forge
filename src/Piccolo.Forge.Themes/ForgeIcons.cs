using Avalonia;
using Avalonia.Media;

namespace Piccolo.Forge.Themes;

/// <summary>
/// 库自己画的矢量图标。
/// </summary>
/// <remarks>
/// <b>这些是 Forge 自己画的，没有描上游的图形。</b>
/// 上游那套图标是 JetBrains 的美术资产，「读懂设计再自己写一遍」适用于度量与行为，
/// 不适用于别人的美术。这里按审出来的**尺寸**画：
/// 统一以 16×16 为画布，实际显示时缩放到
/// <see cref="ForgeMetrics.StripeButtonIconSize"/>（20，紧凑 16）。
/// <para>
/// 宿主要用自己的图标随时可以：条带按钮与面板的 <c>Icon</c> 都是 <see cref="object"/>。
/// 这里给的是**库自己要用的那几个**（关闭、图钉、更多、分屏），
/// 外加一个没给图标时的默认图形。
/// </para>
/// </remarks>
public static class ForgeIcons
{
    /// <summary>图标画布的边长 16 —— 所有路径都按这个坐标系画。</summary>
    public const double ViewBoxSize = 16;

    /// <summary>叉：关闭。</summary>
    public static Geometry Close { get; } = Parse("M 4,4 L 12,12 M 12,4 L 4,12");

    /// <summary>图钉：固定住的标签页画它，点它是解固定。</summary>
    public static Geometry Pin { get; } = Parse("M 6,2 L 10,2 L 10,8 L 12,10 L 4,10 L 6,8 Z M 8,10 L 8,14");

    /// <summary>三个点：「更多」。</summary>
    public static Geometry More { get; } = Parse(
        "M 3,7 L 5,7 L 5,9 L 3,9 Z M 7,7 L 9,7 L 9,9 L 7,9 Z M 11,7 L 13,7 L 13,9 L 11,9 Z");

    /// <summary>竖着切一刀（上下两块）。</summary>
    public static Geometry SplitVertical { get; } = Parse("M 2,2 L 14,2 L 14,14 L 2,14 Z M 2,8 L 14,8");

    /// <summary>横着切一刀（左右两块）。</summary>
    public static Geometry SplitHorizontal { get; } = Parse("M 2,2 L 14,2 L 14,14 L 2,14 Z M 8,2 L 8,14");

    /// <summary>没给图标时画的默认图形：一个空框。</summary>
    public static Geometry DefaultToolWindow { get; } = Parse("M 3,3 L 13,3 L 13,13 L 3,13 Z");

    /// <summary>
    /// 把图标画在指定矩形里（等比缩放并居中）。
    /// </summary>
    /// <param name="context">绘制上下文。</param>
    /// <param name="icon">要画的图形。</param>
    /// <param name="bounds">目标矩形。</param>
    /// <param name="brush">线条颜色。</param>
    /// <param name="filled">是填充还是描边。</param>
    /// <param name="thickness">描边粗细。</param>
    public static void Draw(
        DrawingContext context,
        Geometry icon,
        Rect bounds,
        IBrush brush,
        bool filled = false,
        double thickness = 1)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(icon);

        var scale = Math.Min(bounds.Width, bounds.Height) / ViewBoxSize;
        if (scale <= 0)
        {
            return;
        }

        var size = ViewBoxSize * scale;
        var transform = Matrix.CreateScale(scale, scale)
                        * Matrix.CreateTranslation(
                            bounds.X + ((bounds.Width - size) / 2),
                            bounds.Y + ((bounds.Height - size) / 2));

        using (context.PushTransform(transform))
        {
            context.DrawGeometry(
                filled ? brush : null,
                filled ? null : new Pen(brush, thickness / scale),
                icon);
        }
    }

    private static Geometry Parse(string data) => Geometry.Parse(data);
}
