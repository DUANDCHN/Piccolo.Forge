namespace Piccolo.Forge.Model;

/// <summary>
/// 把一页拖到编辑区里，落在哪一侧。
/// </summary>
public enum EditorDropSide
{
    /// <summary>落在外面，什么都不做。</summary>
    None,

    /// <summary>落在中间 —— **不分屏**，并进那一组。</summary>
    Center,

    /// <summary>上边：横向切一刀，新的在上。</summary>
    Top,

    /// <summary>左边：纵向切一刀，新的在左。</summary>
    Left,

    /// <summary>下边：横向切一刀，新的在下。</summary>
    Bottom,

    /// <summary>右边：纵向切一刀，新的在右。</summary>
    Right,
}

/// <summary>
/// 拖一页到编辑区上时，落点算在哪一侧。
/// </summary>
/// <remarks>
/// 对应 <c>platform/platform-api/src/com/intellij/ui/tabs/TabsUtil.java:53-92
/// · getDropSideFor</c>。
/// <para>
/// 四条边各是一个**梯形**（不是矩形）：以边长乘比例 <c>r</c> 往里收。
/// 四个梯形沿对角线相接、**互不重叠**——角落归哪边由对角线分，
/// 不是由判定顺序分（顺序只在落点正好压在对角线上时才起作用）。
/// 判定顺序是右 → 左 → 下 → 上，都不中且在框内才是中心（<c>:87-91</c>）。
/// </para>
/// </remarks>
public static class EditorDropPolicy
{
    /// <summary>
    /// 分屏判定的边缘比例，默认 <b>0.2</b>。
    /// </summary>
    /// <remarks>
    /// Registry <c>ide.tabbedPane.dragToSplitRatio</c>
    /// （<c>platform/util/resources/misc/registry.properties:569</c>）。
    /// </remarks>
    public const double DefaultDragToSplitRatio = 0.2;

    /// <summary>比例的下限 0.05（<c>TabsUtil.java:55</c>）。</summary>
    public const double MinRatio = 0.05;

    /// <summary>比例的上限 0.45（同上）。</summary>
    public const double MaxRatio = 0.45;

    /// <summary>
    /// 算出落在哪一侧。
    /// </summary>
    /// <param name="x">落点横坐标（相对目标区域左上角）。</param>
    /// <param name="y">落点纵坐标。</param>
    /// <param name="width">目标区域的宽。</param>
    /// <param name="height">目标区域的高。</param>
    /// <param name="ratio">边缘比例，会被夹到 [0.05, 0.45]。</param>
    /// <returns>落在哪一侧。</returns>
    public static EditorDropSide Resolve(
        double x,
        double y,
        double width,
        double height,
        double ratio = DefaultDragToSplitRatio)
    {
        if (width <= 0 || height <= 0)
        {
            return EditorDropSide.None;
        }

        var r = Math.Clamp(ratio, MinRatio, MaxRatio);

        // 判定顺序照抄上游：右 → 左 → 下 → 上。
        // 四个梯形是平铺的，所以顺序只在正好压在对角线上时才有影响。
        if (InRight(x, y, width, height, r))
        {
            return EditorDropSide.Right;
        }

        if (InLeft(x, y, width, height, r))
        {
            return EditorDropSide.Left;
        }

        if (InBottom(x, y, width, height, r))
        {
            return EditorDropSide.Bottom;
        }

        if (InTop(x, y, width, height, r))
        {
            return EditorDropSide.Top;
        }

        return x >= 0 && y >= 0 && x <= width && y <= height
            ? EditorDropSide.Center
            : EditorDropSide.None;
    }

    /// <summary>
    /// 这一侧要不要分屏。
    /// </summary>
    /// <remarks>
    /// 中心与「落在外面」都不分屏 —— 与工具窗口那边
    /// <c>splitWithContent(dropSide == -1 || CENTER → 不分屏)</c> 同形。
    /// </remarks>
    /// <param name="side">落点。</param>
    /// <returns>要分屏则为 <see langword="true"/>。</returns>
    public static bool IsSplit(EditorDropSide side)
        => side is EditorDropSide.Top or EditorDropSide.Left or EditorDropSide.Bottom or EditorDropSide.Right;

    /// <summary>这一侧对应竖切还是横切。</summary>
    /// <param name="side">落点。</param>
    /// <returns>上下切为 <see langword="true"/>。</returns>
    public static bool IsVertical(EditorDropSide side)
        => side is EditorDropSide.Top or EditorDropSide.Bottom;

    /// <summary>新的那一组落在第一块还是第二块。</summary>
    /// <remarks>往上/往左拖 → 新的在前；往下/往右 → 新的在后。</remarks>
    /// <param name="side">落点。</param>
    /// <returns>新组在第二块则为 <see langword="true"/>。</returns>
    public static bool NewGroupIsSecond(EditorDropSide side)
        => side is EditorDropSide.Bottom or EditorDropSide.Right;

    // 四个梯形，形状照抄 TabsUtil.java:59-85
    private static bool InTop(double x, double y, double w, double h, double r)
        => InPolygon(x, y, [(0, 0), (w, 0), (w * (1 - r), h * r), (w * r, h * r)]);

    private static bool InLeft(double x, double y, double w, double h, double r)
        => InPolygon(x, y, [(0, 0), (w * r, h * r), (w * r, h * (1 - r)), (0, h)]);

    private static bool InBottom(double x, double y, double w, double h, double r)
        => InPolygon(x, y, [(0, h), (w * r, h * (1 - r)), (w * (1 - r), h * (1 - r)), (w, h)]);

    private static bool InRight(double x, double y, double w, double h, double r)
        => InPolygon(x, y, [(w, 0), (w * (1 - r), h * r), (w * (1 - r), h * (1 - r)), (w, h)]);

    private static bool InPolygon(double x, double y, (double X, double Y)[] points)
    {
        var inside = false;

        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            var (xi, yi) = points[i];
            var (xj, yj) = points[j];

            if (yi > y != yj > y && x < ((xj - xi) * (y - yi) / (yj - yi)) + xi)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
