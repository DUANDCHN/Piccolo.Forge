using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Piccolo.Forge.Model;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 把左右两根条带、停靠区和编辑区拼在一起，并跟 <see cref="ToolWindowManager"/> 联动。
/// </summary>
/// <remarks>
/// 对应 <c>ToolWindowPaneNewButtonManager.kt:58-68 · wrapWithControls</c>：
/// 条带在 WEST / EAST，pane 在 CENTER。
/// <para>
/// 按钮落位不在这里算，走 <see cref="StripePlacementResolver"/>；
/// 组内次序与分隔符走 <see cref="StripeGroupLayout"/>。
/// </para>
/// </remarks>
public class ForgeToolWindowFrame : TemplatedControl
{
    private readonly ForgeToolWindowToolbar _left = new() { Side = StripeSide.Left };
    private readonly ForgeToolWindowToolbar _right = new() { Side = StripeSide.Right };
    private readonly ForgeDockPane _pane = new();
    private readonly DockPanel _root = new();
    private readonly Dictionary<string, Control> _content = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ToolWindowContentManager> _contentManagers = new(StringComparer.Ordinal);

    private ToolWindowManager? _manager;
    private string? _dragId;
    private Point _dragStart;

    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeToolWindowFrame, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    /// <summary><see cref="MoreButtonSide"/> 的属性定义。</summary>
    public static readonly StyledProperty<StripeSide> MoreButtonSideProperty =
        AvaloniaProperty.Register<ForgeToolWindowFrame, StripeSide>(nameof(MoreButtonSide));

    /// <summary><see cref="ShowToolWindowNames"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> ShowToolWindowNamesProperty =
        AvaloniaProperty.Register<ForgeToolWindowFrame, bool>(nameof(ShowToolWindowNames));

    /// <summary><see cref="EditorContent"/> 的属性定义。</summary>
    public static readonly StyledProperty<Control?> EditorContentProperty =
        AvaloniaProperty.Register<ForgeToolWindowFrame, Control?>(nameof(EditorContent));

    static ForgeToolWindowFrame()
    {
        PaletteProperty.Changed.AddClassHandler<ForgeToolWindowFrame>((f, _) => f.Rebuild());
        EditorContentProperty.Changed.AddClassHandler<ForgeToolWindowFrame>((f, _) => f.Rebuild());
        ShowToolWindowNamesProperty.Changed.AddClassHandler<ForgeToolWindowFrame>((f, _) => f.Rebuild());
        MoreButtonSideProperty.Changed.AddClassHandler<ForgeToolWindowFrame>((f, _) => f.Rebuild());
    }

    /// <summary>建一个框架。</summary>
    public ForgeToolWindowFrame()
    {
        DockPanel.SetDock(_left, Dock.Left);
        DockPanel.SetDock(_right, Dock.Right);
        _root.Children.Add(_left);
        _root.Children.Add(_right);
        _root.Children.Add(_pane);
        VisualChildren.Add(_root);
        LogicalChildren.Add(_root);
    }

    /// <summary>配色。</summary>
    public ForgePalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>
    /// 「更多」按钮显示在哪一边。
    /// </summary>
    /// <remarks>
    /// 同一时刻只有一根条带显示它，默认 LEFT
    /// （<c>ToolWindowManagerState.kt:60</c>、<c>MoreSquareStripeButton.kt:77-79</c>）。
    /// </remarks>
    public StripeSide MoreButtonSide
    {
        get => GetValue(MoreButtonSideProperty);
        set => SetValue(MoreButtonSideProperty, value);
    }

    /// <summary>
    /// 条带上是否在图标下面显示名字。
    /// </summary>
    /// <remarks>
    /// 对应 `UISettings.showToolWindowsNames`，默认 false（<c>UISettingsState.kt:83</c>）。
    /// 打开时条带宽度变成 59（<c>ResizeStripeManager.kt:215-218</c>）。
    /// </remarks>
    public bool ShowToolWindowNames
    {
        get => GetValue(ShowToolWindowNamesProperty);
        set => SetValue(ShowToolWindowNamesProperty, value);
    }

    /// <summary>中间编辑区放什么。</summary>
    public Control? EditorContent
    {
        get => GetValue(EditorContentProperty);
        set => SetValue(EditorContentProperty, value);
    }

    /// <summary>左条带。</summary>
    public ForgeToolWindowToolbar LeftToolbar => _left;

    /// <summary>右条带。</summary>
    public ForgeToolWindowToolbar RightToolbar => _right;

    /// <summary>停靠区。</summary>
    public ForgeDockPane DockPane => _pane;

    /// <summary>
    /// 取（必要时新建）某个工具窗口的内容管理器。
    /// </summary>
    /// <remarks>一个工具窗口可以装多块内容，标题条上排成 tab（<c>工具窗口模型.md</c> §13）。</remarks>
    /// <param name="id">工具窗口 id。</param>
    /// <returns>内容管理器。</returns>
    public ToolWindowContentManager GetContentManager(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (!_contentManagers.TryGetValue(id, out var manager))
        {
            manager = new ToolWindowContentManager();
            manager.SelectionChanged += (_, _) => Rebuild();
            _contentManagers[id] = manager;
        }

        return manager;
    }

    /// <summary>
    /// 接上一个 manager，之后状态一变就重画。
    /// </summary>
    /// <param name="manager">工具窗口管理器。</param>
    /// <param name="contentFactory">给定 id 造内容；返回 <see langword="null"/> 就用占位。</param>
    public void Attach(ToolWindowManager manager, Func<string, Control?>? contentFactory = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        if (_manager is not null)
        {
            _manager.StateChanged -= OnManagerStateChanged;
        }

        _manager = manager;
        _manager.StateChanged += OnManagerStateChanged;

        _content.Clear();
        foreach (var info in manager.Layout.GetSortedList())
        {
            _content[info.Id] = contentFactory?.Invoke(info.Id) ?? CreatePlaceholder(info.Id);
        }

        Rebuild();
    }

    private void OnManagerStateChanged(object? sender, ToolWindowStateChangedEventArgs e) => Rebuild();

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

    private void Rebuild()
    {
        if (_manager is null)
        {
            return;
        }

        BuildStripes();
        BuildDockPane();
    }

    private void BuildStripes()
    {
        _left.Palette = Palette;
        _right.Palette = Palette;

        var stripeWidth = ShowToolWindowNames ? ForgeMetrics.StripeNamedDefaultWidth : 0;
        _left.CustomWidth = stripeWidth;
        _right.CustomWidth = stripeWidth;

        // 条带空白处的右键菜单只有「显示名字」（ResizeStripeManager.kt:51-59）
        _left.ContextMenu = ForgeMenuBuilder.ForStripeBackground(() => ShowToolWindowNames = !ShowToolWindowNames);
        _right.ContextMenu = ForgeMenuBuilder.ForStripeBackground(() => ShowToolWindowNames = !ShowToolWindowNames);

        foreach (var side in new[] { StripeSide.Left, StripeSide.Right })
        {
            var toolbar = side == StripeSide.Left ? _left : _right;

            foreach (var group in new[] { StripeGroup.Top, StripeGroup.Bottom })
            {
                var panel = group == StripeGroup.Top ? toolbar.TopGroup : toolbar.BottomGroup;
                panel.Children.Clear();

                var anchor = ResolveGroupAnchor(side, group);
                var windows = _manager!.Layout.GetSortedList()
                    .Where(x => x.IsShowStripeButton || x.IsVisible)
                    .Where(x => x.Anchor != ToolWindowAnchor.Top)
                    .Where(x => StripePlacementResolver.Resolve(x.Anchor, x.IsSplit)
                                == new StripePlacement(side, group));

                // 分组分隔符只用在 LEFT/RIGHT 的那一组（AbstractDroppableStripe.kt:99-105）
                var useSeparator = group == StripeGroup.Top;

                foreach (var item in StripeGroupLayout.Build(windows, anchor, useSeparator))
                {
                    panel.Children.Add(item.IsSeparator
                        ? new ForgeStripeSeparator { Palette = Palette }
                        : CreateStripeButton(item.Window!));
                }

                // 「更多」按钮挂在上组之后（ToolWindowToolbar.kt:109）
                if (group == StripeGroup.Top)
                {
                    panel.Children.Add(CreateMoreButton(side));
                }
            }
        }
    }

    private static ToolWindowAnchor ResolveGroupAnchor(StripeSide side, StripeGroup group)
        => group == StripeGroup.Bottom
            ? ToolWindowAnchor.Bottom
            : side == StripeSide.Left ? ToolWindowAnchor.Left : ToolWindowAnchor.Right;

    private Control CreateStripeButton(WindowInfo info)
    {
        var button = new ForgeStripeButton
        {
            Palette = Palette,
            Text = info.Id[..1],
            Title = info.Id,
            ShowName = ShowToolWindowNames,
            StripeWidth = ShowToolWindowNames ? ForgeMetrics.StripeNamedDefaultWidth : 0,
            IsToolWindowVisible = info.IsVisible,
            IsToolWindowActive = string.Equals(_manager!.LastActiveToolWindowId, info.Id, StringComparison.Ordinal)
                                 && info.IsVisible,
            Tag = info.Order,
        };

        var id = info.Id;

        button.ContextMenu = ForgeMenuBuilder.ForStripeButton(
            _manager!, id, () => ShowToolWindowNames = !ShowToolWindowNames);

        button.PointerPressed += (_, e) =>
        {
            // 右键交给菜单，不当拖拽起点
            if (e.GetCurrentPoint(button).Properties.IsRightButtonPressed)
            {
                return;
            }

            _dragId = id;
            _dragStart = e.GetPosition(this);
            e.Pointer.Capture(button);
        };

        button.PointerMoved += (_, e) =>
        {
            if (_dragId is null)
            {
                return;
            }

            var point = e.GetPosition(this);
            var moved = Math.Sqrt(Math.Pow(point.X - _dragStart.X, 2) + Math.Pow(point.Y - _dragStart.Y, 2));

            // 条带按钮走基类的死区 7（MouseDragHelper.java:32）
            if (moved >= ForgeMetrics.StripeButtonDragStartDeadzone)
            {
                button.Opacity = ForgeDragOpacity;
            }
        };

        button.PointerReleased += (_, e) =>
        {
            e.Pointer.Capture(null);
            button.Opacity = 1;

            var dragged = _dragId;
            _dragId = null;
            if (dragged is null)
            {
                return;
            }

            var point = e.GetPosition(this);
            var moved = Math.Sqrt(Math.Pow(point.X - _dragStart.X, 2) + Math.Pow(point.Y - _dragStart.Y, 2));

            if (moved < ForgeMetrics.StripeButtonDragStartDeadzone)
            {
                Toggle(dragged);
                return;
            }

            if (ResolveDropTarget(point) is { } target)
            {
                _manager!.MoveTo(dragged, target.Anchor, target.Order, target.IsSplit);
            }
        };

        return button;
    }

    private ForgeMoreButton CreateMoreButton(StripeSide side)
    {
        var button = new ForgeMoreButton
        {
            Palette = Palette,
            Side = side,
            ActiveSide = MoreButtonSide,
            // 有条带按钮被摘掉（isShowStripeButton=false）时才有东西可挑
            HasHiddenToolWindows = ForgeMorePopup.ResolveItems(_manager!.Layout).Count > 0,
        };

        button.PointerPressed += (_, _) =>
        {
            if (!button.IsAvailable)
            {
                return;
            }

            ForgeMorePopup.Create(_manager!)?.ShowAt(button);
        };

        return button;
    }

    /// <summary>拖拽中按钮的透明度（<c>ToolWindowDragHelper.kt:94 · THUMB_OPACITY</c>）。</summary>
    private const double ForgeDragOpacity = 0.85;

    private void Toggle(string id)
    {
        if (_manager!.Layout.GetInfo(id)!.IsVisible)
        {
            _manager.Hide(id);
        }
        else
        {
            _manager.Activate(id);
        }
    }

    /// <summary>
    /// 松手时落在哪根条带的哪一组、插到第几个。
    /// </summary>
    /// <remarks>落点判定见 <see cref="StripeDropResolver"/>；不在任何条带上就返回 null（本阶段不做浮动）。</remarks>
    private StripeDropTarget? ResolveDropTarget(Point point)
    {
        foreach (var side in new[] { StripeSide.Left, StripeSide.Right })
        {
            var toolbar = side == StripeSide.Left ? _left : _right;
            var toolbarBounds = toolbar.Bounds;
            if (!toolbarBounds.Contains(point))
            {
                continue;
            }

            foreach (var group in new[] { StripeGroup.Top, StripeGroup.Bottom })
            {
                var panel = group == StripeGroup.Top ? toolbar.TopGroup : toolbar.BottomGroup;

                // Bounds 是相对父级的，逐级换算容易差 1px，直接问 Avalonia 要绝对位置
                if (panel.TranslatePoint(default, this) is not { } origin)
                {
                    continue;
                }

                var panelBounds = new Rect(origin, panel.Bounds.Size);
                if (!panelBounds.Contains(point) && panel.Children.Count > 0)
                {
                    continue;
                }

                var offset = point.Y - panelBounds.Y;
                var items = new List<(int Order, double Size)>();
                var separatorOffset = double.MaxValue;
                var accumulated = 0.0;

                foreach (var child in panel.Children)
                {
                    if (child is ForgeStripeSeparator)
                    {
                        separatorOffset = accumulated;
                    }
                    else if (child is ForgeStripeButton b && b.Tag is int order)
                    {
                        items.Add((order, child.Bounds.Height));
                    }

                    accumulated += child.Bounds.Height;
                }

                var insert = StripeDropResolver.ResolveInsertOrder(offset, items);
                var target = StripeDropResolver.Resolve(side, group, offset > separatorOffset, insert);
                return StripeDropResolver.AdjustForReversedBottomGroup(target);
            }
        }

        return null;
    }

    private void BuildDockPane()
    {
        // 内容控件是缓存复用的，重建前必须先从上一轮的父容器上摘下来，
        // 否则 Avalonia 会因为「已经有 visual parent」直接抛异常。
        foreach (var child in _pane.Children.ToList())
        {
            Detach(child);
        }

        _pane.Children.Clear();

        foreach (var info in _manager!.Layout.GetSortedList().Where(x => x.IsVisible))
        {
            var host = new DockPanel();

            var isActive = string.Equals(_manager.LastActiveToolWindowId, info.Id, StringComparison.Ordinal);
            var header = new ForgeToolWindowHeader
            {
                Palette = Palette,
                Title = info.Id,
                IsActive = isActive,
                ContentUiType = info.ContentUiType,
            };

            BuildTabs(header, info, isActive);
            DockPanel.SetDock(header, Dock.Top);
            host.Children.Add(header);
            var content = _content.TryGetValue(info.Id, out var c) ? c : CreatePlaceholder(info.Id);
            Detach(content);
            host.Children.Add(content);

            var border = new Border
            {
                Background = new SolidColorBrush(Palette.Background),
                BorderBrush = new SolidColorBrush(Palette.WindowBorder),
                BorderThickness = ResolveBorderThickness(info.Anchor),
                Child = host,
            };

            ForgeDockPane.SetAnchor(border, info.Anchor);
            ForgeDockPane.SetWeight(border, info.Weight);
            ForgeDockPane.SetIsSplit(border, info.IsSplit);
            ForgeDockPane.SetSideWeight(border, info.SideWeight);
            _pane.Children.Add(border);
        }

        if (EditorContent is { } editor)
        {
            Detach(editor);
            ForgeDockPane.SetAnchor(editor, null);
            _pane.Children.Add(editor);
        }

        AddResizers();
    }

    /// <summary>
    /// 每条有可见窗口的边，在朝编辑区那一侧放一条 6px 的拖拽热区。
    /// </summary>
    /// <remarks>分隔条本身不可见（<c>ToolWindowPane.kt:197</c>），只有热区。</remarks>
    private void AddResizers()
    {
        var anchors = _manager!.Layout.GetSortedList()
            .Where(x => x.IsVisible)
            .Select(x => x.Anchor)
            .Distinct();

        foreach (var anchor in anchors)
        {
            var resizer = new ForgeDockResizer { Anchor = anchor };
            resizer.WeightChanged += OnResizerWeightChanged;
            ForgeDockPane.SetAnchor(resizer, null);
            ForgeDockPane.SetIsResizer(resizer, anchor);
            _pane.Children.Add(resizer);
        }
    }

    private void OnResizerWeightChanged(object? sender, DockWeightChangedEventArgs e)
    {
        if (_manager is null)
        {
            return;
        }

        // 新 UI 默认走统一权重：拖一条边等于改这条边的统一权重（§4）
        _manager.Layout.SetUnifiedAnchorWeight(e.Anchor, e.Weight);

        foreach (var info in _manager.Layout.GetSortedList().Where(x => x.IsVisible && x.Anchor == e.Anchor))
        {
            _manager.Layout.SetInfo(info with { Weight = e.Weight });
        }

        Rebuild();
    }

    /// <summary>把一个控件从它当前的父容器上摘下来。</summary>
    private static void Detach(Control control)
    {
        switch (control.Parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, control):
                decorator.Child = null;
                break;
            case ContentControl host when ReferenceEquals(host.Content, control):
                host.Content = null;
                break;
        }
    }

    /// <summary>
    /// 把这个工具窗口的内容排成标题条上的 tab。
    /// </summary>
    /// <remarks>
    /// COMBO 的工具窗口不排 tab（上游换成一个下拉，<c>ComboContentLayout.java</c>），
    /// 本阶段只画选中那块的名字，不做下拉交互。
    /// </remarks>
    private void BuildTabs(ForgeToolWindowHeader header, WindowInfo info, bool isActive)
    {
        if (!_contentManagers.TryGetValue(info.Id, out var contents) || contents.Contents.Count == 0)
        {
            return;
        }

        if (info.ContentUiType == ToolWindowContentUiType.Combo)
        {
            if (contents.SelectedContent is { } selected)
            {
                header.Tabs.Children.Add(new ForgeContentTab
                {
                    Palette = Palette,
                    Title = selected.DisplayName,
                    IsSoleContent = true,
                });
            }

            return;
        }

        foreach (var content in contents.Contents)
        {
            var isSelected = contents.SelectedContent?.Id == content.Id;
            var tab = new ForgeContentTab
            {
                Palette = Palette,
                Title = content.DisplayName,
                IsSelected = isSelected,
                IsWindowActive = isActive,
                IsSoleContent = contents.IsSoleContent,
                CanBeClosed = contents.CanBeClosed(content),
                IsPinned = content.IsPinned,
            };

            var contentId = content.Id;
            var windowId = info.Id;
            tab.PointerPressed += (_, e) =>
            {
                var manager = GetContentManager(windowId);
                var point = e.GetPosition(tab);

                // 点在右边那个图标上就是关闭/解固定，否则是选中
                if (tab.CanBeClosed && point.X >= tab.Bounds.Width - tab.IconAreaWidth)
                {
                    var alt = e.KeyModifiers.HasFlag(global::Avalonia.Input.KeyModifiers.Alt);
                    manager.InvokeIconAction(contentId, alt);
                    Rebuild();
                    return;
                }

                manager.Select(contentId);
            };

            header.Tabs.Children.Add(tab);
        }
    }

    // 只在朝向编辑区的那一侧画线（InternalDecoratorImpl.kt:757-763 的 insets 规则）
    private static Thickness ResolveBorderThickness(ToolWindowAnchor anchor) => anchor switch
    {
        ToolWindowAnchor.Left => new Thickness(0, 0, 1, 0),
        ToolWindowAnchor.Right => new Thickness(1, 0, 0, 0),
        ToolWindowAnchor.Bottom => new Thickness(0, 1, 0, 0),
        _ => new Thickness(0, 0, 0, 1),
    };

    private Control CreatePlaceholder(string id) => new TextBlock
    {
        Text = id,
        Margin = new Thickness(ForgeMetrics.HeaderLabelInsetLeft, 8, 0, 0),
        Foreground = new SolidColorBrush(Palette.Foreground),
    };
}
