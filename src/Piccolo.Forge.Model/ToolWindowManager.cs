namespace Piccolo.Forge.Model;

/// <summary>
/// 工具窗口的显示/隐藏/激活。
/// </summary>
/// <remarks>
/// 对应上游 <c>platform/platform-impl/src/com/intellij/openapi/wm/impl/ToolWindowManagerImpl.kt</c>
/// 中与布局状态相关的那部分。**不含 UI**：这里只改状态并发事件，画什么由视图层决定。
/// <para>
/// 上游严格区分「布局持有的可变 info」与「entry 持有的只读快照」
/// （<c>ToolWindowManagerImpl.kt:933-937</c>）。这里 <see cref="WindowInfo"/> 本身不可变，
/// <see cref="DesktopLayout"/> 就是那份可变状态。
/// </para>
/// </remarks>
public sealed class ToolWindowManager
{
    private readonly ActiveStack<string> _activeStack = new();
    private readonly SideStack _sideStack = new();
    private readonly List<string> _recentToolWindows = [];

    /// <summary>用一份布局建 manager。</summary>
    /// <param name="layout">布局状态。</param>
    /// <param name="isStackEnabled">
    /// 是否启用「隐藏时自动恢复被挤掉的那个」。上游默认关闭，见
    /// <see cref="ToolWindowStackOptions.IsStackEnabledByDefault"/>。
    /// </param>
    public ToolWindowManager(DesktopLayout layout, bool isStackEnabled = ToolWindowStackOptions.IsStackEnabledByDefault)
    {
        ArgumentNullException.ThrowIfNull(layout);
        Layout = layout;
        IsStackEnabled = isStackEnabled;
    }

    /// <summary>布局状态。</summary>
    public DesktopLayout Layout { get; }

    /// <summary>是否启用侧栈恢复。</summary>
    public bool IsStackEnabled { get; }

    /// <summary>
    /// 是否启用统一权重：激活时用该边的统一权重覆盖窗口自身的 weight。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowManagerImpl.kt:647-656 · isUnifiedToolWindowSizesEnabled</c>，
    /// 新 UI 下默认启用（`rememberSizeForEachToolWindowNewUI` 默认 false，`UISettingsState.kt:97`）。
    /// </remarks>
    public bool IsUnifiedToolWindowSizesEnabled { get; init; } = true;

    /// <summary>最近激活过的工具窗口，最新的在前。</summary>
    /// <remarks>
    /// 对应 <c>ToolWindowManagerImpl.kt:615-616</c> 与 <c>:658 · getRecentToolWindows</c>。
    /// 这是**会落盘**的那份（<c>ToolWindowManagerState.kt:79-85</c>），
    /// 与 <see cref="LastActiveToolWindowId"/> 背后的栈不是一回事。
    /// </remarks>
    public IReadOnlyList<string> RecentToolWindows => _recentToolWindows;

    /// <summary>最后一个活跃过的工具窗口。</summary>
    /// <remarks>读的是 activeStack 的**持久栈**（<c>ToolWindowManagerImpl.kt:737-740</c>）。</remarks>
    public string? LastActiveToolWindowId => _activeStack.PersistentStack.FirstOrDefault();

    /// <summary>状态变了。</summary>
    public event EventHandler<ToolWindowStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// 显示一个工具窗口。同一格里已经可见的那个会被挤掉。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowManagerImpl.kt:916-938 · showToolWindowImpl</c> 与
    /// <c>:940-1006 · doShowWindow</c>：
    /// 置 <c>isVisible</c>、**强制** <c>isShowStripeButton = true</c>、
    /// <c>order == -1</c> 时补 <see cref="DesktopLayout.GetNextOrder"/>；
    /// 然后把同一格（见 <see cref="ToolWindowSlot"/>）里其它可见的隐藏掉，
    /// 被挤掉的若是 DOCKED 且非 autoHide 就压进侧栈。
    /// </remarks>
    /// <param name="id">工具窗口 id。</param>
    /// <returns>本来就可见时返回 <see langword="false"/>。</returns>
    public bool Show(string id)
    {
        var info = Layout.GetInfo(id) ?? throw new InvalidOperationException($"未注册的工具窗口：{id}");
        if (info.IsVisible)
        {
            return false;
        }

        var order = info.Order == -1 ? Layout.GetNextOrder(info.PaneId, info.Anchor) : info.Order;
        var shown = info with { IsVisible = true, IsShowStripeButton = true, Order = order };
        Layout.SetInfo(shown);

        HideOthersInSameSlot(shown);
        _sideStack.Remove(id);

        OnStateChanged(ToolWindowChangeKind.Shown, id);
        return true;
    }

    /// <summary>
    /// 隐藏一个工具窗口。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowManagerImpl.kt:819-854 · hideToolWindow</c> 与
    /// <c>:856-909 · executeHide</c>。
    /// </remarks>
    /// <param name="id">工具窗口 id。</param>
    /// <param name="hideSide">
    /// 是否把同一 (pane, anchor) 上所有可见窗口一起隐掉，并清空该边的侧栈（<c>:867-880</c>）。
    /// </param>
    /// <param name="removeFromStripe">
    /// 是否顺便去掉条带按钮。为真时隐藏后把 paneId 重置回默认（<c>:845-848</c>）。
    /// </param>
    /// <returns>本来就不可见时返回 <see langword="false"/>。</returns>
    public bool Hide(string id, bool hideSide = false, bool removeFromStripe = false)
    {
        var info = Layout.GetInfo(id) ?? throw new InvalidOperationException($"未注册的工具窗口：{id}");
        if (!info.IsVisible)
        {
            return false;
        }

        Deactivate(info);

        if (removeFromStripe)
        {
            var current = Layout.GetInfo(id)!;
            Layout.SetInfo(current with { IsShowStripeButton = false, PaneId = WindowInfo.DefaultPaneId });
        }

        if (hideSide)
        {
            foreach (var other in Layout.GetSortedList()
                         .Where(x => x.IsVisible
                                     && string.Equals(x.PaneId, info.PaneId, StringComparison.Ordinal)
                                     && x.Anchor == info.Anchor)
                         .ToList())
            {
                Deactivate(other);
            }

            if (IsStackEnabled)
            {
                while (!_sideStack.IsEmpty(info.Anchor))
                {
                    _sideStack.Pop(info.Anchor);
                }
            }
        }
        else if (IsStackEnabled)
        {
            RestoreFromSideStack(info);
        }

        OnStateChanged(ToolWindowChangeKind.Hidden, id);
        return true;
    }

    /// <summary>
    /// 激活一个工具窗口：不可见就先显示，然后记进两个「最近」结构。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowManagerImpl.kt:598-645 · activateToolWindow</c>。
    /// 统一权重启用时，先用该边的统一权重覆盖窗口自身的 weight（<c>:606-609</c>）。
    /// </remarks>
    /// <param name="id">工具窗口 id。</param>
    public void Activate(string id)
    {
        var info = Layout.GetInfo(id) ?? throw new InvalidOperationException($"未注册的工具窗口：{id}");

        if (IsUnifiedToolWindowSizesEnabled)
        {
            Layout.SetInfo(info with { Weight = Layout.GetUnifiedAnchorWeight(info.Anchor) });
        }

        _recentToolWindows.Remove(id);
        _recentToolWindows.Insert(0, id);

        if (!Layout.GetInfo(id)!.IsVisible)
        {
            Show(id);
        }

        _activeStack.Push(id);
        OnStateChanged(ToolWindowChangeKind.Activated, id);
    }

    /// <summary>
    /// 关掉当前窗口后，焦点该回到哪个工具窗口。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowManagerImpl.kt:1921-1941 · focusToolWindowByDefault</c>：
    /// 先扫易失栈找第一个可见的，找不到再扫持久栈。
    /// </remarks>
    /// <returns>该激活的 id；都没有则 <see langword="null"/>。</returns>
    public string? FindToolWindowToFocus()
        => FirstVisible(_activeStack.Stack) ?? FirstVisible(_activeStack.PersistentStack);

    /// <summary>
    /// 编辑区拿到焦点：清空易失栈，持久栈不动。
    /// </summary>
    /// <remarks>对应 <c>ToolWindowManagerLifecycle.kt:85-92</c>。</remarks>
    public void OnEditorFocused() => _activeStack.Clear();

    /// <summary>
    /// 焦点跑到别处时，该不该自动隐藏这个窗口。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowManagerLifecycle.kt:116-118</c>：
    /// **SLIDING 无论 <c>isAutoHide</c> 是什么都会自动隐藏**，
    /// 「自动隐藏」在上游有两个来源，不是一个字段。
    /// 走的是 <c>deactivateToolWindow</c> 而非 <c>executeHide</c>，
    /// 所以不触发侧栈恢复 —— 这里用 <see cref="AutoHideOnFocusLost"/> 表达。
    /// </remarks>
    /// <param name="info">窗口状态。</param>
    /// <returns>该自动隐藏则为 <see langword="true"/>。</returns>
    public static bool ShouldAutoHideOnFocusLost(WindowInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return info.IsVisible && (info.IsAutoHide || info.Type == ToolWindowType.Sliding);
    }

    /// <summary>
    /// 因失焦而自动隐藏。
    /// </summary>
    /// <remarks>不触发侧栈恢复，见 <see cref="ShouldAutoHideOnFocusLost"/>。</remarks>
    /// <param name="id">工具窗口 id。</param>
    /// <returns>确实隐藏了则为 <see langword="true"/>。</returns>
    public bool AutoHideOnFocusLost(string id)
    {
        var info = Layout.GetInfo(id) ?? throw new InvalidOperationException($"未注册的工具窗口：{id}");
        if (!ShouldAutoHideOnFocusLost(info))
        {
            return false;
        }

        Deactivate(info);
        OnStateChanged(ToolWindowChangeKind.Hidden, id);
        return true;
    }

    /// <summary>
    /// 拖拽落到条带上的结果。
    /// </summary>
    /// <remarks>
    /// 对应 <c>AbstractDroppableStripe.kt:242-258 · finishDrop</c>：
    /// 拖拽的全部结果就是这一次 <see cref="DesktopLayout.SetSideToolAndAnchor"/>。
    /// 上游用「先隐藏、改完再显示」借互斥挤走新格子里的旧住户
    /// （<c>ToolWindowManagerImpl.kt:1686</c> 起），这里照做。
    /// </remarks>
    /// <param name="id">被拖的工具窗口。</param>
    /// <param name="anchor">目标边。</param>
    /// <param name="order">目标次序，<c>-1</c> 表示末尾。</param>
    /// <param name="isSplit">是否落到同一条边的另一半。</param>
    /// <param name="paneId">目标 pane。</param>
    public void MoveTo(
        string id,
        ToolWindowAnchor anchor,
        int order,
        bool isSplit,
        string paneId = WindowInfo.DefaultPaneId)
    {
        var info = Layout.GetInfo(id) ?? throw new InvalidOperationException($"未注册的工具窗口：{id}");
        var wasVisible = info.IsVisible;

        if (wasVisible)
        {
            Deactivate(info);
        }

        Layout.SetSideToolAndAnchor(id, paneId, anchor, order, isSplit);

        if (wasVisible)
        {
            Show(id);
        }

        OnStateChanged(ToolWindowChangeKind.Moved, id);
    }

    /// <summary>
    /// 切换工具窗口的形态。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowManagerImpl.kt:1757-1783 · setToolWindowTypeImpl</c>：
    /// 不可见就只改字段、UI 不动；可见就「先存外部窗口状态并摘掉，改完再重放」。
    /// **只有内部形态（DOCKED / SLIDING）才写 <see cref="WindowInfo.InternalType"/>**
    /// （<c>:1770-1772</c>，`isInternal` 的定义见 <c>ToolWindowTypeExtensions.kt:9-10</c>）——
    /// 它记的是「上次在窗口里的时候是停靠还是滑出」，从浮动切回来时按它还原。
    /// </remarks>
    /// <param name="id">工具窗口 id。</param>
    /// <param name="type">新形态。</param>
    /// <returns>形态本来就一样时返回 <see langword="false"/>。</returns>
    public bool SetType(string id, ToolWindowType type)
    {
        var info = Layout.GetInfo(id) ?? throw new InvalidOperationException($"未注册的工具窗口：{id}");
        if (info.Type == type)
        {
            return false;
        }

        var wasVisible = info.IsVisible;
        var updated = info with { Type = type };

        if (IsInternal(type))
        {
            updated = updated with { InternalType = type };
        }

        Layout.SetInfo(updated);

        // 可见的话要重放一次，好让新形态参与（或退出）「一格一个可见窗口」的互斥
        if (wasVisible)
        {
            Layout.SetInfo(Layout.GetInfo(id)! with { IsVisible = false });
            Show(id);
        }

        OnStateChanged(ToolWindowChangeKind.TypeChanged, id);
        return true;
    }

    /// <summary>
    /// 从外部形态切回窗口内时，该回到哪种内部形态。
    /// </summary>
    /// <remarks>这就是 <see cref="WindowInfo.InternalType"/> 存在的意义（见 <see cref="SetType"/>）。</remarks>
    /// <param name="id">工具窗口 id。</param>
    /// <returns>该回到的形态。</returns>
    public ToolWindowType ResolveInternalType(string id)
    {
        var info = Layout.GetInfo(id) ?? throw new InvalidOperationException($"未注册的工具窗口：{id}");
        return info.InternalType;
    }

    /// <summary>
    /// 是不是「在主窗口里面」的形态。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowTypeExtensions.kt:9-10 · isInternal</c>：
    /// SLIDING 或 DOCKED。
    /// </remarks>
    /// <param name="type">形态。</param>
    /// <returns>内部形态则为 <see langword="true"/>。</returns>
    public static bool IsInternal(ToolWindowType type)
        => type is ToolWindowType.Sliding or ToolWindowType.Docked;

    /// <summary>
    /// 是不是要另开一个窗口的形态。
    /// </summary>
    /// <param name="type">形态。</param>
    /// <returns>FLOATING 或 WINDOWED 则为 <see langword="true"/>。</returns>
    public static bool IsExternal(ToolWindowType type) => !IsInternal(type);

    private void HideOthersInSameSlot(WindowInfo shown)
    {
        foreach (var other in Layout.GetSortedList().ToList())
        {
            if (string.Equals(other.Id, shown.Id, StringComparison.Ordinal) || !other.IsVisible)
            {
                continue;
            }

            // 外部形态（浮动/独立窗口）不进 pane，也就不参与互斥
            // （ToolWindowManagerImpl.kt:942-947 的前两个分支根本不走 doShowWindow 的挤占逻辑）
            if (IsExternal(shown.Type) || IsExternal(other.Type) || other.Slot != shown.Slot)
            {
                continue;
            }

            var hidden = Deactivate(other);

            // 只有 DOCKED 且非 autoHide 的才进侧栈（ToolWindowManagerImpl.kt:976-977）
            if (IsStackEnabled && hidden.IsDocked && !hidden.IsAutoHide)
            {
                _sideStack.Push(hidden);
            }
        }
    }

    private void RestoreFromSideStack(WindowInfo hidden)
    {
        while (!_sideStack.IsEmpty(hidden.Anchor))
        {
            var stored = _sideStack.Pop(hidden.Anchor);

            // 跳过 isSplit 不一致的（ToolWindowManagerImpl.kt:889-891）
            if (stored.IsSplit != hidden.IsSplit)
            {
                continue;
            }

            // 校验 pane/anchor/type/autoHide 都没变过（:896-899）；
            // 弹出来但没通过的直接丢掉，不压回去
            var current = Layout.GetInfo(stored.Id);
            if (current is null
                || !string.Equals(stored.PaneId, current.PaneId, StringComparison.Ordinal)
                || stored.Anchor != current.Anchor
                || stored.Type != current.Type
                || stored.IsAutoHide != current.IsAutoHide)
            {
                continue;
            }

            if (!current.IsVisible)
            {
                Show(stored.Id);
            }

            return;
        }
    }

    // ToolWindowManagerImpl.kt:698-704 · setHiddenState
    private WindowInfo Deactivate(WindowInfo info)
    {
        var hidden = info with { IsVisible = false, IsActiveOnStart = false };
        Layout.SetInfo(hidden);
        _activeStack.Remove(info.Id, removePersistentAlso: false);
        return hidden;
    }

    private string? FirstVisible(IReadOnlyList<string> ids)
        => ids.FirstOrDefault(x => Layout.GetInfo(x)?.IsVisible == true);

    private void OnStateChanged(ToolWindowChangeKind kind, string id)
        => StateChanged?.Invoke(this, new ToolWindowStateChangedEventArgs(kind, id));
}

/// <summary>状态变化的种类。</summary>
public enum ToolWindowChangeKind
{
    /// <summary>显示了。</summary>
    Shown,

    /// <summary>隐藏了。</summary>
    Hidden,

    /// <summary>激活了。</summary>
    Activated,

    /// <summary>换了位置。</summary>
    Moved,

    /// <summary>换了形态（停靠/滑出/浮动/独立窗口）。</summary>
    TypeChanged,
}

/// <summary>
/// <see cref="ToolWindowManager.StateChanged"/> 的参数。
/// </summary>
/// <param name="kind">变化的种类。</param>
/// <param name="id">发生变化的工具窗口。</param>
public sealed class ToolWindowStateChangedEventArgs(ToolWindowChangeKind kind, string id) : EventArgs
{
    /// <summary>变化的种类。</summary>
    public ToolWindowChangeKind Kind { get; } = kind;

    /// <summary>发生变化的工具窗口。</summary>
    public string Id { get; } = id;
}
