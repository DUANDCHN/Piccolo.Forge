using Avalonia;
using Piccolo.Forge.Model;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 编辑区标签页拖拽的几何判定。
/// </summary>
/// <remarks>
/// 对应 <c>platform/platform-api/src/com/intellij/ui/tabs/impl/DragHelper.java</c>。
/// <para>
/// <b>上游不画插入线。</b>拖动过程中它直接 <c>reallocate</c> 当场换位
/// （<c>DragHelper.java:228-235</c>），被拖的那一页跟着鼠标飘、其余的立刻让位 ——
/// 和侧边栏条带那套是同一种手感。所以这里没有"落点索引 + 蓝条"那一层，
/// 只有"现在该和谁换"。
/// </para>
/// <para>
/// 全是静态纯函数：拿矩形算矩形，不碰控件，好测。
/// </para>
/// </remarks>
internal static class ForgeEditorTabDragTarget
{
    /// <summary>
    /// 起拖阈值：按下后要挪够这么多像素才算拖拽，默认 <b>7</b>。
    /// </summary>
    /// <remarks><c>MouseDragHelper.java:32 · DRAG_START_DEADZONE</c>（上游还乘了缩放系数）。</remarks>
    public const double DragStartDeadZone = 7;

    /// <summary>
    /// 判定"拖出标签行"的比例，默认 <b>0.1</b>。
    /// </summary>
    /// <remarks>
    /// Registry <c>ide.tabbedPane.dragOutMultiplier</c>
    /// （<c>platform/util/resources/misc/registry.properties:567</c>）。
    /// </remarks>
    public const double DragOutMultiplier = 0.1;

    /// <summary>
    /// <c>findMostOverlapping</c> 里的让位余量，<b>0.3</b>。
    /// </summary>
    /// <remarks>见 <see cref="FindMostOverlapping"/> 的说明与 <c>DragHelper.java:273</c>。</remarks>
    public const double FreeSpaceSlack = 0.3;

    /// <summary>
    /// 把矩形挪进边界里（不改大小）。
    /// </summary>
    /// <remarks>
    /// 对应 <c>DragHelper.java:204 · ScreenUtil.moveToFit(dragRec, headerRec, null)</c>。
    /// 上游那个还会在放不下时缩尺寸；这里只夹位置 —— 标签页比标签行小是前提，
    /// 真放不下的话（行被压到比一个标签还窄）拖拽本身已经没意义了。
    /// <para>
    /// 它管的是<b>被拖的标签页不许飞出标签行</b>：没有这一夹，往下拖会把标签画到正文上，
    /// 往左拖会画到行外面去。
    /// </para>
    /// </remarks>
    /// <param name="rect">要挪的矩形。</param>
    /// <param name="bounds">边界。</param>
    /// <returns>挪好之后的矩形。</returns>
    public static Rect MoveToFit(Rect rect, Rect bounds)
    {
        var x = rect.X;
        var y = rect.Y;

        if (rect.Width <= bounds.Width)
        {
            x = Math.Clamp(x, bounds.X, bounds.Right - rect.Width);
        }

        if (rect.Height <= bounds.Height)
        {
            y = Math.Clamp(y, bounds.Y, bounds.Bottom - rect.Height);
        }

        return new Rect(x, y, rect.Width, rect.Height);
    }

    /// <summary>
    /// 已经从原位挪开了多远 —— 也就是"腾出来多少地方"。
    /// </summary>
    /// <remarks>
    /// 对应 <c>DragHelper.java:259-264</c>，**方向相关**：
    /// <code>
    /// if (min(dragRec) &lt; min(origRec))  freeSpace = max(origRec) - max(dragRec);  // 往回拖
    /// else                                freeSpace = min(dragRec) - min(origRec);  // 往前拖
    /// </code>
    /// 两个分支都在量"拖动矩形的后缘离开原位后缘多远"，只是方向不同。
    /// </remarks>
    /// <param name="horizontal">量横轴还是纵轴。</param>
    /// <param name="dragRec">当前拖动矩形。</param>
    /// <param name="originalRec">这一页原本在哪。</param>
    /// <returns>腾出来的距离，可能为负。</returns>
    public static double FreeSpace(bool horizontal, Rect dragRec, Rect originalRec)
    {
        var dragMin = horizontal ? dragRec.X : dragRec.Y;
        var dragMax = horizontal ? dragRec.Right : dragRec.Bottom;
        var origMin = horizontal ? originalRec.X : originalRec.Y;
        var origMax = horizontal ? originalRec.Right : originalRec.Bottom;

        return dragMin < origMin ? origMax - dragMax : dragMin - origMin;
    }

    /// <summary>
    /// 在几个候选里挑一个"该和它换位"的。
    /// </summary>
    /// <remarks>
    /// 对应 <c>DragHelper.java:256-284 · findMostOverlapping</c>。两条规则：
    /// <list type="number">
    /// <item>
    /// <b>候选比腾出来的地方大就跳过</b>（<c>:273</c>，还放宽了 30%）。
    /// 这是防抖闸：没拖够一整个邻居的宽度就别换，否则鼠标在两页交界处微动会来回跳。
    /// <c>freeSpace</c> 为零或负（还没离开原位）时任何候选都过不了这一关 —— 正是要的。
    /// </item>
    /// <item>剩下的里挑<b>重叠面积最大</b>的（<c>:275-280</c>）。</item>
    /// </list>
    /// </remarks>
    /// <param name="dragRec">当前拖动矩形。</param>
    /// <param name="freeSpace">腾出来的距离，见 <see cref="FreeSpace"/>。</param>
    /// <param name="horizontal">按横轴还是纵轴量候选的尺寸。</param>
    /// <param name="candidates">候选矩形，下标即标签页序号；<see langword="null"/> 表示这一格没有候选。</param>
    /// <returns>选中的下标，没有则 <c>-1</c>。</returns>
    public static int FindMostOverlapping(
        Rect dragRec,
        double freeSpace,
        bool horizontal,
        IReadOnlyList<(int Index, Rect Bounds)> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var max = -1.0;
        var best = -1;

        foreach (var (index, bounds) in candidates)
        {
            var size = horizontal ? bounds.Width : bounds.Height;
            if (size > freeSpace + (freeSpace * FreeSpaceSlack))
            {
                continue;
            }

            var overlapWidth = Math.Max(0, Math.Min(dragRec.Right, bounds.Right) - Math.Max(dragRec.X, bounds.X));
            var overlapHeight = Math.Max(0, Math.Min(dragRec.Bottom, bounds.Bottom) - Math.Max(dragRec.Y, bounds.Y));
            var area = overlapWidth * overlapHeight;

            if (area > max)
            {
                max = area;
                best = index;
            }
        }

        return best;
    }

    /// <summary>
    /// 当前该和哪一页换位。
    /// </summary>
    /// <remarks>
    /// 对应 <c>DragHelper.java:206-226</c>。四个探测点取拖动矩形<b>四条边的中点</b>
    /// （上游的 <c>deadZone</c> 都是 0），各自问"这儿压着谁"；
    /// 横向标签行先用左右一对判，判不出再退回上下一对（<c>:216-220</c>）。
    /// </remarks>
    /// <param name="dragRec">当前拖动矩形，须已 <see cref="MoveToFit"/> 过。</param>
    /// <param name="originalRec">这一页原本在哪。</param>
    /// <param name="tabRects">标签行里每一页的矩形，按显示次序。</param>
    /// <param name="sourceIndex">被拖的是第几个 —— 它自己不能当目标。</param>
    /// <param name="horizontal">标签行是横的还是竖的。</param>
    /// <returns>目标下标，没有则 <c>-1</c>。</returns>
    public static int ResolveTarget(
        Rect dragRec,
        Rect originalRec,
        IReadOnlyList<Rect> tabRects,
        int sourceIndex,
        bool horizontal = true)
    {
        ArgumentNullException.ThrowIfNull(tabRects);

        var top = FindAt(tabRects, sourceIndex, new Point(dragRec.Center.X, dragRec.Y));
        var bottom = FindAt(tabRects, sourceIndex, new Point(dragRec.Center.X, dragRec.Bottom));
        var left = FindAt(tabRects, sourceIndex, new Point(dragRec.X, dragRec.Center.Y));
        var right = FindAt(tabRects, sourceIndex, new Point(dragRec.Right, dragRec.Center.Y));

        var alongRow = Pack(tabRects, left, right);
        var acrossRow = Pack(tabRects, top, bottom);

        if (!horizontal)
        {
            (alongRow, acrossRow) = (acrossRow, alongRow);
        }

        var target = FindMostOverlapping(dragRec, FreeSpace(horizontal, dragRec, originalRec), horizontal, alongRow);
        return target >= 0
            ? target
            : FindMostOverlapping(dragRec, FreeSpace(!horizontal, dragRec, originalRec), !horizontal, acrossRow);
    }

    /// <summary>
    /// 拖到行外面去了没有。
    /// </summary>
    /// <remarks>
    /// 对应 <c>SingleRowLayoutStrategy.java:100-105 · Horizontal.isDragOut</c>。
    /// <para>
    /// ⚠ 横向标签行<b>重写掉了基类那条 <c>|dX| &gt; 宽 × 倍率</c></b>
    /// （<c>TabLayout.java:27-30</c>）—— 只剩纵向位移与"被挤出行外"两条。
    /// 这不是省事：横向位移是行内重排要用的，两条判据同时开着的话，
    /// 手一动就判成拖出，行内根本排不了。
    /// </para>
    /// </remarks>
    /// <param name="labelBounds">这一页当前的矩形。</param>
    /// <param name="rowWidth">标签行的宽度。</param>
    /// <param name="deltaX">从按下点算起的横向位移。</param>
    /// <param name="deltaY">纵向位移。</param>
    /// <param name="multiplier">比例，默认 <see cref="DragOutMultiplier"/>。</param>
    /// <returns>算拖出则为 <see langword="true"/>。</returns>
    public static bool IsDragOut(
        Rect labelBounds,
        double rowWidth,
        double deltaX,
        double deltaY,
        double multiplier = DragOutMultiplier)
    {
        if (labelBounds.Right + deltaX < 0 || labelBounds.Right > rowWidth)
        {
            return true;
        }

        return Math.Abs(deltaY) > labelBounds.Height * multiplier;
    }

    /// <summary>
    /// 一组的<b>落点区</b> —— 整块减去标签行。
    /// </summary>
    /// <remarks>
    /// 对应 <c>JBTabs.java:101-115 · getDropArea</c>：标签在上方时
    /// <c>insets.top = 标签高</c>，然后从整块里扣掉。
    /// <para>
    /// ⚠ <b>标签行不算落点区</b>。这不是细节：把标签行算进去的话，
    /// 「拖到上边缘 → 往上切一刀」的判定带会盖在标签行上，
    /// 而那一带正是行内重排要用的地方，两件事会在同一片像素上打架。
    /// </para>
    /// </remarks>
    /// <param name="groupBounds">这一组整块的矩形。</param>
    /// <param name="tabRowHeight">标签行的高度。</param>
    /// <returns>落点区。</returns>
    public static Rect DropArea(Rect groupBounds, double tabRowHeight)
    {
        var top = Math.Clamp(tabRowHeight, 0, groupBounds.Height);
        return new Rect(groupBounds.X, groupBounds.Y + top, groupBounds.Width, groupBounds.Height - top);
    }

    /// <summary>
    /// 按落点方向把落点区裁成要高亮的那一块。
    /// </summary>
    /// <remarks>
    /// 对应 <c>TabsUtil.java:95-111 · updateBoundsWithDropSide</c>：
    /// 四边各取<b>一半</b>，中心不裁（整块）。
    /// </remarks>
    /// <param name="dropArea">落点区，见 <see cref="DropArea"/>。</param>
    /// <param name="side">落在哪一侧。</param>
    /// <returns>要高亮的矩形；<see cref="EditorDropSide.None"/> 时为 <see langword="null"/>。</returns>
    public static Rect? HighlightFor(Rect dropArea, EditorDropSide side) => side switch
    {
        EditorDropSide.Center => dropArea,
        EditorDropSide.Top => dropArea.WithHeight(dropArea.Height / 2),
        EditorDropSide.Left => dropArea.WithWidth(dropArea.Width / 2),
        EditorDropSide.Bottom => new Rect(
            dropArea.X, dropArea.Y + (dropArea.Height / 2), dropArea.Width, dropArea.Height - (dropArea.Height / 2)),
        EditorDropSide.Right => new Rect(
            dropArea.X + (dropArea.Width / 2), dropArea.Y, dropArea.Width - (dropArea.Width / 2), dropArea.Height),
        _ => null,
    };

    private static int FindAt(IReadOnlyList<Rect> tabRects, int sourceIndex, Point point)
    {
        for (var i = 0; i < tabRects.Count; i++)
        {
            if (i != sourceIndex && tabRects[i].Width > 0 && tabRects[i].Contains(point))
            {
                return i;
            }
        }

        return -1;
    }

    private static List<(int Index, Rect Bounds)> Pack(IReadOnlyList<Rect> tabRects, int a, int b)
    {
        var list = new List<(int, Rect)>(2);
        if (a >= 0)
        {
            list.Add((a, tabRects[a]));
        }

        if (b >= 0 && b != a)
        {
            list.Add((b, tabRects[b]));
        }

        return list;
    }
}
