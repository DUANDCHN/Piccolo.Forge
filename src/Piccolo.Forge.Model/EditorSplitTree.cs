namespace Piccolo.Forge.Model;

/// <summary>
/// 分屏树的一个节点：要么是一次分屏，要么是一组标签页。
/// </summary>
/// <remarks>
/// 上游的分屏树是**二叉**的：每个 <c>Splitter</c> 只有 first / second
/// （<c>EditorsSplitters.kt:1270-1279</c>）。三栏是「分屏里再分屏」，
/// 不是一个三元节点。见 <c>docs/upstream/编辑区.md</c> §1。
/// </remarks>
public abstract class EditorNode
{
    /// <summary>这棵子树里的所有标签页组，从左到右 / 从上到下。</summary>
    public abstract IEnumerable<EditorGroup> Groups { get; }
}

/// <summary>
/// 一次分屏。
/// </summary>
/// <param name="isVertical">
/// 竖着切（上下两块）还是横着切（左右两块）。
/// 存档里写 <c>split-orientation="vertical"</c> 即为真（<c>EditorsSplitters.kt:1271</c>、<c>:1326</c>）。
/// </param>
/// <param name="first">第一块。</param>
/// <param name="second">第二块。</param>
/// <param name="proportion">第一块占的比例。</param>
public sealed class EditorSplit(EditorNode first, EditorNode second, bool isVertical, float proportion = EditorSplit.DefaultProportion)
    : EditorNode
{
    /// <summary>分屏比例的默认值 0.5（<c>EditorWindow.kt:549</c>，读档兜底见 <c>EditorsSplitters.kt:1329</c>）。</summary>
    public const float DefaultProportion = 0.5f;

    /// <summary>可拖动的下限 0.1（<c>EditorWindow.kt:549 · minProp</c>）。</summary>
    public const float MinProportion = 0.1f;

    /// <summary>可拖动的上限 0.9（<c>EditorWindow.kt:549 · maxProp</c>）。</summary>
    public const float MaxProportion = 0.9f;

    /// <summary>竖着切（上下）还是横着切（左右）。</summary>
    public bool IsVertical { get; } = isVertical;

    /// <summary>第一块。</summary>
    public EditorNode First { get; internal set; } = first;

    /// <summary>第二块。</summary>
    public EditorNode Second { get; internal set; } = second;

    /// <summary>第一块占的比例，夹在 [0.1, 0.9]。</summary>
    public float Proportion { get; set; } = Math.Clamp(proportion, MinProportion, MaxProportion);

    /// <inheritdoc />
    public override IEnumerable<EditorGroup> Groups => First.Groups.Concat(Second.Groups);
}

/// <summary>
/// 一组标签页 —— 分屏树的叶子。
/// </summary>
/// <remarks>对应上游的 <c>EditorWindow</c>：**只有叶子持有标签页**。</remarks>
public sealed class EditorGroup : EditorNode
{
    private readonly List<EditorTab> _tabs = [];
    private string? _selectedId;
    private string? _previousSelectedId;

    /// <summary>这一组里的标签页，按显示次序。</summary>
    public IReadOnlyList<EditorTab> Tabs => _tabs;

    /// <summary>当前选中的标签页 id。</summary>
    public string? SelectedId => _selectedId;

    /// <summary>
    /// 上一次选中的是谁。
    /// </summary>
    /// <remarks>
    /// 关掉当前标签页时**优先回到它**（<c>EditorWindow.kt:771-777</c>）。
    /// 这是手感的关键，别省。
    /// </remarks>
    public string? PreviousSelectedId => _previousSelectedId;

    /// <inheritdoc />
    public override IEnumerable<EditorGroup> Groups => [this];

    /// <summary>加一个标签页。</summary>
    /// <param name="tab">标签页。</param>
    /// <param name="select">要不要顺便选中它。</param>
    /// <param name="index">插到第几个；<c>-1</c> 表示末尾。</param>
    public void Add(EditorTab tab, bool select = true, int index = -1)
    {
        if (index < 0 || index > _tabs.Count)
        {
            _tabs.Add(tab);
        }
        else
        {
            _tabs.Insert(index, tab);
        }

        if (select)
        {
            Select(tab.Id);
        }
        else
        {
            _selectedId ??= tab.Id;
        }
    }

    /// <summary>选中一个标签页。</summary>
    /// <param name="id">标签页 id。</param>
    /// <returns>选中成功则为 <see langword="true"/>。</returns>
    public bool Select(string id)
    {
        if (!_tabs.Any(x => x.Id == id) || _selectedId == id)
        {
            return false;
        }

        _previousSelectedId = _selectedId;
        _selectedId = id;
        return true;
    }

    /// <summary>
    /// 关掉一个标签页，并按上游规则挑出新的选中项。
    /// </summary>
    /// <remarks>规则见 <see cref="EditorSelectionPolicy"/>。</remarks>
    /// <param name="id">要关的标签页 id。</param>
    /// <param name="history">最近使用历史，MRU 分支用。</param>
    /// <param name="activeMruEditorOnClose">是否沿历史找。</param>
    /// <param name="activeRightEditorOnClose">是否选右邻居。</param>
    /// <returns>确实关掉了则为 <see langword="true"/>。</returns>
    public bool Close(
        string id,
        IReadOnlyList<string>? history = null,
        bool activeMruEditorOnClose = EditorSelectionPolicy.ActiveMruEditorOnCloseDefault,
        bool activeRightEditorOnClose = EditorSelectionPolicy.ActiveRightEditorOnCloseDefault)
    {
        var closingIndex = _tabs.FindIndex(x => x.Id == id);
        if (closingIndex < 0)
        {
            return false;
        }

        var selectedIndex = _selectedId is null ? -1 : _tabs.FindIndex(x => x.Id == _selectedId);
        var next = EditorSelectionPolicy.ComputeTabToSelect(
            _tabs, closingIndex, selectedIndex, _previousSelectedId, history,
            activeMruEditorOnClose, activeRightEditorOnClose);

        _tabs.RemoveAt(closingIndex);

        if (_previousSelectedId == id)
        {
            _previousSelectedId = null;
        }

        if (_selectedId == id)
        {
            _selectedId = next;
            if (_previousSelectedId == next)
            {
                _previousSelectedId = null;
            }
        }

        if (_tabs.Count == 0)
        {
            _selectedId = null;
            _previousSelectedId = null;
        }

        return true;
    }

    /// <summary>
    /// 按上限淘汰标签页。
    /// </summary>
    /// <remarks>规则见 <see cref="EditorTabLimitPolicy"/>。</remarks>
    /// <param name="tabLimit">上限。</param>
    /// <param name="keepId">这一个不许关。</param>
    /// <param name="modifiedIds">哪些改过没存。</param>
    /// <param name="closeNonModifiedFilesFirst">是否先关未修改的。</param>
    /// <returns>被关掉的 id。</returns>
    public IReadOnlyList<string> TrimToSize(
        int tabLimit = EditorTabLimitPolicy.DefaultTabLimit,
        string? keepId = null,
        IReadOnlySet<string>? modifiedIds = null,
        bool closeNonModifiedFilesFirst = EditorTabLimitPolicy.CloseNonModifiedFilesFirstDefault)
    {
        var closing = EditorTabLimitPolicy.ComputeTabsToClose(
            _tabs, tabLimit, keepId, modifiedIds, closeNonModifiedFilesFirst);

        foreach (var id in closing)
        {
            Close(id);
        }

        return closing;
    }

    /// <summary>把一个标签页设成固定 / 取消固定。</summary>
    /// <param name="id">标签页 id。</param>
    /// <param name="pinned">是否固定。</param>
    /// <returns>找到并改了则为 <see langword="true"/>。</returns>
    public bool SetPinned(string id, bool pinned)
    {
        var index = _tabs.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            return false;
        }

        _tabs[index] = _tabs[index] with { IsPinned = pinned };
        return true;
    }
}
