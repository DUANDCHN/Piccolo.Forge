using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>A Forge editor tab row backed by Avalonia's item-container lifecycle.</summary>
internal sealed class ForgeEditorTabStrip : TabStrip
{
    public ForgeEditorTabStrip()
    {
        // 文档标签放不下就**换行**，一个都不藏：文档是用户自己开的，
        // 藏起来就等于找不回来（标签行没有「更多」弹窗，也没有滚动箭头）。
        ItemsPanel = new FuncTemplate<Panel?>(() => new HeaderTabsPanel { WrapRows = true });
        Template = ForgeTabStripTemplate.Create<ForgeEditorTabStrip>();
    }

    public ForgePalette Palette { get; set; } = ForgePalette.ExpUiDark;

    public bool IsGroupActive { get; set; }

    public Func<ForgeEditorTab, object?, object?>? ResolveIcon { get; set; }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new ForgeEditorTab();

    protected override void PrepareContainerForItemOverride(Control element, object? item, int index)
    {
        base.PrepareContainerForItemOverride(element, item, index);
        if (element is ForgeEditorTab tab && item is EditorTabViewModel vm)
        {
            tab.Palette = Palette;
            tab.Title = vm.Title;
            tab.Icon = ResolveIcon?.Invoke(tab, vm.Icon) ?? vm.Icon;
            tab.IsSelected = vm.IsSelected;
            tab.IsGroupActive = IsGroupActive;
            tab.IsPinned = vm.IsPinned;
            tab.CanBeClosed = vm.IsCloseable && !vm.IsPinned;
            tab.DataContext = vm;
        }
    }

    protected override void ClearContainerForItemOverride(Control element)
    {
        if (element is ForgeEditorTab tab)
        {
            // A host may supply a Control as the icon. Release it before another
            // container adopts the same VM (for example when moving groups).
            tab.Icon = null;
            tab.DataContext = null;
        }

        base.ClearContainerForItemOverride(element);
    }
}

/// <summary>A tool-window content tab row backed by Avalonia's item-container lifecycle.</summary>
public sealed class ForgeContentTabStrip : TabStrip
{
    /// <summary>Creates a self-templated content tab row.</summary>
    public ForgeContentTabStrip()
    {
        ItemsPanel = new FuncTemplate<Panel?>(() => new HeaderTabsPanel());
        Template = ForgeTabStripTemplate.Create<ForgeContentTabStrip>();
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);

        // 拖着换次序：与文档标签同一套手感（越过目标就换位），只是这里不跨容器 ——
        // 内容 tab 只属于这一个工具窗口，没有"拖到别的窗口去"这回事。
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, (_, _) => EndDrag(), RoutingStrategies.Direct, handledEventsToo: true);
    }

    private string? _dragId;
    private Point _dragOrigin;
    private bool _dragging;
    private int? _dropIndex;

    /// <summary>
    /// 拖动中：画出**要插到哪条缝**，不当场换位。
    /// </summary>
    /// <remarks>
    /// ★ <b>这是有意偏离上游</b>：上游 expUI 拖着就直接换位、不画任何提示
    /// （<c>JBTabsImpl.kt:3331 · reallocate</c>）。实机反馈是"不直观" ——
    /// 边拖边跳位时，眼睛要在动来动去的一排里找自己那块；画一条缝则先说清"松手会落在哪"。
    /// <para>
    /// 起拖死区 <see cref="ForgeMetrics.StripeButtonDragStartDeadzone"/> = 7：
    /// 按下时手抖几像素不该被当成拖（条带按钮那次"有时要点两下"就是死区没设好）。
    /// </para>
    /// </remarks>
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragId is null)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (!_dragging)
        {
            var moved = Math.Abs(position.X - _dragOrigin.X) + Math.Abs(position.Y - _dragOrigin.Y);
            if (moved < ForgeMetrics.StripeButtonDragStartDeadzone)
            {
                return;
            }

            _dragging = true;
        }

        var gap = GapAt(position);
        _dropIndex = gap;
        ShowDragPreview(position, gap);
    }

    /// <summary>松手：落到画出来的那条缝上。</summary>
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging && _dragId is { } id && _dropIndex is { } index && Manager is { } manager)
        {
            manager.MoveTo(id, index);
        }

        EndDrag();
    }

    private void EndDrag()
    {
        ClearDragPreview();
        _dragId = null;
        _dragging = false;
        _dropIndex = null;
    }

    /// <summary>指针落在第几条缝里（0 = 最前面，ItemCount = 最后面）。</summary>
    /// <remarks>按每块 tab 的**中线**分界：过了中线就算落到它后面 —— 与画出来的竖条同一个判据。</remarks>
    private int GapAt(Point position)
    {
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is not ForgeContentTab tab)
            {
                continue;
            }

            if (position.X < tab.Bounds.Center.X)
            {
                return i;
            }
        }

        return ItemCount;
    }

    /// <summary>
    /// 拖动中的样子：被拖的那一块**半透明跟手**，行内在落点处留一个**空档**。
    /// </summary>
    /// <remarks>
    /// 与文档标签同一套（<c>HeaderTabsPanel.DraggedChild / DraggedX / DropIndex</c>，
    /// 对位上游 <c>JBTabsImpl.kt:2280-2294 · moveDraggedTabLabel</c>）——
    /// 两条 tab 行在同一个界面里，插入提示不该是两种语言。
    /// <para>
    /// 早先这里画的是一条插入竖条：单看没问题，但与文档标签那套摆在一起就是两套设计。
    /// </para>
    /// </remarks>
    private void ShowDragPreview(Point position, int gap)
    {
        if (ItemsPanelRoot is not HeaderTabsPanel panel
            || _dragId is not { } id
            || IndexOf(id) is not { } index
            || ContainerFromIndex(index) is not ForgeContentTab tab)
        {
            return;
        }

        panel.DraggedChild = tab;
        panel.DraggedX = position.X - (tab.Bounds.Width / 2);
        panel.DropIndex = gap > index ? gap - 1 : gap;   // 空档按"除它之外"的次序算
        panel.InvalidateArrange();
    }

    /// <summary>收掉拖动中的样子。</summary>
    private void ClearDragPreview()
    {
        if (ItemsPanelRoot is not HeaderTabsPanel panel)
        {
            return;
        }

        if (panel.DraggedChild is { } dragged)
        {
            dragged.Opacity = 1;
            dragged.ZIndex = 0;
        }

        panel.DraggedChild = null;
        panel.DropIndex = null;
        panel.InvalidateArrange();
    }

    /// <summary>这一块在当前次序里排第几。</summary>
    private int? IndexOf(string id)
    {
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is ForgeContentTab tab
                && tab.DataContext is ToolWindowContent content
                && string.Equals(content.Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return null;
    }

    internal ForgePalette Palette { get; set; } = ForgePalette.ExpUiDark;
    internal bool IsWindowActive { get; set; }

    /// <summary>
    /// 这条 tab 行背后的内容管理器。
    /// </summary>
    /// <remarks>
    /// ⚠ <b>换人时要接/断它的选中通知</b>：点一下 tab 只改**模型**
    /// （<see cref="OnPointerPressed"/> 里调 <c>Manager.Select</c>，并且 <c>e.Handled = true</c>，
    /// 所以 <see cref="TabStrip"/> 自带的选中逻辑根本不跑）。模型变了没人回来刷视图的话，
    /// 高亮就停在原处 —— 实机表现是"点了没反应"（其实模型已经切了）。
    /// <para>
    /// 外框那边的 <c>SyncTabs</c> 只在**工具窗口的状态**变化时跑，而"选了哪块内容"不属于那类，
    /// 指望它补刷是指望不上的。
    /// </para>
    /// </remarks>
    internal ToolWindowContentManager? Manager
    {
        get => _manager;
        set
        {
            if (ReferenceEquals(_manager, value))
            {
                return;
            }

            if (_manager is not null)
            {
                _manager.SelectionChanged -= OnManagerSelectionChanged;
                _manager.Reordered -= OnManagerReordered;
            }

            _manager = value;

            if (_manager is not null)
            {
                _manager.SelectionChanged += OnManagerSelectionChanged;
                _manager.Reordered += OnManagerReordered;
            }
        }
    }

    private ToolWindowContentManager? _manager;

    private void OnManagerSelectionChanged(object? sender, EventArgs e) => SyncSelection();

    /// <summary>次序变了：换一份 ItemsSource 让容器重排，再把选中刷回去。</summary>
    /// <remarks>
    /// 直接改 <c>ItemsSource</c> 而不是原地挪容器：这条 tab 行的容器由 Avalonia 的
    /// item-container 生命周期管，手工挪会与它打架（文档标签那边的"拖影卡住"就是这么来的）。
    /// </remarks>
    private void OnManagerReordered(object? sender, EventArgs e)
    {
        if (Manager is not { } manager)
        {
            return;
        }

        ItemsSource = manager.Contents.ToArray();
        SyncSelection();
    }

    /// <summary>把"选中的是哪块"刷到已经实现出来的那些 tab 上。</summary>
    /// <remarks>
    /// <see cref="PrepareContainerForItemOverride"/> 只在容器**新建/复用**那一刻设一次，
    /// 之后再改选中它不会回头来问 —— 所以这里要挨个刷。
    /// </remarks>
    private void SyncSelection()
    {
        if (Manager is not { } manager)
        {
            return;
        }

        SelectedItem = manager.SelectedContent;

        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is ForgeContentTab tab && tab.DataContext is ToolWindowContent content)
            {
                tab.IsSelected = manager.SelectedContent?.Id == content.Id;
                tab.IsSoleContent = manager.IsSoleContent;
            }
        }
    }

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(Control element, object? item, int index)
    {
        base.PrepareContainerForItemOverride(element, item, index);
        if (element is ForgeContentTab tab && item is ToolWindowContent content && Manager is { } manager)
        {
            tab.Palette = Palette;
            tab.Title = content.DisplayName;
            tab.IsSelected = manager.SelectedContent?.Id == content.Id;
            tab.IsWindowActive = IsWindowActive;
            tab.IsSoleContent = manager.IsSoleContent;
            tab.CanBeClosed = manager.CanBeClosed(content);
            tab.IsPinned = content.IsPinned;
            tab.DataContext = content;
        }
    }

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new ForgeContentTab();

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Manager is not { } manager || e.Source is not Visual source
            || source.FindAncestorOfType<ForgeContentTab>(true) is not
                { DataContext: ToolWindowContent content } tab)
        {
            return;
        }

        var point = e.GetPosition(tab);
        if (tab.CanBeClosed && point.X >= tab.Bounds.Width - tab.IconAreaWidth)
        {
            manager.InvokeIconAction(content.Id, e.KeyModifiers.HasFlag(KeyModifiers.Alt));
        }
        else
        {
            manager.Select(content.Id);

            // 记住"按下的是谁"——移动超过死区才真的开始拖（见 OnPointerMoved）
            _dragId = content.Id;
            _dragOrigin = e.GetPosition(this);
            _dragging = false;
            _dropIndex = null;
        }

        e.Handled = true;
    }
}

internal static class ForgeTabStripTemplate
{
    public static FuncControlTemplate<T> Create<T>() where T : TabStrip
        => new((control, scope) =>
        {
            var presenter = new ItemsPresenter
            {
                Name = "PART_ItemsPresenter",
                ItemsPanel = control.ItemsPanel,
            };
            scope.Register("PART_ItemsPresenter", presenter);
            return presenter;
        });
}
