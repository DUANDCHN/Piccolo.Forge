using System.Collections.ObjectModel;
using System.ComponentModel;
using Piccolo.Forge.Model;

namespace Piccolo.Forge.Mvvm;

/// <summary>
/// 整个工具窗口外壳的 VM：宿主接的就是这个。
/// </summary>
/// <remarks>
/// 它把 <see cref="ToolWindowManager"/> 的状态投影成可绑定的集合。
/// <para>
/// **投影是单向的**：位置与可见性的真相在模型层，这里只跟着变。
/// 想改就调 <see cref="Open"/> / <see cref="Close"/> / <see cref="MoveTo"/>，
/// 它们最终都落到 <see cref="Manager"/> 上。
/// </para>
/// </remarks>
public sealed class ToolWindowShellViewModel : ForgeObservableObject
{
    private readonly Dictionary<string, ToolWindowViewModel> _byId = new(StringComparer.Ordinal);
    private readonly ObservableCollection<ToolWindowViewModel> _toolWindows = [];
    private readonly Dictionary<(StripeSide Side, StripeGroup Group), ObservableCollection<ToolWindowViewModel>> _groups = [];

    private bool _showToolWindowNames;

    /// <summary>用一个管理器建外壳 VM。</summary>
    /// <param name="manager">工具窗口管理器；不给就按新 UI 的默认布局建一个空的。</param>
    public ToolWindowShellViewModel(ToolWindowManager? manager = null)
    {
        Manager = manager ?? new ToolWindowManager(new DesktopLayout());
        Manager.StateChanged += (_, _) => Sync();

        foreach (var side in new[] { StripeSide.Left, StripeSide.Right })
        {
            foreach (var group in new[] { StripeGroup.Top, StripeGroup.Bottom })
            {
                _groups[(side, group)] = [];
            }
        }

        ToolWindows = new ReadOnlyObservableCollection<ToolWindowViewModel>(_toolWindows);

        foreach (var info in Manager.Layout.GetSortedList())
        {
            Track(new ToolWindowViewModel(info.Id, info.Id));
        }

        Sync();
    }

    /// <summary>底下那个管理器。</summary>
    public ToolWindowManager Manager { get; }

    /// <summary>所有工具窗口。</summary>
    public ReadOnlyObservableCollection<ToolWindowViewModel> ToolWindows { get; }

    /// <summary>条带上是否在图标下面显示名字。</summary>
    public bool ShowToolWindowNames
    {
        get => _showToolWindowNames;
        set => SetProperty(ref _showToolWindowNames, value);
    }

    /// <summary>某个工具窗口的状态变了（开合、位置、活跃）。</summary>
    public event EventHandler<ToolWindowStateChangedEventArgs>? StateChanged
    {
        add => Manager.StateChanged += value;
        remove => Manager.StateChanged -= value;
    }

    /// <summary>
    /// 登记一个工具窗口。
    /// </summary>
    /// <remarks>
    /// 位置用 <c>(anchor, isSplit)</c> 表达 —— **不是「四个角」**。
    /// 两者的区别是整个 Forge 项目的起点，见 <c>工具窗口模型.md</c> §1。
    /// </remarks>
    /// <param name="id">工具窗口 id。</param>
    /// <param name="title">显示名。</param>
    /// <param name="anchor">停靠边。</param>
    /// <param name="isSplit">是不是同一条边的另一半。</param>
    /// <param name="icon">条带按钮上的图标。</param>
    /// <returns>建好的 VM。</returns>
    public ToolWindowViewModel Add(
        string id,
        string title,
        ToolWindowAnchor anchor,
        bool isSplit = false,
        object? icon = null)
    {
        return AddCore(id, title, anchor, isSplit, icon, panel: null);
    }

    /// <summary>
    /// 登记一块工具面板 —— **身份从面板自己来**。
    /// </summary>
    /// <remarks>
    /// 这是推荐的登记方式：面板继承 <c>ToolWindowPanel</c> 之后自带
    /// <see cref="IForgePanel.Id"/> / <see cref="IForgePanel.Title"/> / <see cref="IForgePanel.Icon"/>，
    /// 宿主不必再把同样的东西说第二遍。
    /// <para>
    /// 面板的 <c>Title</c> / <c>Icon</c> 改了会**自动跟到**条带按钮与标题条上
    /// （面板实现了 <see cref="INotifyPropertyChanged"/> 时）—— 切语言只改一处。
    /// </para>
    /// <para>
    /// 参数里只剩「停哪条边、哪一半」：**按钮画在哪根条带是算出来的**，
    /// 见 <see cref="StripePlacementResolver.Resolve"/>。
    /// </para>
    /// </remarks>
    /// <param name="panel">面板本身，同时是内容。</param>
    /// <param name="anchor">停靠边。</param>
    /// <param name="isSplit">是不是同一条边的另一半。</param>
    /// <returns>建好的 VM。</returns>
    public ToolWindowViewModel Add(IToolWindowPanel panel, ToolWindowAnchor anchor, bool isSplit = false)
    {
        ArgumentNullException.ThrowIfNull(panel);
        return AddCore(panel.Id, panel.Title, anchor, isSplit, panel.Icon, panel);
    }

    private ToolWindowViewModel AddCore(
        string id,
        string title,
        ToolWindowAnchor anchor,
        bool isSplit,
        object? icon,
        IForgePanel? panel)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var order = Manager.Layout.GetNextOrder(WindowInfo.DefaultPaneId, anchor);
        Manager.Layout.SetInfo(new WindowInfo
        {
            Id = id,
            Anchor = anchor,
            IsSplit = isSplit,
            Order = order,
        });

        var vm = new ToolWindowViewModel(id, title) { Icon = icon };

        if (panel is not null)
        {
            vm.Content = panel;

            // 面板的 Title / Icon 改了自动跟过来，宿主不用手工同步两份
            if (panel is INotifyPropertyChanged observable)
            {
                observable.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is null or nameof(IForgePanel.Title))
                    {
                        vm.Title = panel.Title;
                    }

                    if (e.PropertyName is null or nameof(IForgePanel.Icon))
                    {
                        vm.Icon = panel.Icon;
                    }
                };
            }
        }

        Track(vm);
        Sync();
        return vm;
    }

    /// <summary>按 id 找，没有就 <see langword="null"/>。</summary>
    /// <param name="id">工具窗口 id。</param>
    /// <returns>VM。</returns>
    public ToolWindowViewModel? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>
    /// 某根条带某一组里该显示哪些按钮。
    /// </summary>
    /// <remarks>
    /// 集合是活的：位置一变，元素自己在组之间挪，UI 只要绑一次。
    /// 次序按 <see cref="StripePlacementResolver.CreateButtonLayoutComparer"/>。
    /// </remarks>
    /// <param name="side">哪根条带。</param>
    /// <param name="group">哪一组。</param>
    /// <returns>该组的工具窗口。</returns>
    public ReadOnlyObservableCollection<ToolWindowViewModel> GetGroup(StripeSide side, StripeGroup group)
        => new(_groups[(side, group)]);

    /// <summary>打开（并激活）一个工具窗口。</summary>
    /// <param name="id">工具窗口 id。</param>
    public void Open(string id) => Manager.Activate(id);

    /// <summary>关掉一个工具窗口。</summary>
    /// <param name="id">工具窗口 id。</param>
    /// <param name="removeFromStripe">是否连条带按钮一起摘掉。</param>
    public void Close(string id, bool removeFromStripe = false)
        => Manager.Hide(id, hideSide: false, removeFromStripe: removeFromStripe);

    /// <summary>把一个工具窗口挪到别处。</summary>
    /// <param name="id">工具窗口 id。</param>
    /// <param name="anchor">目标边。</param>
    /// <param name="isSplit">目标是哪一半。</param>
    public void MoveTo(string id, ToolWindowAnchor anchor, bool isSplit)
        => Manager.MoveTo(id, anchor, order: -1, isSplit: isSplit);

    /// <summary>
    /// 某条边上现在显示的是哪个。
    /// </summary>
    /// <remarks>「一格一个可见窗口」，所以同一 <c>(anchor, isSplit)</c> 最多一个。</remarks>
    /// <param name="anchor">停靠边。</param>
    /// <param name="isSplit">哪一半。</param>
    /// <returns>正在显示的那个；没有则 <see langword="null"/>。</returns>
    public ToolWindowViewModel? OpenOn(ToolWindowAnchor anchor, bool isSplit = false)
        => _toolWindows.FirstOrDefault(x => x.IsOpen && x.Anchor == anchor && x.IsSplit == isSplit);

    private void Track(ToolWindowViewModel vm)
    {
        _byId[vm.Id] = vm;
        _toolWindows.Add(vm);
    }

    /// <summary>
    /// 把模型的状态刷进 VM。
    /// </summary>
    /// <remarks>
    /// **只改属性和集合成员，不重建任何 VM** —— 这是这一层存在的理由：
    /// 宿主塞进 <see cref="ToolWindowViewModel.Content"/> 的东西
    /// （相机实时画面、图表、日志）不能因为开关了别的面板就被重造一遍。
    /// </remarks>
    private void Sync()
    {
        var active = Manager.LastActiveToolWindowId;

        foreach (var vm in _toolWindows)
        {
            if (Manager.Layout.GetInfo(vm.Id) is { } info)
            {
                vm.SyncFrom(info, isActive: string.Equals(active, vm.Id, StringComparison.Ordinal) && info.IsVisible);
            }
        }

        SyncGroups();
    }

    private void SyncGroups()
    {
        foreach (var ((side, group), target) in _groups)
        {
            var anchor = group == StripeGroup.Bottom
                ? ToolWindowAnchor.Bottom
                : side == StripeSide.Left ? ToolWindowAnchor.Left : ToolWindowAnchor.Right;

            var wanted = _toolWindows
                .Where(x => (x.IsShowStripeButton || x.IsOpen) && x.Anchor != ToolWindowAnchor.Top)
                .Where(x => x.Placement == new StripePlacement(side, group))
                .Select(x => (Vm: x, Info: Manager.Layout.GetInfo(x.Id)!))
                .OrderBy(x => x.Info, StripePlacementResolver.CreateButtonLayoutComparer(anchor))
                .Select(x => x.Vm)
                .ToList();

            Reconcile(target, wanted);
        }
    }

    /// <summary>
    /// 把目标集合调成想要的样子，**只动有差异的位置**。
    /// </summary>
    /// <remarks>整个清空再填会让绑定到它的控件全部重建，正是要避免的。</remarks>
    private static void Reconcile(
        ObservableCollection<ToolWindowViewModel> target,
        IReadOnlyList<ToolWindowViewModel> wanted)
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
            var item = wanted[i];
            var current = target.IndexOf(item);

            if (current < 0)
            {
                target.Insert(i, item);
            }
            else if (current != i)
            {
                target.Move(current, i);
            }
        }
    }
}
