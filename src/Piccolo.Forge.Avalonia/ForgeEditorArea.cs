using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 编辑区：一棵二叉分屏树，叶子是一组标签页。
/// </summary>
/// <remarks>
/// 与 <see cref="ForgeToolWindowFrame"/> 同一做法：**同步不重建**。
/// 标签页控件与内容宿主按 id 缓存复用，开合别的标签页不会把别人的内容重造。
/// </remarks>
public class ForgeEditorArea : TemplatedControl
{
    // ★ 键是 (组, 标签页 id) 而不是单纯 id。
    //   分屏之后**同一个标签页 id 会同时存在于两个组**（上游就是这样：
    //   split 只是在新组里也打开这一个，原组不动，EditorWindow.kt:570-571），
    //   一个控件不能同时挂在两个父容器下。
    private readonly Dictionary<(EditorGroupViewModel Group, string Id), ForgeEditorTab> _tabs = [];
    private readonly Dictionary<(EditorGroupViewModel Group, string Id), ContentControl> _contents = [];
    private readonly Dictionary<EditorGroupViewModel, Control> _groupHosts = [];
    private readonly Dictionary<EditorGroupViewModel, HeaderTabsPanel> _groupStrips = [];
    private readonly Dictionary<EditorGroupViewModel, ContentControl> _groupBodies = [];
    private readonly Decorator _root = new();

    private EditorAreaViewModel? _area;

    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeEditorArea, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    static ForgeEditorArea()
    {
        PaletteProperty.Changed.AddClassHandler<ForgeEditorArea>((x, _) => x.Sync());
    }

    /// <summary>建一个编辑区控件。</summary>
    public ForgeEditorArea()
    {
        VisualChildren.Add(_root);
        LogicalChildren.Add(_root);
    }

    /// <summary>配色。</summary>
    public ForgePalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>接上的编辑区 VM。</summary>
    public EditorAreaViewModel? Area => _area;

    /// <summary>接上一个编辑区 VM。</summary>
    /// <param name="area">编辑区 VM。</param>
    public void Attach(EditorAreaViewModel area)
    {
        ArgumentNullException.ThrowIfNull(area);

        _area = area;
        area.Area.Changed += (_, _) => Sync();
        Sync();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        _root.Measure(availableSize);
        return _root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        _root.Arrange(new Rect(finalSize));
        return finalSize;
    }

    private void Sync()
    {
        if (_area is null)
        {
            return;
        }

        _root.Child = BuildNode(_area.Area.Root);
    }

    /// <summary>
    /// 把分屏树递归变成控件树。
    /// </summary>
    /// <remarks>
    /// 每个 <see cref="EditorSplit"/> 变一个两格的 <see cref="Grid"/>，
    /// 中间一条可拖的分隔条；叶子变一组标签页。
    /// **叶子控件是缓存复用的**，所以重排分屏树不会重造内容。
    /// </remarks>
    private Control BuildNode(EditorNode node)
    {
        if (node is EditorSplit split)
        {
            var grid = new Grid();
            var first = BuildNode(split.First);
            var second = BuildNode(split.Second);
            var splitter = new GridSplitter { Background = new SolidColorBrush(Palette.WindowBorder) };

            if (split.IsVertical)
            {
                grid.RowDefinitions.Add(new RowDefinition(split.Proportion, GridUnitType.Star));
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                grid.RowDefinitions.Add(new RowDefinition(1 - split.Proportion, GridUnitType.Star));
                splitter.Height = ForgeMetrics.SplitDividerWidth;
                splitter.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
                Grid.SetRow(first, 0);
                Grid.SetRow(splitter, 1);
                Grid.SetRow(second, 2);
            }
            else
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(split.Proportion, GridUnitType.Star));
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                grid.ColumnDefinitions.Add(new ColumnDefinition(1 - split.Proportion, GridUnitType.Star));
                splitter.Width = ForgeMetrics.SplitDividerWidth;
                splitter.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch;
                Grid.SetColumn(first, 0);
                Grid.SetColumn(splitter, 1);
                Grid.SetColumn(second, 2);
            }

            grid.Children.Add(first);
            grid.Children.Add(splitter);
            grid.Children.Add(second);
            return grid;
        }

        var group = (EditorGroup)node;
        var vm = _area!.Groups.First(x => ReferenceEquals(x.Group, group));
        return BuildGroup(vm);
    }

    private Control BuildGroup(EditorGroupViewModel vm)
    {
        if (!_groupHosts.TryGetValue(vm, out var host))
        {
            var strip = new HeaderTabsPanel();
            DockPanel.SetDock(strip, Dock.Top);
            _groupStrips[vm] = strip;

            var body = new ContentControl();
            _groupBodies[vm] = body;

            var panel = new DockPanel();
            panel.Children.Add(strip);
            panel.Children.Add(body);

            host = new Border { Child = panel };
            _groupHosts[vm] = host;
        }

        ((Border)host).Background = new SolidColorBrush(Palette.Background);
        SyncStrip(vm, _groupStrips[vm]);

        // 选中的那一页的内容挂到组的正文槽上
        _groupBodies[vm].Content = vm.Selected is { } selected ? GetContentHost(vm, selected) : null;
        return host;
    }

    private void SyncStrip(EditorGroupViewModel vm, HeaderTabsPanel strip)
    {
        var wanted = new List<Control>();

        foreach (var tab in vm.Tabs)
        {
            var key = (vm, tab.Id);
            if (!_tabs.TryGetValue(key, out var control))
            {
                control = new ForgeEditorTab();
                var id = tab.Id;
                control.PointerPressed += (_, e) =>
                {
                    var point = e.GetPosition(control);
                    if (control.CanBeClosed && point.X >= control.Bounds.Width - control.IconAreaWidth)
                    {
                        _area!.Close(id);
                        return;
                    }

                    _area!.Select(id);
                };

                _tabs[key] = control;
            }

            control.Palette = Palette;
            control.Title = tab.Title;
            control.IsSelected = tab.IsSelected;
            control.IsGroupActive = vm.IsCurrent;
            control.IsPinned = tab.IsPinned;
            control.CanBeClosed = tab.IsCloseable && !tab.IsPinned;
            wanted.Add(control);
        }

        Reconcile(strip, wanted);
    }

    /// <summary>
    /// 取（必要时新建）某一组里某个标签页的内容宿主。
    /// </summary>
    /// <remarks>
    /// 每组一份宿主 —— 同一个标签页可以同时开在两个分屏里。
    /// <para>
    /// ⚠ 因此 <see cref="EditorTabViewModel.Content"/> **应当放 VM 而不是控件**：
    /// 一个 Avalonia 控件不能同时挂在两个父容器下，放控件的话第二个分屏会拿不到它。
    /// 放 VM 则由宿主的 DataTemplate 各渲染一份，这也是上游的做法
    /// （每个 EditorWindow 给同一文件各建一个 composite）。
    /// </para>
    /// </remarks>
    private ContentControl GetContentHost(EditorGroupViewModel group, EditorTabViewModel tab)
    {
        var key = (group, tab.Id);
        if (!_contents.TryGetValue(key, out var host))
        {
            host = new ContentControl();
            _contents[key] = host;
        }

        host.Content = tab.Content;
        return host;
    }

    private static void Reconcile(Panel panel, IReadOnlyList<Control> wanted)
    {
        for (var i = panel.Children.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(panel.Children[i]))
            {
                panel.Children.RemoveAt(i);
            }
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            var current = panel.Children.IndexOf(wanted[i]);
            if (current < 0)
            {
                panel.Children.Insert(i, wanted[i]);
            }
            else if (current != i)
            {
                panel.Children.Move(current, i);
            }
        }
    }
}
