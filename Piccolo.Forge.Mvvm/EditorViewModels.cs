using System.Collections.ObjectModel;
using System.Xml.Linq;
using Piccolo.Forge.Model;

namespace Piccolo.Forge.Mvvm;

/// <summary>
/// 一个编辑区标签页的可观察视图。
/// </summary>
/// <remarks>
/// 与 <see cref="ToolWindowViewModel"/> 同理：它是 <see cref="EditorTab"/> 的投影，
/// <see cref="Content"/> 是 <see cref="object"/>，视图由宿主解析。
/// </remarks>
public sealed class EditorTabViewModel : ForgeObservableObject
{
    private string _title;
    private object? _icon;
    private object? _content;
    private bool _isSelected;
    private bool _isPinned;
    private bool _isPreview;
    private bool _isCloseable = true;

    /// <summary>建一个标签页 VM。</summary>
    /// <param name="id">标签页 id。</param>
    /// <param name="title">显示名。</param>
    public EditorTabViewModel(string id, string? title = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Id = id;
        _title = title ?? id;
    }

    /// <summary>标签页 id。</summary>
    public string Id { get; }

    /// <summary>标签上显示的名字。</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>标签上的图标。</summary>
    public object? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    /// <summary>这一页里放什么。宿主的 VM，视图由宿主解析。</summary>
    public object? Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    /// <summary>是不是这一组里选中的那个。</summary>
    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetProperty(ref _isSelected, value);
    }

    /// <summary>固定住了 —— 永远不会被上限挤掉。</summary>
    public bool IsPinned
    {
        get => _isPinned;
        internal set => SetProperty(ref _isPinned, value);
    }

    /// <summary>预览标签页 —— 超限时第一批被关。</summary>
    public bool IsPreview
    {
        get => _isPreview;
        internal set => SetProperty(ref _isPreview, value);
    }

    /// <summary>能不能关；不能关的不计入上限的分母。</summary>
    public bool IsCloseable
    {
        get => _isCloseable;
        internal set => SetProperty(ref _isCloseable, value);
    }

    internal void SyncFrom(EditorTab tab, bool isSelected)
    {
        Title = tab.Title;
        IsPinned = tab.IsPinned;
        IsPreview = tab.IsPreview;
        IsCloseable = tab.IsCloseable;
        IsSelected = isSelected;
    }
}

/// <summary>
/// 一组标签页的可观察视图。
/// </summary>
public sealed class EditorGroupViewModel : ForgeObservableObject
{
    private readonly ObservableCollection<EditorTabViewModel> _tabs = [];
    private EditorTabViewModel? _selected;
    private bool _isCurrent;

    internal EditorGroupViewModel(EditorGroup group)
    {
        Group = group;
        Tabs = new ReadOnlyObservableCollection<EditorTabViewModel>(_tabs);
    }

    /// <summary>底下那个模型对象。</summary>
    public EditorGroup Group { get; }

    /// <summary>这一组里的标签页，按显示次序。</summary>
    public ReadOnlyObservableCollection<EditorTabViewModel> Tabs { get; }

    /// <summary>选中的那个。</summary>
    public EditorTabViewModel? Selected
    {
        get => _selected;
        private set => SetProperty(ref _selected, value);
    }

    /// <summary>是不是「当前组」—— 新开的标签页落在它身上。</summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        internal set => SetProperty(ref _isCurrent, value);
    }

    internal void Sync(Func<string, EditorTabViewModel> resolve)
    {
        var wanted = Group.Tabs.Select(tab =>
        {
            var vm = resolve(tab.Id);
            vm.SyncFrom(tab, isSelected: tab.Id == Group.SelectedId);
            return vm;
        }).ToList();

        Reconcile(_tabs, wanted);
        Selected = wanted.FirstOrDefault(x => x.IsSelected);
    }

    /// <summary>
    /// 在跨 DocumentDock 搬移的 add 阶段之前，先摘掉已经不属于本组的文档。
    /// </summary>
    /// <remarks>
    /// 对应 Dock 的 Factory.MoveDockable：先从 source VisibleDockables remove，
    /// 再向 target add。若反过来，同一个宿主 Control 图标会同时被两个 TabStrip
    /// 容器收养，目标容器实现失败后就永久显示为缺页。
    /// </remarks>
    internal void PruneMovedDockables()
    {
        var wantedIds = Group.Tabs.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        for (var i = _tabs.Count - 1; i >= 0; i--)
        {
            if (!wantedIds.Contains(_tabs[i].Id))
            {
                _tabs.RemoveAt(i);
            }
        }
    }

    private static void Reconcile(ObservableCollection<EditorTabViewModel> target, IReadOnlyList<EditorTabViewModel> wanted)
    {
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(target[i]))
            {
                target.RemoveAt(i);
            }
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            var current = target.IndexOf(wanted[i]);
            if (current < 0)
            {
                target.Insert(i, wanted[i]);
            }
            else if (current != i)
            {
                target.Move(current, i);
            }
        }
    }
}

/// <summary>
/// 整个编辑区的可观察视图 —— 宿主接的就是这个。
/// </summary>
/// <remarks>
/// 与 <see cref="ToolWindowShellViewModel"/> 同一套做法：
/// **只改属性和集合成员，不重建任何 VM**，宿主塞进 <see cref="EditorTabViewModel.Content"/>
/// 的东西不会因为开合别的标签页就被重造。
/// </remarks>
public sealed class EditorAreaViewModel : ForgeObservableObject
{
    private readonly Dictionary<string, EditorTabViewModel> _tabsById = new(StringComparer.Ordinal);
    private readonly Dictionary<EditorGroup, EditorGroupViewModel> _groupsByModel = [];
    private readonly ObservableCollection<EditorGroupViewModel> _groups = [];
    private EditorTabViewModel? _activeTab;

    /// <summary>建一个编辑区 VM。</summary>
    /// <param name="area">底下的模型；不给就新建一个空的。</param>
    public EditorAreaViewModel(EditorArea? area = null)
    {
        Area = area ?? new EditorArea();
        Area.Changed += (_, _) => Sync();
        Area.TabClosing += (_, e) =>
        {
            if (!_tabsById.TryGetValue(e.Tab.Id, out var vm))
            {
                return;
            }

            // 摘走（搬去别的编辑区）不是关闭：不问宿主要不要拦，也不告诉它"这页关了"
            if (string.Equals(_detaching, e.Tab.Id, StringComparison.Ordinal))
            {
                return;
            }

            var args = new EditorTabClosingViewModelEventArgs(vm);
            TabClosing?.Invoke(this, args);
            e.Cancel = args.Cancel;
        };
        Area.TabClosed += (_, e) =>
        {
            // 关掉的那一页从缓存里摘掉 —— 不摘的话宿主塞的内容（相机会话、图像帧）
            // 会被这个字典一直强引用着，Dispose 了也回收不掉。
            if (string.Equals(_detaching, e.Tab.Id, StringComparison.Ordinal))
            {
                return;
            }

            if (_tabsById.Remove(e.Tab.Id, out var vm))
            {
                TabClosed?.Invoke(this, vm);
            }
        };
        Groups = new ReadOnlyObservableCollection<EditorGroupViewModel>(_groups);
        Sync();
    }

    /// <summary>底下那个模型。</summary>
    public EditorArea Area { get; }

    /// <summary>所有标签页组。</summary>
    public ReadOnlyObservableCollection<EditorGroupViewModel> Groups { get; }

    /// <summary>
    /// 一个标签页被关掉了 —— 宿主在这里释放它的内容。
    /// </summary>
    /// <remarks>
    /// 转发自 <see cref="EditorArea.TabClosed"/>，多给一样东西：关掉那一页的 VM，
    /// 宿主拿 <see cref="EditorTabViewModel.Content"/> 就能 <c>Dispose</c>。
    /// <b>库不替宿主 Dispose</b>：内容是宿主的对象，什么时候该释放只有宿主知道
    /// （同一个会话可能还被别处引用着）。
    /// </remarks>
    public event EventHandler<EditorTabViewModel>? TabClosed;

    /// <summary>
    /// 一个标签页**即将**关闭 —— 宿主可以拦下这一次。
    /// </summary>
    /// <remarks>
    /// 转发自 <see cref="EditorArea.TabClosing"/>。用途见那边的说明：
    /// 「关闭要换成别的动作」的场景（一张图对应一台设备，关图 = 断设备）。
    /// </remarks>
    public event EventHandler<EditorTabClosingViewModelEventArgs>? TabClosing;

    /// <summary>
    /// 当前活动的那一页换人了（换成 <see langword="null"/> 也报）。
    /// </summary>
    /// <remarks>
    /// 对应上游 <c>EditorWindow.kt:232-241</c> 里挂在 tabs 上的
    /// <c>TabsListener.selectionChanged</c> —— 它把新选中的 composite 推进
    /// <c>_currentCompositeFlow</c>，别处（工具窗口、状态栏）都看这条流。
    /// <para>
    /// 「活动」= <b>当前组</b>里选中的那一页：分屏之后每组各有一个选中项，
    /// 但只有当前组的那个算活动（<c>EditorsSplitters.currentWindow</c>）。
    /// </para>
    /// </remarks>
    public event EventHandler<EditorTabViewModel?>? ActiveTabChanged;

    /// <summary>当前活动的那一页；没有则 <see langword="null"/>。</summary>
    public EditorTabViewModel? ActiveTab
    {
        get
        {
            var current = Area.CurrentGroup.SelectedId;
            return current is null ? null : _tabsById.GetValueOrDefault(current);
        }
    }

    /// <summary>
    /// 打开一块内容。
    /// </summary>
    /// <param name="id">标签页 id。</param>
    /// <param name="title">显示名。</param>
    /// <param name="content">内容（宿主的 VM）。</param>
    /// <param name="isPreview">是不是预览页。</param>
    /// <returns>标签页 VM。</returns>
    public EditorTabViewModel Open(string id, string title, object? content = null, bool isPreview = false)
    {
        var vm = Resolve(id);
        vm.Title = title;
        if (content is not null)
        {
            vm.Content = content;
        }

        Area.Open(new EditorTab(id, title, IsPreview: isPreview));
        return vm;
    }

    /// <summary>
    /// 打开一块面板 —— 身份从面板自己来。
    /// </summary>
    /// <remarks>与工具窗口那边的 <c>Add(panel, …)</c> 同形。</remarks>
    /// <param name="panel">编辑区面板。</param>
    /// <param name="isPreview">是不是预览页。</param>
    /// <returns>标签页 VM。</returns>
    public EditorTabViewModel Open(IEditorPanel panel, bool isPreview = false)
    {
        ArgumentNullException.ThrowIfNull(panel);

        // ⚠ **图标必须在 Area.Open 之前设。**
        //    Area.Open 会触发 Changed，视图就在那一刻把标签页画出来 ——
        //    此时若 vm.Icon 还是 null，画出来就是没图标的；而事后给 VM 赋 Icon
        //    **不触发 Area.Changed**，视图不会回头再读。
        //    表现：新开的文档没图标，切一下文档（Select → Changed）图标才冒出来。
        var vm = Resolve(panel.Id);
        vm.Title = panel.Title;
        vm.Content = panel;
        vm.Icon = panel.Icon;

        Area.Open(new EditorTab(panel.Id, panel.Title, IsPreview: isPreview));
        return vm;
    }

    /// <summary>关掉一个标签页。</summary>
    /// <param name="id">标签页 id。</param>
    /// <returns>确实关掉了则为 <see langword="true"/>。</returns>
    public bool Close(string id) => Area.Close(id);

    /// <summary>
    /// 把一个标签页从这个编辑区**摘下来**（不当作关闭）。
    /// </summary>
    /// <remarks>
    /// 给"拖到另一个编辑区/浮出成独立窗口"用：标签的图标与内容都是**宿主的实例**
    /// （相机实时画面就在里头），搬家必须搬同一个对象，不能关掉再重建 ——
    /// 那等于把相机连接断了重连。
    /// <para>
    /// 因此这里绕开 <see cref="Close"/>：不发 <c>TabClosing</c> / <c>TabClosed</c>，
    /// 宿主不会以为这块内容该释放了。
    /// </para>
    /// </remarks>
    /// <param name="id">标签页 id。</param>
    /// <returns>摘下来的标签页 VM；这里没有这一页就是 <see langword="null"/>。</returns>
    public EditorTabViewModel? Detach(string id)
    {
        if (!_tabsById.TryGetValue(id, out var vm))
        {
            return null;
        }

        _detaching = id;
        try
        {
            Area.Close(id);
        }
        finally
        {
            _detaching = null;
        }

        _tabsById.Remove(id);
        return vm;
    }

    /// <summary>
    /// 把一个别处摘下来的标签页收进这个编辑区。
    /// </summary>
    /// <remarks>与 <see cref="Detach"/> 成对；标签的身份、图标、内容原样带过来。</remarks>
    /// <param name="tab">标签页 VM。</param>
    /// <param name="select">收进来之后要不要选中它。</param>
    /// <exception cref="ArgumentNullException"><paramref name="tab"/> 为 <see langword="null"/>。</exception>
    public void Attach(EditorTabViewModel tab, bool select = true)
    {
        ArgumentNullException.ThrowIfNull(tab);

        _tabsById[tab.Id] = tab;
        Area.Open(new EditorTab(tab.Id, tab.Title, IsPreview: tab.IsPreview), select);
    }

    private string? _detaching;

    /// <summary>选中一个标签页。</summary>
    /// <param name="id">标签页 id。</param>
    /// <returns>选中成功则为 <see langword="true"/>。</returns>
    public bool Select(string id) => Area.Select(id);

    /// <summary>拖拽重排：把一页挪到同组另一页的位置上。</summary>
    /// <param name="sourceId">被拖的那一页。</param>
    /// <param name="targetId">拖到谁的位置上。</param>
    /// <returns>确实挪动了则为 <see langword="true"/>。</returns>
    public bool ReorderTab(string sourceId, string targetId) => Area.ReorderTab(sourceId, targetId);

    /// <summary>把一页拖到某一组上，按落点分屏或并入。</summary>
    /// <param name="tabId">被拖的那一页。</param>
    /// <param name="target">落在哪一组上。</param>
    /// <param name="side">落在哪一侧。</param>
    /// <returns>落点生效则为 <see langword="true"/>。</returns>
    public bool DropTab(string tabId, EditorGroup target, EditorDropSide side) => Area.DropTab(tabId, target, side);

    /// <summary>把鼠标实际拖起的组中的标签落到目标组。</summary>
    public bool DropTab(string tabId, EditorGroup source, EditorGroup target, EditorDropSide side)
        => Area.DropTab(tabId, source, target, side);

    /// <summary>
    /// 从存档里恢复整个编辑区。
    /// </summary>
    /// <remarks>
    /// 存档里只有身份（id / 标题 / 钉住 / 预览），**没有内容** —— 内容是宿主的对象，
    /// 不可能塞进 XML。所以恢复分两步：先把树读回来，再让宿主按 id 把内容认领回去。
    /// <para>
    /// <paramref name="contentFactory"/> 返回 <see langword="null"/> 表示「这一页认不出来了」
    /// （文件被删了、插件卸了），这一页会被丢掉 —— 与上游一致：
    /// <c>EditorsSplitters.kt:1748-1768 · resolveFileOrLogError</c> 解析不到文件时
    /// 记一条 warn 然后返回 <c>null</c>，那一页就不再恢复。
    /// </para>
    /// </remarks>
    /// <param name="xml">存档根元素（<see cref="EditorLayoutSerializer"/> 写出来的）。</param>
    /// <param name="contentFactory">按 id 交回内容；不给则所有页都留着、内容为空。</param>
    /// <returns>丢掉的页 id。</returns>
    public IReadOnlyList<string> Restore(XElement xml, Func<string, object?>? contentFactory = null)
    {
        ArgumentNullException.ThrowIfNull(xml);

        EditorLayoutSerializer.Read(xml, Area);

        if (contentFactory is null)
        {
            return [];
        }

        var dropped = new List<string>();
        foreach (var group in Area.Groups.ToList())
        {
            foreach (var tab in group.Tabs.ToList())
            {
                var content = contentFactory(tab.Id);
                if (content is null)
                {
                    dropped.Add(tab.Id);
                    group.Close(tab.Id);
                    _tabsById.Remove(tab.Id);
                    continue;
                }

                var vm = Resolve(tab.Id);
                vm.Content = content;
                if (content is IEditorPanel panel)
                {
                    vm.Icon = panel.Icon;
                }
            }
        }

        // ⚠ 上面那一圈是**直接动 EditorGroup**（绕过 EditorArea）丢弃标签页的：
        //   内容工厂造不出内容的那些页就地关掉。整组都被丢光时树里就多一个空组，
        //   而这条路不经过 EditorArea 的任何方法，不变量补不上 ——
        //   表现是启动后编辑区里凭空一块空白窗格（宿主换了版本、某类文档不再认识时最容易撞上）。
        //   Refresh 就是为这种"我自己改过了"的调用方准备的。
        Area.Refresh();

        Sync();
        return dropped;
    }

    /// <summary>
    /// 双击一个标签页 —— 上游把三件事串在这一个手势上。
    /// </summary>
    /// <remarks>
    /// 照抄 <c>EditorTabbedContainer.kt:339-387 · doProcessDoubleClick</c> 的顺序：
    /// <list type="number">
    /// <item>这一页是<b>预览页</b> → 转正，<b>到此为止</b>（<c>:341-347</c>，不再最大化）；</item>
    /// <item>否则把它所在的那一组撑满 / 收回（<see cref="EditorArea.MaximizeOrNormalize"/>）。</item>
    /// </list>
    /// <para>
    /// 上游还有第三件事：<c>HideAllWindows</c>（把工具窗口全收起来）。它归工具窗口子系统管，
    /// 不在编辑区这一层 —— 宿主要的话自己接（两个高级设置 <c>editor.maximize.on.double.click</c> /
    /// <c>editor.maximize.in.splits.on.double.click</c> 各管一件，上游也是分开的）。
    /// </para>
    /// </remarks>
    /// <param name="id">被双击的标签页 id。</param>
    /// <returns>做了事返回 <see langword="true"/>；这一页不在编辑区里返回 <see langword="false"/>。</returns>
    public bool DoubleClick(string id)
    {
        var group = Area.Groups.FirstOrDefault(g => g.Tabs.Any(t => t.Id == id));
        if (group is null)
        {
            return false;
        }

        if (group.Tabs.First(t => t.Id == id).IsPreview)
        {
            group.PromoteFromPreview(id);
            Sync();
            return true;
        }

        Area.MaximizeOrNormalize(group);
        return true;
    }

    /// <summary>按 id 找标签页 VM。</summary>
    /// <param name="id">标签页 id。</param>
    /// <returns>VM；没有则 <see langword="null"/>。</returns>
    public EditorTabViewModel? Find(string id) => _tabsById.GetValueOrDefault(id);

    private EditorTabViewModel Resolve(string id)
    {
        if (!_tabsById.TryGetValue(id, out var vm))
        {
            vm = new EditorTabViewModel(id);
            _tabsById[id] = vm;
        }

        return vm;
    }

    private void Sync()
    {
        var wanted = new List<EditorGroupViewModel>();

        // Dock Factory.MoveDockable 的事务顺序：所有 source 先 detach，所有 target
        // 后 attach。不能边枚举最终组边各自 Reconcile；目标组通常排在源组前面，
        // 那会短暂让同一个 VM（以及宿主给它的 Control 图标）同时存在于两条 TabStrip。
        foreach (var vm in _groupsByModel.Values)
        {
            vm.PruneMovedDockables();
        }

        foreach (var group in Area.Groups)
        {
            if (!_groupsByModel.TryGetValue(group, out var vm))
            {
                vm = new EditorGroupViewModel(group);
                _groupsByModel[group] = vm;
            }

            vm.Sync(Resolve);
            vm.IsCurrent = ReferenceEquals(group, Area.CurrentGroup);
            wanted.Add(vm);
        }

        for (var i = _groups.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(_groups[i]))
            {
                _groupsByModel.Remove(_groups[i].Group);
                _groups.RemoveAt(i);
            }
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            var current = _groups.IndexOf(wanted[i]);
            if (current < 0)
            {
                _groups.Insert(i, wanted[i]);
            }
            else if (current != i)
            {
                _groups.Move(current, i);
            }
        }

        RaiseActiveTabChanged();
    }

    private void RaiseActiveTabChanged()
    {
        var active = ActiveTab;
        if (ReferenceEquals(active, _activeTab))
        {
            return;
        }

        _activeTab = active;
        ActiveTabChanged?.Invoke(this, active);
    }
}

/// <summary>
/// <see cref="EditorAreaViewModel.TabClosing"/> 的参数。
/// </summary>
/// <param name="tab">即将关闭的那一页。</param>
public sealed class EditorTabClosingViewModelEventArgs(EditorTabViewModel tab) : EventArgs
{
    /// <summary>即将关闭的那一页。</summary>
    public EditorTabViewModel Tab { get; } = tab;

    /// <summary>设成 <see langword="true"/> 拦下这次关闭。</summary>
    public bool Cancel { get; set; }
}
