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
        Area.TabClosed += (_, e) =>
        {
            // 关掉的那一页从缓存里摘掉 —— 不摘的话宿主塞的内容（相机会话、图像帧）
            // 会被这个字典一直强引用着，Dispose 了也回收不掉。
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

        var vm = Open(panel.Id, panel.Title, panel, isPreview);
        vm.Icon = panel.Icon;
        return vm;
    }

    /// <summary>关掉一个标签页。</summary>
    /// <param name="id">标签页 id。</param>
    /// <returns>确实关掉了则为 <see langword="true"/>。</returns>
    public bool Close(string id) => Area.Close(id);

    /// <summary>选中一个标签页。</summary>
    /// <param name="id">标签页 id。</param>
    /// <returns>选中成功则为 <see langword="true"/>。</returns>
    public bool Select(string id)
    {
        foreach (var group in Area.Groups)
        {
            if (group.Select(id))
            {
                Sync();
                return true;
            }
        }

        return false;
    }

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
