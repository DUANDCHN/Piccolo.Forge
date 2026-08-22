using Avalonia;
using Piccolo.Forge.Model;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 一次拖拽算出来的落点。
/// </summary>
/// <param name="Anchor">落到哪条边。</param>
/// <param name="Order">插到什么次序；<c>-1</c> 表示末尾。</param>
/// <param name="IsSplit">落到同一条边的哪一半。</param>
internal readonly record struct StripeDropTarget(ToolWindowAnchor Anchor, int Order, bool IsSplit);

/// <summary>
/// 由落点算插入位置。
/// </summary>
/// <remarks>
/// 对应 <c>AbstractDroppableStripe.kt:374-401</c> 的逐按钮判定与
/// <c>:242-258 · finishDrop</c> 的收尾。
/// </remarks>
internal static class StripeDropResolver
{
    /// <summary>
    /// 落在一组按钮里的第几个之前。
    /// </summary>
    /// <remarks>
    /// 上游逐个比「落点坐标 - 当前累计坐标 &lt; 该按钮尺寸的一半」，
    /// 命中就把插入位置定在这个按钮的 order 上（<c>:374-401</c>）；
    /// 都没命中则兜底 <c>-1</c>（末尾，<c>:437-442</c>）。
    /// </remarks>
    /// <param name="offset">落点在这一组里的纵向偏移。</param>
    /// <param name="items">这一组里每一项的（order, 高度），按显示次序给。</param>
    /// <returns>插入位置的 order；<c>-1</c> 表示末尾。</returns>
    public static int ResolveInsertOrder(double offset, IReadOnlyList<(int Order, double Size)> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var index = ResolveInsertIndex(offset, items.Select(x => x.Size).ToList());
        return index < items.Count ? items[index].Order : -1;
    }

    /// <summary>
    /// 落在第几个之前——**序号**，不是 order。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="ResolveInsertOrder"/> 同一条判据，只是返回序号：
    /// 空槽要插在排布序列的哪个位置，靠的是这个（order 是模型的事，序号是排布的事，
    /// 两者在「被拖那个已从序列里拿掉」之后不再一一对应）。
    /// <para>
    /// ⚠ <paramref name="sizes"/> 必须是**排除被拖项之后**的序列。上游在
    /// <c>AbstractDroppableStripe.kt:601</c> 先 <c>tools.remove(dragButton)</c> 再走这段；
    /// 不排除的话后面每一项的累计坐标都多算一个按钮高，判定整体偏一格
    /// ——表现就是「放在下面、框提示在上面」。
    /// </para>
    /// </remarks>
    /// <param name="offset">落点在这一组里的纵向偏移。</param>
    /// <param name="sizes">排除被拖项之后，每一项的高度，按显示次序给。</param>
    /// <returns>插入序号，范围 <c>[0, sizes.Count]</c>；等于长度表示接在末尾。</returns>
    public static int ResolveInsertIndex(double offset, IReadOnlyList<double> sizes)
    {
        ArgumentNullException.ThrowIfNull(sizes);

        var accumulated = 0.0;
        for (var i = 0; i < sizes.Count; i++)
        {
            if (offset - accumulated < sizes[i] / 2)
            {
                return i;
            }

            accumulated += sizes[i];
        }

        return sizes.Count;
    }

    /// <summary>
    /// 上组（LEFT / RIGHT）的落区。
    /// </summary>
    /// <remarks>
    /// 照抄 <c>ToolWindowToolbar.kt:213-242 · StripeV2.containsPoint</c> 的 LEFT/RIGHT 分支：
    /// <list type="number">
    /// <item>取整根条带的矩形，**高度砍半**（<c>if (halfColumn) bounds.height /= 2</c>）——
    /// 落区是这一列的**上半**，与按钮实际占了多高无关；</item>
    /// <item>向窗口内侧扩出一个工具窗口的宽度（<c>bounds.width += toolWindowWidth</c>），
    /// 右条带则同时把 x 左移同样的量。</item>
    /// </list>
    /// <para>
    /// ⚠ 用「按钮的内容矩形」当落区是错的：那样点落在第一个图标**上方**或按钮堆下面的空白处
    /// 都不在任何一组里，只能靠兜底猜一个 —— 上组按钮少的时候，
    /// 半根条带的高度都会被判成"不属于任何组"。
    /// </para>
    /// </remarks>
    /// <param name="stripe">条带在宿主坐标系里的矩形。</param>
    /// <param name="isRight">是不是右条带。</param>
    /// <param name="toolWindowSize">第一个可见工具窗口的宽度（没有就用兜底 350）。</param>
    /// <returns>落区。</returns>
    public static Rect ResolveTopZone(Rect stripe, bool isRight, double toolWindowSize)
    {
        var width = stripe.Width + toolWindowSize;
        var x = isRight ? stripe.X - toolWindowSize : stripe.X;
        return new Rect(x, stripe.Y, width, stripe.Height / 2);
    }

    /// <summary>
    /// 下组（BOTTOM）的落区。
    /// </summary>
    /// <remarks>
    /// 照抄同一方法的 BOTTOM 分支（<c>:243-252</c>）：整窗底部一条带，
    /// 高 <c>max(第一个可见工具窗口的高, 条带高 + 40)</c>、宽**半个窗口**；
    /// <c>split</c> 时整体右移半屏。
    /// <para>
    /// 上游还会再减掉状态栏高度；Forge 画的是外壳、状态栏归宿主，
    /// 所以这里的"窗口底"就是外壳自己的底边。
    /// </para>
    /// </remarks>
    /// <param name="frame">外壳自己的尺寸。</param>
    /// <param name="stripeHeight">条带高（上游的 <c>height</c>）。</param>
    /// <param name="toolWindowSize">第一个可见工具窗口的高度。</param>
    /// <param name="isSplit">是不是右半那一组。</param>
    /// <returns>落区。</returns>
    public static Rect ResolveBottomZone(Size frame, double stripeHeight, double toolWindowSize, bool isSplit)
    {
        var height = Math.Max(toolWindowSize, stripeHeight + BottomZoneExtra);
        var width = frame.Width / 2;
        var x = isSplit ? width : 0;
        return new Rect(x, frame.Height - height, width, height);
    }

    /// <summary>下组落区在条带高之上再加的余量 40（<c>ToolWindowToolbar.kt:245</c>）。</summary>
    public const double BottomZoneExtra = 40;

    /// <summary>
    /// 把「落在哪根条带的哪一组」翻译成 anchor + isSplit。
    /// </summary>
    /// <remarks>
    /// 是 <see cref="StripePlacementResolver.Resolve"/> 的逆：
    /// 下组 → anchor=BOTTOM，落在右条带就是 <c>isSplit=true</c>
    /// （<c>ToolWindowPaneNewButtonManager.kt:228-233</c>）。
    /// 上组 → anchor 取该侧本身；LEFT/RIGHT 的 isSplit 由**落点在分隔符哪一侧**决定
    /// （<c>AbstractDroppableStripe.kt:463-469</c>），由 <paramref name="isAfterSeparator"/> 表达。
    /// </remarks>
    /// <param name="side">落到哪根条带。</param>
    /// <param name="group">落到哪一组。</param>
    /// <param name="isAfterSeparator">上组里，落点是否在分隔符之后。</param>
    /// <param name="order">插入位置。</param>
    /// <returns>落点。</returns>
    public static StripeDropTarget Resolve(
        StripeSide side,
        StripeGroup group,
        bool isAfterSeparator,
        int order)
    {
        if (group == StripeGroup.Bottom)
        {
            return new StripeDropTarget(ToolWindowAnchor.Bottom, order, side == StripeSide.Right);
        }

        var anchor = side == StripeSide.Left ? ToolWindowAnchor.Left : ToolWindowAnchor.Right;
        return new StripeDropTarget(anchor, order, isAfterSeparator);
    }

    /// <summary>
    /// 新 UI 的 BOTTOM 竖条带落点要把 order 加一。
    /// </summary>
    /// <remarks>
    /// 对应 <c>AbstractDroppableStripe.kt:250-253</c>：那一组是倒序排的（§1.2），
    /// 插入位置的含义反了，所以 finishDrop 里补一次 <c>order++</c>。
    /// </remarks>
    /// <param name="target">原始落点。</param>
    /// <returns>修正后的落点。</returns>
    public static StripeDropTarget AdjustForReversedBottomGroup(StripeDropTarget target)
        => target.Anchor == ToolWindowAnchor.Bottom
            ? target with { Order = target.Order + 1 }
            : target;
}
