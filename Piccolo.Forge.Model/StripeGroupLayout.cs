namespace Piccolo.Forge.Model;

/// <summary>
/// 条带一组里的一项：要么是一个工具窗口按钮，要么是那条分组分隔符。
/// </summary>
/// <remarks>
/// 上游把分隔符当成**伪按钮**排进同一个列表
/// （<c>AbstractDroppableStripe.kt:119-138 · separatorStripe</c>、<c>:590-596</c>），
/// 所以它占的位置就是它的 preferredSize（32×11），不是「组之间加了 padding」。
/// </remarks>
/// <param name="Window">这一项对应的工具窗口；分隔符时为 <see langword="null"/>。</param>
public readonly record struct StripeItem(WindowInfo? Window)
{
    /// <summary>这一项是不是分隔符。</summary>
    public bool IsSeparator => Window is null;

    /// <summary>那条分组分隔符。</summary>
    public static StripeItem Separator => new((WindowInfo?)null);
}

/// <summary>
/// 排一组条带按钮：定序 + 决定分隔符插在哪。
/// </summary>
/// <remarks>
/// 对应 <c>platform/platform-impl/src/com/intellij/openapi/wm/impl/AbstractDroppableStripe.kt:571-624
/// · getButtonsToLayOut</c>。像素排布（统一宽度、间隙 0）在视图层，见 <c>度量与主题键.md</c> §1.4。
/// </remarks>
public static class StripeGroupLayout
{
    /// <summary>
    /// 把一组窗口排成条带里的显示次序。
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item>按 <see cref="StripePlacementResolver.CreateButtonLayoutComparer"/> 定序（`:589`、`:619`）。</item>
    /// <item>只有新 UI 的 LEFT/RIGHT 竖条带用分隔符
    /// （<c>:99-105 · useStripeButtonSeparator</c>），由 <paramref name="useSeparator"/> 表达。</item>
    /// <item>分隔符插在**第一个 <c>isSplit=true</c>** 的前面（`:590-596`）。</item>
    /// <item>如果它落在**索引 0**（整组都是 split），非拖拽时把它去掉（`:602-611`）。</item>
    /// </list>
    /// 拖拽中还会在末尾补一个带 dnd 状态的分隔符（`:613-617`），那是交互态，这里不建模。
    /// </remarks>
    /// <param name="windows">该组的窗口。</param>
    /// <param name="anchor">该组的停靠边，决定次序（BOTTOM 组是倒序）。</param>
    /// <param name="useSeparator">是否用分组分隔符。</param>
    /// <returns>排好的显示次序。</returns>
    public static IReadOnlyList<StripeItem> Build(
        IEnumerable<WindowInfo> windows,
        ToolWindowAnchor anchor,
        bool useSeparator)
    {
        ArgumentNullException.ThrowIfNull(windows);

        var sorted = windows
            .Order(StripePlacementResolver.CreateButtonLayoutComparer(anchor))
            .Select(x => new StripeItem(x))
            .ToList();

        if (!useSeparator || sorted.Count == 0)
        {
            return sorted;
        }

        var firstSplit = sorted.FindIndex(x => x.Window!.IsSplit);
        if (firstSplit < 0)
        {
            return sorted;
        }

        // 落在索引 0 说明整组都是 split，非拖拽时不画分隔符
        if (firstSplit == 0)
        {
            return sorted;
        }

        sorted.Insert(firstSplit, StripeItem.Separator);
        return sorted;
    }
}
