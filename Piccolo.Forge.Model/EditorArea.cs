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
    /// 标签页**即将**关闭；宿主可以把 <see cref="EditorTabClosingEventArgs.Cancel"/> 设成
    /// <see langword="true"/> 拦下这一次。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="TabClosed"/> 的分工：这条在动手之前问、可否决；那条是事后通知、
    /// 用来释放宿主自己的对象。
    /// <para>
    /// 存在的理由是「关闭要换成别的动作」这类场景：一张图对应一台设备时，
    /// 关图的语义是断设备 —— 宿主取消这次关闭、发起断开，等断开流程回头再来关它。
    /// 没有这个钩子的话，宿主只能在 <see cref="TabClosed"/> 里补一刀断开，
    /// 那时标签页已经没了：设备断开失败或用户在确认框里点了「否」，图也回不来。
    /// </para>
    /// </remarks>
    public event EventHandler<EditorTabClosingEventArgs>? TabClosing;

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

            // EditorTab 是值类型，FirstOrDefault 给的是 default 而不是 null ——
            // 判 Id 才是「这一组里有没有」（同 Split 里那处的写法）。
            if (tab.Id is null)
            {
                continue;
            }

            // 先问一句「这张能关吗」——宿主可能要把关闭换成别的动作。
            // 典型：一张图对应一台设备，关图 = 断设备，那就得先取消这次关闭、
            // 走完断开流程再由断开的回调来关它，否则会出现「图没了、设备还连着」，
            // 或者两条路各 Dispose 一次。
            var closing = new EditorTabClosingEventArgs(tab);
            TabClosing?.Invoke(this, closing);
            if (closing.Cancel)
            {
                return false;
            }

            if (group.Close(id))
            {
                // 空了的组由 OnChanged 里的不变量摘掉，这里不重复判
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
    /// 选中一个标签页，并把它所在的组设成当前组。
    /// </summary>
    /// <remarks>
    /// ⚠ **必须走这一层，不能直接调 <see cref="EditorGroup.Select"/>** ——
    /// 那个方法改完**没人发通知**，视图监听的是本类的 <see cref="Changed"/>。
    /// 直接调的后果是：模型选中了，界面纹丝不动；用户以为"点了没反应"，
    /// 再点一下变成双击、而双击走的是另一条会重绘的路 —— 表现成
    /// **「切标签必须双击」**。同一个坑 <see cref="SetPinned"/> 的注释里已经记过一次。
    /// <para>
    /// 顺带把所在组设为当前组：点另一组的标签页，焦点本来就该跟过去。
    /// </para>
    /// <para>
    /// ⚠ **判据是「这一组里有没有这一页」，不是 <see cref="EditorGroup.Select"/> 的返回值。**
    /// 那个方法在「本来就选中着」时返回 <see langword="false"/>（它答的是"选中项变了没有"），
    /// 拿它当"找到没有"用，就会在**这一页已经是所在组的选中项**时整个方法空转：
    /// <see cref="CurrentGroup"/> 不跟过去、<see cref="Changed"/> 也不广播。
    /// <para>
    /// 后果不是抽象的：分屏时点另一格里那张已选中的图，焦点不过去（跟随活动文档的
    /// 分析面板于是跟错相机）；宿主程序化排完宫格调一句 <c>Select(第一路)</c> 想把
    /// 焦点放回第一格，也会**静默失败**，当前组停留在最后切出去的那一格 ——
    /// 再点「单栏」时就以那一格为幸存组收拢，标签顺序跟着乱掉。
    /// </para>
    /// </para>
    /// </remarks>
    /// <param name="id">标签页 id。</param>
    /// <returns>找到并选中了则为 <see langword="true"/>。</returns>
    public bool Select(string id)
    {
        foreach (var group in Groups)
        {
            if (group.Tabs.All(x => x.Id != id))
            {
                continue;
            }

            group.Select(id);
            CurrentGroup = group;
            OnChanged();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 拖拽重排：把一页挪到同组另一页的位置上。
    /// </summary>
    /// <remarks>
    /// 对应 <c>JBTabsImpl.kt:3331 · reallocate</c>；换位规则见 <see cref="EditorGroup.Reorder"/>。
    /// <para>
    /// <b>只在组内重排</b> —— 上游的 <c>reallocate</c> 动的是 <c>visibleInfos</c>，
    /// 那就是一个标签条 = 一组。跨组是另一条路（拖出去），走
    /// <see cref="DropTab(string, EditorGroup, EditorDropSide)"/>。
    /// 所以两页不在同一组时这里什么都不做，而不是"就近凑合一下"。
    /// </para>
    /// </remarks>
    /// <param name="sourceId">被拖的那一页。</param>
    /// <param name="targetId">拖到谁的位置上。</param>
    /// <returns>确实挪动了则为 <see langword="true"/>。</returns>
    public bool ReorderTab(string sourceId, string targetId)
    {
        var group = Groups.FirstOrDefault(g => g.Tabs.Any(t => t.Id == sourceId));
        if (group is null || group.Tabs.All(t => t.Id != targetId))
        {
            return false;
        }

        if (!group.Reorder(sourceId, targetId))
        {
            return false;
        }

        OnChanged();
        return true;
    }

    /// <summary>
    /// 关掉这一组里除了它以外的标签页。
    /// </summary>
    /// <remarks>
    /// 逐个走 <see cref="Close"/>，好让 <see cref="TabClosing"/> 的否决与
    /// <see cref="TabClosed"/> 的释放对**每一页**都成立 —— 宿主可能对其中某几页有话说
    /// （比如"这张图对应的设备正在采集"），批量关不能绕过它。
    /// <para>
    /// **固定住的和不可关的都跳过**：固定的语义就是"别动我"，
    /// 而 <c>IsCloseable=false</c> 的连关闭按钮都没有。
    /// </para>
    /// </remarks>
    /// <param name="id">保留哪一个。</param>
    /// <returns>实际关掉了几个。</returns>
    public int CloseOthers(string id) => CloseWhere(id, (_, _) => true, keepSelf: true);

    /// <summary>关掉这一组里所有能关的标签页（含它自己）。</summary>
    /// <param name="id">这一组里的任意一个标签页 id，用来定位是哪一组。</param>
    /// <returns>实际关掉了几个。</returns>
    public int CloseAll(string id) => CloseWhere(id, (_, _) => true, keepSelf: false);

    /// <summary>关掉它**左边**（显示次序在前）的标签页。</summary>
    /// <param name="id">基准标签页。</param>
    /// <returns>实际关掉了几个。</returns>
    public int CloseToTheLeft(string id) => CloseWhere(id, (index, pivot) => index < pivot, keepSelf: true);

    /// <summary>关掉它**右边**（显示次序在后）的标签页。</summary>
    /// <param name="id">基准标签页。</param>
    /// <returns>实际关掉了几个。</returns>
    public int CloseToTheRight(string id) => CloseWhere(id, (index, pivot) => index > pivot, keepSelf: true);

    /// <summary>
    /// 批量关闭的共同实现。
    /// </summary>
    /// <remarks>
    /// ⚠ **先把要关的 id 快照下来再逐个关**。边遍历边关会在集合被改的同时读它，
    /// 而且每关一个都可能触发组的移除、把序号搅乱。
    /// </remarks>
    private int CloseWhere(string id, Func<int, int, bool> predicate, bool keepSelf)
    {
        var group = Groups.FirstOrDefault(g => g.Tabs.Any(t => t.Id == id));
        if (group is null)
        {
            return 0;
        }

        var tabs = group.Tabs.ToList();
        var pivot = tabs.FindIndex(t => t.Id == id);
        if (pivot < 0)
        {
            return 0;
        }

        var targets = new List<string>();
        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            if (keepSelf && i == pivot)
            {
                continue;
            }

            // 固定住的与不可关的都跳过
            if (tab.IsPinned || !tab.IsCloseable || !predicate(i, pivot))
            {
                continue;
            }

            targets.Add(tab.Id);
        }

        var closed = 0;
        foreach (var target in targets)
        {
            if (Close(target))
            {
                closed++;
            }
        }

        return closed;
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

        var source = Groups.FirstOrDefault(g => g.Tabs.Any(t => t.Id == tabId));
        return source is not null && DropTab(tabId, source, target, side);
    }

    /// <summary>把指定源组中的一页拖到目标组。</summary>
    /// <remarks>
    /// <see cref="Split"/> 会让同一标签 id 同时存在于两组；拖拽层必须把源组身份传下来，
    /// 不能再按 id 反查，否则总会误取树中靠前的副本。
    /// </remarks>
    /// <param name="tabId">被拖的标签 id。</param>
    /// <param name="source">鼠标实际拖起标签的组。</param>
    /// <param name="target">落点组。</param>
    /// <param name="side">落点方向。</param>
    /// <returns>落点生效则为 <see langword="true"/>。</returns>
    public bool DropTab(string tabId, EditorGroup source, EditorGroup target, EditorDropSide side)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (side == EditorDropSide.None)
        {
            return false;
        }

        if (!Groups.Contains(source) || !Groups.Contains(target))
        {
            return false;
        }

        var operation = side switch
        {
            EditorDropSide.Center => DocumentDockOperation.Fill,
            EditorDropSide.Left => DocumentDockOperation.Left,
            EditorDropSide.Right => DocumentDockOperation.Right,
            EditorDropSide.Top => DocumentDockOperation.Top,
            EditorDropSide.Bottom => DocumentDockOperation.Bottom,
            _ => DocumentDockOperation.None,
        };

        return DocumentDockFactory.MoveDockable(
            tabId, source, target, operation,
            Replace,
            group => CurrentGroup = group,
            OnChanged);
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
    /// 收成一组 —— 页序 = 屏幕次序（左→右 / 上→下），幸存组恒为第一组。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="UnsplitAll"/> 的分工：那一条照抄上游（合并到**调用方指定**的那一组、
    /// 兄弟的页一律**追加**在后面），是用户点「取消分屏」时的手势语义；
    /// 这一条是**程序化归位**用的（宫格 → 单栏），要的是两条上游给不了的保证：
    /// <list type="number">
    /// <item><b>幸存组确定</b>：不看 <see cref="CurrentGroup"/>（它随焦点飘），恒取第一组，
    /// 所以同一个宫格收几次都收成同一个样子；</item>
    /// <item><b>页序确定</b>：按屏幕次序重排。走上游那条时，收进右下角那一格的话，
    /// 页序会变成「右下那一格的页在最前」——用户看到的是标签顺序凭空乱掉。</item>
    /// </list>
    /// </remarks>
    /// <returns>幸存下来的那一组。</returns>
    public EditorGroup CollapseToSingleGroup()
    {
        var group = CollapseCore();
        OnChanged();
        return group;
    }

    /// <summary>
    /// 把若干页排成 <paramref name="rows"/>×<paramref name="cols"/> 的**均匀**宫格。
    /// </summary>
    /// <remarks>
    /// 实际形状由 <see cref="EditorGridPolicy.Resolve"/> 定（格数按页数封顶，
    /// 多出来的页塞最后一格）。<paramref name="tabIds"/> 之外的页（宿主另开的静态文档等）
    /// 留在第一格。
    /// <para>
    /// <b>为什么这件事必须在模型层做，而不是宿主拿 <see cref="DropTab(string, EditorGroup, EditorDropSide)"/> 敲。</b>
    /// 敲得出格子，敲不出**比例**：每切一刀都是新建一个 0.5 的二叉分屏节点，
    /// 三格于是成了 50% / 25% / 25%，四格才碰巧均匀。而 <see cref="EditorSplit.Proportion"/>
    /// 挂在树的内部节点上，宿主根本够不着（<see cref="Root"/> 只读，
    /// 要改就得自己在树上找刚切出来的那个节点 —— 那是把分屏树的知识搬进 UI 宿主）。
    /// 树形与比例本来就是编辑区自己的账，同 <see cref="UnsplitAll"/> /
    /// <see cref="MaximizeOrNormalize"/>。
    /// </para>
    /// <para>
    /// 均匀是这么来的：一行 k 格切出来是**右嵌套**的
    /// <c>(g0, (g1, (g2 …)))</c>，所以从外往里第 i 刀取 <c>1/(k-i)</c> ——
    /// 外层 1/k 给第一格，剩下的 (k-1)/k 再按 1/(k-1) 切，逐格恰好 1/k。行方向同理。
    /// </para>
    /// </remarks>
    /// <param name="tabIds">要进格子的页，**按格子的行主序**。</param>
    /// <param name="rows">行数，<c>&lt;= 0</c> 表示按页数自动。</param>
    /// <param name="cols">列数，<c>&lt;= 0</c> 表示按页数自动。</param>
    /// <returns>确实排成了宫格则为 <see langword="true"/>；不足两页排不成宫格时收回单组并返回 <see langword="false"/>。</returns>
    public bool ArrangeGrid(IReadOnlyList<string> tabIds, int rows, int cols)
    {
        ArgumentNullException.ThrowIfNull(tabIds);

        var known = _root.Groups.SelectMany(g => g.Tabs).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var ids = tabIds.Where(known.Contains).Distinct(StringComparer.Ordinal).ToList();

        // 先收成一组：从「当前是什么形状」出发去改是算不清的，收平了再切才有确定的起点。
        var survivor = CollapseCore();

        if (ids.Count < 2)
        {
            OnChanged();
            return false;
        }

        var shape = EditorGridPolicy.Resolve(ids.Count, rows, cols);
        survivor.Select(ids[0]);

        // 行：每一行从上一行切出去 → 右嵌套的竖直分屏
        var rowGroups = new List<EditorGroup> { survivor };
        var rowSplits = new List<EditorSplit>();
        for (var r = 1; r < shape.Rows; r++)
        {
            var index = r * shape.Cols;
            if (index >= shape.CellCount || Carve(rowGroups[r - 1], ids[index], vertical: true) is not { } carved)
            {
                break;
            }

            rowSplits.Add(carved.Split);
            rowGroups.Add(carved.Created);
        }

        // 列：每一行内部再右嵌套地切出各格
        for (var r = 0; r < rowGroups.Count; r++)
        {
            var previous = rowGroups[r];
            var colSplits = new List<EditorSplit>();
            for (var c = 1; c < shape.Cols; c++)
            {
                var index = (r * shape.Cols) + c;
                if (index >= shape.CellCount || Carve(previous, ids[index], vertical: false) is not { } carved)
                {
                    break;
                }

                colSplits.Add(carved.Split);
                previous = carved.Created;
            }

            Distribute(colSplits);
        }

        Distribute(rowSplits);

        // 多于格数的那些：留在最后一格当标签页（预设的语义是「最大形状」，见 EditorGridPolicy）
        if (GroupOf(ids[shape.CellCount - 1]) is { } last)
        {
            for (var i = shape.CellCount; i < ids.Count; i++)
            {
                MoveInto(last, ids[i]);
            }
        }

        CurrentGroup = survivor;
        OnChanged();
        return true;
    }

    /// <summary>把一条右嵌套分屏链的比例摊匀：从外往里第 i 刀取 <c>1/(n+1-i)</c>。</summary>
    private static void Distribute(IReadOnlyList<EditorSplit> chain)
    {
        for (var i = 0; i < chain.Count; i++)
        {
            chain[i].Proportion = 1f / (chain.Count + 1 - i);
        }
    }

    /// <summary>
    /// 从树上任意位置取一页，切成 <paramref name="target"/> 旁边的新一格。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Split"/> 的差别：那个只能带走**本组**选中的那一页；
    /// 这里要的是「把第 k 路挪到第 k 格」，源在哪一组由 id 决定，不受 target 限制。
    /// </remarks>
    private (EditorSplit Split, EditorGroup Created)? Carve(EditorGroup target, string tabId, bool vertical)
    {
        if (GroupOf(tabId) is not { } source)
        {
            return null;
        }

        var tab = source.Tabs.First(x => x.Id == tabId);
        var created = new EditorGroup();
        created.Add(tab);

        var split = new EditorSplit(target, created, vertical);
        Replace(target, split);
        source.Close(tabId);
        return (split, created);
    }

    /// <summary>把一页并进某一组（不分屏）。</summary>
    private void MoveInto(EditorGroup target, string tabId)
    {
        if (GroupOf(tabId) is not { } source || ReferenceEquals(source, target))
        {
            return;
        }

        var tab = source.Tabs.First(x => x.Id == tabId);
        source.Close(tabId);
        target.Add(tab, select: false);
    }

    /// <summary>这一页在哪一组（树里靠前的那份）。</summary>
    private EditorGroup? GroupOf(string tabId) => _root.Groups.FirstOrDefault(g => g.Tabs.Any(x => x.Id == tabId));

    /// <summary>收成一组，但**不广播** —— 给「收完还要接着改树」的调用方用。</summary>
    private EditorGroup CollapseCore()
    {
        var groups = _root.Groups.ToList();
        var survivor = groups[0];
        if (groups.Count == 1)
        {
            return survivor;
        }

        // 屏幕次序（Groups 就是左→右 / 上→下）。同一 id 可能同时开在两组，去重。
        var order = new List<EditorTab>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tab in groups.SelectMany(g => g.Tabs))
        {
            if (seen.Add(tab.Id))
            {
                order.Add(tab);
            }
        }

        var selected = CurrentGroup.SelectedId;

        foreach (var tab in survivor.Tabs.ToList())
        {
            survivor.Close(tab.Id);
        }

        foreach (var tab in order)
        {
            survivor.Add(tab, select: false);
        }

        _root = survivor;
        CurrentGroup = survivor;

        if (selected is not null)
        {
            survivor.Select(selected);
        }

        return survivor;
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

    /// <summary>
    /// 直接改过组之后，重新建立不变量并广播。
    /// </summary>
    /// <remarks>
    /// 给<b>绕过本类、直接动 <see cref="EditorGroup"/></b> 的调用方用
    /// （读档时按内容工厂的结果逐个丢弃标签页就是这么干的）。
    /// 正常路径不需要它 —— 本类自己的方法都已经走 <see cref="OnChanged"/>。
    /// </remarks>
    public void Refresh() => OnChanged();

    /// <summary>
    /// 广播「树变了」——<b>广播之前先把不变量补齐</b>。
    /// </summary>
    /// <remarks>
    /// 这是整个类唯一的出口，所以不变量放在这里，而不是散在每个改动方法里。
    /// <para>
    /// <b>为什么非这样不可。</b>此前是每个改动方法各自判「这一组空了吗、要不要摘」，
    /// 判据还各写各的（<c>Close</c> 判 <c>Tabs.Count == 0</c>、
    /// <c>DropTab</c> 判 <c>Count == 0 &amp;&amp; !ReferenceEquals(source, target)</c>…）。
    /// 结果是<b>只要有一条路忘了判，树里就多一个空组</b>，编辑区凭空出现空白窗格；
    /// 而且漏一条修一条，永远不知道还剩几条没堵
    /// —— 「拖动之后中间空了」就是这么反复出现的。
    /// </para>
    /// <para>
    /// 现在改成：<b>改动方法只管把标签页搬对，树的形状由这里兜底。</b>
    /// 新增改动路径不必再想「要不要摘空组」，忘了也不会漏。
    /// </para>
    /// </remarks>
    private void OnChanged()
    {
        EnforceInvariants();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 树的两条不变量。
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item>
    /// <b>没有空组</b> —— 除非整个编辑区就剩这一组（那是"什么都没打开"的正常状态，
    /// 对应上游 <c>EditorWindow.kt:735</c>「<c>tabCount == 0</c> 就销毁窗口」，
    /// 以及 <c>:546-548</c>「<c>split</c> 不生成会留下空窗格的分屏」）。
    /// </item>
    /// <item><b><see cref="CurrentGroup"/> 指向树里确实存在的组。</b></item>
    /// </list>
    /// <para>
    /// 摘要循环着摘：摘掉一个会让父分屏塌陷成它的兄弟，那个兄弟里可能还有空组。
    /// </para>
    /// </remarks>
    private void EnforceInvariants()
    {
        while (true)
        {
            var empty = _root.Groups.FirstOrDefault(g => g.Tabs.Count == 0);

            // 没有空组，或者空的那个就是根 —— 后者是「编辑区空着」，合法，留着
            if (empty is null || FindParent(_root, empty) is not { } parent)
            {
                break;
            }

            Replace(parent, ReferenceEquals(parent.First, empty) ? parent.Second : parent.First);
        }

        if (!_root.Groups.Any(g => ReferenceEquals(g, CurrentGroup)))
        {
            CurrentGroup = _root.Groups.First();
        }
    }
}


/// <summary>
/// <see cref="EditorArea.TabClosing"/> 的参数。
/// </summary>
/// <param name="tab">即将关闭的标签页。</param>
public sealed class EditorTabClosingEventArgs(EditorTab tab) : EventArgs
{
    /// <summary>即将关闭的标签页。</summary>
    public EditorTab Tab { get; } = tab;

    /// <summary>设成 <see langword="true"/> 拦下这次关闭。</summary>
    public bool Cancel { get; set; }
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
