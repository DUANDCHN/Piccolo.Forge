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

    /// <summary>
    /// 竖着的三个点：「更多 / 选项」。
    /// </summary>
    /// <remarks>
    /// 标题条与标签条右端那个按钮用它（新 UI 里是**竖**的 ⋮；条带末尾那个"还有几个工具窗口"
    /// 用的是横的 <see cref="More"/>，两处不是一回事，别混用）。
    /// </remarks>
    public static Geometry MoreVertical { get; } = Parse(
        "M 7,3 L 9,3 L 9,5 L 7,5 Z M 7,7 L 9,7 L 9,9 L 7,9 Z M 7,11 L 9,11 L 9,13 L 7,13 Z");

    /// <summary>
    /// 隐藏（收起面板）：一条横线。
    /// </summary>
    /// <remarks>
    /// 对应上游标题条的 <c>HideAction</c>（<c>ToolWindowHeader.kt:119</c>）。
    /// 上游用的是 <c>AllIcons.General.HideToolWindow</c>（JetBrains 的美术资产，Forge 不带），
    /// 新 UI 里它就是一条短横 —— 与"最小化"同义，一眼认得出。
    /// </remarks>
    public static Geometry Hide { get; } = Parse("M 3.5,8 L 12.5,8");

    /// <summary>竖着切一刀（上下两块）。</summary>
    public static Geometry SplitVertical { get; } = Parse("M 2,2 L 14,2 L 14,14 L 2,14 Z M 2,8 L 14,8");

    /// <summary>横着切一刀（左右两块）。</summary>
    public static Geometry SplitHorizontal { get; } = Parse("M 2,2 L 14,2 L 14,14 L 2,14 Z M 8,2 L 8,14");

    /// <summary>没给图标时画的默认图形：一个空框。</summary>
    public static Geometry DefaultToolWindow { get; } = Parse("M 3,3 L 13,3 L 13,13 L 3,13 Z");

    // ── 常见工具窗口的图形 ──────────────────────────────────────────
    // 同样是自己画的。上游那些图标叫什么、长什么样是 JetBrains 的美术，
    // 这里只按「一眼能认出是什么」画个同义的形状，宿主随时可以换掉。

    /// <summary>文件夹：项目 / 文件树。</summary>
    public static Geometry Project { get; } = Parse("M 2,4 L 6,4 L 7.5,6 L 14,6 L 14,13 L 2,13 Z");

    /// <summary>层级：结构 / 大纲。</summary>
    public static Geometry Structure { get; } = Parse(
        "M 3,3 L 3,12 M 3,6 L 8,6 M 3,12 L 8,12 M 8,4 L 13,4 L 13,8 L 8,8 Z M 8,10 L 13,10 L 13,14 L 8,14 Z");

    /// <summary>提示符：终端 / 控制台。</summary>
    public static Geometry Terminal { get; } = Parse(
        "M 2,3 L 14,3 L 14,13 L 2,13 Z M 4.5,6.5 L 7,8 L 4.5,9.5 M 8.5,10.5 L 12,10.5");

    /// <summary>分支：版本控制。</summary>
    public static Geometry VersionControl { get; } = Parse(
        "M 5,4 L 5,12 M 5,7 C 5,7 11,7 11,7 M 11,4 L 11,7 "
        + "M 4,3 L 6,3 L 6,5 L 4,5 Z M 10,3 L 12,3 L 12,5 L 10,5 Z M 4,11 L 6,11 L 6,13 L 4,13 Z");

    /// <summary>对勾：提交。</summary>
    public static Geometry Commit { get; } = Parse("M 3,8.5 L 6.5,12 L 13,4");

    /// <summary>感叹三角：问题 / 警告。</summary>
    public static Geometry Problems { get; } = Parse("M 8,2.5 L 14,13 L 2,13 Z M 8,6.5 L 8,9.5 M 8,11 L 8,11.8");

    /// <summary>机架：服务。</summary>
    public static Geometry Services { get; } = Parse(
        "M 2,3 L 14,3 L 14,7 L 2,7 Z M 2,9 L 14,9 L 14,13 L 2,13 Z M 4.5,5 L 5.5,5 M 4.5,11 L 5.5,11");

    /// <summary>铃铛：通知。</summary>
    public static Geometry Notifications { get; } = Parse(
        "M 8,2 C 5,2 4,4 4,7 L 4,10 L 3,11.5 L 13,11.5 L 12,10 L 12,7 C 12,4 11,2 8,2 Z M 6.5,11.5 C 6.5,13.5 9.5,13.5 9.5,11.5");

    /// <summary>圆柱：数据库。</summary>
    public static Geometry Database { get; } = Parse(
        "M 3,4 C 3,2.5 13,2.5 13,4 L 13,12 C 13,13.5 3,13.5 3,12 Z M 3,4 C 3,5.5 13,5.5 13,4 M 3,8 C 3,9.5 13,9.5 13,8");

    /// <summary>放大镜：查找。</summary>
    public static Geometry Find { get; } = Parse(
        "M 7,2.5 C 9.5,2.5 11.5,4.5 11.5,7 C 11.5,9.5 9.5,11.5 7,11.5 C 4.5,11.5 2.5,9.5 2.5,7 C 2.5,4.5 4.5,2.5 7,2.5 Z M 10.5,10.5 L 14,14");

    /// <summary>三角：运行。</summary>
    public static Geometry Run { get; } = Parse("M 4,3 L 13,8 L 4,13 Z");

    /// <summary>虫子：调试。</summary>
    public static Geometry Debug { get; } = Parse(
        "M 5,6 C 5,3.5 11,3.5 11,6 L 11,10 C 11,12.5 5,12.5 5,10 Z "
        + "M 2.5,7 L 5,7 M 11,7 L 13.5,7 M 2.5,11 L 5,10 M 11,10 L 13.5,11 M 6,4 L 5,2.5 M 10,4 L 11,2.5");

    /// <summary>书签：书签 / 收藏。</summary>
    public static Geometry Bookmarks { get; } = Parse("M 4,2.5 L 12,2.5 L 12,13.5 L 8,10 L 4,13.5 Z");

    /// <summary>列表：待办 / 表格。</summary>
    public static Geometry TodoList { get; } = Parse(
        "M 3,4 L 4,4 M 6,4 L 13,4 M 3,8 L 4,8 M 6,8 L 13,8 M 3,12 L 4,12 M 6,12 L 13,12");

    /// <summary>图表：统计 / 直方图。</summary>
    public static Geometry Chart { get; } = Parse(
        "M 2.5,13.5 L 13.5,13.5 M 4,13.5 L 4,9 M 7,13.5 L 7,5 M 10,13.5 L 10,7 M 13,13.5 L 13,3");

    /// <summary>相机：设备 / 采集。</summary>
    public static Geometry Camera { get; } = Parse(
        "M 2,5 L 5,5 L 6.5,3 L 9.5,3 L 11,5 L 14,5 L 14,13 L 2,13 Z "
        + "M 8,6.5 C 9.5,6.5 10.5,7.5 10.5,9 C 10.5,10.5 9.5,11.5 8,11.5 C 6.5,11.5 5.5,10.5 5.5,9 C 5.5,7.5 6.5,6.5 8,6.5 Z");

    /// <summary>滑块：参数 / 设置。</summary>
    public static Geometry Settings { get; } = Parse(
        "M 2.5,4.5 L 13.5,4.5 M 2.5,8 L 13.5,8 M 2.5,11.5 L 13.5,11.5 "
        + "M 5,3 L 6,3 L 6,6 L 5,6 Z M 10,6.5 L 11,6.5 L 11,9.5 L 10,9.5 Z M 6.5,10 L 7.5,10 L 7.5,13 L 6.5,13 Z");

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
