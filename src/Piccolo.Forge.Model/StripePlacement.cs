namespace Piccolo.Forge.Model;

/// <summary>
/// 新 UI 里两根竖条带之一。
/// </summary>
/// <remarks>
/// 新 UI 只有左右两根条带，没有 TOP，BOTTOM 也不占高度
/// （<c>ToolWindowPaneNewButtonManager.kt:160-167 · getStripeHeight</c>）。
/// </remarks>
public enum StripeSide
{
    /// <summary>左条带。</summary>
    Left,

    /// <summary>右条带。</summary>
    Right,
}

/// <summary>
/// 条带内的分组：一根条带分上下两组。
/// </summary>
/// <remarks>对应 <c>ToolWindowToolbar.kt:44-45 · topStripe / bottomStripe</c>。</remarks>
public enum StripeGroup
{
    /// <summary>上组。</summary>
    Top,

    /// <summary>下组。</summary>
    Bottom,
}

/// <summary>
/// 一个条带按钮画在哪：哪根条带、哪一组。
/// </summary>
/// <param name="Side">条带。</param>
/// <param name="Group">组。</param>
public readonly record struct StripePlacement(StripeSide Side, StripeGroup Group);

/// <summary>
/// 由 <c>(anchor, isSplit)</c> 推出按钮落位。
/// </summary>
/// <remarks>
/// 「面板停在哪」和「按钮画在哪」在上游是两件事，落位是**派生量**，不进持久化。
/// 规则出处 <c>ToolWindowPaneNewButtonManager.kt:228-233 · findToolbar</c> 与
/// <c>ToolWindowLeftToolbar.kt:11-12</c>、<c>ToolWindowRightToolbar.kt:11-12</c>。
/// </remarks>
public static class StripePlacementResolver
{
    /// <summary>
    /// 算出按钮落在哪根条带的哪一组。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>LEFT → 左条带上组；RIGHT → 右条带上组。</item>
    /// <item>BOTTOM 且 <paramref name="isSplit"/> 为 false → **左**条带下组。</item>
    /// <item>BOTTOM 且 <paramref name="isSplit"/> 为 true → **右**条带下组。</item>
    /// <item>LEFT / RIGHT 的 <paramref name="isSplit"/> 不换条带，只影响组内次序
    /// （<c>AbstractDroppableStripe.kt:57-72</c>，分隔符见 <c>:99-105</c>）。</item>
    /// </list>
    /// TOP 在新 UI 无条带可落：上游默认布局构造不出 TOP
    /// （<c>defaultToolWindowlayoutProvider.kt:234</c> 直接抛异常），这里同样拒绝。
    /// </remarks>
    /// <param name="anchor">停靠边。</param>
    /// <param name="isSplit">是否是同一条边的另一半。</param>
    /// <returns>按钮落位。</returns>
    /// <exception cref="ArgumentOutOfRangeException">anchor 是 TOP 时抛出。</exception>
    public static StripePlacement Resolve(ToolWindowAnchor anchor, bool isSplit) => anchor switch
    {
        ToolWindowAnchor.Left => new StripePlacement(StripeSide.Left, StripeGroup.Top),
        ToolWindowAnchor.Right => new StripePlacement(StripeSide.Right, StripeGroup.Top),
        ToolWindowAnchor.Bottom => new StripePlacement(
            isSplit ? StripeSide.Right : StripeSide.Left,
            StripeGroup.Bottom),
        _ => throw new ArgumentOutOfRangeException(
            nameof(anchor),
            anchor,
            "新 UI 没有 TOP 条带（defaultToolWindowlayoutProvider.kt:234）"),
    };

    /// <summary>
    /// 一组按钮在条带内的排列次序。
    /// </summary>
    /// <remarks>
    /// 对应 <c>AbstractDroppableStripe.kt:57-77 · createButtonLayoutComparator</c>：
    /// <c>isSplit=false</c> 一律在前；新 UI 的 BOTTOM 竖条带 order **倒序**；
    /// <c>order == -1</c> 视作 <see cref="int.MaxValue"/>。
    /// </remarks>
    /// <param name="anchor">该组的停靠边。</param>
    /// <returns>比较器。</returns>
    public static IComparer<WindowInfo> CreateButtonLayoutComparer(ToolWindowAnchor anchor)
        => new ButtonLayoutComparer(anchor);

    private sealed class ButtonLayoutComparer(ToolWindowAnchor anchor) : IComparer<WindowInfo>
    {
        public int Compare(WindowInfo? x, WindowInfo? y)
        {
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(y);

            if (x.IsSplit != y.IsSplit)
            {
                return x.IsSplit ? 1 : -1;
            }

            // 竖条带里的 BOTTOM 组是倒序的：用户是从下往上看这一组的
            return anchor == ToolWindowAnchor.Bottom
                ? OrderOf(y) - OrderOf(x)
                : OrderOf(x) - OrderOf(y);
        }

        private static int OrderOf(WindowInfo info) => info.Order == -1 ? int.MaxValue : info.Order;
    }
}
