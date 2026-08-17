using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
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
    private Point _dragGrab;
    private global::Avalonia.Media.Imaging.RenderTargetBitmap? _dragThumb;
    private (StripeSide Side, StripeGroup Group)? _lastZone;
    private readonly ForgeDragOverlay _overlay = new();

    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeToolWindowFrame, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    /// <summary><see cref="TransparentBackdrop"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> TransparentBackdropProperty =
        AvaloniaProperty.Register<ForgeToolWindowFrame, bool>(nameof(TransparentBackdrop));

    /// <summary><see cref="StripeButtonArc"/> 的属性定义。</summary>
    public static readonly StyledProperty<double?> StripeButtonArcProperty =
        AvaloniaProperty.Register<ForgeToolWindowFrame, double?>(nameof(StripeButtonArc));

    /// <summary><see cref="MoreButtonSide"/> 的属性定义。</summary>
    public static readonly StyledProperty<StripeSide> MoreButtonSideProperty =
        AvaloniaProperty.Register<ForgeToolWindowFrame, StripeSide>(nameof(MoreButtonSide));

    /// <summary><see cref="ShowHeaderButtons"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> ShowHeaderButtonsProperty =
        AvaloniaProperty.Register<ForgeToolWindowFrame, bool>(nameof(ShowHeaderButtons), defaultValue: true);

    /// <summary><see cref="ShowToolWindowNames"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> ShowToolWindowNamesProperty =
        AvaloniaProperty.Register<ForgeToolWindowFrame, bool>(nameof(ShowToolWindowNames));

    /// <summary><see cref="EditorContent"/> 的属性定义。</summary>
    public static readonly StyledProperty<Control?> EditorContentProperty =
        AvaloniaProperty.Register<ForgeToolWindowFrame, Control?>(nameof(EditorContent));

    static ForgeToolWindowFrame()
    {
        PaletteProperty.Changed.AddClassHandler<ForgeToolWindowFrame>((f, _) => f.Sync());

        // 外观换了一份：重跑取色（可能换了族或取色回调），再同步一遍（岛几何、字重、行高都在里面）。
        // ⚠ **不要在这里把 _variantApplied 归零**（曾经这么写过，是个双重 bug）：
        // ApplyThemeVariant 开头那道「宿主是不是自己管配色」的探测用 IsSet(PaletteProperty) 判，
        // 而我们自己写过 Palette 之后它恒为真。归零 _variantApplied 等于把探测重新武装，
        // 于是换族那一下被判成"宿主自己管"，_paletteSetByHost 被永久钉死 ——
        // 换族当场不生效，此后连跟随系统明暗也一起停摆。
        // _variantApplied 保持为真时，ApplyThemeVariant 会跳过探测、直接按新族重算，这正是要的。
        ForgeChrome.AppearanceProperty.Changed.AddClassHandler<ForgeToolWindowFrame>((f, _) =>
        {
            f.ApplyThemeVariant();
            f.Sync();
        });
        TransparentBackdropProperty.Changed.AddClassHandler<ForgeToolWindowFrame>((f, _) => f.Sync());
        StripeButtonArcProperty.Changed.AddClassHandler<ForgeToolWindowFrame>((f, _) => f.Sync());
        EditorContentProperty.Changed.AddClassHandler<ForgeToolWindowFrame>((f, _) => f.Sync());
        ShowHeaderButtonsProperty.Changed.AddClassHandler<ForgeToolWindowFrame>((f, _) => f.Sync());
        ShowToolWindowNamesProperty.Changed.AddClassHandler<ForgeToolWindowFrame>((f, _) =>
        {
            // 菜单里的「显示工具窗口名称」改的是控件属性；写回 VM 才跟着布局一起存下去。
            // 两边都是幂等 setter，回写不会来回弹。
            if (f._shell is { } shell)
            {
                shell.ShowToolWindowNames = f.ShowToolWindowNames;
            }

            f.Sync();
        });
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

        // 覆盖层加在 _root **之后** —— 父控件的 Render 在子控件之下，
        // 拖拽反馈必须盖在条带与面板之上才看得见。
        VisualChildren.Add(_overlay);
        LogicalChildren.Add(_overlay);

        ActualThemeVariantChanged += (_, _) => ApplyThemeVariant();

        // ★ 拖动与松手接在**框架**上：同步（Sync）会重排、重挂条带按钮，
        //   接在按钮身上的处理器会跟着那次重挂一起失效。
        //   **点击不在其列** —— 那归 Button 自己（见 WireStripeButton），
        //   框架只管"拖没拖、拖到哪"。
        const RoutingStrategies gestureRoutes =
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble | RoutingStrategies.Direct;
        AddHandler(PointerMovedEvent, OnStripePointerMoved, gestureRoutes, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnStripePointerReleased, gestureRoutes, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, OnStripePointerCaptureLost, gestureRoutes, handledEventsToo: true);

        // 标题条拖出去 = 浮动（见 WireHeaderDrag）
        AddHandler(PointerMovedEvent, OnHeaderPointerMoved, gestureRoutes, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnHeaderPointerReleased, gestureRoutes, handledEventsToo: true);

        // ★ 焦点跟踪：「失焦自动收起」（停靠不固定 / 取消停靠）靠它才动得起来。
        //   上游是全局焦点监听（ToolWindowManagerLifecycle.kt:97-106）；这里用**按下**当焦点转移的时机 ——
        //   面板内容五花八门（图像、表格、宿主的自绘控件），不少压根不接受键盘焦点，
        //   只等 GotFocus 会漏掉一大半点击。Tunnel + handledEventsToo：子控件吃掉的按下也算数。
        AddHandler(PointerPressedEvent, OnFocusMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(GotFocusEvent, OnFocusMoved, gestureRoutes, handledEventsToo: true);
    }

    /// <summary>
    /// 焦点落到了某处：按下/取得焦点的位置在哪个面板里，别的「失焦就收」的面板就该收了。
    /// </summary>
    /// <remarks>
    /// 落点不在任何面板里（编辑区、条带空白、命令栏…）就等同于"焦点离开了工具窗口"，
    /// 对应上游的 <c>OnEditorFocused</c> 那一支。
    /// <para>条带按钮不算：点它是开关面板，<see cref="Toggle"/> 会自己走激活/收起那条路，
    /// 在这里再判一次会把刚要打开的那个当成"焦点去了别处"。</para>
    /// </remarks>
    private void OnFocusMoved(object? sender, RoutedEventArgs e)
    {
        if (_shell is not { } shell || e.Source is not Visual source)
        {
            return;
        }

        // 这两样都不是「焦点去了别处」，按下它们不该把面板收掉：
        //   · 条带按钮 —— 点它是开关面板，Toggle 自己会走激活/收起那条路；
        //   · 拖手 —— 拖的正是这块面板的边界。⚠ 这条不写就得到「取消停靠的面板抓不住」：
        //     本处理器是 Tunnel，按下时**先于**拖手跑，判成失焦 → 面板当场收起 → 拖动无从开始。
        if (source.GetSelfAndVisualAncestors().Any(x => x is ForgeStripeButton or ForgeDockResizer))
        {
            return;
        }

        var owner = _panelHosts.FirstOrDefault(x => source.GetSelfAndVisualAncestors().Contains(x.Value));

        if (owner.Key is null)
        {
            shell.Manager.OnEditorFocused();
            return;
        }

        // 点进面板 = 焦点到了它：**激活**它（条带按钮的选中态读的就是这个），
        // 顺带把别的「失焦就收」的面板收起来（Activate 内部做）。
        // 已经是活动的就别再激活一遍 —— 那会白白触发一次全量同步。
        if (!string.Equals(shell.Manager.ActiveToolWindowId, owner.Key, StringComparison.Ordinal))
        {
            shell.Manager.Activate(owner.Key);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyThemeVariant();
    }

    private bool _variantApplied;
    private bool _paletteSetByHost;

    /// <summary>
    /// 按宿主的明暗变体挑一套配色。
    /// </summary>
    /// <remarks>
    /// **宿主显式赋过 <see cref="Palette"/> 就不管了** —— 跟随变体是默认行为，
    /// 不该悄悄盖掉宿主写进去的值（这条是渲染快照把暗色渲成浅色才发现的）。
    /// <para>
    /// 判据用 <c>IsSet</c> 而不是「有没有收到变更通知」：属性的默认值就是
    /// <see cref="ForgePalette.ExpUiDark"/>，宿主赋同一个值**不会触发变更通知**，
    /// 靠通知判断会漏掉这种情况（正是这么漏的）。
    /// </para>
    /// </remarks>
    /// <summary>
    /// 外观：配色族、取色、岛几何、标题条与标签行的字重和行高。
    /// </summary>
    /// <remarks>
    /// 一个对象管完，**设在这儿就自动流到编辑区**（继承式附加属性，见
    /// <see cref="ForgeChrome.AppearanceProperty"/>）—— 宿主不必两边各设一遍，也就不会只设一边。
    /// 改一处用 <c>with</c>：<c>frame.Appearance = frame.Appearance with { Family = … };</c>
    /// </remarks>
    public ForgeAppearance Appearance
    {
        get => ForgeChrome.GetAppearance(this);
        set => ForgeChrome.SetAppearance(this, value);
    }

    private void ApplyThemeVariant()
    {
        if (!Appearance.FollowThemeVariant)
        {
            return;
        }

        // 我们自己写过之后 IsSet 也为真，所以只在第一次之前认它
        if (!_variantApplied && IsSet(PaletteProperty))
        {
            _paletteSetByHost = true;
        }

        if (_paletteSetByHost)
        {
            return;
        }

        var wanted = Appearance.PaletteResolver?.Invoke(Appearance.Family, ActualThemeVariant)
                     ?? ForgePalette.ForTheme(Appearance.Family, ActualThemeVariant);
        if (!ReferenceEquals(Palette, wanted))
        {
            Palette = wanted;
        }

        _variantApplied = true;
    }

    /// <summary>配色。</summary>
    /// <remarks>
    /// <see cref="ForgeAppearance.FollowThemeVariant"/> 为真时（默认），它跟着宿主的明暗变体自动切，
    /// 不需要手动赋值。这是**算出来的结果**，想换配色请改 <see cref="Appearance"/>。
    /// </remarks>
    public ForgePalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>
    /// 不画「海」，让宿主窗口背后的东西透上来（Windows 云母 / 亚克力等）。默认 <see langword="false"/>。
    /// </summary>
    /// <remarks>
    /// 影响的是**没有被岛占住的那些地方**：停靠区底、条带底、岛与岛之间那条缝。
    /// 岛自己照旧不透明——面板内容要能读，透了就没法看。
    /// <para>
    /// 与配色**正交**，所以做成控件上的开关而不是「换一套透明配色」：
    /// 直接赋 <see cref="Palette"/> 会被当成「宿主自己管配色」，从此不再跟随系统明暗
    /// （见 <see cref="ForgeAppearance.FollowThemeVariant"/>）——那是拿一个 bug 换一个效果。
    /// </para>
    /// <para>
    /// ⚠ 只是「不画」。窗口本身透不透明由**宿主**负责（Avalonia 侧是
    /// <c>TransparencyLevelHint</c> + 透明的 <c>Window.Background</c>）；宿主没配的话，
    /// 透出来的是宿主自己的底色，不会凭空出现云母。
    /// </para>
    /// </remarks>
    public bool TransparentBackdrop
    {
        get => GetValue(TransparentBackdropProperty);
        set => SetValue(TransparentBackdropProperty, value);
    }

    /// <summary>
    /// 条带按钮的圆角**直径**；<see langword="null"/> 表示用主题默认（常规 12 / 紧凑 8）。
    /// </summary>
    /// <remarks>
    /// ⚠ 是直径不是半径，与上游 <c>Button.ToolWindow.arc</c> 同语义
    /// （见 <see cref="ForgeMetrics.StripeButtonArc"/>）。想要"完全的圆"就传按钮短边的长度。
    /// </remarks>
    public double? StripeButtonArc
    {
        get => GetValue(StripeButtonArcProperty);
        set => SetValue(StripeButtonArcProperty, value);
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

    /// <summary>
    /// 面板标题条右侧那两个按钮（⋮ 选项 / — 隐藏）显不显示。默认开。
    /// </summary>
    /// <remarks>上游恒显示（<c>ToolWindowHeader.kt:119</c>）；这里给宿主留一个关掉的余地。</remarks>
    public bool ShowHeaderButtons
    {
        get => GetValue(ShowHeaderButtonsProperty);
        set => SetValue(ShowHeaderButtonsProperty, value);
    }

    /// <summary>
    /// 宿主往某个面板的标题条上追加自己的按钮（排在内置那两个左边）。
    /// </summary>
    /// <remarks>
    /// 参数是面板 id，返回这个面板要加的按钮；返回 <see langword="null"/> 或空就不加。
    /// 对应上游的 <c>ToolWindow.setTitleActions(...)</c>。
    /// <example>
    /// <code>
    /// frame.HeaderButtonsExtension = id =&gt; id == "CamList"
    ///     ? [new ForgeHeaderButton { Icon = ForgeIcons.Find }]
    ///     : null;
    /// </code>
    /// </example>
    /// ⚠ 每次同步都会问一次，**同一个面板请返回同一批控件实例**（或按 id 缓存），
    /// 否则每次重画都在造新控件。
    /// </remarks>
    public Func<string, IReadOnlyList<Control>?>? HeaderButtonsExtension { get; set; }

    /// <summary>
    /// 宿主往面板 ⋮ 菜单里追加自己的项。
    /// </summary>
    /// <remarks>
    /// 对应上游的 <c>additionalGearActions</c>（<c>ToolWindowImpl.kt:857-866</c>）。
    /// <para>
    /// <b>库默认只放布局相关的三项</b>（视图模式 / 移至 / 从边栏中移除）——
    /// 上游那份里的「快速搜索 / 显示工具栏 / 标签页分组 / 关闭所有 / 帮助」都属于
    /// **面板内容**那一层，库不知道宿主往面板里放了什么，给了也用不上。要就从这里塞。
    /// </para>
    /// <para>参数是（菜单, 面板 id）；菜单原样交给宿主，<c>Add</c> 追加、<c>Insert(0, …)</c> 插到最前。</para>
    /// <example>
    /// <code>
    /// frame.HeaderMenuExtension = (menu, id) =&gt;
    /// {
    ///     if (id != DockId.CAM_LIST.GetId()) return;
    ///     menu.Items.Add(new Separator());
    ///     menu.Items.Add(new MenuItem { Header = "刷新设备列表" });
    /// };
    /// </code>
    /// </example>
    /// </remarks>
    public Action<ContextMenu, string>? HeaderMenuExtension { get; set; }

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

    /// <summary>拖拽反馈那一层 —— 盖在所有控件之上，只画不吃鼠标。</summary>
    public ForgeDragOverlay DragOverlay => _overlay;

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
            _shell.PropertyChanged -= OnShellPropertyChanged;
        }

        _shell = shell;
        _shell.StateChanged += OnShellStateChanged;
        NormalizeViewModes();
        _shell.PropertyChanged += OnShellPropertyChanged;

        // 「显示名字」的真相在 VM（它跟布局一起存），控件跟着它走 ——
        // 宿主的恢复顺序是「先 Attach、后读存档」，所以还得订阅着，等存档读回来再跟一次。
        //
        // 接上的这一刻谁说了算：**宿主显式设过控件就以控件为准**（判据用 IsSet，
        // 同 Palette 那处），否则以 VM 为准。反过来的话，宿主在建控件时配的初值
        // 会被一个还没读存档的 VM 冲掉。
        if (IsSet(ShowToolWindowNamesProperty))
        {
            _shell.ShowToolWindowNames = ShowToolWindowNames;
        }
        else
        {
            ShowToolWindowNames = _shell.ShowToolWindowNames;
        }

        Sync();
    }

    private void OnShellPropertyChanged(object? sender, global::System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToolWindowShellViewModel.ShowToolWindowNames) && _shell is { } shell)
        {
            ShowToolWindowNames = shell.ShowToolWindowNames;
        }
    }

    /// <summary>接上的外壳 VM。</summary>
    public ToolWindowShellViewModel? Shell => _shell;

    private void OnShellStateChanged(object? sender, ToolWindowStateChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        // ★「激活」只改外观，**不重排**。
        //   全量同步会把面板宿主重新挂进停靠区，而点面板里的按钮时正好会触发一次激活 ——
        //   容器一重挂，按下就丢了、点击发不出来（与"标签右键菜单弹不出来"同一类坑）。
        //   激活能改变的只有条带按钮的选中态和标题条的活动色，刷这两样就够。
        if (e.Kind == ToolWindowChangeKind.Activated)
        {
            RefreshActiveAppearance();
            return;
        }

        // 读档走的也是这条（RestoreLayout → StateChanged），所以掰形态放在这儿
        NormalizeViewModes();
        Sync();
    }

    /// <summary>只把「谁是活动窗口」这件事刷到外观上：条带按钮的选中态、标题条的活动色。</summary>
    private void RefreshActiveAppearance()
    {
        if (_shell is not { } shell)
        {
            return;
        }

        foreach (var vm in shell.ToolWindows)
        {
            if (_stripeButtons.TryGetValue(vm.Id, out var button))
            {
                button.IsToolWindowVisible = vm.IsOpen;
                button.IsToolWindowActive = vm.IsActive;
            }

            if (_panelHeaders.TryGetValue(vm.Id, out var header))
            {
                header.IsActive = vm.IsActive;
            }
        }
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
        _overlay.Arrange(new Rect(finalSize));
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

        // 停靠区自己的底色是「海」——岛形态下，面板之间那条缝里露出来的就是它。
        // expUI 的 MainBackground 等于 ToolWindow.background，且缝宽为 0，看不出区别。
        // ⚠ 透底时用 Transparent 而**不是 null**：null 的 Panel 不参与命中测试，
        //    拖拽落点、条带空白处的右键菜单会一起失灵。
        _pane.Background = new SolidColorBrush(
            TransparentBackdrop ? Colors.Transparent : Palette.MainBackground);

        SyncStripes();
        SyncDockPane();
    }

    private void SyncStripes()
    {
        _left.Palette = Palette;
        _right.Palette = Palette;
        _left.TransparentBackdrop = TransparentBackdrop;
        _right.TransparentBackdrop = TransparentBackdrop;

        var stripeWidth = ShowToolWindowNames ? ForgeMetrics.StripeNamedDefaultWidth : 0;
        _left.CustomWidth = stripeWidth;
        _right.CustomWidth = stripeWidth;

        // 条带空白处的右键菜单只有「显示名字」（ResizeStripeManager.kt:51-59）
        // ⚠ 每次同步**重建**，不是 ??= 造一次 —— 菜单里那个勾要反映当前的 ShowToolWindowNames，
        //    只造一次的话勾永远停在第一次的状态。
        _left.ContextMenu = ForgeMenuBuilder.ForStripeBackground(
            () => ShowToolWindowNames = !ShowToolWindowNames, ShowToolWindowNames);
        _right.ContextMenu = ForgeMenuBuilder.ForStripeBackground(
            () => ShowToolWindowNames = !ShowToolWindowNames, ShowToolWindowNames);

        // ⚠ **先把所有组都摘干净，再统一往里放。**
        // 一个按钮换组时（比如从下组挪到上组），如果逐组处理，轮到目标组插入时
        // 它还挂在原来那组上 —— Avalonia 直接抛「already has a visual parent」。
        // 顺序还只在一个方向上出事（下→上崩、上→下不崩），是那种能潜伏很久的 bug。
        var plan = new List<(Panel Panel, IReadOnlyList<Control> Wanted)>();

        foreach (var side in new[] { StripeSide.Left, StripeSide.Right })
        {
            var toolbar = side == StripeSide.Left ? _left : _right;

            foreach (var group in new[] { StripeGroup.Top, StripeGroup.Bottom })
            {
                var panel = group == StripeGroup.Top ? toolbar.TopGroup : toolbar.BottomGroup;
                plan.Add((panel, BuildGroupChildren(side, group)));
            }
        }

        foreach (var (panel, wanted) in plan)
        {
            RemoveUnwanted(panel, wanted);
        }

        foreach (var (panel, wanted) in plan)
        {
            InsertAndOrder(panel, wanted);
        }
    }

    /// <summary>
    /// 算出一组里应该有哪些控件（**复用已有按钮**，不新建）。
    /// </summary>
    /// <param name="side">哪根条带。</param>
    /// <param name="group">哪一组。</param>
    /// <returns>这一组应有的子控件，按显示次序。</returns>
    private IReadOnlyList<Control> BuildGroupChildren(StripeSide side, StripeGroup group)
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

            wanted.Add(GetStripeButton(vm, side));
        }

        if (group == StripeGroup.Top)
        {
            // 「更多」按钮挂在上组之后（ToolWindowToolbar.kt:109）
            wanted.Add(GetMoreButton(side));
        }

        return wanted;
    }

    /// <summary>把面板的子控件调成 <paramref name="wanted"/>，只动有差异的位置。</summary>
    /// <remarks>跨面板搬控件时别用它，用 <see cref="RemoveUnwanted"/> + <see cref="InsertAndOrder"/> 分两遍。</remarks>
    private static void Reconcile(Panel panel, IReadOnlyList<Control> wanted)
    {
        RemoveUnwanted(panel, wanted);
        InsertAndOrder(panel, wanted);
    }

    /// <summary>第一遍：把不该留在这个面板里的摘掉。</summary>
    /// <param name="panel">面板。</param>
    /// <param name="wanted">这个面板应有的子控件。</param>
    private static void RemoveUnwanted(Panel panel, IReadOnlyList<Control> wanted)
    {
        for (var i = panel.Children.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(panel.Children[i]))
            {
                panel.Children.RemoveAt(i);
            }
        }
    }

    /// <summary>第二遍：把该有的放进来并排好次序。</summary>
    /// <param name="panel">面板。</param>
    /// <param name="wanted">这个面板应有的子控件。</param>
    private static void InsertAndOrder(Panel panel, IReadOnlyList<Control> wanted)
    {
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
    private ForgeStripeButton GetStripeButton(ToolWindowViewModel vm, StripeSide side)
    {
        if (!_stripeButtons.TryGetValue(vm.Id, out var button))
        {
            button = new ForgeStripeButton { Tag = vm };
            WireStripeButton(button, vm);
            _stripeButtons[vm.Id] = button;
        }

        button.Palette = Palette;
        // 菜单每次同步重建，理由同 SyncStripes 里那处：勾选状态要跟着走
        button.ContextMenu = ForgeMenuBuilder.ForStripeButton(
            _shell!.Manager, vm.Id, () => ShowToolWindowNames = !ShowToolWindowNames, ShowToolWindowNames);
        // 内边距按左右取不同的键；Islands 下两侧镜像不对称，设错边图标会朝窗口里侧挤。
        // 按钮实例是**复用**的，拖到另一条带时这里要跟着改，不能只在新建时设。
        button.IsLeftStripe = side == StripeSide.Left;
        button.ArcOverride = StripeButtonArc;
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

        // 点击**归框架控件管**：Button 只在「按下与松开落在同一个按钮上」时才发 Click，
        // 那正是点击的定义 —— 手抖多少像素都不影响，键盘空格 / 回车也照发。
        // 曾经这里自己判"位移没超过拖拽死区才算点击"，做出过「侧边栏按钮有时要点两下」。
        button.Click += (_, _) => Toggle(id);

        // ⚠ 必须 handledEventsToo：Button 的类处理器会把 PointerPressed 标成 handled
        //   （它要靠这一下进入按下态），普通的 `+=` 处理器会被跳过 —— 于是拖拽起不来。
        button.AddHandler(
            PointerPressedEvent,
            (object? _, PointerPressedEventArgs e) =>
        {
            // 右键交给菜单，不当拖拽起点
            if (e.GetCurrentPoint(button).Properties.IsRightButtonPressed)
            {
                return;
            }

            _dragId = id;
            _dragStart = e.GetPosition(this);
            // 抓在按钮的哪个位置——虚影要让同一个点始终待在光标下，不然一开拖就"跳"一下。
            // 对应 ToolWindowDragHelper.kt:250 · setInitialOffsetFromStripeButton。
            _dragGrab = e.GetPosition(button);
            _dragButtonBounds = button.TranslatePoint(default, this) is { } origin
                ? new Rect(origin, button.Bounds.Size)
                : default;

            // ⚠ **全程不抢指针捕获**：捕获在 Button 手上，它才收得到松手、才发得出 Click。
            //    抢过来自己管，就等于把"按下-松开算一次点击"重新实现一遍。
        },
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble | RoutingStrategies.Direct,
            handledEventsToo: true);
    }

    // ── 标题条拖出去 = 浮动 ────────────────────────────────────────────────
    //
    // 对位上游 ToolWindowDragHelper：按住面板的标题条往外拖，面板就脱出来浮着。
    // 走的是**和菜单里「浮动」同一条路**（SetType(Floating)），不另开一种状态 ——
    // 否则就会有"拖出来的浮动"和"点出来的浮动"两套行为，早晚对不齐。

    private string? _headerDragId;
    private Point _headerDragStart;
    private Point _headerDragGrab;
    private bool _headerDragging;

    private void WireHeaderDrag(ForgeToolWindowHeader header, string id)
        => header.AddHandler(
            PointerPressedEvent,
            (object? _, PointerPressedEventArgs e) =>
            {
                // 已经兼任窗口标题栏的（浮动）不在此列：那时按住是**搬窗口**，归系统
                if (header.IsWindowTitleBar
                    || !e.GetCurrentPoint(header).Properties.IsLeftButtonPressed
                    || !header.IsWindowDragArea(e.Source as Visual))
                {
                    return;
                }

                _headerDragId = id;
                _headerDragging = false;
                _headerDragStart = e.GetPosition(this);

                // 抓在面板的哪个位置 —— 浮出来之后要让同一个点待在光标下，不然会"跳"一下
                _headerDragGrab = _panelHosts.TryGetValue(id, out var host)
                    ? e.GetPosition(host)
                    : default;
            },
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);

    private void OnHeaderPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_headerDragId is not { } id || _shell is not { } shell)
        {
            return;
        }

        var point = e.GetPosition(this);

        if (!_headerDragging)
        {
            var moved = Math.Sqrt(Math.Pow(point.X - _headerDragStart.X, 2) + Math.Pow(point.Y - _headerDragStart.Y, 2));
            if (moved < ForgeMetrics.StripeButtonDragStartDeadzone)
            {
                return;
            }

            _headerDragging = true;
            shell.Manager.SetType(id, ToolWindowType.Floating);

            // 拖出去之后指针还得归我们，不然一出主窗口就收不到移动了
            e.Pointer.Capture(this);
        }

        FollowPointer(id, point);

        // 落点反馈与拖条带按钮**同一套**：条带上让出插入槽、画高亮。
        // 拖回条带上松手 = 重新停靠（见 OnHeaderPointerReleased）。
        UpdateDragFeedback(point);
    }

    private void OnHeaderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_headerDragging || _headerDragId is not { } id || _shell is not { } shell)
        {
            _headerDragId = null;
            _headerDragging = false;
            return;
        }

        var point = e.GetPosition(this);

        // ⚠ 顺序同拖条带按钮那条：**先算落点，再清拖拽态** ——
        //   落点判定靠 DraggedButton（读 _headerDragId）把自己排除在外。
        var target = ResolveDropTarget(point);

        _overlay.Clear();
        _headerDragId = null;
        _headerDragging = false;
        ClearDragLayout();

        if (target is not { } t)
        {
            // 松手不在条带上 → 就停在浮动状态，位置已经跟着光标了
            return;
        }

        // 落回条带 = 重新停靠：先回内部形态（模型记着上次是停靠固定还是不固定），
        // 再搬到落点那一格。次序不能反 —— 还浮着的时候搬位置，落点那格的互斥判定看不到它。
        shell.Manager.SetType(id, shell.Manager.ResolveInternalType(id));
        shell.Manager.MoveTo(id, t.Anchor, t.Order, t.IsSplit);
    }

    /// <summary>让浮出来的窗口跟着光标走，抓点保持在按下时的那个位置。</summary>
    private void FollowPointer(string id, Point point)
    {
        if (!_externalWindows.TryGetValue(id, out var external))
        {
            return;
        }

        if (this.PointToScreen(point - _headerDragGrab) is { } origin)
        {
            external.Window.Position = origin;
        }
    }

    private void OnStripePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragId is null || DraggedButton is not { } button)
        {
            return;
        }

        var point = e.GetPosition(this);
        var moved = Math.Sqrt(Math.Pow(point.X - _dragStart.X, 2) + Math.Pow(point.Y - _dragStart.Y, 2));

        // 拖动开始的两个条件，缺一不可：
        //   ① 走够基类的死区 7（MouseDragHelper.java:32）；
        //   ② **已经离开这个按钮**。
        // ② 不是上游的原话，是这边必须加的一条：拖动一开始就会把被拖按钮排成零尺寸
        //   （腾出落点槽），而按钮一旦没了尺寸，Button 就判不出"松手落在自己身上"、
        //   发不出 Click —— 手抖八九个像素的一次点击会当场变成"拖了一下又没拖成"，
        //   也就是「侧边栏按钮有时要点两下」。
        if (moved < ForgeMetrics.StripeButtonDragStartDeadzone || _dragButtonBounds.Contains(point))
        {
            return;
        }

        // 拖动**从这一刻**起有视觉：快照拍在按钮还排在原位的时候（此后它被排成零尺寸就拍不到了），
        // 对应上游 createStripeButtonDragImage 在 dragStarted 时产出 BufferedImage。
        // 这一步只改视觉，不碰捕获 —— 理由见按下处。
        _dragThumb ??= ForgeDragOverlay.CaptureThumb(button);
        UpdateDragFeedback(point);
    }

    private void OnStripePointerReleased(object? sender, PointerReleasedEventArgs e)
        => EndStripeGesture(e.GetPosition(this), commit: true);

    /// <summary>捕获丢了（窗口失活、别处抢走…）：拖到一半被打断，按取消收尾。</summary>
    private void OnStripePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        => EndStripeGesture(_dragStart, commit: false);

    /// <summary>
    /// 手势收尾：只管「搬没搬家」，<b>点击不归这里</b>（那是 <c>Button.Click</c>）。
    /// </summary>
    /// <remarks>
    /// 「拖起来了」且「松手落在原按钮**之外**」才算搬家。落回原处 = 什么都没搬，
    /// 而 Button 会照常发 Click —— 于是它就是一次点击，不用这里再判一遍。
    /// </remarks>
    private void EndStripeGesture(Point point, bool commit)
    {
        if (_dragId is null)
        {
            return;
        }

        if (DraggedButton is { } button)
        {
            button.Opacity = 1;
        }

        _overlay.Clear();

        var dragged = _dragId;
        var moved = _dragThumb is not null && !_dragButtonBounds.Contains(point);

        // ⚠ 顺序要紧：ResolveDropTarget 靠 DraggedButton（读 _dragId）把被拖项排除在外，
        // 所以**先算落点，再清 _dragId 与排布态**。反过来的话被拖的那个又被算进序列，
        // 落点当场偏一格 —— 正是「放在下面、框提示在上面」那个 bug 的另一半。
        var target = commit && moved ? ResolveDropTarget(point) : null;

        _dragId = null;
        ClearDragLayout();

        if (commit && moved && dragged is not null && target is { } t)
        {
            _shell!.Manager.MoveTo(dragged, t.Anchor, t.Order, t.IsSplit);
        }
    }

    private ForgeMoreButton GetMoreButton(StripeSide side)
    {
        if (!_moreButtons.TryGetValue(side, out var button))
        {
            button = new ForgeMoreButton { Side = side, IsLeftStripe = side == StripeSide.Left };
            var self = button;
            button.Click += (_, _) =>
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

    /// <summary>按下那一刻，被按的条带按钮在框架坐标系里的矩形（松手落回它里面 = 没搬家）。</summary>
    private Rect _dragButtonBounds;

    /// <summary>上一次算出来的空槽落点：哪一组、第几个之前。</summary>
    private (StripeItemsPanel Panel, int Index)? _lastDropSlot;

    /// <summary>
    /// 正在被拖走的那个条带按钮；没在拖就是 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 两种拖法共用它：拖条带按钮，和**拖面板标题条**（那时面板已经浮出来了，
    /// 但落点判定要把它自己的按钮排除在外 —— 与拖按钮时是同一件事）。
    /// </remarks>
    private ForgeStripeButton? DraggedButton
        => (_headerDragId ?? _dragId) is { } id && _stripeButtons.TryGetValue(id, out var button) ? button : null;

    /// <summary>
    /// 一个条带按钮对应的工具窗口在模型里的 order。
    /// </summary>
    /// <remarks>
    /// ⚠ **别从 <c>Tag</c> 读**。<c>Tag</c> 装的是 <see cref="ToolWindowViewModel"/>
    /// （见 <c>GetStripeButton</c>），而落点解析里曾写成 <c>Tag is int</c> ——
    /// 那个条件**永远为假**，于是每次拖放算出来的 order 都是 -1（接到末尾）。
    /// 上组表现为"拖到哪都排最后"；BOTTOM 组因为是倒序显示，-1 经
    /// <see cref="StripeDropResolver.AdjustForReversedBottomGroup"/> 变成 0、
    /// 拿到最小 order，**显示在最下面** —— 用户报的「拖到第一个反而变成最下面」就是它。
    /// <para>
    /// 类型不匹配的模式匹配编译器不报错，也没有任何测试覆盖到"落点 order 是对的"，
    /// 所以它一直没被发现。现在直接问模型要，绕开这个中转。
    /// </para>
    /// </remarks>
    private int? OrderOf(ForgeStripeButton button)
        => button.Tag is ToolWindowViewModel vm && _shell?.Manager.Layout.GetInfo(vm.Id) is { } info
            ? info.Order
            : null;

    /// <summary>一组里参与排布的项数（排除被拖走的那个），即"接到末尾"对应的序号。</summary>
    private int CountLayoutItems(StripeItemsPanel panel)
    {
        var dragged = DraggedButton;
        var count = 0;
        foreach (var child in panel.Children)
        {
            if (child.IsVisible && !ReferenceEquals(child, dragged))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>这一组里有没有那条分组分隔符（决定要不要画「另一半」的虚线框）。</summary>
    private static bool HasSeparator(StripeItemsPanel panel)
    {
        foreach (var child in panel.Children)
        {
            if (child is ForgeStripeSeparator && child.IsVisible)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>四个按钮组（左右两根条带 × 上下两组）。</summary>
    private IEnumerable<StripeItemsPanel> StripePanels()
    {
        yield return _left.TopGroup;
        yield return _left.BottomGroup;
        yield return _right.TopGroup;
        yield return _right.BottomGroup;
    }

    /// <summary>
    /// 收掉拖拽期的排布态：被拖项归位、空槽撤掉。
    /// </summary>
    /// <remarks>
    /// **松手、以及任何提前退出拖拽的路径都要调**——漏掉一处，那个按钮就会一直
    /// 被排在面板外面（<see cref="StripeItemsPanel.ArrangeOverride"/> 把它挪到了 y 为负的地方），
    /// 表现是「拖完之后少了一个图标」，而且下次 Sync 也救不回来。
    /// </remarks>
    private void ClearDragLayout()
    {
        foreach (var panel in StripePanels())
        {
            panel.DraggedChild = null;
            panel.DropIndex = -1;
        }

        _lastDropSlot = null;
        _lastZone = null;
        _dragThumb?.Dispose();
        _dragThumb = null;
    }

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
    /// <summary>紧凑与否是按钮级的属性，从条带上现有的按钮读一个。</summary>
    /// <returns>当前是不是紧凑模式。</returns>
    private bool ResolveCompact()
        => _left.TopGroup.Children.OfType<ForgeStripeButton>().FirstOrDefault()?.IsCompact ?? false;

    /// <summary>
    /// 拖动过程中的三样反馈：目标区域高亮、条带上的落点方块、「另一半」的虚线框。
    /// </summary>
    /// <remarks>
    /// 出处见 <see cref="ForgeDragOverlay"/>。落点方块按上游
    /// <c>AbstractDroppableStripe.kt:634-642</c> 去掉图标 padding，
    /// 所以它比按钮小一圈、正好是图标那块地。
    /// </remarks>
    /// <param name="point">当前指针位置（相对本控件）。</param>
    private void UpdateDragFeedback(Point point)
    {
        _overlay.Palette = Palette;
        _overlay.IsCompact = ResolveCompact();

        var metrics = ForgeStripeButtonMetrics.ForTheme(Palette.Family, _overlay.IsCompact);
        var buttonHeight = metrics.Height;

        // 被拖的那个从**每一组**的排布里拿掉——它可能正被拖去另一根条带，
        // 只清源组的话，目标组那边仍会把它算进去。
        var dragged = DraggedButton;
        foreach (var panel in StripePanels())
        {
            panel.DraggedChild = dragged;
            panel.DropIndex = -1;
            panel.DropSlotHeight = buttonHeight;
        }

        // 跟随光标的虚影：抓取点始终待在光标下。画的是拖拽开始时拍的快照，
        // 与被拖控件的排布解耦（它此刻已被排成零尺寸）。
        if (dragged is not null && _dragThumb is not null)
        {
            _overlay.ThumbImage = _dragThumb;
            _overlay.ThumbRect = new Rect(point - _dragGrab, _dragThumb.Size);
        }

        _lastDropSlot = null;
        if (ResolveDropTarget(point) is not { } target)
        {
            _overlay.Clear();
            return;
        }

        // ⚠ 左右一律从**落在哪根条带**推导，不能看 target.Anchor ——
        //    下组的 anchor 恒为 BOTTOM，拿它判会让右条带下组用成左条带的内边距，
        //    落点方块整体偏 3px（用户报的「右下角的投影不居中」）。
        //    四个角表现不一致，根子就是同一件事在不同地方分别从 side 和 anchor 推。
        var side = _lastZone?.Side ?? (target.Anchor == ToolWindowAnchor.Right ? StripeSide.Right : StripeSide.Left);
        var toolbar = side == StripeSide.Right ? _right : _left;
        var padding = metrics.IconPadding(side == StripeSide.Left, ShowToolWindowNames);

        // 让目标组真的空出一格：后面的按钮跟着让位（layoutDragButton 推进 eachY 的等价物）。
        // 必须立刻 UpdateLayout —— DropSlotRect 是 Arrange 期算的，不排一次读到的还是上一帧。
        if (_lastDropSlot is { } slot)
        {
            slot.Panel.DropIndex = slot.Index;
            slot.Panel.UpdateLayout();

            // 蓝框画在**那个空槽**上，不另算一遍位置。
            // 原先是「钉在指针那一行」——与真正的插入位置各算各的，于是必然出现
            // 「框提示的位置和松手后落到的位置不一致」。
            if (slot.Panel.DropSlotRect is { } rect
                && slot.Panel.TranslatePoint(rect.Position, this) is { } origin)
            {
                _overlay.DropRect = new Rect(
                    origin.X + padding.Left,
                    origin.Y + padding.Top,
                    Math.Max(0, slot.Panel.Bounds.Width - padding.Horizontal),
                    Math.Max(0, rect.Height - padding.Vertical));

                // 「另一半」：30 见方的虚线框，贴着落点方块下面。
                // ⚠ 条件是**该组里真的有那条分组分隔符**（AbstractDroppableStripe.kt:644-654
                //    「只在竖条带、且分隔器可见时画」），不是 target.IsSplit ——
                //    下组的 IsSplit 只表示"落在右条带"，右下角于是永远多一个虚线框。
                _overlay.SplitBoxRect = HasSeparator(slot.Panel)
                    ? new Rect(
                        origin.X + ((slot.Panel.Bounds.Width - ForgeMetrics.StripeSplitDropBoxSize) / 2),
                        origin.Y + rect.Height,
                        ForgeMetrics.StripeSplitDropBoxSize,
                        ForgeMetrics.StripeSplitDropBoxSize)
                    : null;
            }
        }

        _overlay.AreaRect = ResolveDropAreaRect(target.Anchor, toolbar, target.IsSplit);
    }

    /// <summary>
    /// 目标停靠区那片高亮占多大。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowToolbar.kt:284-302 · getToolWindowDropAreaScreenBounds</c>：
    /// 取那条边上第一个可见工具窗口的尺寸；一个都没开时退回
    /// <c>getFirstVisibleToolWindowSize</c> 的兜底 <b>350</b>（<c>:271</c>）。
    /// <para>
    /// ⚠ <b>「落到另一半时只高亮那一半」这条是按实机行为做的，不是按上游代码。</b>
    /// 上游 <c>ToolWindowDragHelper.kt:429-444</c> 拿到整块 bounds 之后会按
    /// <c>getDropToSide()</c> 折半，而 <c>StripeV2.getDropToSide()</c>（<c>:202-210</c>）
    /// 返回的是**非空** Boolean（split 恒 true，否则 <c>super ?: false</c>），
    /// 照代码读下来**两半都会被折半**——即落在左半时也该只高亮左半。
    /// 但实机 Rider 里落左半是整宽、落右半才是半宽，用户实测确认。
    /// 这里跟实机走；两者对不上的原因没查清，先记在这儿，别照代码"改回去"。
    /// </para>
    /// </remarks>
    /// <param name="anchor">目标边。</param>
    /// <param name="toolbar">那条边的条带。</param>
    /// <param name="isSplit">是不是落到这条边的「另一半」。</param>
    /// <returns>高亮矩形。</returns>
    private Rect ResolveDropAreaRect(ToolWindowAnchor anchor, Control toolbar, bool isSplit)
    {
        var origin = toolbar.TranslatePoint(default, this) ?? default;
        var stripe = toolbar.Bounds.Width;

        var host = _pane.Children.FirstOrDefault(
            x => x.IsVisible && ForgeDockPane.GetAnchor(x) == anchor);

        var visibleSize = host is null
            ? ForgeMetrics.DropAreaFallbackSize
            : anchor.IsHorizontal() ? host.Bounds.Height : host.Bounds.Width;

        var area = anchor switch
        {
            ToolWindowAnchor.Left => new Rect(origin.X + stripe, 0, visibleSize, Bounds.Height),
            ToolWindowAnchor.Right => new Rect(origin.X - visibleSize, 0, visibleSize, Bounds.Height),
            ToolWindowAnchor.Top => new Rect(0, 0, Bounds.Width, visibleSize),
            _ => new Rect(0, Math.Max(0, Bounds.Height - visibleSize), Bounds.Width, visibleSize),
        };

        if (!isSplit)
        {
            return area;
        }

        // 落到「另一半」：横边取右半、竖边取下半（切分方向与 ForgeDockPane.SplitSlot 一致）
        return anchor.IsHorizontal()
            ? area.WithX(area.X + (area.Width / 2)).WithWidth(area.Width / 2)
            : area.WithY(area.Y + (area.Height / 2)).WithHeight(area.Height / 2);
    }

    /// <summary>
    /// 落点属于哪根条带的哪一组。
    /// </summary>
    /// <remarks>
    /// 照抄 <c>ToolWindowToolbar.kt:138-149 · getStripeFor(screenPoint)</c>：
    /// **先问上组，再问下组，都不落就是 null（不给放）**。
    /// 两个落区的形状见 <see cref="StripeDropResolver.ResolveTopZone"/> /
    /// <see cref="StripeDropResolver.ResolveBottomZone"/>。
    /// <para>
    /// ⚠ 落区**不是按钮的内容矩形**。原先按内容矩形判、都不中就兜底成上组，于是
    /// 「点落在第一个图标上方」这种再正常不过的位置也会被判成上停靠——
    /// 用户实测报的就是这个。
    /// </para>
    /// </remarks>
    private (StripeSide Side, StripeGroup Group)? ResolveDropZone(Point point)
    {
        foreach (var side in new[] { StripeSide.Left, StripeSide.Right })
        {
            var toolbar = side == StripeSide.Left ? _left : _right;
            if (!toolbar.IsVisible || toolbar.TranslatePoint(default, this) is not { } origin)
            {
                continue;
            }

            var stripe = new Rect(origin, toolbar.Bounds.Size);

            var top = StripeDropResolver.ResolveTopZone(
                stripe, side == StripeSide.Right, FirstVisibleToolWindowSize(horizontal: true));
            if (top.Contains(point))
            {
                return (side, StripeGroup.Top);
            }
        }

        // 下组是**整窗底部**的一条带，与鼠标在哪根条带上无关；左半 / 右半分 split
        foreach (var side in new[] { StripeSide.Left, StripeSide.Right })
        {
            var toolbar = side == StripeSide.Left ? _left : _right;
            var bottom = StripeDropResolver.ResolveBottomZone(
                Bounds.Size,
                toolbar.BottomGroup.Bounds.Height,
                FirstVisibleToolWindowSize(horizontal: false),
                isSplit: side == StripeSide.Right);

            if (bottom.Contains(point))
            {
                return (side, StripeGroup.Bottom);
            }
        }

        return null;
    }

    /// <summary>
    /// 第一个可见工具窗口占多大；一个都没开就用兜底 350。
    /// </summary>
    /// <remarks>对应 <c>ToolWindowToolbar.kt:290-301 · getFirstVisibleToolWindowSize</c>。</remarks>
    private double FirstVisibleToolWindowSize(bool horizontal)
    {
        foreach (var vm in _shell!.ToolWindows)
        {
            if (!vm.IsOpen || _shell.Manager.Layout.GetInfo(vm.Id) is not { } info)
            {
                continue;
            }

            return (horizontal ? Bounds.Width : Bounds.Height) * info.Weight;
        }

        return ForgeMetrics.DropAreaFallbackSize;
    }

    private StripeDropTarget? ResolveDropTarget(Point point)
    {
        _lastZone = null;
        if (ResolveDropZone(point) is { } zone)
        {
            _lastZone = zone;
            var side = zone.Side;
            var group = zone.Group;
            var toolbar = side == StripeSide.Left ? _left : _right;

            {
                var panel = group == StripeGroup.Top ? toolbar.TopGroup : toolbar.BottomGroup;

                // Bounds 是相对父级的，逐级换算容易差 1px，直接问 Avalonia 要绝对位置
                if (panel.TranslatePoint(default, this) is not { } origin)
                {
                    return null;
                }

                var panelBounds = new Rect(origin, panel.Bounds.Size);

                var offset = point.Y - panelBounds.Y;

                // ⚠ **被拖的那个不参与**（AbstractDroppableStripe.kt:601 的 tools.remove(dragButton)）。
                // 它还挂在树上、Bounds 也还在，但排布时当它不存在；算落点时一并排除，
                // 否则它后面每一项的累计坐标都多算一个按钮高，判定整体偏一格。
                var dragged = DraggedButton;
                var sizes = new List<double>();
                // 与 sizes 平行：该位置是哪个按钮的 order；分隔符占一格但没有 order，记 null
                var orders = new List<int?>();
                var separatorIndex = int.MaxValue;

                foreach (var child in panel.Children)
                {
                    if (!child.IsVisible || ReferenceEquals(child, dragged))
                    {
                        continue;
                    }

                    if (child is ForgeStripeSeparator)
                    {
                        separatorIndex = sizes.Count;
                        orders.Add(null);
                    }
                    else if (child is ForgeStripeButton b && OrderOf(b) is { } o)
                    {
                        orders.Add(o);
                    }
                    else
                    {
                        orders.Add(null);
                    }

                    sizes.Add(child.DesiredSize.Height);
                }

                var index = StripeDropResolver.ResolveInsertIndex(offset, sizes);

                // 序号 → order：插在第 index 个之前，就是取从它起第一个真按钮的 order；
                // 后面全是分隔符（或没有了）就接末尾。**不能拿 index 直接索引按钮列表**——
                // 分隔符也占一格，两者对不上。
                var order = -1;
                for (var i = index; i < orders.Count; i++)
                {
                    if (orders[i] is { } found)
                    {
                        order = found;
                        break;
                    }
                }

                var target = StripeDropResolver.Resolve(side, group, index > separatorIndex, order);
                _lastDropSlot = (panel, index);
                return StripeDropResolver.AdjustForReversedBottomGroup(target);
            }
        }

        // 落区都不中 —— 上游此时 getStripeFor 返回 null，就是「这里不能放」。
        // 别再兜一个默认组：那正是「点落在第一个图标上方就变成上停靠」的来源。
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

            SyncPanel(vm, host);

            // 外部形态（浮动 / 独立窗口）不进停靠区，交给自己的宿主窗口
            if (ToolWindowManager.IsExternal(info.Type))
            {
                ShowExternal(vm, info, host);
                continue;
            }

            CloseExternal(vm.Id);

            ForgeDockPane.SetAnchor(host, info.Anchor);
            ForgeDockPane.SetWeight(host, info.Weight);
            ForgeDockPane.SetIsSplit(host, info.IsSplit);
            ForgeDockPane.SetSideWeight(host, info.SideWeight);

            // SLIDING = 盖在编辑区上，不占它的地方（ToolWindowPane.kt:432-459 的 PALETTE_LAYER 那一支）
            ForgeDockPane.SetIsOverlay(host, info.Type == ToolWindowType.Sliding);

            wanted.Add(host);
        }

        // 关掉的、或者切回内部形态的，把外部窗口收掉
        foreach (var id in _externalWindows.Keys.ToList())
        {
            var info = _shell.Manager.Layout.GetInfo(id);
            if (info is null || !info.IsVisible || !ToolWindowManager.IsExternal(info.Type))
            {
                CloseExternal(id);
            }
        }

        if (EditorContent is { } editor)
        {
            ForgeDockPane.SetAnchor(editor, null);
            wanted.Add(editor);
        }

        wanted.AddRange(GetResizers());
        Reconcile(_pane, wanted);
    }

    private readonly Dictionary<string, ForgeFloatingToolWindow> _externalWindows = new(StringComparer.Ordinal);

    /// <summary>当前开着的外部宿主窗口（浮动 / 独立窗口）。</summary>
    public IReadOnlyCollection<ForgeFloatingToolWindow> ExternalWindows => _externalWindows.Values;

    /// <summary>
    /// 浮动 / 独立窗口：把面板宿主搬进它自己的窗口。
    /// </summary>
    /// <remarks>
    /// 宿主控件是**同一个**（<see cref="GetPanelHost"/> 缓存的那个），只是换了父容器 ——
    /// 相机画面、图表这些不会因为切形态被重造。
    /// <para>
    /// 关窗口 = 收起这个工具窗口（<c>FloatingDecorator</c> 的关闭按钮走的也是 hide），
    /// 否则窗口没了、台账还记着"开着"。
    /// </para>
    /// </remarks>
    private void ShowExternal(ToolWindowViewModel vm, WindowInfo info, Border host)
    {
        if (_externalWindows.TryGetValue(vm.Id, out var existing) && existing.Type == info.Type)
        {
            return;
        }

        CloseExternal(vm.Id);

        // ★ 默认尺寸 = 面板**停靠时的那块地方**，不是让窗口自己按内容长
        //   （ToolWindowManagerDecorators.kt:237-243：先取 internalDecorator.size，为 0 才退回 preferredSize）。
        //   所以要在 Detach 之前量，摘下来之后 Bounds 就归零了。
        var docked = host.Bounds.Size;
        Detach(host);

        var external = new ForgeFloatingToolWindow(
            info.Type,
            vm.Title,
            ProjectName,
            host,
            ExternalWindowFactory?.Invoke(info.Type));
        _externalWindows[vm.Id] = external;

        if (ExternalWindowTitle?.Invoke(info.Type, vm.Title) is { } title)
        {
            external.Window.Title = title;
        }

        var id = vm.Id;
        external.Window.Closing += (_, _) =>
        {
            if (_externalWindows.Remove(id) && _shell?.Manager.Layout.GetInfo(id) is { IsVisible: true })
            {
                _shell.Manager.Hide(id);
            }
        };

        var owner = TopLevel.GetTopLevel(this) as Window;
        PlaceExternal(external, info, docked, owner);

        if (owner is not null)
        {
            external.Window.Show(owner);
        }
        else
        {
            external.Window.Show();
        }
    }

    /// <summary>
    /// 摆好外部窗口的位置和大小。
    /// </summary>
    /// <remarks>
    /// 三条分支全在 <see cref="FloatingBoundsResolver"/> 里（对位
    /// <c>ToolWindowManagerDecorators.kt:212-252 · setExternalDecoratorBounds</c>）：
    /// 存过且还在屏幕上就原样用，跑出屏幕就拉回来再居中，没存过就用停靠时的大小再居中。
    /// </remarks>
    private static void PlaceExternal(
        ForgeFloatingToolWindow external,
        WindowInfo info,
        Size docked,
        Window? owner)
    {
        var fallback = (
            Width: (int)Math.Round(docked.Width > 0 ? docked.Width : ForgeMetrics.DefaultExternalWidth),
            Height: (int)Math.Round(docked.Height > 0 ? docked.Height : ForgeMetrics.DefaultExternalHeight));

        var ideFrame = owner is null
            ? new FloatingBounds(0, 0, fallback.Width, fallback.Height)
            : new FloatingBounds(
                owner.Position.X,
                owner.Position.Y,
                (int)Math.Round(owner.Bounds.Width),
                (int)Math.Round(owner.Bounds.Height));

        var screenArea = owner?.Screens.ScreenFromWindow(owner)?.WorkingArea;
        var screen = screenArea is { } area
            ? new FloatingBounds(area.X, area.Y, area.Width, area.Height)
            : ideFrame;

        external.ApplyBounds(info.FloatingBounds, screen, ideFrame, fallback);
    }

    /// <summary>收掉某个工具窗口的外部宿主窗口，把面板宿主还回来。</summary>
    private void CloseExternal(string id)
    {
        if (!_externalWindows.Remove(id, out var external))
        {
            return;
        }

        // 先把内容摘出来再关窗，否则宿主控件跟着窗口一起没了
        external.ReleaseContent();
        external.Window.Close();
    }

    /// <summary>独立窗口标题里那个项目名（<c>"{面板名} - {项目名}"</c>）。</summary>
    /// <remarks>宿主没给就用空串 —— 库不知道"项目"是什么。</remarks>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// 切成外部形态时，用什么窗体装它。
    /// </summary>
    /// <remarks>
    /// 独立窗口（WINDOWED）该带最小化 / 最大化 / 关闭，**但那排按钮长什么样是宿主的事**——
    /// 一个应用里所有窗体应当是同一副长相，库不该自己画一套、更不该退回系统默认那副。
    /// 所以这里只给口子：宿主返回自己的窗体（连同标题栏、按钮、圆角、材质），
    /// 库只往里塞标题和面板内容。
    /// <para>
    /// 返回 <see langword="null"/>（或不设这个口子）就用库自带的朴素窗口：
    /// 独立窗口用系统装饰，浮动无装饰。浮动一般不必接管——它本来就没有标题栏。
    /// </para>
    /// </remarks>
    public Func<ToolWindowType, Window?>? ExternalWindowFactory { get; set; }

    /// <summary>
    /// 浮动窗口的标题栏多高 —— 浮动没有系统标题栏，面板标题条兼任，就该按这个高度长。
    /// </summary>
    /// <remarks>
    /// 默认取 <see cref="ForgeMetrics.WindowTitleBarHeight"/>；宿主自绘的窗体标题栏若是别的高度，
    /// 在这里对齐，浮动出来的窗口才和应用里其它窗口一个身量。
    /// </remarks>
    public double TitleBarHeight { get; set; } = ForgeMetrics.WindowTitleBarHeight;

    /// <summary>
    /// 独立窗口形态下，标题条右端要给窗口的最小化 / 最大化 / 关闭留出多宽。
    /// </summary>
    /// <remarks>
    /// 独立窗口的标题栏与面板标题条**合成一行**：面板的标题和标签在左，面板自己的按钮居右，
    /// 窗口那三个系统按钮在最右。那三个由宿主的 chrome 画在客户区上层、不参与布局，
    /// 所以得在这里报出宽度。0 = 宿主窗体自带标题栏，这一行不用让。
    /// </remarks>
    public double CaptionButtonsWidth { get; set; }

    /// <summary>
    /// 外部窗口的标题写什么。
    /// </summary>
    /// <remarks>
    /// 入参是形态和面板名，返回 <see langword="null"/> 表示这一种不改、走默认。
    /// 不设就按上游那套（浮动只用面板名，独立窗口是
    /// <c>"{面板名} - {项目名}"</c>，见 <see cref="ForgeFloatingToolWindow.ComposeTitle"/>）。
    /// 宿主的窗体若自带标题栏、想在那一行写别的（比如软件名），在这里给。
    /// </remarks>
    public Func<ToolWindowType, string, string?>? ExternalWindowTitle { get; set; }

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

        // ⋮ 选项：与条带按钮右键菜单同一份（隐藏 / 移到某边 / 显示名字）——
        //   同一个工具窗口在两处给出两套动作会让人以为是两个东西。
        // — 隐藏：收起面板但**留着条带按钮**（点一下就回来）。菜单里的「隐藏」是另一码事，
        //   它 removeFromStripe=true、连按钮一起摘掉，得从「视图」菜单找回来。
        var id = vm.Id;
        header.OptionsRequested += (_, _) => ShowHeaderOptions(id);
        header.HideRequested += (_, _) => _shell?.Manager.Hide(id);

        // 停靠回去：回到它上次的**内部**形态（停靠固定 / 停靠不固定），
        // 模型记着这个（WindowInfo.InternalType，SetType 切到外部形态时不动它）。
        header.DockRequested += (_, _) =>
        {
            if (_shell is { } shell)
            {
                shell.Manager.SetType(id, shell.Manager.ResolveInternalType(id));
            }
        };

        WireHeaderDrag(header, id);

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

        ForgeIslandDecor.Apply(host, Palette, info.Anchor, Appearance.IslandMetrics);

        var header = _panelHeaders[vm.Id];
        header.Palette = Palette;
        header.Title = vm.Title;
        header.TitleFontWeight = Appearance.HeaderTitleFontWeight;

        // 标题条坐在岛顶上：跟着岛磨圆上面两个角，否则它会把那两段弧盖掉（见 TopCornerRadius）。
        // 减掉描边自己占的 1px —— 内圆角比外圆角小这一圈。
        header.TopCornerRadius = (Appearance.IslandMetrics ?? Palette.Island) is { } island && Palette.Island is not null
            ? Math.Max(0, island.CornerRadius - 1)
            : 0;
        header.IsActive = vm.IsActive;
        header.ContentUiType = info.ContentUiType;
        header.ShowButtons = ShowHeaderButtons;

        // 「停靠回去」只在独立出去之后才有意义
        header.ShowDockButton = ToolWindowManager.IsExternal(info.Type);

        // 浮动时宿主窗体没有标题栏，这条兼任 → 按窗口标题栏的高度长。
        // 独立窗口另说：那种窗体自带标题栏（第一行），这条仍是普通的面板标题条（第二行）；
        // 只有当宿主把系统按钮也摆到这一行时才需要让位（CaptionButtonsWidth，默认 0 不让）。
        header.TitleBarHeight = info.Type == ToolWindowType.Floating ? TitleBarHeight : double.NaN;
        header.HeightOverride = Appearance.HeaderHeight;
        header.CaptionButtonsWidth = info.Type == ToolWindowType.Windowed ? CaptionButtonsWidth : 0;
        header.SetExtraButtons(HeaderButtonsExtension?.Invoke(vm.Id));
        SyncTabs(header, vm, info.ContentUiType);

        _panelContents[vm.Id].Content = vm.Content;
    }

    /// <summary>
    /// 这个控件**画得出来**的视图模式。
    /// </summary>
    /// <remarks>
    /// 五种都画得出来。注意它们不是五种 <see cref="ToolWindowType"/> ——
    /// 停靠固定与停靠不固定是同一种形态（DOCKED），差在失焦收不收
    /// （见 <see cref="ToolWindowViewMode"/>）。
    /// <para>
    /// ⚠ <b>画不出来的模式不该摆上菜单</b>：模型允许它们（那是照上游建的），
    /// 但视图若把它当普通停靠面板塞进同一列，就会得到「按钮亮了两个、面板只显示一个」
    /// ——SLIDING 与 DOCKED 在上游是**两个格**（<c>ToolWindowSlot</c> 的键含形态，
    /// <c>ToolWindowManagerImpl.kt:962-966</c>），互斥判定会放行。而且会写进布局存档。
    /// </para>
    /// </remarks>
    public static IReadOnlyList<ToolWindowViewMode> SupportedViewModes { get; } =
    [
        ToolWindowViewMode.DockPinned,
        ToolWindowViewMode.DockUnpinned,
        ToolWindowViewMode.Undock,
        ToolWindowViewMode.Float,
        ToolWindowViewMode.Window,
    ];

    /// <summary>
    /// 把画不出来的形态掰回停靠固定。
    /// </summary>
    /// <remarks>
    /// 兜的是**存档**：老版本（或这一版加了视图模式菜单之后）存下过 SLIDING 之类，
    /// 读回来就是一列里两块面板叠着。修完菜单只是不再产生新的坏状态，
    /// 已经躺在用户磁盘上的那份还得在这儿治好。
    /// </remarks>
    private void NormalizeViewModes()
    {
        if (_shell is not { } shell)
        {
            return;
        }

        foreach (var info in shell.Manager.Layout.GetSortedList().ToList())
        {
            if (!SupportedViewModes.Contains(ToolWindowViewModes.Resolve(info)))
            {
                shell.Manager.SetType(info.Id, ToolWindowType.Docked);
            }
        }
    }

    private void ShowHeaderOptions(string id)
    {
        if (_shell is not { } shell || !_panelHeaders.TryGetValue(id, out var header))
        {
            return;
        }

        var menu = ForgeMenuBuilder.ForHeaderOptions(shell.Manager, id, HeaderMenuExtension, SupportedViewModes);

        // 挂在 ⋮ 那个按钮上弹，位置才对得上（挂在整条标题条上会从鼠标处弹）
        menu.PlacementTarget = header.OptionsButton;
        menu.Placement = PlacementMode.BottomEdgeAlignedRight;
        menu.Open(header.OptionsButton);
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
        header.Tabs.Palette = Palette;
        header.Tabs.Manager = contents;
        header.Tabs.IsWindowActive = vm.IsActive;

        if (contents.Contents.Count == 0)
        {
            header.Tabs.ItemsSource = null;
            return;
        }

        if (uiType == ToolWindowContentUiType.Combo)
        {
            header.Tabs.ItemsSource = contents.SelectedContent is { } selected
                ? new[] { selected }
                : null;
            return;
        }

        header.Tabs.ItemsSource = contents.Contents.ToArray();
        header.Tabs.SelectedItem = contents.SelectedContent;
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

            // 覆盖层（取消停靠）浮在编辑区上，得有东西把它和被盖住的内容分开。
            // **两套配色的分法不一样，照上游走**：
            //   · 非岛（expUI）：面板与编辑区贴死，靠一条 1px 的 divider
            //     （InternalDecoratorImpl.kt:599-612，专给 SLIDING 加的那条）。
            //     颜色取 EditorTabsBorder（"内容与内容之间那条线"，分屏那条也是它）——
            //     不能用 WindowBorder，它在岛配色里是 #00000000，画了等于没画。
            //   · 岛（Islands）：面板本来就是一座岛 —— 圆角、四周留缝，缝里透出下面的编辑区
            //     （XNextRoundedBorder.kt:38-47：arc 35、outerInsets 5~6，
            //     且那圈是用 withEditorBackground 画的）。**这时不画线**，
            //     画了就等于在岛外面又糊一条边，反而把"透底"糊死。
            var overlay = _shell.ToolWindows.Any(x =>
                x.IsOpen
                && x.Anchor == anchor
                && _shell.Manager.Layout.GetInfo(x.Id)?.Type == ToolWindowType.Sliding);

            // 形态由**配色族**决定，与宿主给的岛几何无关 —— 同 ForgeIslandDecor.Apply 的判据
            var isIsland = Palette.Island is not null;

            resizer.LineBrush = overlay && !isIsland ? new SolidColorBrush(Palette.EditorTabsBorder) : null;

            yield return resizer;
        }

        // 格内分割线：只有这条边**同时**有 split 与非 split 的窗口开着才需要
        foreach (var anchor in anchors)
        {
            var open = _shell.ToolWindows.Where(x => x.IsOpen && x.Anchor == anchor).ToList();
            if (!open.Any(x => x.IsSplit) || !open.Any(x => !x.IsSplit))
            {
                continue;
            }

            if (!_splitResizers.TryGetValue(anchor, out var split))
            {
                split = new ForgeDockResizer { Anchor = anchor, IsSplitDivider = true };
                split.WeightChanged += OnResizerWeightChanged;
                ForgeDockPane.SetAnchor(split, null);
                ForgeDockPane.SetIsSplitResizer(split, anchor);
                _splitResizers[anchor] = split;
            }

            yield return split;
        }
    }

    private readonly Dictionary<ToolWindowAnchor, ForgeDockResizer> _splitResizers = [];

    private readonly Dictionary<ToolWindowAnchor, ForgeDockResizer> _resizers = [];

    private void OnResizerWeightChanged(object? sender, DockWeightChangedEventArgs e)
    {
        if (_shell is null)
        {
            return;
        }

        var layout = _shell.Manager.Layout;

        if (e.IsSideWeight)
        {
            // 格内分割线：改的是 sideWeight，**两半都要写**（ForgeDockPane 取的是非 split 那个，
            // 但只写一半会让存档里两条记录互相矛盾，重启后按哪条走就看谁先被读到）。
            foreach (var info in layout.GetSortedList().Where(x => x.IsVisible && x.Anchor == e.Anchor))
            {
                layout.SetInfo(info with { SideWeight = e.Weight });
            }

            Sync();
            return;
        }

        // 新 UI 默认走统一权重：拖一条边等于改这条边的统一权重（§4）
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
        // ⚠ 裁剪是停靠区替它加的（被覆盖层压着时让开那条带，见 ForgeDockPane.ClipUnderOverlays）。
        //   离开停靠区就得清掉：带着它搬进浮动窗口的话，那边没人再管，
        //   面板会被一条与新容器毫无关系的线切掉一块。
        control.Clip = null;

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
    // 面板边框的落位已并入 ForgeIslandDecor.Apply —— 岛形态与方角形态是同一处决策，
    // 分两处写必然有一天只改了一处。

    private Control CreatePlaceholder(string id) => new TextBlock
    {
        Text = id,
        Margin = new Thickness(ForgeMetrics.HeaderLabelInsetLeft, 8, 0, 0),
        Foreground = new SolidColorBrush(Palette.Foreground),
    };
}
