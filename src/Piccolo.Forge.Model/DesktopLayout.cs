namespace Piccolo.Forge.Model;

/// <summary>
/// 一整套工具窗口布局。
/// </summary>
/// <remarks>
/// 对应上游 <c>platform/platform-impl/src/com/intellij/openapi/wm/impl/DesktopLayout.kt</c>。
/// </remarks>
public sealed class DesktopLayout
{
    private readonly Dictionary<string, WindowInfo> _idToInfo = new(StringComparer.Ordinal);
    private readonly Dictionary<ToolWindowAnchor, float> _unifiedWeights = new()
    {
        [ToolWindowAnchor.Top] = WindowInfo.DefaultWeight,
        [ToolWindowAnchor.Left] = WindowInfo.DefaultWeight,
        [ToolWindowAnchor.Bottom] = WindowInfo.DefaultWeight,
        [ToolWindowAnchor.Right] = WindowInfo.DefaultWeight,
    };

    /// <summary>建一个空布局。</summary>
    public DesktopLayout()
    {
    }

    /// <summary>用一组窗口状态建布局。</summary>
    /// <param name="infos">窗口状态，按 <see cref="WindowInfo.Id"/> 去重。</param>
    public DesktopLayout(IEnumerable<WindowInfo> infos)
    {
        ArgumentNullException.ThrowIfNull(infos);
        foreach (var info in infos)
        {
            _idToInfo[info.Id] = info;
        }
    }

    /// <summary>取一个窗口的状态，没有则 <see langword="null"/>。</summary>
    /// <param name="id">工具窗口 id。</param>
    /// <returns>窗口状态。</returns>
    public WindowInfo? GetInfo(string id) => _idToInfo.GetValueOrDefault(id);

    /// <summary>写入一个窗口的状态。</summary>
    /// <param name="info">窗口状态。</param>
    public void SetInfo(WindowInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        _idToInfo[info.Id] = info;
    }

    /// <summary>按上游次序排好的全部窗口状态。</summary>
    /// <remarks>次序见 <see cref="Comparer"/>（<c>DesktopLayout.kt:169 · getSortedList</c>）。</remarks>
    /// <returns>排好序的列表。</returns>
    public IReadOnlyList<WindowInfo> GetSortedList()
        => _idToInfo.Values.OrderBy(x => x, Comparer).ToList();

    /// <summary>
    /// 取某条带上「下一个可用的 order」。
    /// </summary>
    /// <remarks>
    /// 对应 <c>DesktopLayout.kt:33-35 · getNextOrder</c>：该组里最大 order + 1，空组返回 0。
    /// </remarks>
    /// <param name="paneId">所属 pane。</param>
    /// <param name="anchor">停靠边。</param>
    /// <returns>下一个 order。</returns>
    public int GetNextOrder(string paneId, ToolWindowAnchor anchor)
    {
        var max = int.MinValue;
        foreach (var info in _idToInfo.Values)
        {
            if (string.Equals(info.PaneId, paneId, StringComparison.Ordinal) && info.Anchor == anchor)
            {
                max = Math.Max(max, info.Order);
            }
        }

        return max == int.MinValue ? 0 : max + 1;
    }

    /// <summary>取某条边的统一权重。</summary>
    /// <remarks>
    /// 对应 <c>DesktopLayout.kt:65 · getUnifiedAnchorWeight</c>；
    /// 新 UI 默认启用统一权重，激活时会用它覆盖窗口自身的 weight
    /// （<c>ToolWindowManagerImpl.kt:606-609</c>）。
    /// </remarks>
    /// <param name="anchor">停靠边。</param>
    /// <returns>该边的统一权重。</returns>
    public float GetUnifiedAnchorWeight(ToolWindowAnchor anchor) => _unifiedWeights[anchor];

    /// <summary>设某条边的统一权重。</summary>
    /// <param name="anchor">停靠边。</param>
    /// <param name="weight">权重。</param>
    public void SetUnifiedAnchorWeight(ToolWindowAnchor anchor, float weight)
        => _unifiedWeights[anchor] = Math.Clamp(weight, 0f, 1f);

    /// <summary>
    /// 把一个窗口挪到新的 pane / 边 / 次序，并顺带修正同条带上其它窗口的次序。
    /// </summary>
    /// <remarks>
    /// 对应 <c>DesktopLayout.kt:75-101 · setAnchor</c>：
    /// <paramref name="newOrder"/> 为 <c>-1</c> 时插到条带末尾；
    /// 否则把目标条带里 <c>order &gt;= newOrder</c> 的其它窗口整体 +1。
    /// </remarks>
    /// <param name="id">要挪动的窗口 id。</param>
    /// <param name="newPaneId">目标 pane。</param>
    /// <param name="newAnchor">目标边。</param>
    /// <param name="newOrder">目标次序，<c>-1</c> 表示末尾。</param>
    /// <returns>次序被顺带改动的其它窗口。</returns>
    public IReadOnlyList<WindowInfo> SetAnchor(
        string id,
        string newPaneId,
        ToolWindowAnchor newAnchor,
        int newOrder)
    {
        var info = _idToInfo[id];
        var affected = new List<WindowInfo>();
        var order = newOrder;

        if (order == -1)
        {
            order = GetNextOrder(newPaneId, newAnchor);
        }
        else
        {
            foreach (var other in _idToInfo.Values.ToList())
            {
                if (!string.Equals(other.Id, id, StringComparison.Ordinal)
                    && string.Equals(other.PaneId, newPaneId, StringComparison.Ordinal)
                    && other.Anchor == newAnchor
                    && other.Order != -1
                    && other.Order >= order)
                {
                    var shifted = other with { Order = other.Order + 1 };
                    _idToInfo[shifted.Id] = shifted;
                    affected.Add(shifted);
                }
            }
        }

        _idToInfo[id] = info with { Order = order, PaneId = newPaneId, Anchor = newAnchor };
        return affected;
    }

    /// <summary>
    /// 按上游规则重排 order：先排序，再按边分组从 0 递增；<c>order == -1</c> 的跳过。
    /// </summary>
    /// <remarks>对应 <c>DesktopLayout.kt:198-214 · normalizeOrder</c>。</remarks>
    public void NormalizeOrder()
    {
        var order = 0;
        var lastAnchor = ToolWindowAnchor.Top;
        foreach (var info in GetSortedList())
        {
            if (info.Order == -1)
            {
                continue;
            }

            if (lastAnchor != info.Anchor)
            {
                lastAnchor = info.Anchor;
                order = 0;
            }

            _idToInfo[info.Id] = info with { Order = order++ };
        }
    }

    /// <summary>
    /// 上游的窗口排序：先比边的权重，再比 order。
    /// </summary>
    /// <remarks>对应 <c>DesktopLayout.kt:190-193 · windowInfoComparator</c>。</remarks>
    public static IComparer<WindowInfo> Comparer { get; } = new WindowInfoComparer();

    private sealed class WindowInfoComparer : IComparer<WindowInfo>
    {
        public int Compare(WindowInfo? x, WindowInfo? y)
        {
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(y);

            var weightDiff = x.Anchor.SortWeight() - y.Anchor.SortWeight();
            return weightDiff != 0 ? weightDiff : x.Order - y.Order;
        }
    }
}
