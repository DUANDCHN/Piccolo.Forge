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
            }

            _manager = value;

            if (_manager is not null)
            {
                _manager.SelectionChanged += OnManagerSelectionChanged;
            }
        }
    }

    private ToolWindowContentManager? _manager;

    private void OnManagerSelectionChanged(object? sender, EventArgs e) => SyncSelection();

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
