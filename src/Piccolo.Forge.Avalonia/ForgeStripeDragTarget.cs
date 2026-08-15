using Piccolo.Forge.Model;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 一次拖拽算出来的落点。
/// </summary>
/// <param name="Anchor">落到哪条边。</param>
/// <param name="Order">插到什么次序；<c>-1</c> 表示末尾。</param>
/// <param name="IsSplit">落到同一条边的哪一半。</param>
public readonly record struct StripeDropTarget(ToolWindowAnchor Anchor, int Order, bool IsSplit);

/// <summary>
/// 由落点算插入位置。
/// </summary>
/// <remarks>
/// 对应 <c>AbstractDroppableStripe.kt:374-401</c> 的逐按钮判定与
/// <c>:242-258 · finishDrop</c> 的收尾。
/// </remarks>
public static class StripeDropResolver
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

        var accumulated = 0.0;
        foreach (var (order, size) in items)
        {
            if (offset - accumulated < size / 2)
            {
                return order;
            }

            accumulated += size;
        }

        return -1;
    }

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
