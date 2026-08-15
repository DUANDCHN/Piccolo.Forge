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

    /// <summary>
    /// 一个标签页被关掉了。
    /// </summary>
    /// <remarks>
    /// 对应上游 <c>EditorWindow.kt:759</c> 在关闭的 <c>finally</c> 里广播
    /// <c>FileEditorManagerListener.fileClosed</c>。
    /// <para>
    /// <b>为什么单独一个事件而不是让宿主从 <see cref="Changed"/> 里比对</b>：
    /// 宿主要在这里释放资源（相机会话、图像帧、订阅），比对法漏一次就是漏一次泄漏，
    /// 而且分不清「关掉了」和「挪到另一组去了」。
    /// </para>
    /// </remarks>
    public event EventHandler<EditorTabClosedEventArgs>? TabClosed;

    /// <summary>
    /// 整棵树换掉 —— 读档用。
    /// </summary>
    /// <param name="root">新的根。</param>
    /// <param name="current">新的当前组。</param>
    internal void ReplaceRoot(EditorNode root, EditorGroup current)
    {
        _root = root;
        CurrentGroup = current;
        OnChanged();
    }

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
            var tab = group.Tabs.FirstOrDefault(x => x.Id == id);
            if (group.Close(id))
            {
                // 空了的组要从树上摘掉（除非它是唯一一组）
                if (group.Tabs.Count == 0)
                {
                    RemoveGroup(group);
                }

                OnChanged();

                // 先把树整理好再广播：宿主在这个回调里会去读编辑区的现状
                // （「还剩几张图」「现在活动的是谁」），读到半成品是错的。
                TabClosed?.Invoke(this, new EditorTabClosedEventArgs(tab));
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

    /// <summary>
    /// 把一页拖到某一组上，按落点处理。
    /// </summary>
    /// <remarks>
    /// 落点判定见 <see cref="EditorDropPolicy"/>：
    /// **中心不分屏**，并进那一组；四边则按方向切一刀。
    /// </remarks>
    /// <param name="tabId">被拖的那一页。</param>
    /// <param name="target">落在哪一组上。</param>
    /// <param name="side">落在哪一侧。</param>
    /// <returns>落点生效则为 <see langword="true"/>。</returns>
    public bool DropTab(string tabId, EditorGroup target, EditorDropSide side)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (side == EditorDropSide.None)
        {
            return false;
        }

        var source = Groups.FirstOrDefault(g => g.Tabs.Any(t => t.Id == tabId));
        if (source is null)
        {
            return false;
        }

        var tab = source.Tabs.First(t => t.Id == tabId);

        if (side == EditorDropSide.Center)
        {
            if (ReferenceEquals(source, target))
            {
                return false;
            }

            source.Close(tabId);
            target.Add(tab);
            if (source.Tabs.Count == 0)
            {
                RemoveGroup(source);
            }

            CurrentGroup = target;
            OnChanged();
            return true;
        }

        var created = new EditorGroup();
        created.Add(tab);

        var split = EditorDropPolicy.NewGroupIsSecond(side)
            ? new EditorSplit(target, created, EditorDropPolicy.IsVertical(side))
            : new EditorSplit(created, target, EditorDropPolicy.IsVertical(side));

        Replace(target, split);
        source.Close(tabId);

        if (source.Tabs.Count == 0 && !ReferenceEquals(source, target))
        {
            RemoveGroup(source);
        }

        CurrentGroup = created;
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

    /// <summary>
    /// 把某一组撑满 —— 再来一次就恢复均分。
    /// </summary>
    /// <remarks>
    /// 对应上游 <c>MaximizeEditorInSplitAction.kt:32-48 · actionPerformed</c>，
    /// 也就是双击标签页（非预览页）触发的那个动作
    /// （<c>EditorTabbedContainer.kt:339-387 · doProcessDoubleClick</c>）。
    /// <para>
    /// 规则（<c>:94-115 · getSplittersToMaximize</c>）：从这一组往根上走，沿途每个分屏节点
    /// —— 这一组在<b>第一块</b>那侧就把比例推到上限 0.9、在<b>第二块</b>那侧就推到下限 0.1
    /// （<c>EditorWindow.kt:550</c> 的 <c>minProp=0.1f, maxProp=0.9f</c>）。
    /// 沿途**已经全都在极值**（没有可推的了）就说明当前已是最大化状态，
    /// 这时改走归一化：把编辑区里<b>所有</b>分屏节点设回 0.5
    /// （<c>:43-47</c> 的 <c>getSplittersToNormalize</c> 那一支）。
    /// </para>
    /// <para>
    /// ⚠ 归一化是<b>全局</b>的、最大化是<b>沿途</b>的 —— 这个不对称照抄上游：
    /// 撑开一路之后想收回来，用户要的是「全都回到均分」，而不是只把刚才那一路收回去。
    /// </para>
    /// <para>
    /// 上游那段 350ms 的动画（<c>:50-66 · setProportion</c>）不在模型层，
    /// 要不要动画是视图的事。
    /// </para>
    /// </remarks>
    /// <param name="group">要撑满的那一组。</param>
    /// <returns>撑满了返回 <see langword="true"/>；本来就是撑满的、这次归一化了返回 <see langword="false"/>。</returns>
    public bool MaximizeOrNormalize(EditorGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        // 沿途：这一组在哪一侧 → 该推到哪个极值
        var toMaximize = new List<(EditorSplit Split, bool IsFirst)>();
        EditorNode node = group;
        while (FindParent(_root, node) is { } parent)
        {
            var isFirst = ReferenceEquals(parent.First, node);
            var target = isFirst ? EditorSplit.MaxProportion : EditorSplit.MinProportion;

            // 已经在极值的不算「可推的」——沿途全都在极值才判定为"当前已最大化"
            if (Math.Abs(parent.Proportion - target) > float.Epsilon)
            {
                toMaximize.Add((parent, isFirst));
            }

            node = parent;
        }

        if (toMaximize.Count > 0)
        {
            foreach (var (split, isFirst) in toMaximize)
            {
                split.Proportion = isFirst ? EditorSplit.MaxProportion : EditorSplit.MinProportion;
            }

            OnChanged();
            return true;
        }

        foreach (var split in AllSplits(_root))
        {
            split.Proportion = EditorSplit.DefaultProportion;
        }

        OnChanged();
        return false;
    }

    /// <summary>这棵树上的所有分屏节点。</summary>
    private static IEnumerable<EditorSplit> AllSplits(EditorNode node)
    {
        if (node is not EditorSplit split)
        {
            yield break;
        }

        yield return split;

        foreach (var child in AllSplits(split.First).Concat(AllSplits(split.Second)))
        {
            yield return child;
        }
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


/// <summary>
/// <see cref="EditorArea.TabClosed"/> 的参数。
/// </summary>
/// <param name="tab">被关掉的那一页（关掉之前的样子）。</param>
public sealed class EditorTabClosedEventArgs(EditorTab tab) : EventArgs
{
    /// <summary>被关掉的那一页。</summary>
    public EditorTab Tab { get; } = tab;
}
