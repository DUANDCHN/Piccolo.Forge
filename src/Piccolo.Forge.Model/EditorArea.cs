namespace Piccolo.Forge.Model;

/// <summary>
/// 整个编辑区：一棵二叉分屏树 + 「当前是哪一组」。
/// </summary>
/// <remarks>
/// 对应上游的 <c>EditorsSplitters</c>（<c>docs/upstream/编辑区.md</c> §1、§3）。
/// </remarks>
public sealed class EditorArea
{
    private EditorNode _root;

    /// <summary>建一个只有一组标签页的编辑区。</summary>
    public EditorArea()
    {
        var group = new EditorGroup();
        _root = group;
        CurrentGroup = group;
    }

    /// <summary>分屏树的根。</summary>
    public EditorNode Root => _root;

    /// <summary>
    /// 当前是哪一组 —— 新开的标签页落在它身上。
    /// </summary>
    /// <remarks>对应 <c>EditorsSplitters.currentWindow</c>。</remarks>
    public EditorGroup CurrentGroup { get; private set; }

    /// <summary>所有标签页组，从左到右 / 从上到下。</summary>
    public IReadOnlyList<EditorGroup> Groups => _root.Groups.ToList();

    /// <summary>编辑区变了（开合、选中、分屏）。</summary>
    public event EventHandler? Changed;

    /// <summary>把某一组设成当前组。</summary>
    /// <param name="group">要设成当前的组。</param>
    public void SetCurrentGroup(EditorGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (!Groups.Contains(group))
        {
            throw new ArgumentException("这一组不在编辑区里。", nameof(group));
        }

        CurrentGroup = group;
        OnChanged();
    }

    /// <summary>在当前组打开一个标签页。</summary>
    /// <param name="tab">标签页。</param>
    /// <param name="select">要不要选中。</param>
    public void Open(EditorTab tab, bool select = true)
    {
        CurrentGroup.Add(tab, select);
        OnChanged();
    }

    /// <summary>关掉当前组里的一个标签页。</summary>
    /// <param name="id">标签页 id。</param>
    /// <returns>确实关掉了则为 <see langword="true"/>。</returns>
    public bool Close(string id)
    {
        foreach (var group in Groups)
        {
            if (group.Close(id))
            {
                // 空了的组要从树上摘掉（除非它是唯一一组）
                if (group.Tabs.Count == 0)
                {
                    RemoveGroup(group);
                }

                OnChanged();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 把一个标签页设成固定 / 取消固定。
    /// </summary>
    /// <remarks>
    /// 在编辑区这一层给一个入口，是因为 <see cref="EditorGroup.SetPinned"/> 改完
    /// **没人发通知**，绑上去的 UI 不会更新 —— 状态变更必须经过持有事件的那一层。
    /// </remarks>
    /// <param name="id">标签页 id。</param>
    /// <param name="pinned">是否固定。</param>
    /// <returns>找到并改了则为 <see langword="true"/>。</returns>
    public bool SetPinned(string id, bool pinned)
    {
        foreach (var group in Groups)
        {
            if (group.SetPinned(id, pinned))
            {
                OnChanged();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 把一组分成两组。
    /// </summary>
    /// <remarks>
    /// 照抄 <c>EditorWindow.kt:518-585 · split</c> 的两条硬规则：
    /// <list type="bullet">
    /// <item>**空组不允许分屏**（<c>:528-530</c>，上游返回 null）；</item>
    /// <item>**只把选中的那一个带过去**，不是复制整组（<c>:570-571</c> 的注释原话）。</item>
    /// </list>
    /// 新分出来的比例固定 0.5（<c>:549</c>）。
    /// </remarks>
    /// <param name="group">要分的那一组。</param>
    /// <param name="isVertical">竖着切（上下）还是横着切（左右）。</param>
    /// <param name="tabId">带过去哪一个；不给就带选中的那个。</param>
    /// <param name="newGroupIsSecond">新组放在第二块（上游默认 true）。</param>
    /// <returns>新建的那一组；分不了返回 <see langword="null"/>。</returns>
    public EditorGroup? Split(
        EditorGroup group,
        bool isVertical,
        string? tabId = null,
        bool newGroupIsSecond = true)
    {
        ArgumentNullException.ThrowIfNull(group);

        // 空组分不了屏
        if (group.Tabs.Count < 1)
        {
            return null;
        }

        var movingId = tabId ?? group.SelectedId;
        if (movingId is null)
        {
            return null;
        }

        var moving = group.Tabs.FirstOrDefault(x => x.Id == movingId);
        if (moving.Id is null)
        {
            return null;
        }

        var created = new EditorGroup();
        // 只把选中那一个带过去；pin 状态跟着走（EditorWindow.kt:580）
        created.Add(moving with { IsPreview = false });

        var split = newGroupIsSecond
            ? new EditorSplit(group, created, isVertical)
            : new EditorSplit(created, group, isVertical);

        Replace(group, split);
        CurrentGroup = created;
        OnChanged();
        return created;
    }

    /// <summary>
    /// 把一组与它的兄弟合回去。
    /// </summary>
    /// <remarks>
    /// 照抄 <c>EditorWindow.kt:939-980 · unsplit</c>：把兄弟的标签页搬进来，
    /// **搬的时候仍然守上限**，超了就丢掉不搬（<c>:955-967</c>）；
    /// 选中项优先用本组原来的，否则用兄弟组的。
    /// </remarks>
    /// <param name="group">保留下来的那一组。</param>
    /// <param name="tabLimit">上限。</param>
    /// <returns>确实合并了则为 <see langword="true"/>。</returns>
    public bool Unsplit(EditorGroup group, int tabLimit = EditorTabLimitPolicy.DefaultTabLimit)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (FindParent(_root, group) is not { } parent)
        {
            return false;
        }

        var sibling = ReferenceEquals(parent.First, group) ? parent.Second : parent.First;
        var selected = group.SelectedId;

        foreach (var siblingGroup in sibling.Groups)
        {
            selected ??= siblingGroup.SelectedId;

            foreach (var tab in siblingGroup.Tabs)
            {
                var alreadyThere = group.Tabs.Any(x => x.Id == tab.Id);
                if (!alreadyThere && group.Tabs.Count < tabLimit)
                {
                    group.Add(tab, select: false);
                }
            }
        }

        Replace(parent, group);

        if (selected is not null)
        {
            group.Select(selected);
        }

        CurrentGroup = group;
        OnChanged();
        return true;
    }

    /// <summary>一直合到只剩一组。</summary>
    /// <remarks>对应 <c>EditorWindow.kt:983-988 · unsplitAll</c>。</remarks>
    /// <param name="group">保留下来的那一组。</param>
    public void UnsplitAll(EditorGroup group)
    {
        while (Unsplit(group))
        {
            // 直到它不在任何分屏里
        }
    }

    /// <summary>
    /// 把一组从树上摘掉，让它的兄弟顶替整个分屏节点。
    /// </summary>
    /// <remarks>
    /// 对应 <c>EditorWindow.kt:791-820 · removeFromSplitter</c>——
    /// 二叉树删叶子的标准做法。唯一的一组不会被摘。
    /// </remarks>
    /// <param name="group">要摘掉的组。</param>
    /// <returns>确实摘掉了则为 <see langword="true"/>。</returns>
    public bool RemoveGroup(EditorGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (FindParent(_root, group) is not { } parent)
        {
            return false;
        }

        var sibling = ReferenceEquals(parent.First, group) ? parent.Second : parent.First;
        Replace(parent, sibling);

        if (ReferenceEquals(CurrentGroup, group))
        {
            CurrentGroup = sibling.Groups.First();
        }

        OnChanged();
        return true;
    }

    private void Replace(EditorNode oldNode, EditorNode newNode)
    {
        if (ReferenceEquals(_root, oldNode))
        {
            _root = newNode;
            return;
        }

        var parent = FindParent(_root, oldNode)
                     ?? throw new InvalidOperationException("节点不在这棵树上。");

        if (ReferenceEquals(parent.First, oldNode))
        {
            parent.First = newNode;
        }
        else
        {
            parent.Second = newNode;
        }
    }

    private static EditorSplit? FindParent(EditorNode node, EditorNode target)
    {
        if (node is not EditorSplit split)
        {
            return null;
        }

        if (ReferenceEquals(split.First, target) || ReferenceEquals(split.Second, target))
        {
            return split;
        }

        return FindParent(split.First, target) ?? FindParent(split.Second, target);
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
