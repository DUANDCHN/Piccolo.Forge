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
    private readonly Dictionary<EditorGroupViewModel, Border> _groupStripHosts = [];
    private readonly Dictionary<EditorGroupViewModel, ContentControl> _groupBodies = [];
    private readonly Decorator _root = new();

    private EditorAreaViewModel? _area;

    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeEditorArea, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    static ForgeEditorArea()
    {
        // ⚠ 这些都是**纯外观**，不要走 Sync（那会重建整棵树，见 RefreshAppearance 的说明）
        PaletteProperty.Changed.AddClassHandler<ForgeEditorArea>((x, _) => x.RefreshAppearance());
        IslandMetricsProperty.Changed.AddClassHandler<ForgeEditorArea>((x, _) => x.RefreshAppearance());

        // 换族要重跑取色。⚠ 别把 _variantApplied 归零，理由见 ForgeToolWindowFrame 同处的注释
        // （会把「宿主自己管配色」那道探测重新武装，换族与跟随明暗一起坏掉）。
        ThemeFamilyProperty.Changed.AddClassHandler<ForgeEditorArea>((x, _) => x.ApplyThemeVariant());
    }

    /// <summary><see cref="FollowThemeVariant"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> FollowThemeVariantProperty =
        AvaloniaProperty.Register<ForgeEditorArea, bool>(nameof(FollowThemeVariant), defaultValue: true);

    /// <summary><see cref="ThemeFamily"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgeThemeFamily> ThemeFamilyProperty =
        AvaloniaProperty.Register<ForgeEditorArea, ForgeThemeFamily>(nameof(ThemeFamily));

    /// <summary>
    /// 宿主往标签页右键菜单里追加自己的项。
    /// </summary>
    /// <remarks>
    /// 参数是（菜单, 这一页的 id）。库只提供**编辑区自己的语义**
    /// （关闭系列 / 拆分系列 / 固定），宿主专属的（另存为、在资源管理器中显示、
    /// 版本控制…）从这里加 —— 让 Forge 去建模 Git 或书签是错的分层。
    /// <example>
    /// <code>
    /// editor.TabMenuExtension = (menu, id) =&gt;
    /// {
    ///     menu.Items.Add(new Separator());
    ///     menu.Items.Add(new MenuItem { Header = "另存为…" });
    /// };
    /// </code>
    /// </example>
    /// </remarks>
    public Action<ContextMenu, string>? TabMenuExtension { get; set; }

    /// <summary><see cref="IslandMetrics"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgeIslandMetrics?> IslandMetricsProperty =
        AvaloniaProperty.Register<ForgeEditorArea, ForgeIslandMetrics?>(nameof(IslandMetrics));

    /// <summary>
    /// 岛的几何；<see langword="null"/> 用配色自带的。
    /// </summary>
    /// <remarks>⚠ 与 <see cref="ForgeToolWindowFrame.IslandMetrics"/> **两边都要设**，只设一边圆角会不一致。</remarks>
    public ForgeIslandMetrics? IslandMetrics
    {
        get => GetValue(IslandMetricsProperty);
        set => SetValue(IslandMetricsProperty, value);
    }

    /// <summary>
    /// 配色族——方角的 expUI 还是圆角留缝的 Islands。默认 <see cref="ForgeThemeFamily.ExpUi"/>。
    /// </summary>
    /// <remarks>
    /// 编辑区与外框是两个独立控件，**两边都要设**，否则会出现「四边是岛、中间还是方角」。
    /// </remarks>
    public ForgeThemeFamily ThemeFamily
    {
        get => GetValue(ThemeFamilyProperty);
        set => SetValue(ThemeFamilyProperty, value);
    }

    /// <summary>建一个编辑区控件。</summary>
    public ForgeEditorArea()
    {
        VisualChildren.Add(_root);
        LogicalChildren.Add(_root);

        // 覆盖层加在 _root **之后** —— 父控件的 Render 在子控件之下，
        // 落点高亮必须盖在分屏与正文之上才看得见（同 ForgeToolWindowFrame）。
        VisualChildren.Add(_overlay);
        LogicalChildren.Add(_overlay);

        ActualThemeVariantChanged += (_, _) => ApplyThemeVariant();

        // 焦点进出要重画：活动/失焦是两套配色
    }

    private readonly ForgeDragOverlay _overlay = new();

    /// <summary>
    /// 跟着宿主的明暗变体自动切配色。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="ForgeToolWindowFrame.FollowThemeVariant"/> 同义、同默认值。
    /// <b>它必须和外壳一样默认开着</b>：编辑区是外壳中间那块，两者跟不同的主题走，
    /// 结果就是浅色界面里中间一大片是黑的 —— 而且看上去像「主题没做」，
    /// 不像「有个开关没打开」。
    /// </remarks>
    public bool FollowThemeVariant
    {
        get => GetValue(FollowThemeVariantProperty);
        set => SetValue(FollowThemeVariantProperty, value);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyThemeVariant();
    }

    /// <inheritdoc />
    /// <remarks>焦点进出要重画：活动 / 失焦是两套配色（见 Sync 里 IsGroupActive 的注释）。</remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsKeyboardFocusWithinProperty)
        {
            // ⚠ 这里**绝不能**调 Sync。重建会换父容器、换父容器会带走焦点，
            //   焦点一动又回到这里 —— 正是那条重入的源头。
            RefreshAppearance();
        }
    }

    private bool _variantApplied;
    private bool _paletteSetByHost;

    /// <summary>
    /// 按当前明暗变体挑一套配色。
    /// </summary>
    /// <remarks>
    /// 宿主显式赋过 <see cref="Palette"/> 就不再插手 —— 判据用 <see cref="AvaloniaObject.IsSet"/>
    /// 而不是「值变了没有」：属性的默认值本身就是一套配色，宿主赋成同一套时不会有变更通知。
    /// 我们自己写过之后 <c>IsSet</c> 也为真，所以只在第一次之前认它。
    /// </remarks>
    private void ApplyThemeVariant()
    {
        if (!FollowThemeVariant)
        {
            return;
        }

        if (!_variantApplied && IsSet(PaletteProperty))
        {
            _paletteSetByHost = true;
        }

        if (_paletteSetByHost)
        {
            return;
        }

        var wanted = ForgePalette.ForTheme(ThemeFamily, ActualThemeVariant);
        if (!ReferenceEquals(Palette, wanted))
        {
            Palette = wanted;
        }

        _variantApplied = true;
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
        _overlay.Measure(availableSize);
        return _root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        _root.Arrange(new Rect(finalSize));
        _overlay.Arrange(new Rect(finalSize));
        return finalSize;
    }

    private void Sync()
    {
        if (_area is null)
        {
            return;
        }

        // ★ 重建过程中**不接受重入**。
        //
        //   重建要把缓存的组宿主换父容器，而换父容器会带走键盘焦点 ——
        //   焦点一动 IsKeyboardFocusWithin 就变，它的处理器过去直接调 Sync，
        //   于是在外层建到一半时重入。内层建完一棵树、外层拿着过期的局部变量接着建，
        //   结果是**视图与模型对不上**：模型明明两组，界面上只剩一个标签页。
        //   现象正是「拖动分屏后中间空了，再打开一个标签又全出来」——
        //   后来那次 Sync 没被焦点打断，就建对了。
        //
        //   处理方式是合并而不是排队：重入期间只记一笔，外层建完再补一次。
        //   补的那一次读的是**当时**的模型，中间那些中间态没有意义。
        if (_rebuilding)
        {
            _rebuildAgain = true;
            return;
        }

        _rebuilding = true;
        try
        {
            do
            {
                _rebuildAgain = false;
                _splitters.Clear();

                // ★ 建歪了也**绝不能让编辑区变成一片空白**。
                //
                //   重建过程中会把已有的组宿主从旧父容器上摘下来（换父容器是必须的），
                //   所以只要 BuildNode 中途抛异常，_root.Child 就停在 null ——
                //   而它是编辑区唯一的可见内容。一次异常 = 整块中间区域全没，
                //   下一次 Sync（比如再打开一个文档）才恢复。
                //   这正是"拖动分屏后中间空了、再打开一个标签又全出来"的**表现机制**
                //   （病根是别处，但放大成"全白"是这里）。
                //
                //   兜住之后，同样的异常最坏只是这一次没更新，屏幕上还是上一版的树。
                var previous = _root.Child;
                try
                {
                    PruneCaches();
                    _root.Child = BuildNode(_area.Area.Root);
                }
                catch
                {
                    _root.Child ??= previous;
                    throw;
                }
            }
            while (_rebuildAgain);
        }
        finally
        {
            _rebuilding = false;
        }
    }

    private bool _rebuilding;
    private bool _rebuildAgain;

    /// <summary>
    /// 把模型里已经不存在的 (组, 标签页) 从各个缓存里清掉。
    /// </summary>
    /// <remarks>
    /// 三个按 <c>(组, id)</c> 或 <c>组</c> 做键的缓存此前<b>只增不减</b>。两个后果：
    /// <list type="number">
    /// <item>
    /// <b>收养不了。</b>标签页从 A 组搬到 B 组时，B 会新建一个内容宿主去装
    /// <see cref="EditorTabViewModel.Content"/>，而 A 那个旧宿主还攥着同一个对象 ——
    /// 宿主要是塞的控件（而不是 VM），Avalonia 当场抛「已有父容器」。
    /// </item>
    /// <item>
    /// <b>放不掉。</b>关掉的标签页、被摘掉的组，它们的内容宿主一直被字典强引用着，
    /// 宿主塞进来的相机会话 / 图像帧跟着回收不了。
    /// </item>
    /// </list>
    /// <para>
    /// ⚠ <b>必须在重建之前跑</b>：得先让旧宿主松开 <c>Content</c>，新宿主才收养得了。
    /// 放到重建之后就晚了。
    /// </para>
    /// </remarks>
    private void PruneCaches()
    {
        var liveGroups = new HashSet<EditorGroupViewModel>(_area!.Groups);
        var liveTabs = new HashSet<(EditorGroupViewModel, string)>();

        foreach (var vm in _area.Groups)
        {
            foreach (var tab in vm.Tabs)
            {
                liveTabs.Add((vm, tab.Id));
            }
        }

        foreach (var key in _contents.Keys.Where(k => !liveTabs.Contains(k)).ToList())
        {
            _contents[key].Content = null;
            _contents.Remove(key);
        }

        foreach (var key in _tabs.Keys.Where(k => !liveTabs.Contains(k)).ToList())
        {
            // ⚠ 光 Detach 不够。Detach 解开的是「这个标签页控件挂在谁底下」，
            //   而它自己**还攥着宿主给的图标控件**（ForgeEditorTab 把 Control 型的 Icon
            //   收成了 VisualChildren）。不松开的话，标签页搬到另一组时新建的
            //   ForgeEditorTab 去收养同一个图标，当场抛「已有父容器」——
            //   异常打断 Sync，整块编辑区变空白。这正是"拖动分屏后中间空了"的真凶。
            _tabs[key].Icon = null;
            Detach(_tabs[key]);
            _tabs.Remove(key);
        }

        foreach (var group in _groupHosts.Keys.Where(g => !liveGroups.Contains(g)).ToList())
        {
            if (_groupBodies.TryGetValue(group, out var body))
            {
                body.Content = null;
            }

            Detach(_groupHosts[group]);
            _groupHosts.Remove(group);
            _groupStrips.Remove(group);
            _groupStripHosts.Remove(group);
            _groupBodies.Remove(group);
        }
    }

    /// <summary>
    /// 只刷外观，<b>不动树</b>。
    /// </summary>
    /// <remarks>
    /// <see cref="Sync"/> 过去把两件事混在一起：**对齐树结构**与**刷新外观**。
    /// 于是「焦点进出」这种纯外观的变化也会引发整棵树重建 —— 又慢，又正好是
    /// 上面那条重入的触发源（重建换父容器 → 焦点跑掉 → 再次进来）。
    /// <para>
    /// 拆开之后：模型变了才重建树，配色 / 焦点这类只走这里。
    /// </para>
    /// </remarks>
    private void RefreshAppearance()
    {
        if (_area is null)
        {
            return;
        }

        foreach (var vm in _area.Groups)
        {
            if (_groupHosts.TryGetValue(vm, out var host))
            {
                ForgeIslandDecor.Apply((Border)host, Palette, anchor: null, IslandMetrics);
            }

            if (_groupStripHosts.TryGetValue(vm, out var stripHost))
            {
                stripHost.BorderBrush = new SolidColorBrush(Palette.EditorTabsBorder);
            }

            // 「活动」= 这一组是当前组，且编辑区握着键盘焦点（详见 SyncStrip 同处的注释）
            var active = vm.IsCurrent && IsKeyboardFocusWithin;
            foreach (var tab in vm.Tabs)
            {
                if (_tabs.TryGetValue((vm, tab.Id), out var control))
                {
                    control.Palette = Palette;
                    control.IsGroupActive = active;
                }
            }
        }

        foreach (var splitter in _splitters)
        {
            splitter.Background = new SolidColorBrush(Palette.WindowBorder);
        }

        _overlay.Palette = Palette;
    }

    private readonly List<GridSplitter> _splitters = [];

    /// <summary>
    /// 把一个控件从当前父容器上摘下来。
    /// </summary>
    /// <remarks>
    /// 分屏那一刻，新建的 Grid 要收养原来那个组宿主（Border），可它还挂在上一次的
    /// Decorator / Grid 底下 —— Avalonia 不许一个控件有两个父，当场抛
    /// <c>The control Border already has a visual parent Decorator while trying to
    /// add it as a child of Grid</c>。
    /// <para>
    /// ⚠ <b>摘的时机必须是"新父要收养它"的那一刻，不能在 <see cref="Sync"/> 开头一次摘光。</b>
    /// 一次摘光的话，<see cref="BuildNode"/> 中途只要抛出任何异常，
    /// <c>_root.Child</c> 就停在 <see langword="null"/> —— 而它是编辑区**唯一**的可见内容，
    /// 于是整块中间区域变成一片空白，比原来那个异常还难查。
    /// 就地摘则最坏只是局部没接上，旧树的其余部分还在屏幕上。
    /// </para>
    /// </remarks>
    private static void Detach(Control child)
    {
        switch (child.Parent)
        {
            case Panel panel:
                panel.Children.Remove(child);
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, child):
                decorator.Child = null;
                break;
            case ContentControl content when ReferenceEquals(content.Content, child):
                content.Content = null;
                break;
        }
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
            _splitters.Add(splitter);

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
            _groupStrips[vm] = strip;

            // 标签条底下那条 1px 分割线（EditorTabs.underTabsBorderColor）——
            // 它属于**标签条整体**，不属于某一个标签页，所以画在这里而不是 ForgeEditorTab 里。
            // 漏了它，标签条与正文之间没有分界，看着是糊在一起的。
            var stripHost = new Border { Child = strip, BorderThickness = new Thickness(0, 0, 0, 1) };
            _groupStripHosts[vm] = stripHost;
            DockPanel.SetDock(stripHost, Dock.Top);

            var body = new ContentControl();
            _groupBodies[vm] = body;

            var panel = new DockPanel();
            panel.Children.Add(stripHost);
            panel.Children.Add(body);

            host = new Border { Child = panel };
            _groupHosts[vm] = host;
        }

        // anchor 传 null：编辑区在中间，方角形态下不画分隔线（那条线归四边的面板画）
        ForgeIslandDecor.Apply((Border)host, Palette, anchor: null, IslandMetrics);
        SyncStrip(vm, _groupStrips[vm]);

        _groupStripHosts[vm].BorderBrush = new SolidColorBrush(Palette.EditorTabsBorder);

        // 选中的那一页的内容挂到组的正文槽上
        _groupBodies[vm].Content = vm.Selected is { } selected ? GetContentHost(vm, selected) : null;

        // 就地摘：调用方（Grid 或 _root）马上就要收养它。理由见 Detach 的说明。
        Detach(host);
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
                var self = control;
                var owner = vm;
                control.PointerPressed += (_, e) =>
                {
                    var point = e.GetPosition(self);
                    // 命中区问 ForgeEditorTab 要 —— 与它画悬停底用的是同一个矩形
                    if (self.CanBeClosed && self.TabIconHitRect().Contains(point))
                    {
                        _area!.Close(id);
                        return;
                    }

                    // 双击：预览页转正，否则撑满 / 收回
                    // （EditorTabbedContainer.kt:339-387 · doProcessDoubleClick）
                    if (e.ClickCount == 2)
                    {
                        _area!.DoubleClick(id);
                        return;
                    }

                    _area!.Select(id);

                    // 起拖记在选中**之后**：上游也是按下就先切过去
                    // （拖动本身不改选中项，reallocate 只动次序）。
                    // ⚠ 关闭按钮那一支已经 return 了 —— 上游同样不让从关闭按钮起拖
                    //   （DragHelper.java:289 findLabel 遇到 InplaceButton 直接返回 null）。
                    BeginPress(owner, self, id, e);
                };

                control.PointerMoved += (_, e) => DragTo(owner, self, e);
                control.PointerReleased += (_, e) =>
                {
                    EndDrag(owner, self, commit: true);
                    e.Pointer.Capture(null);
                };

                // 捕获被别人抢走（弹窗、Alt-Tab…）算**取消**：收摊但不落库，
                // 否则标签会一直飘着，或者莫名其妙切出一刀分屏
                control.PointerCaptureLost += (_, _) => EndDrag(owner, self, commit: false);

                _tabs[key] = control;
            }

            control.Palette = Palette;
            // 每次同步都重建：菜单要反映**当前**的固定状态（勾选与文案都跟着它）
            control.ContextMenu = ForgeMenuBuilder.ForEditorTab(_area!, tab.Id, tab.IsPinned, TabMenuExtension);
            control.Title = tab.Title;
            // 图标要是个控件，同一时刻只能挂在一个标签页控件下。
            // 分屏「拆分」是把同一页**同时**开在两组里（EditorWindow.kt:570-571），
            // 两个 ForgeEditorTab 会抢同一个图标实例 —— 先从别人那儿摘下来再给自己，
            // 否则又是「已有父容器」。
            // 宿主想让两边都有图标，Icon 传 Func<Control> 工厂即可，见 ResolveIcon。
            if (tab.Icon is Control shared)
            {
                foreach (var other in _tabs.Values)
                {
                    if (!ReferenceEquals(other, control) && ReferenceEquals(other.Icon, shared))
                    {
                        other.Icon = null;
                    }
                }
            }

            control.Icon = ResolveIcon(control, tab.Icon);
            control.IsSelected = tab.IsSelected;
            // ⚠ 「活动」= **这一组是当前组，且编辑区握着键盘焦点**。
            //    上游是 tabs.isActiveTabs(info)（IslandsTabPainterAdapter.kt:50），判的是焦点，
            //    不是"哪一组是当前组"。只判后者的话单组场景恒为真 —— 焦点跑到工具窗口了，
            //    标签页还亮着活动色（用户问「哪儿来的蓝色」就是这个）。
            control.IsGroupActive = vm.IsCurrent && IsKeyboardFocusWithin;
            control.IsPinned = tab.IsPinned;
            control.CanBeClosed = tab.IsCloseable && !tab.IsPinned;
            wanted.Add(control);
        }

        Reconcile(strip, wanted);
    }

    private ForgeEditorTab? _dragTab;
    private EditorGroupViewModel? _dragGroup;
    private string? _dragId;
    private Point _pressPoint;
    private Vector _holdDelta;
    private Rect _dragOriginalRec;
    private bool _dragStarted;
    private bool _dragOut;
    private EditorGroupViewModel? _dropGroup;
    private EditorDropSide _dropSide;
    private global::Avalonia.Media.Imaging.RenderTargetBitmap? _dragThumb;

    /// <summary>
    /// 按下：记住是谁、按在哪，但**先不算拖拽**。
    /// </summary>
    /// <remarks>
    /// 对应 <c>DragHelper.java:156-162 · processMousePressed</c>。上游那段注释值得照抄一遍：
    /// 按下会切换选中，而切换选中**可能引起重排**，所以必须在这一刻就把
    /// "按的是哪一页"记死，否则等到真开始拖的时候按坐标反查，拖到的会是另一页（IDEA-65073）。
    /// </remarks>
    private void BeginPress(EditorGroupViewModel group, ForgeEditorTab control, string id, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed
            || !_groupStrips.TryGetValue(group, out var strip))
        {
            return;
        }

        _dragGroup = group;
        _dragTab = control;
        _dragId = id;
        _pressPoint = e.GetPosition(strip);
        _dragStarted = false;
        e.Pointer.Capture(control);
    }

    /// <summary>
    /// 拖动中：算出该和谁换位，当场换。
    /// </summary>
    /// <remarks>
    /// 对应 <c>DragHelper.java:164-246 · processDrag</c>。
    /// 几何全在 <see cref="ForgeEditorTabDragTarget"/> 里，这里只负责取数、落库、重排。
    /// </remarks>
    private void DragTo(EditorGroupViewModel group, ForgeEditorTab control, PointerEventArgs e)
    {
        if (!ReferenceEquals(_dragTab, control) || _dragId is null || _area is null
            || !_groupStrips.TryGetValue(group, out var strip)
            || strip.SlotOf(control) is not { } origin)
        {
            return;
        }

        var point = e.GetPosition(strip);
        var moved = point - _pressPoint;

        if (!_dragStarted)
        {
            if (Math.Abs(moved.X) < ForgeEditorTabDragTarget.DragStartDeadZone
                && Math.Abs(moved.Y) < ForgeEditorTabDragTarget.DragStartDeadZone)
            {
                return;
            }

            _dragStarted = true;

            // 抓握偏移：按在标签页内部什么位置，整个拖动过程都保持这个相对位置
            // （DragHelper.java:178 myHoldDelta）。少了它，标签会"跳"到鼠标下居中。
            _holdDelta = _pressPoint - origin.Position;
            _dragOriginalRec = origin;
        }

        // 拖出标签行 —— **一旦置上，这一次拖动里就不再回到行内重排**
        // （MouseDragHelper.java:207-217：myDetachingMode 是个单向闩，
        //  置上那一刻先 processDragFinish(e, true) 收掉行内那套，此后只走 processDragOut）。
        // 少了这个闩，人在标签行上下边缘来回蹭时会在"重排"与"分屏"之间反复横跳。
        if (!_dragOut
            && ForgeEditorTabDragTarget.IsDragOut(origin, strip.Bounds.Width, moved.X, moved.Y))
        {
            _dragOut = true;
            _dragThumb = ForgeDragOverlay.CaptureThumb(control);

            // 收掉行内拖动：标签落回自己的格子，接下来它以虚影的形式跟着光标
            strip.DraggedChild = null;
            strip.InvalidateArrange();
        }

        if (_dragOut)
        {
            UpdateDropOver(e, origin.Size);
            return;
        }

        var dragRec = ForgeEditorTabDragTarget.MoveToFit(
            new Rect(point.X - _holdDelta.X, point.Y - _holdDelta.Y, origin.Width, origin.Height),
            new Rect(strip.Bounds.Size));

        var order = group.Tabs.Select(t => _tabs.GetValueOrDefault((group, t.Id))).ToList();
        var rects = order.Select(c => c is not null ? strip.SlotOf(c) ?? default : default).ToList();
        var target = ForgeEditorTabDragTarget.ResolveTarget(dragRec, _dragOriginalRec, rects, order.IndexOf(control));

        if (target >= 0)
        {
            var targetId = group.Tabs[target].Id;
            if (_area.ReorderTab(_dragId, targetId))
            {
                // 换完位要**立刻**重排一次并把原位刷新成新格子
                // （DragHelper.java:231-233：reallocate 之后马上重读 dragOriginalRec）。
                // 不刷的话 freeSpace 会一直按最初那一格算，越拖越大，
                // 于是一次拖动能连着穿过好几页 —— 那道防抖闸就白设了。
                strip.UpdateLayout();
                _dragOriginalRec = strip.SlotOf(control) ?? _dragOriginalRec;
            }
        }

        strip.DraggedChild = control;
        strip.DraggedX = dragRec.X;
        strip.InvalidateArrange();
    }

    /// <summary>
    /// 拖出状态下：光标底下是哪一组、落在哪一侧、该高亮哪一块。
    /// </summary>
    /// <remarks>
    /// 对应 <c>DockableEditorTabbedContainer.kt:271-301 · processDropOver</c>
    /// 与其中的 <c>MyDropAreaPainter</c>。落点方向的梯形判定复用模型层的
    /// <see cref="EditorDropPolicy"/>（它照的是 <c>TabsUtil.getDropSideFor</c>）。
    /// </remarks>
    private void UpdateDropOver(PointerEventArgs e, Size thumbSize)
    {
        var here = e.GetPosition(this);

        _dropGroup = null;
        _dropSide = EditorDropSide.None;
        _overlay.AreaRect = null;

        foreach (var candidate in _area!.Groups)
        {
            if (!_groupHosts.TryGetValue(candidate, out var host)
                || BoundsIn(host) is not { } bounds
                || !bounds.Contains(here))
            {
                continue;
            }

            var stripHeight = _groupStripHosts.TryGetValue(candidate, out var stripHost)
                ? stripHost.Bounds.Height
                : 0;

            var area = ForgeEditorTabDragTarget.DropArea(bounds, stripHeight);
            var side = EditorDropPolicy.Resolve(here.X - area.X, here.Y - area.Y, area.Width, area.Height);

            _dropGroup = candidate;
            _dropSide = side;
            _overlay.AreaRect = ForgeEditorTabDragTarget.HighlightFor(area, side);
            break;
        }

        _overlay.Palette = Palette;
        _overlay.ThumbImage = _dragThumb;
        _overlay.ThumbRect = new Rect(here.X - _holdDelta.X, here.Y - _holdDelta.Y, thumbSize.Width, thumbSize.Height);
    }

    private readonly Dictionary<ForgeEditorTab, (object Source, Control Made)> _madeIcons = [];

    /// <summary>
    /// 把宿主给的图标变成这一个标签页控件能用的东西。
    /// </summary>
    /// <remarks>
    /// <see cref="Geometry"/> 是<b>数据</b>，画多少遍都行，原样返回。
    /// <see cref="Control"/> 是<b>实例</b>，只能有一个父容器 —— 同一页开在两个分屏里时两边会抢。
    /// <para>
    /// 宿主要让两边都显示，把 <c>Icon</c> 传成 <b><c>Func&lt;Control&gt;</c> 工厂</b>：
    /// 每个标签页控件各叫一次，各拿一份自己的实例，谁也不抢谁。
    /// 结果按标签页控件缓存，重建时不会每次重造。
    /// </para>
    /// </remarks>
    private object? ResolveIcon(ForgeEditorTab control, object? icon)
    {
        if (icon is not Func<Control> factory)
        {
            _madeIcons.Remove(control);
            return icon;
        }

        if (_madeIcons.TryGetValue(control, out var cached) && ReferenceEquals(cached.Source, icon))
        {
            return cached.Made;
        }

        var made = factory();
        _madeIcons[control] = (icon, made);
        return made;
    }

    /// <summary>某个后代控件在本控件坐标系里的矩形。</summary>
    private Rect? BoundsIn(Control child) =>
        child.TranslatePoint(default, this) is { } origin ? new Rect(origin, child.Bounds.Size) : null;

    private void EndDrag(EditorGroupViewModel group, ForgeEditorTab control, bool commit)
    {
        if (!ReferenceEquals(_dragTab, control))
        {
            return;
        }

        if (_groupStrips.TryGetValue(group, out var strip))
        {
            strip.DraggedChild = null;
            strip.InvalidateArrange();
        }

        // ⚠ 落库要在清状态**之前**，而且只在真松手时做。
        //   捕获被抢走（弹窗、Alt-Tab）走的是取消，不该顺手切一刀分屏。
        if (commit && _dragOut && _dragId is not null && _dropGroup is not null
            && _dropSide != EditorDropSide.None)
        {
            _area!.DropTab(_dragId, _dropGroup.Group, _dropSide);
        }

        _overlay.Clear();
        _dragThumb = null;
        _dragOut = false;
        _dropGroup = null;
        _dropSide = EditorDropSide.None;
        _dragTab = null;
        _dragGroup = null;
        _dragId = null;
        _dragStarted = false;
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

        foreach (var control in wanted)
        {
            if (!panel.Children.Contains(control))
            {
                panel.Children.Add(control);
            }
        }

        // ★ 次序交给面板去排，**不动 Children 的顺序**。
        //
        //   原来这里用 Children.Move 把子控件挪到正确位置。Avalonia 的 Move 是
        //   摘下来再挂回去，顺带把**指针捕获**也丢了 —— 正在拖的那个标签页一旦被挪，
        //   后面的 PointerReleased 就不再送到它身上，拖动收不了尾，
        //   标签停在松手处不归位、标签条里留一个缺口。
        //
        //   （这件事本不该由我们操心：次序本来就该由数据源决定、容器由控件自己管。
        //     见 HeaderTabsPanel.Order 上的说明。）
        if (panel is HeaderTabsPanel strip)
        {
            strip.Order = wanted;
        }
    }
}
