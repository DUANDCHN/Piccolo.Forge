namespace Piccolo.Forge.Model;

/// <summary>
/// 编辑区里的一个标签页。
/// </summary>
/// <remarks>
/// 对应上游的一个 <c>EditorComposite</c>（<c>工具窗口模型</c>之外，见
/// <c>docs/upstream/编辑区.md</c>）。
/// <para>
/// 上游一个文件可以有多个编辑器并排切换（文本 / 设计视图），那一层 Forge 不做：
/// **一个标签页 = 一块内容**。
/// </para>
/// </remarks>
/// <param name="Id">标签页 id，存档按它认账。</param>
/// <param name="Title">标签上显示的名字。</param>
/// <param name="IsPinned">固定住了 —— 固定的**永远不会被上限挤掉**。</param>
/// <param name="IsPreview">预览标签页 —— 超限时**第一批被关**。</param>
/// <param name="IsCloseable">能不能关；不能关的**不计入上限的分母**。</param>
/// <param name="IsInHistory">在不在最近使用历史里；不在历史里的**优先被淘汰**。</param>
public readonly record struct EditorTab(
    string Id,
    string Title,
    bool IsPinned = false,
    bool IsPreview = false,
    bool IsCloseable = true,
    bool IsInHistory = true) : IDockable;

/// <summary>
/// 关掉一个标签页之后该选中谁。
/// </summary>
/// <remarks>
/// 对应 <c>EditorWindow.kt:766-782 · getTabToSelect</c> 与
/// <c>:825-855 · computeIndexToSelect</c>。
/// 单独做成纯函数是因为它最容易做错、又最影响手感。
/// </remarks>
public static class EditorSelectionPolicy
{
    /// <summary>
    /// 沿历史往回找（对应 <c>activeMruEditorOnClose</c>）。默认**关**。
    /// </summary>
    /// <remarks><c>UISettingsState.kt:159</c>。</remarks>
    public const bool ActiveMruEditorOnCloseDefault = false;

    /// <summary>
    /// 选右邻居（对应 <c>activeRightEditorOnClose</c>）。默认**关**（只有 AppCode 开）。
    /// </summary>
    /// <remarks><c>UISettingsState.kt:162</c>。</remarks>
    public const bool ActiveRightEditorOnCloseDefault = false;

    /// <summary>
    /// 算出关掉之后选中哪个下标。
    /// </summary>
    /// <remarks>
    /// 顺序照抄上游：
    /// <list type="number">
    /// <item>被关的**不是**当前选中的 → 选中不动（<c>:826-830</c>）；</item>
    /// <item><paramref name="activeMruEditorOnClose"/> → 沿历史往回找第一个还开着的（<c>:833-848</c>）；</item>
    /// <item><paramref name="activeRightEditorOnClose"/> 且右边还有 → 右邻居（<c>:849-851</c>）；</item>
    /// <item>否则 → 左邻居；已经是第一个就返回 <c>-1</c>（<c>:853-854</c>）。</item>
    /// </list>
    /// **两个开关默认都是 false，所以默认路径是「左邻居」。**
    /// </remarks>
    /// <param name="tabs">关之前的标签页，按显示次序。</param>
    /// <param name="closingIndex">被关的那个的下标。</param>
    /// <param name="selectedIndex">关之前选中的下标。</param>
    /// <param name="history">最近使用历史，越靠后越新；用于 MRU 分支。</param>
    /// <param name="activeMruEditorOnClose">是否沿历史找。</param>
    /// <param name="activeRightEditorOnClose">是否选右邻居。</param>
    /// <returns>关之前的坐标系里该选中的下标；没有可选的返回 <c>-1</c>。</returns>
    public static int ComputeIndexToSelect(
        IReadOnlyList<EditorTab> tabs,
        int closingIndex,
        int selectedIndex,
        IReadOnlyList<string>? history = null,
        bool activeMruEditorOnClose = ActiveMruEditorOnCloseDefault,
        bool activeRightEditorOnClose = ActiveRightEditorOnCloseDefault)
    {
        ArgumentNullException.ThrowIfNull(tabs);

        // 关的不是选中的那个，选中不动
        if (selectedIndex != closingIndex)
        {
            return selectedIndex;
        }

        if (activeMruEditorOnClose && history is not null)
        {
            for (var i = history.Count - 1; i >= 0; i--)
            {
                var id = history[i];
                if (string.Equals(id, tabs[closingIndex].Id, StringComparison.Ordinal))
                {
                    continue;
                }

                var index = IndexOf(tabs, id);
                if (index >= 0)
                {
                    return index;
                }
            }
        }
        else if (activeRightEditorOnClose && closingIndex + 1 < tabs.Count)
        {
            return closingIndex + 1;
        }

        // 默认：左邻居
        return closingIndex > 0 ? closingIndex - 1 : -1;
    }

    /// <summary>
    /// 关掉之后该选中哪个标签页。
    /// </summary>
    /// <remarks>
    /// 上游先看被关那个记着的「上一次选中的是谁」，只要它还在就选它
    /// （<c>EditorWindow.kt:771-777</c>）；否则才去算下标。
    /// </remarks>
    /// <param name="tabs">关之前的标签页。</param>
    /// <param name="closingIndex">被关的下标。</param>
    /// <param name="selectedIndex">关之前选中的下标。</param>
    /// <param name="previousSelectionId">被关那个记着的「上一次选中」。</param>
    /// <param name="history">最近使用历史。</param>
    /// <param name="activeMruEditorOnClose">是否沿历史找。</param>
    /// <param name="activeRightEditorOnClose">是否选右邻居。</param>
    /// <returns>该选中的标签页 id；没有则 <see langword="null"/>。</returns>
    public static string? ComputeTabToSelect(
        IReadOnlyList<EditorTab> tabs,
        int closingIndex,
        int selectedIndex,
        string? previousSelectionId = null,
        IReadOnlyList<string>? history = null,
        bool activeMruEditorOnClose = ActiveMruEditorOnCloseDefault,
        bool activeRightEditorOnClose = ActiveRightEditorOnCloseDefault)
    {
        ArgumentNullException.ThrowIfNull(tabs);

        if (previousSelectionId is not null
            && !string.Equals(previousSelectionId, tabs[closingIndex].Id, StringComparison.Ordinal)
            && IndexOf(tabs, previousSelectionId) >= 0)
        {
            return previousSelectionId;
        }

        var index = ComputeIndexToSelect(
            tabs, closingIndex, selectedIndex, history, activeMruEditorOnClose, activeRightEditorOnClose);

        return index >= 0 && index < tabs.Count ? tabs[index].Id : null;
    }

    private static int IndexOf(IReadOnlyList<EditorTab> tabs, string id)
    {
        for (var i = 0; i < tabs.Count; i++)
        {
            if (string.Equals(tabs[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
