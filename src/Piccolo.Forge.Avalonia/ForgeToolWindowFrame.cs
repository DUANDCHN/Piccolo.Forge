using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
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
    // ★ 按 id 复用：**同步时只改属性、增删父子关系，绝不重建这些控件**。
    //   宿主塞进来的内容（相机实时画面、图表、日志）不能因为开关了别的面板就被重造。
    private readonly Dictionary<string, ContentControl> _panelContents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Border> _panelHosts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ForgeToolWindowHeader> _panelHeaders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ForgeStripeButton> _stripeButtons = new(StringComparer.Ordinal);

    private ToolWindowShellViewModel? _shell;
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
        PaletteProperty.Changed.AddClassHandler<ForgeToolWindowFrame>((f, _) => f.Sync());
        EditorContentProperty.Changed.AddClassHandler<ForgeToolWindowFrame>((f, _) => f.Sync());
        ShowToolWindowNamesProperty.Changed.AddClassHandler<ForgeToolWindowFrame>((f, _) => f.Sync());
        MoreButtonSideProperty.Changed.AddClassHandler<ForgeToolWindowFrame>((f, _) => f.Sync());
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
    /// 取某个工具窗口的内容管理器。
    /// </summary>
    /// <remarks>一个工具窗口可以装多块内容，标题条上排成 tab（<c>工具窗口模型.md</c> §13）。</remarks>
    /// <param name="id">工具窗口 id。</param>
    /// <returns>内容管理器。</returns>
    public ToolWindowContentManager GetContentManager(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        var vm = _shell?.Find(id) ?? throw new InvalidOperationException($"没有登记 id 为 '{id}' 的工具窗口。");
        if (!_contentSubscriptions.Add(id))
        {
            return vm.Contents;
        }

        vm.Contents.SelectionChanged += (_, _) => Sync();
        return vm.Contents;
    }

    private readonly HashSet<string> _contentSubscriptions = new(StringComparer.Ordinal);

    /// <summary>
    /// 接上外壳 VM，之后状态一变就同步。
    /// </summary>
    /// <remarks>
    /// **同步不是重建** —— 见 <see cref="Sync"/>。
    /// </remarks>
    /// <param name="shell">外壳 VM。</param>
    public void Attach(ToolWindowShellViewModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);

        if (_shell is not null)
        {
            _shell.StateChanged -= OnShellStateChanged;
        }

        _shell = shell;
        _shell.StateChanged += OnShellStateChanged;

        Sync();
    }

    /// <summary>接上的外壳 VM。</summary>
    public ToolWindowShellViewModel? Shell => _shell;

    private void OnShellStateChanged(object? sender, ToolWindowStateChangedEventArgs e) => Sync();

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

    /// <summary>
    /// 把 VM 的状态同步到控件上。
    /// </summary>
    /// <remarks>
    /// 这个方法**只做三件事**：改已有控件的属性、按需新建**还不存在**的控件、
    /// 调整父子关系。它不会碰任何已经存在的内容控件 ——
    /// 这正是 <c>Piccolo.Forge.Mvvm</c> 那一层存在的理由。
    /// </remarks>
    private void Sync()
    {
        if (_shell is null)
        {
            return;
        }

        SyncStripes();
        SyncDockPane();
    }

    private void SyncStripes()
    {
        _left.Palette = Palette;
        _right.Palette = Palette;

        var stripeWidth = ShowToolWindowNames ? ForgeMetrics.StripeNamedDefaultWidth : 0;
        _left.CustomWidth = stripeWidth;
        _right.CustomWidth = stripeWidth;

        // 条带空白处的右键菜单只有「显示名字」（ResizeStripeManager.kt:51-59）
        _left.ContextMenu ??= ForgeMenuBuilder.ForStripeBackground(() => ShowToolWindowNames = !ShowToolWindowNames);
        _right.ContextMenu ??= ForgeMenuBuilder.ForStripeBackground(() => ShowToolWindowNames = !ShowToolWindowNames);

        foreach (var side in new[] { StripeSide.Left, StripeSide.Right })
        {
            var toolbar = side == StripeSide.Left ? _left : _right;

            foreach (var group in new[] { StripeGroup.Top, StripeGroup.Bottom })
            {
                var panel = group == StripeGroup.Top ? toolbar.TopGroup : toolbar.BottomGroup;
                SyncGroup(panel, side, group);
            }
        }
    }

    /// <summary>
    /// 把一组按钮同步成 VM 说的样子：**复用已有按钮，只调位置**。
    /// </summary>
    private void SyncGroup(Panel panel, StripeSide side, StripeGroup group)
    {
        var anchor = ResolveGroupAnchor(side, group);
        var windows = _shell!.GetGroup(side, group);

        // 分组分隔符只用在 LEFT/RIGHT 的那一组（AbstractDroppableStripe.kt:99-105）
        var useSeparator = group == StripeGroup.Top;
        var wantedSplit = useSeparator && windows.Any(x => x.IsSplit) && !windows.All(x => x.IsSplit);

        var wanted = new List<Control>();
        foreach (var vm in windows)
        {
            if (wantedSplit && vm.IsSplit && wanted.All(x => x is not ForgeStripeSeparator))
            {
                wanted.Add(GetSeparator(side, group));
            }

            wanted.Add(GetStripeButton(vm));
        }

        if (group == StripeGroup.Top)
        {
            // 「更多」按钮挂在上组之后（ToolWindowToolbar.kt:109）
            wanted.Add(GetMoreButton(side));
        }

        Reconcile(panel, wanted);
    }

    /// <summary>把面板的子控件调成 <paramref name="wanted"/>，只动有差异的位置。</summary>
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
            var child = wanted[i];
            var current = panel.Children.IndexOf(child);

            if (current < 0)
            {
                panel.Children.Insert(i, child);
            }
            else if (current != i)
            {
                panel.Children.Move(current, i);
            }
        }
    }

    private readonly Dictionary<(StripeSide, StripeGroup), ForgeStripeSeparator> _separators = [];
    private readonly Dictionary<StripeSide, ForgeMoreButton> _moreButtons = [];

    private ForgeStripeSeparator GetSeparator(StripeSide side, StripeGroup group)
    {
        if (!_separators.TryGetValue((side, group), out var separator))
        {
            separator = new ForgeStripeSeparator();
            _separators[(side, group)] = separator;
        }

        separator.Palette = Palette;
        return separator;
    }

    private static ToolWindowAnchor ResolveGroupAnchor(StripeSide side, StripeGroup group)
        => group == StripeGroup.Bottom
            ? ToolWindowAnchor.Bottom
            : side == StripeSide.Left ? ToolWindowAnchor.Left : ToolWindowAnchor.Right;

    /// <summary>
    /// 取（必要时新建）某个工具窗口的条带按钮，之后只更新它的状态。
    /// </summary>
    private ForgeStripeButton GetStripeButton(ToolWindowViewModel vm)
    {
        if (!_stripeButtons.TryGetValue(vm.Id, out var button))
        {
            button = new ForgeStripeButton { Tag = vm };
            WireStripeButton(button, vm);
            _stripeButtons[vm.Id] = button;
        }

        button.Palette = Palette;
        button.Icon = vm.Icon;
        button.Text = vm.Icon is null ? vm.Title[..1] : null;
        button.Title = vm.Title;
        button.ShowName = ShowToolWindowNames;
        button.StripeWidth = ShowToolWindowNames ? ForgeMetrics.StripeNamedDefaultWidth : 0;
        button.IsToolWindowVisible = vm.IsOpen;
        button.IsToolWindowActive = vm.IsActive;

        return button;
    }

    private void WireStripeButton(ForgeStripeButton button, ToolWindowViewModel vm)
    {
        var id = vm.Id;

        button.ContextMenu = ForgeMenuBuilder.ForStripeButton(
            _shell!.Manager, id, () => ShowToolWindowNames = !ShowToolWindowNames);

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
                _shell!.Manager.MoveTo(dragged, target.Anchor, target.Order, target.IsSplit);
            }
        };
    }

    private ForgeMoreButton GetMoreButton(StripeSide side)
    {
        if (!_moreButtons.TryGetValue(side, out var button))
        {
            button = new ForgeMoreButton { Side = side };
            var self = button;
            button.PointerPressed += (_, _) =>
            {
                if (self.IsAvailable)
                {
                    ForgeMorePopup.Create(_shell!.Manager)?.ShowAt(self);
                }
            };

            _moreButtons[side] = button;
        }

        button.Palette = Palette;
        button.ActiveSide = MoreButtonSide;
        // 有条带按钮被摘掉（isShowStripeButton=false）时才有东西可挑
        button.HasHiddenToolWindows = ForgeMorePopup.ResolveItems(_shell!.Manager.Layout).Count > 0;

        return button;
    }

    /// <summary>拖拽中按钮的透明度（<c>ToolWindowDragHelper.kt:94 · THUMB_OPACITY</c>）。</summary>
    private const double ForgeDragOpacity = 0.85;

    private void Toggle(string id)
    {
        if (_shell!.Manager.Layout.GetInfo(id)!.IsVisible)
        {
            _shell.Close(id);
        }
        else
        {
            _shell.Open(id);
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

    /// <summary>
    /// 同步停靠区：复用面板宿主，只调可见性与位置。
    /// </summary>
    /// <remarks>
    /// **面板宿主与它的内容控件从不重建** —— 开关别的面板不会让相机画面、
    /// 图表、日志被重造一遍。
    /// </remarks>
    private void SyncDockPane()
    {
        var wanted = new List<Control>();

        foreach (var vm in _shell!.ToolWindows.Where(x => x.IsOpen))
        {
            var info = _shell.Manager.Layout.GetInfo(vm.Id)!;
            var host = GetPanelHost(vm);

            ForgeDockPane.SetAnchor(host, info.Anchor);
            ForgeDockPane.SetWeight(host, info.Weight);
            ForgeDockPane.SetIsSplit(host, info.IsSplit);
            ForgeDockPane.SetSideWeight(host, info.SideWeight);

            SyncPanel(vm, host);
            wanted.Add(host);
        }

        if (EditorContent is { } editor)
        {
            ForgeDockPane.SetAnchor(editor, null);
            wanted.Add(editor);
        }

        wanted.AddRange(GetResizers());
        Reconcile(_pane, wanted);
    }

    /// <summary>取（必要时新建）一个工具窗口的面板宿主。</summary>
    private Border GetPanelHost(ToolWindowViewModel vm)
    {
        if (_panelHosts.TryGetValue(vm.Id, out var existing))
        {
            return existing;
        }

        var header = new ForgeToolWindowHeader();
        DockPanel.SetDock(header, Dock.Top);
        _panelHeaders[vm.Id] = header;

        // 内容用 ContentControl 承载 object —— 视图由**宿主**的 ViewLocator / DataTemplate 解析
        var content = new ContentControl();
        _panelContents[vm.Id] = content;

        var body = new DockPanel();
        body.Children.Add(header);
        body.Children.Add(content);

        var host = new Border { Child = body };
        _panelHosts[vm.Id] = host;
        return host;
    }

    private void SyncPanel(ToolWindowViewModel vm, Border host)
    {
        var info = _shell!.Manager.Layout.GetInfo(vm.Id)!;

        host.Background = new SolidColorBrush(Palette.Background);
        host.BorderBrush = new SolidColorBrush(Palette.WindowBorder);
        host.BorderThickness = ResolveBorderThickness(info.Anchor);

        var header = _panelHeaders[vm.Id];
        header.Palette = Palette;
        header.Title = vm.Title;
        header.IsActive = vm.IsActive;
        header.ContentUiType = info.ContentUiType;
        SyncTabs(header, vm, info.ContentUiType);

        _panelContents[vm.Id].Content = vm.Content;
    }

    /// <summary>
    /// 把这个工具窗口的内容排成标题条上的 tab。
    /// </summary>
    /// <remarks>
    /// COMBO 的工具窗口不排 tab（上游换成一个下拉，<c>ComboContentLayout.java</c>），
    /// 本阶段只画选中那块的名字，不做下拉交互。
    /// </remarks>
    private void SyncTabs(ForgeToolWindowHeader header, ToolWindowViewModel vm, ToolWindowContentUiType uiType)
    {
        var contents = vm.Contents;
        header.Tabs.Children.Clear();

        if (contents.Contents.Count == 0)
        {
            return;
        }

        if (uiType == ToolWindowContentUiType.Combo)
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
            var tab = new ForgeContentTab
            {
                Palette = Palette,
                Title = content.DisplayName,
                IsSelected = contents.SelectedContent?.Id == content.Id,
                IsWindowActive = vm.IsActive,
                IsSoleContent = contents.IsSoleContent,
                CanBeClosed = contents.CanBeClosed(content),
                IsPinned = content.IsPinned,
            };

            var contentId = content.Id;
            tab.PointerPressed += (_, e) =>
            {
                var point = e.GetPosition(tab);

                // 点在右边那个图标上就是关闭/解固定，否则是选中
                if (tab.CanBeClosed && point.X >= tab.Bounds.Width - tab.IconAreaWidth)
                {
                    var alt = e.KeyModifiers.HasFlag(global::Avalonia.Input.KeyModifiers.Alt);
                    contents.InvokeIconAction(contentId, alt);
                    Sync();
                    return;
                }

                contents.Select(contentId);
            };

            header.Tabs.Children.Add(tab);
        }
    }

    /// <summary>
    /// 每条有可见窗口的边，在朝编辑区那一侧放一条 6px 的拖拽热区。
    /// </summary>
    /// <remarks>分隔条本身不可见（<c>ToolWindowPane.kt:197</c>），只有热区。</remarks>
    private IEnumerable<Control> GetResizers()
    {
        var anchors = _shell!.ToolWindows
            .Where(x => x.IsOpen)
            .Select(x => x.Anchor)
            .Distinct();

        foreach (var anchor in anchors)
        {
            if (!_resizers.TryGetValue(anchor, out var resizer))
            {
                resizer = new ForgeDockResizer { Anchor = anchor };
                resizer.WeightChanged += OnResizerWeightChanged;
                ForgeDockPane.SetAnchor(resizer, null);
                ForgeDockPane.SetIsResizer(resizer, anchor);
                _resizers[anchor] = resizer;
            }

            yield return resizer;
        }
    }

    private readonly Dictionary<ToolWindowAnchor, ForgeDockResizer> _resizers = [];

    private void OnResizerWeightChanged(object? sender, DockWeightChangedEventArgs e)
    {
        if (_shell is null)
        {
            return;
        }

        // 新 UI 默认走统一权重：拖一条边等于改这条边的统一权重（§4）
        var layout = _shell.Manager.Layout;
        layout.SetUnifiedAnchorWeight(e.Anchor, e.Weight);

        foreach (var info in layout.GetSortedList().Where(x => x.IsVisible && x.Anchor == e.Anchor))
        {
            layout.SetInfo(info with { Weight = e.Weight });
        }

        Sync();
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
