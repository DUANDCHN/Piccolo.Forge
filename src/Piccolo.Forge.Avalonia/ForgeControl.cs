using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Piccolo.Forge.Model;
using Piccolo.Forge.Mvvm;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// Forge 的**总控件**：把左右两根条带、停靠区和编辑区拼在一起，
/// 并跟 <see cref="ToolWindowManager"/> 联动。宿主摆进 XAML 的就是它。
/// </summary>
/// <remarks>
/// 名字对位 Dock.Avalonia 的 <c>DockControl</c> —— 库名 + <c>Control</c> = 那个总控件。
/// Forge 里**只有它和 <see cref="ForgeEditorControl"/> 带 <c>Control</c> 后缀**，
/// 其余（<see cref="ForgeStripeButton"/>、<see cref="ForgeToolWindowHeader"/>…）都是零件。
/// <para>
/// 上游对位 <c>ToolWindowPane</c>（<c>ToolWindowPaneNewButtonManager.kt:58-68 · wrapWithControls</c>）：
/// 条带在 WEST / EAST，pane 在 CENTER。名字里不再带 "ToolWindow" 是有意的 ——
/// 它拼进来的还有编辑区（<see cref="EditorContent"/>），叫工具窗口只说了一半。
/// </para>
/// <para>
/// 按钮落位不在这里算，走 <see cref="StripePlacementResolver"/>；
/// 组内次序与分隔符走 <see cref="StripeGroupLayout"/>。
/// </para>
/// </remarks>
public class ForgeControl : TemplatedControl
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
    private global::Avalonia.Media.Imaging.RenderTargetBitmap? _dragPanelThumb;

    /// <summary>
    /// 条带拖拽的幽灵窗——拖影就是它，**全程在**。
    /// </summary>
    /// <remarks>
    /// 与拖标题条（<c>_headerGhost</c>）、拖编辑区标签（<c>ForgeEditorControl</c>）同一条路：
    /// 拖影是一个置顶的真窗口，不画在浮层里。三处统一之后，「拖影的样子 = 松手会发生什么」
    /// 这条语言只有一份实现（<see cref="ForgeDragGhost.SetFloatPreview"/>）。
    /// <para>
    /// 也是上游的形：拖影是独立 <c>JDialog</c>，<c>relocateImageView</c> 只改它的位置
    /// （<c>ToolWindowDragHelper.kt:299 / :821</c>），从不参与主窗口的绘制。
    /// </para>
    /// </remarks>
    private ForgeDragGhost? _dragGhost;
    private (StripeSide Side, StripeGroup Group)? _lastZone;
    private readonly ForgeDragOverlay _overlay = new();


    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeControl, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    /// <summary><see cref="IslandBorderBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> IslandBorderBrushProperty =
        AvaloniaProperty.Register<ForgeControl, IBrush?>(nameof(IslandBorderBrush));

    /// <summary>
    /// 岛那圈描边的画刷，由 <c>ControlTheme</c> 从资源键给（REQ-067 收尾项）。
    /// </summary>
    /// <remarks>
    /// 它交给 <see cref="ForgeIslandDecor.Apply"/> 去画。这是全仓最后一处从 C# 取色的地方 ——
    /// 归了资源键之后，宿主重定义 <c>Forge.Islands.IslandBorder</c> 即可，
    /// 不必再给 <c>PaletteResolver</c> 回调。
    /// </remarks>
    public IBrush? IslandBorderBrush
    {
        get => GetValue(IslandBorderBrushProperty);
        set => SetValue(IslandBorderBrushProperty, value);
    }

    /// <summary><see cref="DockDividerBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> DockDividerBrushProperty =
        AvaloniaProperty.Register<ForgeControl, IBrush?>(nameof(DockDividerBrush));

    /// <summary>
    /// 同一条边上**两块面板之间**那条 1px 分割线的画刷，由 <c>ControlTheme</c> 从资源键给。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与 <see cref="IslandBorderBrush"/> 同一条路子（REQ-067 把颜色从 C# 归到资源键）：
    /// 之前这条线直接取 <c>Palette.WindowBorder</c>，而 <c>WindowBorder</c> 没有对应的资源键，
    /// **宿主想调深一点根本够不着** —— 只能去改库里从上游转写来的调色板，那会把保真度一起改掉。
    /// 归了键之后宿主重定义 <c>Forge.ExpUi.WindowBorder</c> 即可，明暗各盖各的。
    /// </para>
    /// <para>
    /// 为 <see langword="null"/> 时回落到 <c>Palette.WindowBorder</c>（上游值），行为与改造前一致。
    /// </para>
    /// <para>
    /// 岛（Islands）档**不画这条线**：两座岛之间本来就有缝，再画一条等于把缝糊死 ——
    /// 这条判据在画的地方，不在这里。
    /// </para>
    /// </remarks>
    public IBrush? DockDividerBrush
    {
        get => GetValue(DockDividerBrushProperty);
        set => SetValue(DockDividerBrushProperty, value);
    }

    /// <summary>
    /// <see cref="Shell"/> 的属性定义。
    /// </summary>
    /// <remarks>
    /// 做成可绑定属性而不是只读 + <c>Attach()</c>，是为了让整个外壳能在 XAML 里声明：
    /// <code>&lt;forge:ForgeControl Shell="{Binding Shell.ViewModel}"/&gt;</code>
    /// 对位 Dock.Avalonia 的 <c>&lt;DockControl Layout="{Binding Layout}"/&gt;</c>。
    /// <c>Attach()</c> 仍在，它现在就是写这个属性。
    /// </remarks>
    public static readonly StyledProperty<ToolWindowShellViewModel?> ShellProperty =
        AvaloniaProperty.Register<ForgeControl, ToolWindowShellViewModel?>(nameof(Shell));

    /// <summary><see cref="TransparentBackdrop"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> TransparentBackdropProperty =
        AvaloniaProperty.Register<ForgeControl, bool>(nameof(TransparentBackdrop));

    /// <summary><see cref="StripeButtonArc"/> 的属性定义。</summary>
    public static readonly StyledProperty<double?> StripeButtonArcProperty =
        AvaloniaProperty.Register<ForgeControl, double?>(nameof(StripeButtonArc));

    /// <summary><see cref="MoreButtonSide"/> 的属性定义。</summary>
    public static readonly StyledProperty<StripeSide> MoreButtonSideProperty =
        AvaloniaProperty.Register<ForgeControl, StripeSide>(nameof(MoreButtonSide));

    /// <summary><see cref="ShowHeaderButtons"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> ShowHeaderButtonsProperty =
        AvaloniaProperty.Register<ForgeControl, bool>(nameof(ShowHeaderButtons), defaultValue: true);

    /// <summary><see cref="ShowToolWindowNames"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> ShowToolWindowNamesProperty =
        AvaloniaProperty.Register<ForgeControl, bool>(nameof(ShowToolWindowNames));

    /// <summary><see cref="EditorContent"/> 的属性定义。</summary>
    public static readonly StyledProperty<Control?> EditorContentProperty =
        AvaloniaProperty.Register<ForgeControl, Control?>(nameof(EditorContent));

    static ForgeControl()
    {
        PaletteProperty.Changed.AddClassHandler<ForgeControl>((f, _) => f.Sync());

        IslandBorderBrushProperty.Changed.AddClassHandler<ForgeControl>((f, _) => f.Sync());

        DockDividerBrushProperty.Changed.AddClassHandler<ForgeControl>((f, _) => f.Sync());

        // 外观换了一份：重跑取色（可能换了族或取色回调），再同步一遍（岛几何、字重、行高都在里面）。
        // ⚠ **不要在这里把 _variantApplied 归零**（曾经这么写过，是个双重 bug）：
        // ApplyThemeVariant 开头那道「宿主是不是自己管配色」的探测用 IsSet(PaletteProperty) 判，
        // 而我们自己写过 Palette 之后它恒为真。归零 _variantApplied 等于把探测重新武装，
        // 于是换族那一下被判成"宿主自己管"，_paletteSetByHost 被永久钉死 ——
        // 换族当场不生效，此后连跟随系统明暗也一起停摆。
        // _variantApplied 保持为真时，ApplyThemeVariant 会跳过探测、直接按新族重算，这正是要的。
        ForgeChrome.AppearanceProperty.Changed.AddClassHandler<ForgeControl>((f, _) =>
        {
            f.ApplyThemeVariant();
            f.Sync();
        });
        TransparentBackdropProperty.Changed.AddClassHandler<ForgeControl>((f, _) => f.Sync());
        ShellProperty.Changed.AddClassHandler<ForgeControl, ToolWindowShellViewModel?>(
            (f, e) => f.OnShellChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
        StripeButtonArcProperty.Changed.AddClassHandler<ForgeControl>((f, _) => f.Sync());
        EditorContentProperty.Changed.AddClassHandler<ForgeControl>((f, _) => f.Sync());
        ShowHeaderButtonsProperty.Changed.AddClassHandler<ForgeControl>((f, _) => f.Sync());
        ShowToolWindowNamesProperty.Changed.AddClassHandler<ForgeControl>((f, _) =>
        {
            // 菜单里的「显示工具窗口名称」改的是控件属性；写回 VM 才跟着布局一起存下去。
            // 两边都是幂等 setter，回写不会来回弹。
            if (f._shell is { } shell)
            {
                shell.ShowToolWindowNames = f.ShowToolWindowNames;
            }

            f.Sync();
        });
        MoreButtonSideProperty.Changed.AddClassHandler<ForgeControl>((f, _) => f.Sync());
    }

    /// <summary>建一个框架。</summary>
    public ForgeControl()
    {
        DockPanel.SetDock(_left, Dock.Left);
        DockPanel.SetDock(_right, Dock.Right);
        _root.Children.Add(_left);
        _root.Children.Add(_right);
        _root.Children.Add(_pane);

        // 像素锁定（宿主开了 KeepAnchorSizeOnResize 时）让台账里的 weight 跟不上窗口尺寸，
        // 停靠区反算出等价值回抛给这里写回去 —— 与用户拖拖手是同一条写入路径。
        _pane.AnchorWeightRebased += OnResizerWeightChanged;

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
        AddHandler(KeyDownEvent, OnStripeKeyDown, gestureRoutes, handledEventsToo: true);

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
        // ★ 两个开关必须同族（REQ-068 R2）：装进 Application.Styles 的是哪套主题、
        //   这份 Appearance 说的是哪族。不一致的症状是**局部**长相不对（模板画 Islands 的圆角块，
        //   而 C# 侧按 expUI 算几何取色），肉眼极难归因 —— Debug 下当场炸成一句可读的话。
        //
        // ⚠ **只在宿主真给过外观时才算数。** 控件多半是 XAML 建的，挂树那一下外观还是库默认
        //   （宿主的那份在 code-behind 里，晚一步才设）—— 那个瞬间的 Family 不是任何人的主张，
        //   拿它去断言就是"启动即崩"（真机撞过：配置是 Islands，主窗口 XAML 一挂树就炸）。
        //   这一档改为**跟随装了哪族**：没人主张时，装的那套就是唯一真值。
        var hostGaveAppearance = !ReferenceEquals(Appearance, ForgeAppearance.Default);
        if (hostGaveAppearance)
        {
            ForgeThemeManager.AssertConsistent(Appearance.Family);
        }

        var family = hostGaveAppearance
            ? Appearance.Family
            : ForgeThemeManager.Current ?? Appearance.Family;

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

        var wanted = Appearance.PaletteResolver?.Invoke(family, ActualThemeVariant)
                     ?? ForgePalette.ForTheme(family, ActualThemeVariant);
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

    /// <summary>
    /// 左右两条边**默认多宽**（像素）；<see cref="double.NaN"/>（默认）= 按 <c>weight</c> 算。
    /// </summary>
    /// <remarks>转发给 <see cref="ForgeDockPane.DefaultAnchorWidth"/>，生效条件见那儿。</remarks>
    public double DefaultAnchorWidth
    {
        get => _pane.DefaultAnchorWidth;
        set => _pane.DefaultAnchorWidth = value;
    }

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
        Shell = shell;
    }

    /// <summary>换了一个外壳 VM：退订旧的、订上新的、按新状态同步一遍。</summary>
    /// <param name="oldShell">上一个（可能没有）。</param>
    /// <param name="shell">新的（<see langword="null"/> = 摘下来，不接任何 VM）。</param>
    private void OnShellChanged(ToolWindowShellViewModel? oldShell, ToolWindowShellViewModel? shell)
    {
        if (oldShell is not null)
        {
            oldShell.StateChanged -= OnShellStateChanged;
            oldShell.PropertyChanged -= OnShellPropertyChanged;
        }

        _shell = shell;
        if (shell is null)
        {
            Sync();
            return;
        }

        shell.StateChanged += OnShellStateChanged;
        NormalizeViewModes();
        shell.PropertyChanged += OnShellPropertyChanged;

        // 「显示名字」的真相在 VM（它跟布局一起存），控件跟着它走 ——
        // 宿主的恢复顺序是「先 Attach、后读存档」，所以还得订阅着，等存档读回来再跟一次。
        //
        // 接上的这一刻谁说了算：**宿主显式设过控件就以控件为准**（判据用 IsSet，
        // 同 Palette 那处），否则以 VM 为准。反过来的话，宿主在建控件时配的初值
        // 会被一个还没读存档的 VM 冲掉。
        if (IsSet(ShowToolWindowNamesProperty))
        {
            shell.ShowToolWindowNames = ShowToolWindowNames;
        }
        else
        {
            ShowToolWindowNames = shell.ShowToolWindowNames;
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

    /// <summary>
    /// 接上的外壳 VM；赋值即接上（同 <see cref="Attach"/>），可在 XAML 里绑定。
    /// </summary>
    public ToolWindowShellViewModel? Shell
    {
        get => GetValue(ShellProperty);
        set => SetValue(ShellProperty, value);
    }

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

        // ★ 布局意图真的换了（读档 / 恢复默认布局 / 换边 / 开合）—— 停靠区记着的那份像素作废，
        //   下一轮按台账里的 weight 重新播种。**窗口改大小、以及把像素反算回 weight 写台账**
        //   都不走这条路，那正是"面板按像素固定"要守住的东西。
        _pane.InvalidateAnchorSizes();
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

    /// <summary>
    /// 「显示名字」时条带多宽 —— 宿主给的是**底的宽度**，条带宽 = 边距 + 底 + 边距。
    /// </summary>
    /// <remarks>宿主没指定就走上游那 59（上游那个数本身就是条带宽）。</remarks>
    private double ResolvedStripeNamedWidth
        => double.IsNaN(Appearance.StripeNamedHighlightWidth) || double.IsNaN(Appearance.StripeHighlightInsetHorizontal)
            ? ForgeMetrics.StripeNamedDefaultWidth
            : Appearance.StripeNamedHighlightWidth + (Appearance.StripeHighlightInsetHorizontal * 2);

    private void SyncStripes()
    {
        _left.Palette = Palette;
        _right.Palette = Palette;
        _left.TransparentBackdrop = TransparentBackdrop;
        _right.TransparentBackdrop = TransparentBackdrop;

        // 宿主自己定了条带宽（StripeButtonWidth）时，内侧那条线压在内容上、不再占宽 ——
        // 否则总宽 = 给的宽 + 1，图标在总宽里落半像素，取整后看着偏一边。
        var hostSized = !double.IsNaN(Appearance.StripeHighlightInsetHorizontal) && !double.IsNaN(Appearance.StripeHighlightSize);
        _left.BorderOverlaysContent = hostSized;
        _right.BorderOverlaysContent = hostSized;

        var stripeWidth = ShowToolWindowNames ? ResolvedStripeNamedWidth : 0;
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

        // 拖拽期（processDrop）：按钮序列里**去掉被拖的那个**再算分隔符（AbstractDroppableStripe.kt:598 tools.remove(dragButton)），
        // 并且照上游 :600-613 补两条规则——
        //   ① 分隔符落在最前（剩下的全是 split）：留着，标成拖拽态；
        //   ② 一条分隔符都没有而组里还有按钮：在末尾临时加一条拖拽态的 ——
        //      落在它下面 = 下半栏（split）。没有这条，组里还没有 split 按钮时就永远拖不出下半栏。
        var dragging = _dndSeparatorGroup == (side, group);
        var dragged = dragging ? DraggedButton : null;
        var tools = windows.Where(vm => dragged is null || !ReferenceEquals(GetStripeButton(vm, side), dragged)).ToList();
        var wantedSplit = useSeparator && tools.Any(x => x.IsSplit) && !tools.All(x => x.IsSplit);
        var separatorAtTop = useSeparator && dragging && tools.Count > 0 && tools.All(x => x.IsSplit);
        var separatorAtEnd = useSeparator && dragging && tools.Count > 0 && !tools.Any(x => x.IsSplit);

        var wanted = new List<Control>();
        if (separatorAtTop)
        {
            wanted.Add(GetSeparator(side, group));
        }

        foreach (var vm in windows)
        {
            if (wantedSplit && vm.IsSplit && wanted.All(x => x is not ForgeStripeSeparator))
            {
                wanted.Add(GetSeparator(side, group));
            }

            wanted.Add(GetStripeButton(vm, side));
        }

        if (separatorAtEnd)
        {
            wanted.Add(GetSeparator(side, group));
        }

        if (useSeparator)
        {
            GetSeparator(side, group).IsDragAndDrop = separatorAtTop || separatorAtEnd;
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

    /// <summary>拖拽期间正被当作落点的那一组（只对上组有意义）：它的分隔符按拖拽规则排（见 BuildGroupChildren）。</summary>
    private (StripeSide, StripeGroup)? _dndSeparatorGroup;
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

            // 标题 / 图标 / 角标改了**即时**跟到按钮上，不等下一次 Sync（角标尤其要即时：它报的是"刚出事"）。
            // 按钮与 VM 同寿（都活到外壳拆掉），不解订阅。
            var captured = button;
            vm.PropertyChanged += (_, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(ToolWindowViewModel.HasBadge): captured.HasBadge = vm.HasBadge; break;
                    case nameof(ToolWindowViewModel.Title): captured.Title = vm.Title; captured.Text = vm.Icon is null ? vm.Title[..1] : null; break;
                    case nameof(ToolWindowViewModel.Icon): captured.Icon = vm.Icon; captured.Text = vm.Icon is null ? vm.Title[..1] : null; break;
                }
            };
        }

        // 菜单每次同步重建，理由同 SyncStripes 里那处：勾选状态要跟着走
        button.ContextMenu = ForgeMenuBuilder.ForStripeButton(
            _shell!.Manager, vm.Id, () => ShowToolWindowNames = !ShowToolWindowNames, ShowToolWindowNames);
        ApplySquareButtonAppearance(button, side);
        button.NameFontSizeOverride = Appearance.StripeNameFontSize;
        button.NameFontWeightOverride = Appearance.StripeNameFontWeight;
        button.NamedContentInset = Appearance.StripeNamedContentInset;
        button.Icon = vm.Icon;
        button.Text = vm.Icon is null ? vm.Title[..1] : null;
        button.Title = vm.Title;
        button.HasBadge = vm.HasBadge;
        button.ShowName = ShowToolWindowNames;
        button.StripeWidth = ShowToolWindowNames ? ResolvedStripeNamedWidth : 0;
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
            _dragStarted = false;
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
    private ForgeToolWindowHeader? _headerDragHeader;
    private ForgeDragGhost? _headerGhost;
    private Point _headerGhostGrab;
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
                _headerDragHeader = header;

                // 抓握点两份：面板系的（松手浮出时让同一个点待在光标下）
                // 与标题条系的（幽灵窗紧凑形态的抓握）
                _headerDragGrab = _panelHosts.TryGetValue(id, out var host)
                    ? e.GetPosition(host)
                    : default;
                _headerGhostGrab = e.GetPosition(header);
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

            // ★ 死区用标题条自己的 100（HeaderDragStartDeadzone），**不是**条带按钮那 7：
            //   标题条上全是常点的按钮和双击区，7px 一抖就起拖、面板飞出去 —— 误触太多
            //  （实机反馈）。上游注册表本来就是两档分开的。
            if (moved < ForgeMetrics.HeaderDragStartDeadzone)
            {
                return;
            }

            _headerDragging = true;

            // ★ 拖动中**不真的浮出**（曾经起拖即 SetType(Floating)、真窗口跟手）：
            //   面板原地不动，跟手的是幽灵窗。松手才真正落位/浮出。
            // ★ 跟手的样子与**拖条带图标完全一样**（同一块图标块），只是左边多一条
            //   「移至 …」的标签说清会落到哪一格 —— 拖标题条和拖图标搬的是同一样东西，
            //   长相没有理由是两副。出了面板仍换成浮窗缩略图（第三档）。
            if (_panelHosts.TryGetValue(id, out var host))
            {
                var button = _stripeButtons.GetValueOrDefault(id);
                var icon = button?.FindDescendantOfType<ForgeIconHost>()?.Icon;

                _headerGhost = new ForgeDragGhost(
                    icon is not null
                        ? ForgeDragOverlay.CaptureThumb(
                            icon,
                            new SolidColorBrush(button?.IdleForeground ?? Palette.ButtonForeground))
                        : null,
                    Palette,
                    ForgeDragOverlay.CaptureThumb(host),
                    button?.HighlightRect(button.Bounds.Size).Size ?? default,
                    button?.HoverBackgroundBrush ?? new SolidColorBrush(Palette.ButtonHoverBackground),
                    button?.ArcCornerRadius ?? default);
                _headerGhost.Show();
            }

            // 指针归我们，不然一出主窗口就收不到移动了
            e.Pointer.Capture(this);
        }

        // 落点反馈与拖条带按钮**同一套**：条带上让出插入槽、画高亮。
        // 拖回条带上松手 = 重新停靠（见 OnHeaderPointerReleased）。
        // ⚠ 要**先**算落点：下面幽灵窗那一档挑哪副长相，靠的就是这一步的结果。
        UpdateDragFeedback(point);

        if (_headerGhost is { } ghost)
        {
            // 拖影的样子 = 松手会发生什么，判据只有一个：**有没有落点**。
            //   有 → 图标块 + 「移至 …」标签（会停靠到那一格）；
            //   没有、且还在本面板里 → 只有图标块（松手 = 取消，什么都不变）；
            //   没有、且已经出了本面板 → 浮窗缩略图（松手会浮出）。
            // ⚠ 曾经拿"出没出本面板"当浮动判据 —— 那是"落到别处一律浮出"时代的写法。
            //   现在条带上到处都能停靠，出了本面板照样可能有落点，
            //   于是拖到别的停靠位仍显示浮窗缩略图（用户实机提的）。
            // 照上游 isDragOut（ToolWindowDragHelper.kt:172-181）：
            //   ① 落在**任何**可见的停靠面板里 → 不算拖出去；
            //   ② 否则看有没有目标条带，没有才算拖出去。
            // ⚠ 曾经拿"出没出**本**面板"当判据 —— 于是拖到别的停靠位仍是浮窗缩略图（用户实机提的）。
            var target = CurrentDropTarget();

            ghost.SetFloatPreview(target is null && !IsOverDockedPanel(point));
            ghost.SetLabel(
                target is { } t ? ForgeMenuBuilder.DescribeMoveTarget(t.Anchor, t.IsSplit) : null,
                onRight: target is { } tt && LabelOnRight(tt));
            ghost.MoveTo(this.PointToScreen(point), new Vector(_headerGhostGrab.X, _headerGhostGrab.Y));
        }
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
        var insideSource = _panelHosts.TryGetValue(id, out var sourceHost)
                           && new Rect(sourceHost.Bounds.Size).Contains(e.GetPosition(sourceHost));

        _headerGhost?.Close();
        _headerGhost = null;
        _headerDragHeader = null;
        _overlay.Clear();
        _headerDragId = null;
        _headerDragging = false;
        ClearDragLayout();

        if (target is { } t)
        {
            // 落回条带 = 停靠到那一格：先回内部形态（模型记着上次是固定还是不固定）再搬 ——
            // 次序不能反，浮着的时候搬位置，落点那格的互斥判定看不到它
            shell.Manager.SetType(id, shell.Manager.ResolveInternalType(id));
            shell.Manager.MoveTo(id, t.Anchor, t.Order, t.IsSplit);
            return;
        }

        // 本面板之内松手 = 取消（紧凑形态全程都在说"还没挑好落点"）—— 什么都不变
        if (insideSource)
        {
            return;
        }

        // 面板外松手 = 这一刻才真正浮出；窗口摆到光标处（按下的抓握点对齐）
        shell.Manager.SetType(id, ToolWindowType.Floating);
        if (_externalWindows.TryGetValue(id, out var external)
            && this.PointToScreen(point - _headerDragGrab) is { } origin)
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

        // 起拖的两个条件，缺一不可 —— 且**只在起拖前判一次**（见 _dragStarted）：
        //   ① 走够基类的死区 7（MouseDragHelper.java:32）；
        //   ② **已经离开这个按钮**。
        // ② 不是上游的原话，是这边必须加的一条：拖动一开始就会把被拖按钮排成零尺寸
        //   （腾出落点槽），而按钮一旦没了尺寸，Button 就判不出"松手落在自己身上"、
        //   发不出 Click —— 手抖八九个像素的一次点击会当场变成"拖了一下又没拖成"，
        //   也就是「侧边栏按钮有时要点两下」。
        if (!_dragStarted)
        {
            if (moved < ForgeMetrics.StripeButtonDragStartDeadzone || _dragButtonBounds.Contains(point))
            {
                return;
            }

            _dragStarted = true;

            // 拖动**从这一刻**起有视觉：快照拍在按钮还排在原位的时候（此后它被排成零尺寸就拍不到了），
            // 对应上游 createStripeButtonDragImage 在 dragStarted 时产出 BufferedImage。
            // 这一步只改视觉，不碰捕获 —— 理由见按下处。
            // 「松手会浮出窗口」那一档要给的是**这个面板的缩略图**；面板还没建/没开就退回按钮快照
            // （ForgeDragGhost 自己会 windowThumb ?? tabThumb）。
            _dragPanelThumb ??= _dragId is { } id && _panelHosts.TryGetValue(id, out var host)
                ? ForgeDragOverlay.CaptureThumb(host)
                : null;

            // 跟手的是**这个图标本身**，不是整颗按钮的快照。
            // ⚠ 拍的是图标控件，不是那个图标槽（ForgeIconHost）—— 槽跟按钮一样大，
            //    且图标在槽里是按 IconPlacement 摆的（左右条带的内边距不一样），
            //    拍整个槽再"居中"，居中的是**槽**、不是图标，看着就是偏的（用户实机提的）。
            //    拍图标本身得到的是贴边位图，摆进背板才真的居中。
            // 前景退回常规那一档：开着的面板，按钮是激活态、图标是白的，
            // 而拖影背板取的是悬停底色 —— 白图标落在浅色主题的浅灰背板上看不见（用户实机提的）。
            _dragThumb ??= button.FindDescendantOfType<ForgeIconHost>()?.Icon is { } icon
                ? ForgeDragOverlay.CaptureThumb(icon, new SolidColorBrush(button.IdleForeground))
                    ?? ForgeDragOverlay.CaptureThumb(button)
                : ForgeDragOverlay.CaptureThumb(button);

            // 背板 = 这颗按钮的**背板**（悬停高亮那块 30×30），不是它占的整格 ——
            // 按钮控件跟条带一样宽，拿 Bounds 当背板会胖一圈。落点方块用同一个来源，两者天然等大。
            // 底色与圆角都问**这颗按钮**要：拖影就是"被拎起来的那个图标按钮"，
            // 长相该与它一致（宿主改了圆角/颜色，这里跟着走，不留第二份常量）。
            _dragGhost ??= OpenGhost(
                new ForgeDragGhost(
                    _dragThumb,
                    Palette,
                    _dragPanelThumb,
                    button.HighlightRect(button.Bounds.Size).Size,
                    button.HoverBackgroundBrush ?? new SolidColorBrush(Palette.ButtonHoverBackground),
                    button.ArcCornerRadius));

        }

        UpdateDragFeedback(point);
    }

    private void OnStripePointerReleased(object? sender, PointerReleasedEventArgs e)
        => EndStripeGesture(e.GetPosition(this), commit: true);

    /// <summary>
    /// 捕获丢了：**先试着抢回来**，抢不回来才按取消收尾。
    /// </summary>
    /// <remarks>
    /// ⚠ 这个信号在两个平台上语义不一样，不能直接当成「用户取消了拖拽」：
    /// <list type="bullet">
    /// <item>Windows 的捕获是 OS 级的（<c>SetCapture</c>），丢了基本就是真被打断；</item>
    /// <item>macOS 没有对应 API，Avalonia 的 mac 后端在**主窗口不再是 key window** 时就会发它
    /// —— 拖拽期间任何一扇窗口浮出来（我们自己的拖影就是一扇）都可能触发。</item>
    /// </list>
    /// 把它一律当打断，等于把 Windows 的语义写死进业务逻辑，mac 上表现为
    /// 「拖影一出现，拖拽就停在那儿动不了」（用户实机）。
    /// <para>
    /// 所以这里只把它当作「可能有麻烦」：重新捕获一次，成了就继续拖。真正终止拖拽的权威信号
    /// 是<b>松手</b>（<see cref="OnStripePointerReleased"/>）与 <b>Esc</b>
    /// （<see cref="OnStripeKeyDown"/>）—— 手势的存活由本类的状态机说了算，不由平台信号说了算。
    /// </para>
    /// </remarks>
    private void OnStripePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_dragId is not null && TryRecapture(e.Pointer))
        {
            return;
        }

        EndStripeGesture(_dragStart, commit: false);
    }

    /// <summary>把指针重新捕回本控件；平台不给就返回 <see langword="false"/>。</summary>
    private bool TryRecapture(IPointer pointer)
    {
        try
        {
            pointer.Capture(this);
        }
        catch (InvalidOperationException)
        {
            // 指针已经不在了（设备拔了 / 触摸抬起）：没得抢，按打断收尾。
            return false;
        }

        return ReferenceEquals(pointer.Captured, this);
    }

    /// <summary>Esc 取消拖拽 —— 与松手并列的另一个权威终止信号。</summary>
    /// <remarks>
    /// 上游 <c>ToolWindowDragHelper.kt</c> 同样把 Esc 当取消。补这一条也是为了兜住
    /// <see cref="OnStripePointerCaptureLost"/> 那边的新策略：既然捕获丢失不再终止拖拽，
    /// 就得留一条用户随时能用的出路，免得极端情况下拖拽态卡着下不来。
    /// </remarks>
    private void OnStripeKeyDown(object? sender, KeyEventArgs e)
    {
        if (_dragId is null || e.Key != Key.Escape)
        {
            return;
        }

        EndStripeGesture(_dragStart, commit: false);
        e.Handled = true;
    }

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
        _dragStarted = false;
        ClearDragLayout();

        if (!commit || !moved || dragged is null)
        {
            return;
        }

        if (target is { } t)
        {
            _shell!.Manager.MoveTo(dragged, t.Anchor, t.Order, t.IsSplit);
            return;
        }

        // 落在任何落区之外 = **浮出来**，不是"什么都不做"。
        // 条带上下两个落区之间那段空白（上组只占上半、下组是窗口底部一条）也算"之外" ——
        // 实机 Rider 就是在那儿松手直接浮窗（用户实测）。上游同一条路：
        // ToolWindowDragHelper.kt:457 · processDragFinish 拿不到目标条带就走浮动分支。
        // 与拖标题条松手在面板外是同一件事，见 OnHeaderPointerReleased 末尾。
        FloatAt(dragged, point);
    }

    /// <summary>
    /// 让一个工具窗口浮出来，窗口摆到光标处。
    /// </summary>
    /// <remarks>
    /// 没开的先开——浮出一个看不见的窗口等于什么都没发生。
    /// 抓握点对齐：按下时抓在按钮哪个位置，浮窗就摆在哪，不会"跳"一下。
    /// </remarks>
    /// <param name="id">哪个工具窗口。</param>
    /// <param name="point">松手的位置（相对本控件）。</param>
    private void FloatAt(string id, Point point)
    {
        var shell = _shell!;

        if (shell.Manager.Layout.GetInfo(id) is { IsVisible: false })
        {
            shell.Open(id);
        }

        shell.Manager.SetType(id, ToolWindowType.Floating);

        if (_externalWindows.TryGetValue(id, out var external)
            && this.PointToScreen(point - _dragGrab) is { } origin)
        {
            external.Window.Position = origin;
        }
    }

    /// <summary>
    /// 把外观里那套**方按钮**的度量交给按钮 —— 条带按钮与「更多」按钮共用。
    /// </summary>
    /// <remarks>
    /// <b>它们是兄弟按钮</b>（<c>SquareStripeButton.kt:62-80</c>），尺寸、圆角、图标大小、
    /// 高亮内边距本来就该同一档。
    /// <para>
    /// ⚠ <b>由来（2026-08-26 宿主实机："三个点歪了、背板大小也不一样"）。</b>
    /// 这一段原先只写在条带按钮那条路上，「更多」按钮只设了 Palette / Side / 有没有可挑的，
    /// 于是它 <c>IsHostSized</c> 为假 —— 退回上游那套 40×40 与 Islands 的**左右不对称内边距**
    /// （5/2，本意是把窄按钮在宽条带里推向外缘）。结果就是它比邻居大一圈、图标还偏一边。
    /// 抽成一处之后，宿主再加外观键时不会只喂到其中一颗。
    /// </para>
    /// </remarks>
    /// <param name="button">要喂参数的按钮。</param>
    /// <param name="side">它长在哪根条带上 —— 内边距按左右取不同的键（Islands 两侧镜像）。</param>
    private void ApplySquareButtonAppearance(ForgeSquareButtonBase button, StripeSide side)
    {
        button.Palette = Palette;
        // 内边距按左右取不同的键；Islands 下两侧镜像不对称，设错边图标会朝窗口里侧挤。
        // 按钮实例是**复用**的，拖到另一条带时这里要跟着改，不能只在新建时设。
        button.IsLeftStripe = side == StripeSide.Left;
        // 外观里给的是**半径**，ArcOverride 收的是**直径** —— 在这一处换算，别让两边都换
        button.ArcOverride = Appearance.StripeButtonCornerRadius is { } radius
            ? radius * 2
            : StripeButtonArc;
        button.HighlightSize = Appearance.StripeHighlightSize;
        button.IconSizeOverride = Appearance.StripeIconSize;
        button.HighlightInsetHorizontal = Appearance.StripeHighlightInsetHorizontal;
        button.HighlightInsetVertical = Appearance.StripeHighlightInsetVertical;
    }

    private ForgeMoreButton GetMoreButton(StripeSide side)
    {
        if (!_moreButtons.TryGetValue(side, out var button))
        {
            button = new ForgeMoreButton { Side = side, IsLeftStripe = side == StripeSide.Left };
            // 菜单挂在按钮的 Flyout 上、条目每次打开前重建 —— 两条都是必须的，理由见 ForgeMorePopup.Attach。
            ForgeMorePopup.Attach(button, _shell!);

            _moreButtons[side] = button;
        }

        ApplySquareButtonAppearance(button, side);
        button.ActiveSide = MoreButtonSide;
        // 有条带按钮被摘掉（isShowStripeButton=false）时才有东西可挑
        button.HasHiddenToolWindows = ForgeMorePopup.ResolveItems(_shell!.Manager.Layout).Count > 0;

        return button;
    }

    /// <summary>拖拽中按钮的透明度（<c>ToolWindowDragHelper.kt:94 · THUMB_OPACITY</c>）。</summary>
    private const double ForgeDragOpacity = 0.85;

    /// <summary>按下那一刻，被按的条带按钮在框架坐标系里的矩形（松手落回它里面 = 没搬家）。</summary>
    private Rect _dragButtonBounds;

    /// <summary>
    /// 这一次手势**已经起拖了**——起拖的两个条件只在起拖前判，起拖后不再判。
    /// </summary>
    /// <remarks>
    /// ⚠ 少了这个闩，「离开这个按钮」那条会在**每个**指针事件上重判：
    /// 拖出去再拖回来，指针一进原按钮那块地，移动处理就整个提前 return ——
    /// 虚影当场停在那儿不再跟手（用户看到的是「拖着的图标没了」），落点也不再更新。
    /// 而且它天然是方向相关的：往下拖离开了没事，往上拖回来就中招，
    /// 正是用户报的「先往下拖、再往上拖就很卡」。
    /// <para>
    /// 上游的死区同样只管**起拖**（<c>MouseDragHelper.java:32</c>），起拖之后每一个
    /// 移动事件都照常喂给 <c>relocate</c>。
    /// </para>
    /// </remarks>
    private bool _dragStarted;

    /// <summary>
    /// 当前留着空槽的那一组；落点不在任何条带上、或没在拖，就是 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 插到第几个**不记在这儿**——那是排布算的，见 <see cref="StripeItemsPanel.DropIndex"/>。
    /// </remarks>
    private StripeItemsPanel? _dropPanel;

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
        SetDropPanel(null);
        if (_dndSeparatorGroup is not null)
        {
            _dndSeparatorGroup = null;
            SyncStripes();
        }

        foreach (var panel in StripePanels())
        {
            panel.DraggedChild = null;
        }

        _lastZone = null;

        // ⚠ 顺序与延后都要紧：快照是拖影窗口 Image.Source 上挂着的那张，
        //    先让拖影松开它（CloseAndDetach），再把销毁排到这一帧绘制之后（ReleaseThumb）。
        //    当场 Dispose 会在窗口关闭后的那次 Paint 上炸掉整个进程 —— 见 ReleaseThumb 的注释。
        _dragGhost?.CloseAndDetach();
        _dragGhost = null;
        ForgeDragOverlay.ReleaseThumb(_dragThumb);
        _dragThumb = null;
        ForgeDragOverlay.ReleaseThumb(_dragPanelThumb);
        _dragPanelThumb = null;
    }

    /// <summary>开一扇幽灵窗。</summary>
    private static ForgeDragGhost OpenGhost(ForgeDragGhost ghost)
    {
        ghost.Show();
        return ghost;
    }

    /// <summary>拖影跟手：抓握点始终待在光标下。</summary>
    /// <param name="point">当前指针位置（相对本控件）。</param>
    private void MoveGhost(Point point)
        => _dragGhost?.MoveTo(this.PointToScreen(point), new Vector(_dragGrab.X, _dragGrab.Y));

    /// <summary>
    /// 条带拖影现在是不是「松手会浮出窗口」那一档。
    /// </summary>
    /// <remarks>给用例看的：这条是硬约束——落不下去的地方，拖影必须是小窗口预览。</remarks>
    public bool StripeDragShowsFloatPreview => _dragGhost?.IsFloatPreview ?? false;

    /// <summary>
    /// 换一组留空槽：别的组撤掉，这一组留着。
    /// </summary>
    /// <remarks>
    /// 顺带管着 <c>LayoutUpdated</c> 的订阅——蓝框的位置只能在**排布之后**
    /// 才读得到（<see cref="StripeItemsPanel.DropSlotRect"/> 是排布期算的），
    /// 所以拖拽期间挂一下这个事件、排完再去校正，而不是反过来为了读它强制排布。
    /// 不拖的时候不留订阅。
    /// </remarks>
    /// <param name="panel">要留空槽的那一组；<see langword="null"/> 表示都撤掉。</param>
    private void SetDropPanel(StripeItemsPanel? panel)
    {
        foreach (var other in StripePanels())
        {
            if (!ReferenceEquals(other, panel) && other.DropOffset is not null)
            {
                other.DropOffset = null;
            }
        }

        if (ReferenceEquals(_dropPanel, panel))
        {
            return;
        }

        if (_dropPanel is null)
        {
            LayoutUpdated += OnDragLayoutUpdated;
        }
        else if (panel is null)
        {
            LayoutUpdated -= OnDragLayoutUpdated;
            _overlay.DropRect = null;
            _overlay.SplitBoxRect = null;
        }

        _dropPanel = panel;
    }

    /// <summary>
    /// 当前落点；落不下去就是 <see langword="null"/>。
    /// </summary>
    /// <remarks>用的是上一帧排布定下的插入位置——与蓝框、与松手后真正落到的那一格同一个来源。</remarks>
    private StripeDropTarget? CurrentDropTarget()
    {
        if (_dropPanel is not { } panel || _lastZone is not { } zone)
        {
            return null;
        }

        return ResolveTargetFrom(panel, zone)
               ?? StripeDropResolver.Resolve(zone.Side, zone.Group, isAfterSeparator: false, order: -1);
    }

    /// <summary>
    /// 指针是不是落在**某个可见的停靠面板**里。
    /// </summary>
    /// <remarks>
    /// 上游 <c>isDragOut</c> 的第一条：落在可见的停靠面板里就**不算拖出去**
    /// （<c>ToolWindowDragHelper.kt:172-181 · isPointInVisibleDockedToolWindow</c>）。
    /// 少了这条，拖影一离开源面板就变成浮窗缩略图 —— 哪怕它正悬在另一块停靠面板上。
    /// </remarks>
    /// <param name="point">指针位置（相对本控件）。</param>
    /// <returns>在不在。</returns>
    private bool IsOverDockedPanel(Point point)
    {
        foreach (var (id, host) in _panelHosts)
        {
            if (!host.IsVisible
                || _shell?.Manager.Layout.GetInfo(id) is not { IsVisible: true } info
                || ToolWindowManager.IsExternal(info.Type))
            {
                continue;
            }

            if (host.TranslatePoint(default, this) is { } origin
                && new Rect(origin, host.Bounds.Size).Contains(point))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>排布落定了，把蓝框挪到那个空槽上。</summary>
    private void OnDragLayoutUpdated(object? sender, EventArgs e) => UpdateDropIndicator();

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
    /// <para>
    /// ★ <b>这里一次排布都不强制。</b> 每个指针事件只做两件事：把落点交给目标组
    /// （<see cref="StripeItemsPanel.DropOffset"/>）、把虚影挪一下（只影响绘制）。
    /// 让位、插入位置、空槽矩形都交给下一帧的排布去算，蓝框由
    /// <see cref="OnDragLayoutUpdated"/> 在排完之后校正。
    /// 与上游 <c>processDropButton</c> 只 <c>revalidate(); repaint()</c> 同形
    /// （<c>AbstractDroppableStripe.kt:197-223</c>）。
    /// </para>
    /// <para>
    /// ⚠ 曾经是反过来的：先在排布之外算出插入位置、写回去、**同步 <c>UpdateLayout()</c>**、
    /// 再把空槽矩形读回来画框。那句同步排布在 Avalonia 是整棵可视树的事，
    /// 于是鼠标每动一下整个停靠区（相机画面、日志、图表）就跟着重量一遍 ——
    /// 用户报的「拖动很卡」就是它。别再把"读一眼排布结果"写进指针事件里。
    /// </para>
    /// </remarks>
    /// <param name="point">当前指针位置（相对本控件）。</param>
    private void UpdateDragFeedback(Point point)
    {
        _overlay.Palette = Palette;
        _overlay.IsCompact = ResolveCompact();

        var metrics = ForgeStripeButtonMetrics.ForTheme(Palette.Family, _overlay.IsCompact);

        // 被拖的那个从**每一组**的排布里拿掉——它可能正被拖去另一根条带，
        // 只清源组的话，目标组那边仍会把它算进去。
        var dragged = DraggedButton;
        foreach (var panel in StripePanels())
        {
            panel.DraggedChild = dragged;

            // 空槽高 = 被拖那颗按钮**自己**的高：显名时它被名字撑高，拿度量里的图标格高
            // 会让空槽比按钮矮一截，落点方块跟着矮（拖影却是整块背板，两者对不上）。
            // 它已被排成零尺寸，Bounds 不能用；DesiredSize 仍在量（见 StripeItemsPanel.MeasureOverride）。
            panel.DropSlotHeight = dragged is { DesiredSize.Height: > 0 } d ? d.DesiredSize.Height : metrics.Height;
        }

        // 跟随光标的拖影：它是个独立窗口，挪它跟主窗口的排布、绘制都没关系。
        MoveGhost(point);

        _lastZone = ResolveDropZone(point);
        if (_lastZone is not { } zone || PanelOf(zone) is not { } target || !ApplyDropOffset(target, zone, point))
        {
            SetDropPanel(null);
            if (_dndSeparatorGroup is not null)
            {
                _dndSeparatorGroup = null;
                SyncStripes();
            }

            // 只撤落点反馈 —— 拖影要一直跟手，哪怕此刻落不下去（两个落区之间那段空白）。
            _overlay.ClearDropFeedback();

            // ★ 硬约束：这儿松手会**浮出一个窗口**，那拖影现在就得长成小窗口的样子。
            //   拖影的样子 = 松手会发生什么；不切的话用户分不清松手是浮动还是换位。
            _dragGhost?.SetFloatPreview(true);
            _dragGhost?.SetLabel(null);
            MoveGhost(point);
            return;
        }

        SetDropPanel(target);

        // 落到哪一组，那一组的分隔符就按拖拽规则排（临时分隔符 / 置顶分隔符）；换组时把上一组收回正常排布。
        if (_dndSeparatorGroup != zone)
        {
            _dndSeparatorGroup = zone;
            SyncStripes();
        }

        // 有人接得住 → 拖影回到"按钮的样子"。先切形态再挪位置（形态换了尺寸跟着变）。
        _dragGhost?.SetFloatPreview(false);
        MoveGhost(point);

        // ⚠ 左右一律从**落在哪根条带**推导，不能看 anchor —— 下组的 anchor 恒为 BOTTOM，
        //    拿它判会让右条带下组用成左条带的内边距，落点方块整体偏 3px
        //    （用户报的「右下角的投影不居中」）。四个角表现不一致，根子就是
        //    同一件事在不同地方分别从 side 和 anchor 推。
        var toolbar = zone.Side == StripeSide.Right ? _right : _left;

        // 高亮那片用的是上一帧排布定下的插入位置：这一帧的还没排出来。
        // 差一帧只在刚进这一组时看得见，下一帧自己就对了。
        var drop = ResolveTargetFrom(target, zone)
                   ?? StripeDropResolver.Resolve(zone.Side, zone.Group, isAfterSeparator: false, order: -1);

        // 「移至 …」标签与拖标题条时同一套（上游 ToolWindowDragHelper 两条路都挂）：
        // 拖影上写明松手会去哪；标签在左条带时挂到图标右边，免得飘出窗外。标签尺寸变了要再挪一次。
        _dragGhost?.SetLabel(
            ForgeMenuBuilder.DescribeMoveTarget(drop.Anchor, drop.IsSplit),
            onRight: LabelOnRight(drop));
        MoveGhost(point);

        _overlay.AreaRect = ResolveDropAreaRect(drop.Anchor, toolbar, drop.IsSplit);
        UpdateDropIndicator();
    }

    /// <summary>
    /// 蓝色落点方块与「另一半」虚线框：画在排布让出来的那个空槽上。
    /// </summary>
    /// <remarks>
    /// 只读排布结果，不触发排布。落点方块按上游
    /// <c>AbstractDroppableStripe.kt:634-642</c> 去掉图标 padding。
    /// </remarks>
    private void UpdateDropIndicator()
    {
        if (_dropPanel is not { } panel
            || _lastZone is not { } zone
            || panel.DropSlotRect is not { } rect
            || panel.TranslatePoint(rect.Position, this) is not { } origin)
        {
            _overlay.DropRect = null;
            _overlay.SplitBoxRect = null;
            return;
        }

        // 落点方块 = 按钮的**背板**，与跟手那块拖影背板一模一样大 ——
        // 两者说的本来就是同一件事：这个图标松手后占的那块地。
        // 所以尺寸从同一个来源取（HighlightRect），而不是各自扣各自的内边距（那必然漂开）。
        var cell = DraggedButton is { } dragged
            ? dragged.HighlightRect(new Size(panel.Bounds.Width, rect.Height))
            : new Rect(0, 0, panel.Bounds.Width, rect.Height);

        // 落点方块的圆角跟按钮走（宿主可能收过圆角）
        _overlay.DropCornerRadius = DraggedButton?.ArcCornerRadius ?? default;

        _overlay.DropRect = new Rect(
            origin.X + cell.X,
            origin.Y + cell.Y,
            Math.Max(0, cell.Width),
            Math.Max(0, cell.Height));

        // 「另一半」：30 见方的虚线框，贴着落点方块下面。
        // ⚠ 条件是**该组里真的有那条分组分隔符**（AbstractDroppableStripe.kt:644-654
        //    「只在竖条带、且分隔器可见时画」），不是落点的 IsSplit ——
        //    下组的 IsSplit 只表示"落在右条带"，右下角于是永远多一个虚线框。
        // 「另一半」虚线框：**只在分隔符是拖拽期临时加的那条时画**（separator.dndState）—— 组里本来就有下半栏时正常拖放，
        // 不需要指示；没有下半栏才用虚线框告诉用户"放这儿会出一个"。
        // 位置**固定在分隔符旁边**，不跟落点方块走（AbstractDroppableStripe.kt paintComponent）——
        // 分隔符在末尾 / 中间：画在线下方、在一格按钮高度里居中：sep.y + sep.h + (slotH − 30) / 2；
        // 分隔符置顶：画在线上方：sep.y − (slotH + 30) / 2。
        // 落点方块仍跟着指针排，排到线下面时两者正好重合 —— 那就是"放这儿 = 下半栏"。
        var box = ForgeMetrics.StripeSplitDropBoxSize;
        _overlay.SplitBoxRect = null;
        if (SeparatorOf(panel) is { IsDragAndDrop: true } sep && sep.TranslatePoint(default, this) is { } sepOrigin)
        {
            var atTop = panel.Children.IndexOf(sep) == 0;
            var boxTop = atTop
                ? sepOrigin.Y - ((rect.Height + box) / 2)
                : sepOrigin.Y + sep.Bounds.Height + ((rect.Height - box) / 2);
            _overlay.SplitBoxRect = new Rect(origin.X + ((panel.Bounds.Width - box) / 2), boxTop, box, box);
        }
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
                stripe, side == StripeSide.Right, FirstVisibleToolWindowSize(new StripePlacement(side, StripeGroup.Top)));
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
                FirstVisibleToolWindowSize(new StripePlacement(side, StripeGroup.Bottom)),
                isSplit: side == StripeSide.Right);

            if (bottom.Contains(point))
            {
                return (side, StripeGroup.Bottom);
            }
        }

        return null;
    }

    /// <summary>
    /// **这根条带的这一组**里第一个可见工具窗口占多大；一个都没开就用兜底 350。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowToolbar.kt:261-272 · getFirstVisibleToolWindowSize</c>：
    /// 上游遍历的是 <c>getButtons()</c>——**本条带自己的按钮**，下组只会看到 BOTTOM 窗口。
    /// <para>
    /// ⚠ 曾经遍历的是全部工具窗口：下方一个没开、左侧开着一个 weight 0.2 的，
    /// 下组落区就被算成 0.2 × 窗高而不是兜底 350，比上游矮一半多 ——
    /// 用户报的「要拖到很下面才命中下停靠」就是它。上组同理会拿下方窗口的 weight 去乘窗宽。
    /// </para>
    /// </remarks>
    /// <param name="placement">哪根条带的哪一组。</param>
    private double FirstVisibleToolWindowSize(StripePlacement placement)
    {
        var horizontal = placement.Group == StripeGroup.Top;
        foreach (var vm in _shell!.ToolWindows)
        {
            if (!vm.IsOpen
                || _shell.Manager.Layout.GetInfo(vm.Id) is not { } info
                || StripePlacementResolver.Resolve(info.Anchor, info.IsSplit) != placement)
            {
                continue;
            }

            return (horizontal ? Bounds.Width : Bounds.Height) * info.Weight;
        }

        return ForgeMetrics.DropAreaFallbackSize;
    }

    /// <summary>某一组落区对应的按钮组。</summary>
    private StripeItemsPanel PanelOf((StripeSide Side, StripeGroup Group) zone)
    {
        var toolbar = zone.Side == StripeSide.Left ? _left : _right;
        return zone.Group == StripeGroup.Top ? toolbar.TopGroup : toolbar.BottomGroup;
    }

    /// <summary>
    /// 把落点换算成组内偏移交给那一组；插到第几个由它自己在排布期算。
    /// </summary>
    /// <remarks>
    /// ⚠ 换算的基准是「**去掉被拖项、也不含空槽**那条序列的顶」，不是这一组当前的顶边。
    /// 下组贴底排布（<c>ForgeToolWindowToolbar</c> 的 <c>Dock.Bottom</c>），**顶边会动**：
    /// 摘掉被拖的那个往下掉一格、留出空槽又往上顶一格。拿顶边换算，
    /// 同一个指针位置在相邻两帧会解出两个格子——表现为下组落点偏一格、
    /// 且在格子边界上来回翻（翻一次重排一次，也是拖动发卡的一份）。
    /// <para>
    /// 所以下组一律从**底边**倒推：底边是稳的，序列高当场按同一条口径算
    /// （<see cref="SequenceHeight"/>），跟上一帧排的是什么无关。
    /// 上组是 <c>Dock.Top</c>，顶边本来就不动，直接用。
    /// </para>
    /// </remarks>
    /// <param name="panel">目标组。</param>
    /// <param name="zone">落在哪根条带的哪一组。</param>
    /// <param name="point">当前指针位置（相对本控件）。</param>
    /// <returns>换算成功没有（拿不到坐标就是没有）。</returns>
    private bool ApplyDropOffset(StripeItemsPanel panel, (StripeSide Side, StripeGroup Group) zone, Point point)
    {
        // Bounds 是相对父级的，逐级换算容易差 1px，直接问 Avalonia 要绝对位置
        if (panel.TranslatePoint(default, this) is not { } origin)
        {
            return false;
        }

        var top = zone.Group == StripeGroup.Bottom
            ? origin.Y + panel.Bounds.Height - SequenceHeight(panel)
            : origin.Y;

        panel.DropOffset = point.Y - top;
        return true;
    }

    /// <summary>
    /// 「移至 …」标签挂图标块的右边还是左边：上游按目标名里含不含 "Left" 翻面（ToolWindowDragHelper.kt:361-368），
    /// 「Left Top / Left Bottom / Bottom Left」都算 —— 即**落在左条带的一律挂右边**（含左条带的底组），其余挂左边。
    /// 只判 anchor == Left 会漏掉「底部 左侧」，表现是左条带上半标签在右、下半标签却跑到左边。
    /// </summary>
    private static bool LabelOnRight(StripeDropTarget target)
        => StripePlacementResolver.Resolve(target.Anchor, target.IsSplit).Side == StripeSide.Left;

    /// <summary>这一组里可见的那条分隔符；没有就是 null。</summary>
    private static ForgeStripeSeparator? SeparatorOf(StripeItemsPanel panel)
    {
        foreach (var child in panel.Children)
        {
            if (child is ForgeStripeSeparator { IsVisible: true } sep) return sep;
        }

        return null;
    }


    /// <summary>
    /// 一组里参与排布的那条序列有多高（去掉被拖项、不含空槽）。
    /// </summary>
    /// <remarks>
    /// 口径必须与 <see cref="StripeItemsPanel.ArrangeOverride"/> 一致：每一项算上它后面那道缝。
    /// </remarks>
    private double SequenceHeight(StripeItemsPanel panel)
    {
        var dragged = DraggedButton;
        var total = 0.0;

        foreach (var child in panel.Children)
        {
            if (child.IsVisible && !ReferenceEquals(child, dragged))
            {
                total += child.DesiredSize.Height + ForgeMetrics.StripeButtonGap;
            }
        }

        return total;
    }

    /// <summary>
    /// 把排布定下的插入位置翻成落点（anchor + order + 另一半）。
    /// </summary>
    /// <remarks>
    /// 序号是**排布**的事，order 是**模型**的事，两者在"被拖那个已从序列里拿掉"之后不再一一对应，
    /// 所以要靠一张与排布同序的 order 表把序号翻过去。
    /// </remarks>
    /// <param name="panel">留着空槽的那一组。</param>
    /// <param name="zone">落在哪根条带的哪一组。</param>
    /// <returns>落点；这一组还没排出空槽就是 <see langword="null"/>。</returns>
    private StripeDropTarget? ResolveTargetFrom(StripeItemsPanel panel, (StripeSide Side, StripeGroup Group) zone)
    {
        var index = panel.DropIndex;
        if (index < 0)
        {
            return null;
        }

        // ⚠ 这里的枚举必须与 StripeItemsPanel 排布时的一模一样（可见、且不是被拖的那个），
        // 否则序号对不上位。被拖那个不参与是上游 AbstractDroppableStripe.kt:601 的
        // tools.remove(dragButton)。
        var dragged = DraggedButton;
        // 与排布序列平行：该位置是哪个按钮的 order；分隔符占一格但没有 order，记 null
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
                separatorIndex = orders.Count;
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
        }

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

        var target = StripeDropResolver.Resolve(zone.Side, zone.Group, index > separatorIndex, order);
        return StripeDropResolver.AdjustForReversedBottomGroup(target);
    }

    /// <summary>
    /// 松手时落在哪根条带的哪一组、插到第几个。
    /// </summary>
    /// <remarks>
    /// **只有松手这一次**才强制排布：要拿的是"用户看见的那个空槽"落到哪，
    /// 而它是排布算的。拖动过程中不能这么干（那正是拖动发卡的来源，
    /// 见 <see cref="UpdateDragFeedback"/>）。
    /// </remarks>
    private StripeDropTarget? ResolveDropTarget(Point point)
    {
        _lastZone = ResolveDropZone(point);

        // 落区都不中 —— 上游此时 getStripeFor 返回 null，就是「这里不能放」。
        // 别再兜一个默认组：那正是「点落在第一个图标上方就变成上停靠」的来源。
        if (_lastZone is not { } zone)
        {
            return null;
        }

        var panel = PanelOf(zone);
        if (!ApplyDropOffset(panel, zone, point))
        {
            return null;
        }

        panel.UpdateLayout();
        return ResolveTargetFrom(panel, zone);
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

        ForgeIslandDecor.Apply(
            host, Palette, info.Anchor, Appearance.IslandMetrics,
            borderBrush: IslandBorderBrush,
            // 非岛档那条朝向编辑区的边线：宿主给了就用宿主的（DockDividerBrush，走资源键），
            // 没给回落 Palette.WindowBorder。这条线才是经典档「面板之间那条线」的真身。
            dockBorderBrush: DockDividerBrush);

        var header = _panelHeaders[vm.Id];
        header.Palette = Palette;
        header.Title = vm.Title;
        header.TitleFontWeight = Appearance.HeaderTitleFontWeight;

        // 面板名的字号：宿主没给（NaN）就保持控件默认，别把 NaN 写进去。
        if (!double.IsNaN(Appearance.HeaderTitleFontSize))
        {
            header.TitleFontSize = Appearance.HeaderTitleFontSize;
        }

        // 标题条上的内容标签与文档标签共用同一支字重（宿主要的是"两处标签一样粗"）。
        header.Tabs.TitleFontWeight = Appearance.TabTitleFontWeight;

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
        header.TitleInsetLeft = Appearance.HeaderTitleInsetLeft;
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

        // 把这块面板的内容（标题条上那排 tab）一并交给菜单：⋮ 是"这块面板还能干什么"的兜底入口，
        // 面板窄到 tab 排不下时，那里就是唯一还能切块的地方。只有一块时菜单侧自己不出。
        var contents = shell.ToolWindows.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal))?.Contents;
        var menu = ForgeMenuBuilder.ForHeaderOptions(shell.Manager, id, HeaderMenuExtension, SupportedViewModes, contents);

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

            // ⚠ **分隔线一条都不在这儿画** —— 停靠也好、取消停靠（覆盖层）也好，
            //   面板与被它盖住 / 挨着的内容之间那条 1px，统统由**面板自己**画
            //   （ForgeIslandDecor 非岛分支给 host 加朝向编辑区那一侧的 1px 边框；
            //   岛档不画线，靠圆角留缝分隔，那是配色族定的形态）。
            //
            //   早先这里给覆盖层单独补过一条（对位 InternalDecoratorImpl.kt:599-612），
            //   结果是**两条叠在一起**：面板那条照画不误，于是取消停靠时那条边线有 2px，
            //   比停靠时粗一倍（实测非岛暗色档 #383E47 两列）。
            //   线归面板画还有一个好处：长度天然等于面板本人。resizer 横跨整条边，
            //   面板只占其中一段时（比如底边还开着一块通栏面板），多出来的那截会一路
            //   穿过别人的地盘 —— 实机反馈"下半部分还能看到上半部分的边缘的线"就是那么来的。
            //   颜色要调，调的是 WindowBorder（宿主的 DockDividerBrush），不是在这儿补线。
            resizer.LineBrush = null;

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

            // 这条线**要画出来**（上游那条就是 OnePixelSplitter，ToolWindowPane.kt:617）。
            //   · 非岛（expUI）：两块面板贴死，中间不画线就完全看不出边界在哪
            //     —— 实机反馈"下面左右两个面板打开时中间没有分割线"说的就是这里；
            //   · 岛（Islands）：两座岛之间本来就有缝，再画一条等于把缝糊死。
            //
            // ⚠ 颜色取 **WindowBorder**（上游的 Borders.color），**不是** EditorTabsBorder：
            //   这条线分的是**面板与面板**，与面板四周那圈边是同一件事，而那圈边正是用
            //   WindowBorder 画的（ForgeIslandDecor 的非岛分支）。取 EditorTabsBorder 会明显偏浅 ——
            //   实机反馈"这条分割线是灰的，上面那些是黑的"就是这么来的。
            //   （覆盖层那条 divider 仍用 EditorTabsBorder：它分的是**面板与被盖住的编辑区**，
            //    是"内容与内容之间"，上游也确实是另一条键。）
            //   "WindowBorder 在岛配色里是全透明"这一条不构成问题：岛档本来就不画这条线。
            // 画刷优先用宿主给的（DockDividerBrush，走资源键）；没给才回落到上游的 WindowBorder。
            split.LineBrush = Palette.Island is not null
                ? null
                : DockDividerBrush ?? new SolidColorBrush(Palette.WindowBorder);

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

        // ★ 排布的真值是**像素**：拖出来的像素直接落到停靠区上，不经过 weight 往返 ——
        //   一来一回的量化误差在窄尺寸下足够让边界每帧跳一格（"标题栏一跳一跳"）。
        //   下面写台账的 weight 只为存档，写回来不会反过来改像素（见 ForgeDockPane）。
        _pane.SetAnchorSize(e.Anchor, e.Pixels);

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
