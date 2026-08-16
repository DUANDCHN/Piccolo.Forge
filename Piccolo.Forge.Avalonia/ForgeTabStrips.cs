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
        ItemsPanel = new FuncTemplate<Panel?>(() => new HeaderTabsPanel());
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
    internal ToolWindowContentManager? Manager { get; set; }
    internal bool IsWindowActive { get; set; }

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
