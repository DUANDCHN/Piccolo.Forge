namespace Piccolo.Forge.Model;

/// <summary>
/// 标签页超过上限时淘汰谁。
/// </summary>
/// <remarks>
/// 对应 <c>EditorWindow.kt:1049-1120 · trimToSize / doTrimSize / getTabClosingOrder</c>，
/// 见 <c>docs/upstream/编辑区.md</c> §2.2。同样单独做成纯函数。
/// </remarks>
public static class EditorTabLimitPolicy
{
    /// <summary>标签页上限，默认 <b>30</b>（<c>UISettingsState.kt:68 · editorTabLimit</c>）。</summary>
    public const int DefaultTabLimit = 30;

    /// <summary>先关未修改的，默认 <b>false</b>（<c>UISettingsState.kt:157</c>）。</summary>
    public const bool CloseNonModifiedFilesFirstDefault = false;

    /// <summary>
    /// 算出这一轮该关掉哪些标签页。
    /// </summary>
    /// <remarks>
    /// 顺序照抄上游：
    /// <list type="number">
    /// <item>**所有预览标签页一律先关**（<c>:1071-1073</c>），不管超没超限；</item>
    /// <item>还超限的话，按淘汰顺序逐个关到不超为止（<c>:1082-1090</c>）；</item>
    /// <item>淘汰顺序：**先关不在历史里的**（<c>:1099-1104</c>），
    /// 然后（<paramref name="closeNonModifiedFilesFirst"/> 打开时）按历史顺序关未修改的（<c>:1105-1114</c>）。</item>
    /// </list>
    /// 两条硬规则：
    /// <list type="bullet">
    /// <item>**固定住的永远不被挤掉**（<c>:1173-1178 · fileCanBeClosed</c>）；</item>
    /// <item>**不可关闭的不计入分母**（<c>:1076</c> 减去 <c>:1180-1182</c>）。</item>
    /// </list>
    /// </remarks>
    /// <param name="tabs">当前标签页，按显示次序。</param>
    /// <param name="tabLimit">上限。</param>
    /// <param name="keepId">这一个不许关（通常是刚打开的那个）。</param>
    /// <param name="modifiedIds">哪些是「改过还没存」的。</param>
    /// <param name="closeNonModifiedFilesFirst">是否先关未修改的。</param>
    /// <returns>该关掉的标签页 id，按关闭顺序。</returns>
    public static IReadOnlyList<string> ComputeTabsToClose(
        IReadOnlyList<EditorTab> tabs,
        int tabLimit = DefaultTabLimit,
        string? keepId = null,
        IReadOnlySet<string>? modifiedIds = null,
        bool closeNonModifiedFilesFirst = CloseNonModifiedFilesFirstDefault)
    {
        ArgumentNullException.ThrowIfNull(tabs);

        var closing = new List<string>();
        var remaining = tabs.ToList();

        // ① 预览标签页一律先关
        foreach (var tab in tabs.Where(x => x.IsPreview && !IsKept(x, keepId)))
        {
            closing.Add(tab.Id);
            remaining.RemoveAll(x => x.Id == tab.Id);
        }

        if (IsUnderLimit(remaining, tabLimit, keepId))
        {
            return closing;
        }

        // ② 淘汰顺序：先不在历史里的，再（可选）未修改的
        foreach (var id in BuildClosingOrder(remaining, modifiedIds, closeNonModifiedFilesFirst))
        {
            if (IsUnderLimit(remaining, tabLimit, keepId))
            {
                break;
            }

            var tab = remaining.First(x => x.Id == id);
            if (!CanBeClosed(tab, keepId))
            {
                continue;
            }

            closing.Add(id);
            remaining.RemoveAll(x => x.Id == id);
        }

        return closing;
    }

    /// <summary>
    /// 这一组算不算已经在上限之内。
    /// </summary>
    /// <remarks>
    /// 对应 <c>:1076 · isUnderLimit</c>：分母**减掉不可关闭的**；
    /// 一个都关不动时也算「在限内」，否则会死循环。
    /// </remarks>
    /// <param name="tabs">标签页。</param>
    /// <param name="tabLimit">上限。</param>
    /// <param name="keepId">不许关的那个。</param>
    /// <returns>在限内则为 <see langword="true"/>。</returns>
    public static bool IsUnderLimit(IReadOnlyList<EditorTab> tabs, int tabLimit, string? keepId = null)
    {
        ArgumentNullException.ThrowIfNull(tabs);

        var excluded = tabs.Count(x => !x.IsCloseable);
        return tabs.Count - excluded <= tabLimit
               || tabs.Count == 0
               || !tabs.Any(x => CanBeClosed(x, keepId));
    }

    /// <summary>这个标签页能不能被淘汰。</summary>
    /// <remarks>对应 <c>:1173-1178 · fileCanBeClosed</c>：固定住的、指定保留的、不可关的都不行。</remarks>
    /// <param name="tab">标签页。</param>
    /// <param name="keepId">不许关的那个。</param>
    /// <returns>可以关则为 <see langword="true"/>。</returns>
    public static bool CanBeClosed(EditorTab tab, string? keepId = null)
        => tab.IsCloseable && !tab.IsPinned && !IsKept(tab, keepId);

    private static bool IsKept(EditorTab tab, string? keepId)
        => keepId is not null && string.Equals(tab.Id, keepId, StringComparison.Ordinal);

    private static IEnumerable<string> BuildClosingOrder(
        IReadOnlyList<EditorTab> tabs,
        IReadOnlySet<string>? modifiedIds,
        bool closeNonModifiedFilesFirst)
    {
        var order = new List<string>();

        // 先关不在历史里的
        foreach (var tab in tabs.Where(x => !x.IsInHistory))
        {
            order.Add(tab.Id);
        }

        if (closeNonModifiedFilesFirst)
        {
            foreach (var tab in tabs.Where(x => modifiedIds?.Contains(x.Id) != true))
            {
                if (!order.Contains(tab.Id))
                {
                    order.Add(tab.Id);
                }
            }
        }

        // 兜底：剩下的按显示次序
        foreach (var tab in tabs)
        {
            if (!order.Contains(tab.Id))
            {
                order.Add(tab.Id);
            }
        }

        return order;
    }
}
