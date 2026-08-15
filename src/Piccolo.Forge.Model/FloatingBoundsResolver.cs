namespace Piccolo.Forge.Model;

/// <summary>
/// 浮动 / 独立窗口的位置与尺寸怎么定。
/// </summary>
/// <remarks>
/// 对应 <c>platform/platform-impl/src/com/intellij/openapi/wm/impl/ToolWindowManagerDecorators.kt:212-252
/// · setExternalDecoratorBounds</c>，三条分支见 <see cref="Resolve"/>。
/// </remarks>
public static class FloatingBoundsResolver
{
    /// <summary>
    /// 算出该用的 bounds，以及要不要相对主窗口居中。
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item>存过且**在屏幕上有效** → 原样用，不居中（<c>:222-226</c>）；</item>
    /// <item>存过且宽高 &gt; 0 但跑出屏幕 → 拉回屏幕内，**然后居中**（<c>:227-233</c>）；</item>
    /// <item>没存过 → 用 <paramref name="fallbackSize"/>，然后居中（<c>:234-246</c>）。</item>
    /// </list>
    /// </remarks>
    /// <param name="stored">存档里的 bounds。</param>
    /// <param name="screen">屏幕范围。</param>
    /// <param name="fallbackSize">没存过时用的尺寸（上游取内部 decorator 的 size，为 0 时退回 preferredSize）。</param>
    /// <returns>该用的 bounds，以及是否需要居中。</returns>
    public static (FloatingBounds Bounds, bool NeedToCenter) Resolve(
        FloatingBounds? stored,
        FloatingBounds screen,
        (int Width, int Height) fallbackSize)
    {
        if (stored is { } bounds && IsValid(bounds, screen))
        {
            return (bounds, false);
        }

        if (stored is { Width: > 0, Height: > 0 } offscreen)
        {
            return (MoveToFit(offscreen, screen), true);
        }

        return (new FloatingBounds(0, 0, fallbackSize.Width, fallbackSize.Height), true);
    }

    /// <summary>
    /// 这个 bounds 还在屏幕上吗。
    /// </summary>
    /// <param name="bounds">要检查的 bounds。</param>
    /// <param name="screen">屏幕范围。</param>
    /// <returns>宽高为正且与屏幕有交集则为 <see langword="true"/>。</returns>
    public static bool IsValid(FloatingBounds bounds, FloatingBounds screen)
        => bounds is { Width: > 0, Height: > 0 }
           && bounds.X < screen.X + screen.Width
           && bounds.Y < screen.Y + screen.Height
           && bounds.X + bounds.Width > screen.X
           && bounds.Y + bounds.Height > screen.Y;

    /// <summary>
    /// 把跑出屏幕的窗口拉回来（尺寸不变，先按屏幕裁一次上限）。
    /// </summary>
    /// <param name="bounds">原 bounds。</param>
    /// <param name="screen">屏幕范围。</param>
    /// <returns>拉回屏幕内的 bounds。</returns>
    public static FloatingBounds MoveToFit(FloatingBounds bounds, FloatingBounds screen)
    {
        var width = Math.Min(bounds.Width, screen.Width);
        var height = Math.Min(bounds.Height, screen.Height);
        var x = Math.Clamp(bounds.X, screen.X, screen.X + screen.Width - width);
        var y = Math.Clamp(bounds.Y, screen.Y, screen.Y + screen.Height - height);

        return new FloatingBounds(x, y, width, height);
    }

    /// <summary>
    /// 相对一个父窗口居中。
    /// </summary>
    /// <remarks>对应 <c>:248-251 · setLocationRelativeTo(parentFrame)</c>。</remarks>
    /// <param name="bounds">要居中的 bounds。</param>
    /// <param name="parent">父窗口的 bounds。</param>
    /// <returns>居中之后的 bounds。</returns>
    public static FloatingBounds CenterOn(FloatingBounds bounds, FloatingBounds parent)
        => bounds with
        {
            X = parent.X + ((parent.Width - bounds.Width) / 2),
            Y = parent.Y + ((parent.Height - bounds.Height) / 2),
        };
}
