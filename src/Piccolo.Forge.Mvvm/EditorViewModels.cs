using System.Collections.ObjectModel;
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

    /// <summary>建一个编辑区 VM。</summary>
    /// <param name="area">底下的模型；不给就新建一个空的。</param>
    public EditorAreaViewModel(EditorArea? area = null)
    {
        Area = area ?? new EditorArea();
        Area.Changed += (_, _) => Sync();
        Groups = new ReadOnlyObservableCollection<EditorGroupViewModel>(_groups);
        Sync();
    }

    /// <summary>底下那个模型。</summary>
    public EditorArea Area { get; }

    /// <summary>所有标签页组。</summary>
    public ReadOnlyObservableCollection<EditorGroupViewModel> Groups { get; }

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
    }
}
