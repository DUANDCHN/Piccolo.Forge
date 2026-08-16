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
/// 编辑区：一棵二叉分屏树，叶子是一组标签页。
/// </summary>
/// <remarks>
/// 标签页容器由 <see cref="TabStrip"/> 按 <c>ItemsSource</c> 管理；正文宿主按组与 id
/// 复用，开合别的标签页不会重造宿主交进来的正文控件。
/// </remarks>
public class ForgeEditorArea : TemplatedControl
{
    // 正文宿主的键是 (组, 标签页 id) 而不是单纯 id：分屏后同一个 id
    // 可同时存在于两个组（EditorWindow.kt:570-571）。
    private readonly Dictionary<(EditorGroupViewModel Group, string Id), ContentControl> _contents = [];
    private readonly Dictionary<EditorGroupViewModel, Control> _groupHosts = [];
    private readonly Dictionary<EditorGroupViewModel, ForgeEditorTabStrip> _groupStrips = [];
    private readonly Dictionary<EditorGroupViewModel, Border> _groupStripHosts = [];
    private readonly Dictionary<EditorGroupViewModel, ContentControl> _groupBodies = [];
    private readonly Dictionary<EditorGroupViewModel, StackPanel> _groupActions = [];
    // IntelliJ/Rider 的整个文档区是一座连续的 editor island；分屏叶子只是
    // 这座岛内部的 EditorWindow，不能各自再套 Margin/圆角，否则拆分后会
    // 变成两座互不相干的岛，也会在中间制造一条错误的“海”。
    private readonly Border _root = new();

    private EditorAreaViewModel? _area;

    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeEditorArea, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    static ForgeEditorArea()
    {
        // ⚠ 这些都是**纯外观**，不要走 Sync（那会重建整棵树，见 RefreshAppearance 的说明）
        PaletteProperty.Changed.AddClassHandler<ForgeEditorArea>((x, _) => x.RefreshAppearance());
        IslandMetricsProperty.Changed.AddClassHandler<ForgeEditorArea>((x, _) => x.RefreshAppearance());
        ShowTabActionsProperty.Changed.AddClassHandler<ForgeEditorArea>((x, _) => x.Sync());

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

    /// <summary>
    /// 每组标签条右端那个 ⋮ 按钮显不显示。默认开。
    /// </summary>
    /// <remarks>
    /// 上游标签条右端也是一条工具栏（<c>EditorTabbedContainer</c> 的 <c>TabList</c> 等动作）。
    /// 它在**标签排不下时尤其要紧** —— 那是找回看不见那几页的唯一入口。
    /// </remarks>
    public bool ShowTabActions
    {
        get => GetValue(ShowTabActionsProperty);
        set => SetValue(ShowTabActionsProperty, value);
    }

    /// <summary>
    /// 宿主往标签条右端追加自己的按钮（排在 ⋮ 左边）。
    /// </summary>
    /// <remarks>
    /// 与 <c>ForgeToolWindowFrame.HeaderButtonsExtension</c> 同形，只是编辑区一组一条标签行，
    /// 所以参数是那一组的第一页 id（没有页时为 <see langword="null"/>）。
    /// ⚠ 同一组请返回同一批控件实例，别每次同步都造新的。
    /// </remarks>
    public Func<string?, IReadOnlyList<Control>?>? TabActionsExtension { get; set; }

    /// <summary>
    /// ⋮ 的菜单：宿主可以往里追加自己的项。
    /// </summary>
    /// <remarks>参数是（菜单, 那一组的第一页 id）。库自己放的是「标签页列表 + 关闭系列」。</remarks>
    public Action<ContextMenu, string?>? TabActionsMenuExtension { get; set; }

    /// <summary><see cref="ShowTabActions"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> ShowTabActionsProperty =
        AvaloniaProperty.Register<ForgeEditorArea, bool>(nameof(ShowTabActions), defaultValue: true);

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
        _overlay.IsVisible = false;

        ActualThemeVariantChanged += (_, _) => ApplyThemeVariant();

        // 分屏树同步会临时摘挂组宿主，组内 TabStrip 不是稳定的拖拽所有者。
        // 编辑区本身不参与那次摘挂，由它统一接收移动、松手与捕获丢失，保证会话必能收尾。
        const RoutingStrategies dragRoutes = RoutingStrategies.Tunnel | RoutingStrategies.Bubble | RoutingStrategies.Direct;
        AddHandler(PointerMovedEvent, OnDragPointerMoved, dragRoutes, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnDragPointerReleased, dragRoutes, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, OnDragPointerCaptureLost, dragRoutes, handledEventsToo: true);

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

    // ── 拖出去 = 浮成独立的编辑窗口 ────────────────────────────────────────
    //
    // 对位上游 DockManagerImpl / DockableEditorTabbedContainer：把标签拖离所有容器，
    // 就新开一个窗口装着它；拖到别的容器上，就交给那个容器。
    // 浮窗里是**一整个编辑区**（能再开标签、能分屏），不是"单文档窗口"。

    private readonly Dictionary<Window, ForgeEditorArea> _floatingWindows = [];
    private ForgeEditorArea? _floatingOwner;

    /// <summary>这个编辑区浮出去的那些窗口。</summary>
    public IReadOnlyCollection<Window> FloatingWindows => _floatingWindows.Keys;

    /// <summary>
    /// 浮出去的编辑窗口用什么窗体装。
    /// </summary>
    /// <remarks>
    /// 与工具窗口那边同一个道理：一个应用里所有窗体该是同一副长相，而那副长相是宿主的事，
    /// 库只往里塞内容。不给就用一个朴素窗口。
    /// </remarks>
    public Func<Window>? ExternalWindowFactory { get; set; }

    /// <summary>
    /// 指针跑出本编辑区之后的反馈：示意窗口跟着走，停靠提示交给光标底下那个编辑区。
    /// </summary>
    /// <remarks>
    /// 覆盖层只画得到自己这个窗口里，所以出了窗口必须换成一个真窗口当拖影
    /// （<see cref="ForgeDragGhost"/>）；而"松手会落到哪儿"的提示，得由**目标那边**画 ——
    /// 它才知道自己的组和边界在哪。
    /// </remarks>
    private void UpdateCrossWindowFeedback(EditorDockDragSession session, Point here)
    {
        var inside = new Rect(Bounds.Size).Contains(here);
        var screen = ToScreen(here);
        session.LastScreenPoint = screen;

        // 指向别的编辑区（比如从浮窗拖回主窗口）：让那边画停靠提示
        var external = inside
            ? null
            : Family().FirstOrDefault(x => !ReferenceEquals(x, this) && x.HitTestScreen(screen));

        if (!ReferenceEquals(session.ExternalTarget, external))
        {
            session.ExternalTarget?.ClearDropHint();
            session.ExternalTarget = external;
        }

        external?.ShowDropHint(screen);

        if (inside)
        {
            session.Ghost?.Close();
            session.Ghost = null;
            return;
        }

        session.Ghost ??= Open(new ForgeDragGhost(session.Thumb, Palette));
        session.Ghost.MoveTo(screen, session.HoldDelta);

        static ForgeDragGhost Open(ForgeDragGhost ghost)
        {
            ghost.Show();
            return ghost;
        }
    }

    /// <summary>别人拖到我头上了：画出"松手就落在这儿"的提示。</summary>
    private void ShowDropHint(PixelPoint screen)
    {
        if (TopLevel.GetTopLevel(this) is not Window window)
        {
            return;
        }

        var here = window.PointToClient(screen) - Bounds.Position;
        var candidate = FindGroupAt(here);

        _overlay.IsVisible = true;
        _overlay.Palette = Palette;
        _overlay.ThumbImage = null;
        _overlay.ThumbRect = default;

        _overlay.AreaRect = candidate is not null && _groupHosts.TryGetValue(candidate, out var host)
            ? BoundsIn(host)
            : new Rect(Bounds.Size);

        _overlay.InvalidateVisual();
    }

    /// <summary>拖走了，把提示收掉。</summary>
    private void ClearDropHint()
    {
        _overlay.Clear();
        _overlay.IsVisible = false;
        _overlay.InvalidateVisual();
    }

    /// <summary>浮出去的窗口，而且里面就一份文档 —— 那时不摆标签条，文档名写在窗口标题上。</summary>
    private bool IsSingleDocumentWindow
        => _floatingOwner is not null
           && _area is { } area
           && area.Groups.Count == 1
           && area.Groups[0].Tabs.Count <= 1;

    /// <summary>把浮窗的标题跟到当前文档上。</summary>
    private void SyncFloatingTitle()
    {
        if (_floatingOwner is null || TopLevel.GetTopLevel(this) is not Window window || _area is not { } area)
        {
            return;
        }

        var title = area.ActiveTab?.Title
                    ?? area.Groups.SelectMany(g => g.Tabs).FirstOrDefault()?.Title;

        if (!string.IsNullOrEmpty(title))
        {
            window.Title = title;
        }
    }

    /// <summary>这一家：主编辑区，加上它浮出去的那些。</summary>
    private IEnumerable<ForgeEditorArea> Family()
    {
        var owner = _floatingOwner ?? this;
        yield return owner;

        foreach (var floating in owner._floatingWindows.Values)
        {
            yield return floating;
        }
    }

    /// <summary>屏幕上这一点是不是落在这个编辑区里。</summary>
    private bool HitTestScreen(PixelPoint screen)
    {
        if (!IsEffectivelyVisible || TopLevel.GetTopLevel(this) is not Window window || !window.IsVisible)
        {
            return false;
        }

        var origin = ToScreen(default);
        var end = ToScreen(new Point(Bounds.Width, Bounds.Height));

        return screen.X >= origin.X && screen.X <= end.X
            && screen.Y >= origin.Y && screen.Y <= end.Y;
    }

    private PixelPoint ToScreen(Point point)
        => TopLevel.GetTopLevel(this) is Window window ? window.PointToScreen(point + Bounds.Position) : default;

    /// <summary>
    /// 把一页挪到新窗口里（右键菜单「在新窗口中打开标签页」走的就是这里）。
    /// </summary>
    /// <remarks>与"把标签拖出编辑区"是**同一条路** —— 不另开一种"菜单开出来的窗口"。</remarks>
    /// <param name="tabId">标签页 id。</param>
    public void MoveToNewWindow(string tabId)
    {
        if (_area?.Detach(tabId) is not { } tab)
        {
            return;
        }

        // 没有拖拽起点，就摆在这个编辑区的中间偏上一点
        var screen = ToScreen(new Point(Bounds.Width / 3, Bounds.Height / 4));
        FloatOut(tab, screen, default);
        CloseIfEmpty();
    }

    /// <summary>把一页浮出去：新开一个窗口，里面是一整个编辑区。</summary>
    private void FloatOut(EditorTabViewModel tab, PixelPoint screen, Vector holdDelta)
    {
        var owner = _floatingOwner ?? this;
        var area = new EditorAreaViewModel();
        var control = new ForgeEditorArea
        {
            ThemeFamily = ThemeFamily,
            IslandMetrics = IslandMetrics,
            ExternalWindowFactory = owner.ExternalWindowFactory,
            _floatingOwner = owner,
        };

        control.Attach(area);
        area.Attach(tab);

        var window = owner.ExternalWindowFactory?.Invoke() ?? new Window();

        // 单文档窗口不摆标签条，文档名写在窗口标题上（见 IsSingleDocumentWindow）
        window.Title = tab.Title;
        window.Content = control;
        window.Width = Math.Max(480, Bounds.Width / 2);
        window.Height = Math.Max(360, Bounds.Height / 2);
        window.Position = new PixelPoint(
            screen.X - (int)holdDelta.X,
            screen.Y - (int)holdDelta.Y);

        // 关掉浮窗 = 把里面还开着的文档送回主编辑区（别把宿主的内容跟着窗口一起扔了）
        window.Closing += (_, _) =>
        {
            foreach (var id in area.Groups.SelectMany(g => g.Tabs).Select(t => t.Id).Distinct().ToList())
            {
                if (area.Detach(id) is { } back)
                {
                    owner.Area?.Attach(back, select: false);
                }
            }

            owner._floatingWindows.Remove(window);
        };

        owner._floatingWindows[window] = control;

        if (TopLevel.GetTopLevel(owner) is Window ownerWindow)
        {
            window.Show(ownerWindow);
        }
        else
        {
            window.Show();
        }
    }

    /// <summary>
    /// 把一个浮出去的编辑窗口整个停靠回来：里面的文档全部退回主编辑区，窗口关掉。
    /// </summary>
    /// <remarks>
    /// 单文档的浮窗不摆标签条（文档名在窗口标题上），也就没有可拖的抓手 ——
    /// 所以要有这么一个明确的入口。宿主把它接到窗体标题栏的按钮上。
    /// </remarks>
    /// <param name="window">要停靠回来的那个窗口；不是本编辑区浮出去的就什么都不做。</param>
    /// <returns>确实停回来了则为 <see langword="true"/>。</returns>
    public bool DockBack(Window window)
    {
        var owner = _floatingOwner ?? this;
        if (window is null || !owner._floatingWindows.ContainsKey(window))
        {
            return false;
        }

        // 关窗时那段收尾会把里面的文档退回主编辑区（见 FloatOut 里的 Closing）
        window.Close();
        return true;
    }

    /// <summary>浮窗里一页都不剩了就关掉它。</summary>
    private void CloseIfEmpty()
    {
        if (_floatingOwner is not { } owner || _area is not { } area)
        {
            return;
        }

        if (area.Groups.Any(g => g.Tabs.Count > 0))
        {
            return;
        }

        var window = owner._floatingWindows.FirstOrDefault(x => ReferenceEquals(x.Value, this)).Key;
        window?.Close();
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
    /// <summary>
    /// 跟随明暗时用哪套配色 —— 不给就用库内置的那套（照上游主题抄的）。
    /// </summary>
    /// <remarks>
    /// 内置那两套是**上游主题的移植**，不该为某个项目改掉。项目要微调
    /// （比如岛与自家窗口底板对比不够），在这里拿内置那套 <c>with</c> 出一套自己的：
    /// <code>PaletteResolver = (family, variant) =>
    ///     ForgePalette.ForTheme(family, variant) with { Background = ... };</code>
    /// </remarks>
    public Func<ForgeThemeFamily, ThemeVariant, ForgePalette>? PaletteResolver { get; set; }

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

        var wanted = PaletteResolver?.Invoke(ThemeFamily, ActualThemeVariant)
                     ?? ForgePalette.ForTheme(ThemeFamily, ActualThemeVariant);
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

        ForgeIslandDecor.Apply(_root, Palette, anchor: null, IslandMetrics, Palette.EditorBackground);

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

        SyncFloatingTitle();
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
            _groupActions.Remove(group);
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

        ForgeIslandDecor.Apply(_root, Palette, anchor: null, IslandMetrics, Palette.EditorBackground);

        foreach (var vm in _area.Groups)
        {
            if (_groupHosts.TryGetValue(vm, out var host))
            {
                ApplyEditorWindowDecor((Border)host);
            }

            if (_groupStripHosts.TryGetValue(vm, out var stripHost))
            {
                stripHost.BorderBrush = new SolidColorBrush(Palette.EditorTabsBorder);
            }

            // 「活动」= 这一组是当前组，且编辑区握着键盘焦点（详见 SyncStrip 同处的注释）
            var active = vm.IsCurrent && IsKeyboardFocusWithin;
            for (var i = 0; i < vm.Tabs.Count; i++)
            {
                if (_groupStrips.TryGetValue(vm, out var strip)
                    && strip.ContainerFromIndex(i) is ForgeEditorTab control)
                {
                    control.Palette = Palette;
                    control.IsGroupActive = active;
                }
            }
        }

        foreach (var splitter in _splitters)
        {
            splitter.Palette = Palette;
            splitter.InvalidateVisual();
        }

        _overlay.Palette = Palette;
    }

    private readonly List<ForgeEditorSplitter> _splitters = [];

    /// <summary>
    /// 拖动中的即时反馈：只改这一个 Grid 的行列比例。
    /// </summary>
    /// <remarks>
    /// 不走模型，所以不触发重建 —— 拖的时候把整棵树连同相机画面重画一遍，
    /// 既卡又会打断正在进行的手势。真值等松手时一次写回。
    /// </remarks>
    private static void Preview(Grid grid, bool isVertical, float proportion)
    {
        var clamped = Math.Clamp(proportion, EditorSplit.MinProportion, EditorSplit.MaxProportion);
        var definitions = isVertical ? (IReadOnlyList<DefinitionBase>)grid.RowDefinitions : grid.ColumnDefinitions;
        if (definitions.Count != 3)
        {
            return;
        }

        if (isVertical)
        {
            grid.RowDefinitions[0].Height = new GridLength(clamped, GridUnitType.Star);
            grid.RowDefinitions[2].Height = new GridLength(1 - clamped, GridUnitType.Star);
        }
        else
        {
            grid.ColumnDefinitions[0].Width = new GridLength(clamped, GridUnitType.Star);
            grid.ColumnDefinitions[2].Width = new GridLength(1 - clamped, GridUnitType.Star);
        }
    }

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
            var splitter = new ForgeEditorSplitter(split.IsVertical) { Palette = Palette };
            _splitters.Add(splitter);

            // 拖动中只改这个 Grid（图像不重建），松手才写回模型 ——
            // 与标签拖拽「行内仅预览、松手才提交」同一条规矩。
            splitter.Preview += (_, p) => Preview(grid, split.IsVertical, p);
            splitter.Committed += (_, p) => _area!.Area.SetProportion(split, p);

            if (split.IsVertical)
            {
                grid.RowDefinitions.Add(new RowDefinition(split.Proportion, GridUnitType.Star));
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                grid.RowDefinitions.Add(new RowDefinition(1 - split.Proportion, GridUnitType.Star));
                Grid.SetRow(first, 0);
                Grid.SetRow(splitter, 1);
                Grid.SetRow(second, 2);
            }
            else
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(split.Proportion, GridUnitType.Star));
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                grid.ColumnDefinitions.Add(new ColumnDefinition(1 - split.Proportion, GridUnitType.Star));
                Grid.SetColumn(first, 0);
                Grid.SetColumn(splitter, 1);
                Grid.SetColumn(second, 2);
            }

            grid.Children.Add(first);
            grid.Children.Add(second);

            grid.Children.Add(splitter);
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
            var strip = new ForgeEditorTabStrip();
            strip.ResolveIcon = ResolveIcon;
            _groupStrips[vm] = strip;
            const RoutingStrategies pointerRoutes = RoutingStrategies.Tunnel | RoutingStrategies.Direct;
            strip.AddHandler(PointerPressedEvent, OnStripPointerPressed, pointerRoutes, handledEventsToo: true);

            // 标签条底下那条 1px 分割线（EditorTabs.underTabsBorderColor）——
            // 它属于**标签条整体**，不属于某一个标签页，所以画在这里而不是 ForgeEditorTab 里。
            // 漏了它，标签条与正文之间没有分界，看着是糊在一起的。
            // 标签行 = [标签条 | 右端动作按钮]。按钮先加、靠右停靠，标签条吃剩下的宽度 ——
            // 反过来的话标签条会先吃满整行，按钮被挤出去。
            var actions = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal };
            DockPanel.SetDock(actions, Dock.Right);
            _groupActions[vm] = actions;

            var stripRow = new DockPanel();
            stripRow.Children.Add(actions);
            stripRow.Children.Add(strip);

            var stripHost = new Border { Child = stripRow, BorderThickness = new Thickness(0, 0, 0, 1) };
            _groupStripHosts[vm] = stripHost;
            DockPanel.SetDock(stripHost, Dock.Top);

            var body = new ContentControl();
            _groupBodies[vm] = body;

            var panel = new DockPanel();
            panel.Children.Add(stripHost);
            panel.Children.Add(body);

            // Dock.Avalonia 的 DocumentControl 把整个 PART_DockPanel 设成透明 DropArea。
            // Border 默认无背景时，正文空白处可能根本命不中视觉元素，
            // 独立 DragSession 就找不到应该合并进的组。
            host = new Border { Child = panel, Background = Brushes.Transparent };
            _groupHosts[vm] = host;
        }

        ApplyEditorWindowDecor((Border)host);
        SyncStrip(vm, _groupStrips[vm]);
        SyncTabActions(vm);

        // 浮出去的窗口里只剩一页时不摆标签条：文档名写在**窗口标题**上，
        // 与 Rider 一致 —— 一个窗口一份文档，还挂个标签条是重复的。
        // 再开第二页或者分了屏，标签条自然回来。
        _groupStripHosts[vm].IsVisible = !IsSingleDocumentWindow;

        _groupStripHosts[vm].BorderBrush = new SolidColorBrush(Palette.EditorTabsBorder);

        // 选中的那一页的内容挂到组的正文槽上
        _groupBodies[vm].Content = vm.Selected is { } selected ? GetContentHost(vm, selected) : null;

        // 就地摘：调用方（Grid 或 _root）马上就要收养它。理由见 Detach 的说明。
        Detach(host);
        return host;
    }

    /// <summary>分屏叶子是连续文档岛内部的 EditorWindow，不是独立岛。</summary>
    /// <remarks>
    /// 底色取 <see cref="ForgePalette.EditorBackground"/> 而不是 <c>Background</c>——
    /// 后者是**工具窗口**那档灰。取错的后果见那个属性的说明（选中标签成白补丁、
    /// 分屏之间的线看不见）。
    /// </remarks>
    private void ApplyEditorWindowDecor(Border host)
    {
        host.Background = new SolidColorBrush(Palette.EditorBackground);
        host.Margin = default;
        host.CornerRadius = default;
        host.BorderBrush = null;
        host.BorderThickness = default;
        host.ClipToBounds = true;
    }

    private void SyncStrip(EditorGroupViewModel vm, ForgeEditorTabStrip strip)
    {
        strip.Palette = Palette;
        strip.IsGroupActive = vm.IsCurrent && IsKeyboardFocusWithin;
        strip.ItemsSource ??= vm.Tabs;
        strip.SelectedItem = vm.Selected;

        // ⚠ 容器可能**这一刻还没实现出来**（刚给 ItemsSource 的那一轮尤其如此），
        //   那样这里的赋值全落空。所以再挂一次「容器实现时」的钩子补配 ——
        //   右键菜单就是这么丢的：标题之类后来还会被别的同步重刷一遍，
        //   菜单只在这儿配一次，落空就永远没有。
        if (_wiredStrips.Add(strip))
        {
            strip.ContainerPrepared += (_, e) =>
            {
                if (e.Container is ForgeEditorTab prepared && e.Index < vm.Tabs.Count)
                {
                    ApplyTabContainer(vm.Tabs[e.Index], prepared, strip);
                }
            };
        }

        for (var i = 0; i < vm.Tabs.Count; i++)
        {
            if (strip.ContainerFromIndex(i) is ForgeEditorTab control)
            {
                ApplyTabContainer(vm.Tabs[i], control, strip);
            }
        }
    }

    private readonly HashSet<ForgeEditorTabStrip> _wiredStrips = [];

    /// <summary>把一个标签页 VM 的样子落到它的容器上。</summary>
    private void ApplyTabContainer(EditorTabViewModel tab, ForgeEditorTab control, ForgeEditorTabStrip strip)
    {
        control.Palette = Palette;

        // 菜单**实例建一次**，内容在每次打开前刷 —— 每次同步都换个新实例的话，
        // 右键按下与松手之间实例就被换掉了，系统那套弹出流程落空，表现是右键没反应。
        if (control.ContextMenu is null)
        {
            var menu = new ContextMenu();
            var owner = control;
            menu.Opening += (_, _) =>
            {
                if (owner.DataContext is EditorTabViewModel current && _area is not null)
                {
                    ForgeMenuBuilder.FillEditorTab(
                        menu, _area, current.Id, current.IsPinned, TabMenuExtension, MoveToNewWindow);
                }
            };

            ForgeMenuBuilder.FillEditorTab(
                menu, _area!, tab.Id, tab.IsPinned, TabMenuExtension, MoveToNewWindow);
            control.ContextMenu = menu;
        }

        control.Title = tab.Title;
        control.Icon = ResolveIcon(control, tab.Icon);
        control.IsSelected = tab.IsSelected;
        control.IsGroupActive = strip.IsGroupActive;
        control.IsPinned = tab.IsPinned;
        control.CanBeClosed = tab.IsCloseable && !tab.IsPinned;
        control.DataContext = tab;
    }

    /// <summary>
    /// 标签条右端那排按钮：宿主的（如果有）+ 一个 ⋮。
    /// </summary>
    /// <remarks>
    /// 按钮实例按组缓存 —— 每次同步重造的话，鼠标正悬在上面时会闪，宿主给的控件也会被反复丢弃。
    /// </remarks>
    private void SyncTabActions(EditorGroupViewModel vm)
    {
        var actions = _groupActions[vm];
        var wanted = new List<Control>();

        if (ShowTabActions)
        {
            var anchor = vm.Tabs.FirstOrDefault()?.Id;
            if (TabActionsExtension?.Invoke(anchor) is { } extra)
            {
                wanted.AddRange(extra);
            }

            wanted.Add(TabActionsButton(vm));
        }

        for (var i = actions.Children.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(actions.Children[i]))
            {
                actions.Children.RemoveAt(i);
            }
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            if (!actions.Children.Contains(wanted[i]))
            {
                actions.Children.Insert(i, wanted[i]);
            }
        }

        foreach (var button in actions.Children.OfType<ForgeHeaderButton>())
        {
            button.Palette = Palette;
        }
    }

    private readonly Dictionary<EditorGroupViewModel, ForgeHeaderButton> _groupMoreButtons = [];

    private ForgeHeaderButton TabActionsButton(EditorGroupViewModel vm)
    {
        if (_groupMoreButtons.TryGetValue(vm, out var button))
        {
            return button;
        }

        button = new ForgeHeaderButton { Icon = ForgeIcons.MoreVertical };
        button.Click += (_, _) => ShowTabActionsMenu(vm);
        _groupMoreButtons[vm] = button;
        return button;
    }

    /// <summary>
    /// ⋮ 的菜单：这一组的标签页列表 + 关闭系列。
    /// </summary>
    /// <remarks>
    /// 标签页列表是这个按钮的**主要价值** —— 标签多到一屏看不全时，这里是唯一一处
    /// 能一眼看到全部并跳过去的地方。上游那个菜单里还有「最近的文件 / 转到文件 / 书签」等
    /// IDE 专属项，本仓没有对应物，需要就由宿主从 <see cref="TabActionsMenuExtension"/> 塞。
    /// <para>
    /// <b>建菜单与弹菜单分开</b>：宿主想从别处（自己的按钮、快捷键）弹同一份菜单时能直接拿；
    /// 也让内容测得到 —— 弹出层在 headless 下建不出来，混在一起就只能靠点开看。
    /// </para>
    /// </remarks>
    /// <param name="group">哪一组。</param>
    /// <returns>菜单。</returns>
    public ContextMenu BuildTabActionsMenu(EditorGroupViewModel group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var menu = new ContextMenu();

        foreach (var tab in group.Tabs)
        {
            var id = tab.Id;
            var item = new MenuItem { Header = tab.Title, Icon = ResolveMenuIcon(tab) };
            item.Click += (_, _) => _area?.Select(id);
            menu.Items.Add(item);
        }

        if (group.Tabs.Count > 0)
        {
            menu.Items.Add(new Separator());

            var anchorId = group.Tabs[0].Id;

            var closeOthers = new MenuItem { Header = "关闭其他标签页" };
            closeOthers.Click += (_, _) => _area?.Area.CloseOthers(group.Selected?.Id ?? anchorId);
            menu.Items.Add(closeOthers);

            var closeAll = new MenuItem { Header = "关闭所有标签页" };
            closeAll.Click += (_, _) => _area?.Area.CloseAll(anchorId);
            menu.Items.Add(closeAll);
        }

        TabActionsMenuExtension?.Invoke(menu, group.Tabs.FirstOrDefault()?.Id);
        return menu;
    }

    private void ShowTabActionsMenu(EditorGroupViewModel vm)
    {
        if (_area is null || !_groupMoreButtons.TryGetValue(vm, out var button))
        {
            return;
        }

        // 先挂到按钮上再开：ContextMenu 得先有宿主控件。每次重建一份，内容随标签页变。
        var menu = BuildTabActionsMenu(vm);
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.BottomEdgeAlignedRight;
        button.ContextMenu = menu;
        menu.Open(button);
    }

    /// <summary>菜单项左边那个图标：宿主给的是工厂就造一份，给的是实例就不用（同一实例不能两处挂）。</summary>
    private static object? ResolveMenuIcon(EditorTabViewModel tab)
        => tab.Icon is Func<Control> factory ? factory() : null;

    private void OnStripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ForgeEditorTabStrip strip
            || e.Source is not Visual source
            || source.FindAncestorOfType<ForgeEditorTab>(includeSelf: true) is not
                { DataContext: EditorTabViewModel tab } control)
        {
            return;
        }

        // 右键交给菜单：不选中、不起拖 —— 选中会触发同步、把容器和菜单重造一遍，
        // 松手时按下的那个元素已经不在了，菜单自然弹不出来。
        if (e.GetCurrentPoint(control).Properties.IsRightButtonPressed)
        {
            return;
        }

        var group = _groupStrips.First(x => ReferenceEquals(x.Value, strip)).Key;
        var point = e.GetPosition(control);
        if (control.CanBeClosed && control.TabIconHitRect().Contains(point))
        {
            _area!.Close(tab.Id);
            e.Handled = true;
            return;
        }

        if (e.ClickCount == 2)
        {
            _area!.DoubleClick(tab.Id);
            e.Handled = true;
            return;
        }

        _area!.Select(tab.Id);
        BeginPress(group, control, tab.Id, e);
    }

    private void OnDragPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dockDrag is not null)
        {
            UpdateDockDrag(_dockDrag, e);
            return;
        }

        if (_dragGroup is not null && _dragTab is not null)
        {
            DragTo(_dragGroup, _dragTab, e);
        }
    }

    private void OnDragPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dockDrag is not null)
        {
            _dockDrag.LastScreenPoint = ToScreen(e.GetPosition(this));
            EndDockDrag(commit: true);
            return;
        }

        if (_dragGroup is not null && _dragTab is not null)
        {
            EndInlineDrag(_dragGroup, _dragTab);
        }
    }

    private void OnDragPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_dockDrag is not null)
        {
            EndDockDrag(commit: false);
            return;
        }

        if (_dragGroup is not null && _dragTab is not null)
        {
            EndInlineDrag(_dragGroup, _dragTab);
        }
    }

    private ForgeEditorTab? _dragTab;
    private IPointer? _dragPointer;
    private EditorGroupViewModel? _dragGroup;
    private string? _dragId;
    private string? _reorderTargetId;
    private Point _pressPoint;
    private Vector _holdDelta;
    private Rect _dragOriginalRec;
    private bool _dragStarted;
    private EditorDockDragSession? _dockDrag;

    /// <summary>
    /// 离开标签行后的独立拖放会话。
    /// </summary>
    /// <remarks>
    /// 对应 IntelliJ <c>EditorTabbedContainerDragOutDelegate</c> 持有的 <c>DragSession</c>：
    /// 它只记模型身份和快照，不持有离开标签行后可能被回收的标签控件。
    /// </remarks>
    private sealed class EditorDockDragSession(
        IPointer pointer,
        EditorGroupViewModel source,
        string tabId,
        Vector holdDelta,
        Size thumbSize,
        global::Avalonia.Media.Imaging.RenderTargetBitmap? thumb)
    {
        public IPointer Pointer { get; } = pointer;
        public EditorGroupViewModel Source { get; } = source;
        public string TabId { get; } = tabId;
        public Vector HoldDelta { get; } = holdDelta;
        public Size ThumbSize { get; } = thumbSize;
        public global::Avalonia.Media.Imaging.RenderTargetBitmap? Thumb { get; } = thumb;
        public EditorGroupViewModel? Target { get; set; }
        public EditorDropSide Side { get; set; }

        /// <summary>最后一次指针位置（屏幕坐标）—— 松手落在别的窗口上时要靠它找落点。</summary>
        public PixelPoint LastScreenPoint { get; set; }

        /// <summary>拖出窗口之后跟着光标走的示意窗口。</summary>
        public ForgeDragGhost? Ghost { get; set; }

        /// <summary>光标此刻指着的**别的**编辑区（拖回主窗口时就是它）。</summary>
        public ForgeEditorArea? ExternalTarget { get; set; }
    }

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
            || !_groupStrips.TryGetValue(group, out var stripControl)
            || stripControl.ItemsPanelRoot is not HeaderTabsPanel strip)
        {
            return;
        }

        _dragGroup = group;
        _dragTab = control;
        _dragPointer = e.Pointer;
        _dragId = id;
        _reorderTargetId = null;
        _pressPoint = e.GetPosition(strip);
        _dragStarted = false;
        // Dock.Avalonia ItemDragHelper 在标签行内只保存按下状态，不主动捕获指针；
        // 离开标签条、正式进入 dock drag 时才把捕获交给整个 DockControl。
        // 这里保持同一边界，避免 TabStrip 的选中/容器生命周期从按下起就被编辑区劫持。
    }

    /// <summary>
    /// 拖动中：算出该和谁换位，只做视觉预览；松手时一次提交。
    /// </summary>
    /// <remarks>
    /// 对应 <c>DragHelper.java:164-246 · processDrag</c>。
    /// 几何全在 <see cref="ForgeEditorTabDragTarget"/> 里，这里只负责取数、落库、重排。
    /// </remarks>
    private void DragTo(EditorGroupViewModel group, ForgeEditorTab control, PointerEventArgs e)
    {
        if (!ReferenceEquals(_dragTab, control) || _dragId is null || _area is null
            || !_groupStrips.TryGetValue(group, out var stripControl)
            || stripControl.ItemsPanelRoot is not HeaderTabsPanel strip
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
        // IntelliJ 用纵向脱离判据；Dock.Avalonia ItemDragHelper 在指针离开
        // 整个 TabStrip 边界时交给跨窗格拖放。Forge 同时支持两者：
        // 在源条内水平移动仍是重排，但水平跨出源条后必须能合并相邻分屏。
        var pointInStrip = e.GetPosition(stripControl);
        var leftSourceStrip = !new Rect(stripControl.Bounds.Size).Contains(pointInStrip);
        if (leftSourceStrip
            || ForgeEditorTabDragTarget.IsDragOut(origin, stripControl.Bounds.Width, moved.X, moved.Y))
        {
            // MouseDragHelper.java:207-217：进入 detaching mode 前先完整结束
            // 行内拖动，再把模型身份交给独立 DragSession。
            var session = new EditorDockDragSession(
                _dragPointer!, group, _dragId, _holdDelta, origin.Size, ForgeDragOverlay.CaptureThumb(control));
            strip.DraggedChild = null;
            strip.InvalidateArrange();
            ClearInlineDrag(releasePointer: false);
            _dockDrag = session;
            session.Pointer.Capture(this);
            UpdateDockDrag(session, e);
            return;
        }

        var dragRec = ForgeEditorTabDragTarget.MoveToFit(
            new Rect(point.X - _holdDelta.X, point.Y - _holdDelta.Y, origin.Width, origin.Height),
            new Rect(strip.Bounds.Size));

        var order = Enumerable.Range(0, group.Tabs.Count)
            .Select(i => stripControl.ContainerFromIndex(i) as ForgeEditorTab)
            .ToList();
        var rects = order.Select(c => c is not null ? strip.SlotOf(c) ?? default : default).ToList();
        var target = ForgeEditorTabDragTarget.ResolveTarget(dragRec, _dragOriginalRec, rects, order.IndexOf(control));

        if (target >= 0)
        {
            // Dock.Avalonia ItemDragHelper 只在 Released() 里调用 Factory.MoveDockable；
            // 拖动中仅做 RenderTransform 预览。这里同样延迟模型变更，保证 TabStrip
            // 不会在手势尚未结束时回收/换父当前容器。
            _reorderTargetId = group.Tabs[target].Id;
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
    private void UpdateDockDrag(EditorDockDragSession session, PointerEventArgs e)
    {
        _overlay.IsVisible = true;
        var here = e.GetPosition(this);

        session.Target = null;
        session.Side = EditorDropSide.None;
        _overlay.AreaRect = null;

        // DockableEditorTabbedContainer.getTabsAt / DockControlState 都从当前指针下的
        // 真实视觉元素向上找停靠容器。不再遍历缓存矩形猜测目标。
        var candidate = FindGroupAt(here);
        if (candidate is not null
            && _groupHosts.TryGetValue(candidate, out var host)
            && BoundsIn(host) is { } bounds)
        {
            var stripHeight = _groupStripHosts.TryGetValue(candidate, out var stripHost)
                ? stripHost.Bounds.Height
                : 0;

            var area = ForgeEditorTabDragTarget.DropArea(bounds, stripHeight);
            // 把标签拖回另一组的标签行，本身就是最明确的“并入这一组”。
            // DropArea 为了避免本组行内重排与分屏判定打架而扣掉了标签行；
            // 但跨组时若仍把这段判成 None，用户把标签拖到目标标签条上松手就毫无反应。
            var overTargetStrip = stripHost is not null
                                  && BoundsIn(stripHost) is { } stripBounds
                                  && stripBounds.Contains(here);
            var side = overTargetStrip
                ? EditorDropSide.Center
                : EditorDropPolicy.Resolve(here.X - area.X, here.Y - area.Y, area.Width, area.Height);

            session.Target = candidate;
            session.Side = side;
            _overlay.AreaRect = ForgeEditorTabDragTarget.HighlightFor(area, side);
        }

        UpdateCrossWindowFeedback(session, here);

        _overlay.Palette = Palette;
        _overlay.ThumbImage = session.Thumb;
        _overlay.ThumbRect = new Rect(
            here.X - session.HoldDelta.X,
            here.Y - session.HoldDelta.Y,
            session.ThumbSize.Width,
            session.ThumbSize.Height);
    }

    private EditorGroupViewModel? FindGroupAt(Point point)
    {
        // 文档正文可能是 NativeControlHost、GPU/视频画面或主动跳过命中测试的控件。
        // 这种情况下 TopLevel.InputHitTest 会停在原生表面、甚至直接返回 null，
        // 再沿 VisualParent 找组就永远找不到目标。停靠目标属于编辑区布局树，
        // 应当像 DockControlState 的注册目标那样按已排布的组宿主边界判定，
        // 与正文采用哪种渲染技术无关。
        EditorGroupViewModel? result = null;
        var resultArea = double.PositiveInfinity;
        foreach (var pair in _groupHosts)
        {
            if (pair.Value.IsVisible
                && BoundsIn(pair.Value) is { } bounds
                && bounds.Contains(point))
            {
                // 正常的分屏叶子互不重叠；取最小者也让将来组宿主出现嵌套时
                // 命中最具体的目标，而不是依赖字典枚举顺序。
                var area = bounds.Width * bounds.Height;
                if (area < resultArea)
                {
                    result = pair.Key;
                    resultArea = area;
                }
            }
        }

        return result;
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

    private void EndInlineDrag(EditorGroupViewModel group, ForgeEditorTab control)
    {
        if (!ReferenceEquals(_dragTab, control))
        {
            return;
        }

        if (_groupStrips.TryGetValue(group, out var stripControl)
            && stripControl.ItemsPanelRoot is HeaderTabsPanel strip)
        {
            strip.DraggedChild = null;
            strip.InvalidateArrange();
        }

        if (_dragId is { } sourceId && _reorderTargetId is { } targetId)
        {
            _area!.ReorderTab(sourceId, targetId);
        }

        ClearInlineDrag(releasePointer: true);
    }

    private void ClearInlineDrag(bool releasePointer)
    {
        var pointer = _dragPointer;
        _dragTab = null;
        _dragPointer = null;
        _dragGroup = null;
        _dragId = null;
        _reorderTargetId = null;
        _dragStarted = false;

        // 比的是「捕获在不在我手上」这个身份，不是相等 —— 用 ReferenceEquals 说明白，
        // 免得读的人以为 IInputElement 上有值语义的相等。
        if (releasePointer && pointer is not null && ReferenceEquals(pointer.Captured, this))
        {
            pointer.Capture(null);
        }
    }

    private void EndDockDrag(bool commit)
    {
        var session = _dockDrag;
        if (session is null)
        {
            return;
        }

        // 先从会话中拍下命令，再完整销毁手势状态。模型变更不会再
        // 触及源 TabStripItem；这个边界对应 IntelliJ DragSession.process/release。
        //
        // ⚠ 用**可空**元组而不是 `default` 当哨兵：`default` 的四个字段全是 null，
        //   而它们的声明类型是非空 —— 判据 `drop != default` 还得靠元组相等去逐字段比引用。
        //   可空元组让「有没有这条命令」由类型系统答，取出来的字段天然非空。
        (EditorGroup Source, EditorGroup Target, string TabId, EditorDropSide Side)? drop =
            commit && session.Target is not null && session.Side != EditorDropSide.None
                ? (session.Source.Group, session.Target.Group, session.TabId, session.Side)
                : null;

        session.Ghost?.Close();
        session.Ghost = null;
        session.ExternalTarget?.ClearDropHint();
        session.ExternalTarget = null;

        _dockDrag = null;
        _overlay.Clear();
        // 真窗口上只清 StyledProperty 后，同一输入派发里紧接着的
        // 分屏树更换可以让合成器保留上一帧拖影。会话结束就直接
        // 把整层移出渲染，下次会话再显示；这也等价于 Dock.Avalonia
        // DragPreviewHelper.Hide() 的生命周期。
        _overlay.IsVisible = false;
        _overlay.InvalidateVisual();
        if (ReferenceEquals(session.Pointer.Captured, this))
        {
            session.Pointer.Capture(null);
        }

        if (drop is { } move)
        {
            _area!.DropTab(move.TabId, move.Source, move.Target, move.Side);
            return;
        }

        if (commit)
        {
            DropOutside(session);
        }
    }

    /// <summary>
    /// 松手时不在本编辑区的任何一组上：搬去光标底下那个编辑区，没有就浮出成独立窗口。
    /// </summary>
    /// <remarks>
    /// 对位上游 <c>DockManagerImpl</c> 那条路：拖出去落在别的容器上就交给它，
    /// 落在空处就新开一个窗口装着（<c>DockableEditorTabbedContainer</c>）。
    /// <para>
    /// 标签的图标与内容是**宿主的实例**（相机实时画面就在里头），所以走
    /// <c>EditorAreaViewModel.Detach/Attach</c> 搬同一个对象，不是关掉再开一个。
    /// </para>
    /// </remarks>
    private void DropOutside(EditorDockDragSession session)
    {
        if (_area is not { } area)
        {
            return;
        }

        var screen = session.LastScreenPoint;

        // 落在另一个编辑区上（比如从浮窗拖回主窗口）。
        // ⚠ 只在**同一家人**里找：主编辑区和它浮出去的那些窗口。
        //   不搞全局登记表 —— 那样会把别的窗口（甚至别的实例）也算成落点，
        //   而且屏幕坐标重叠时根本分不出该给谁。
        var target = Family().FirstOrDefault(x => !ReferenceEquals(x, this) && x.HitTestScreen(screen));

        // 浮窗里就这一页，再拖出去只是把窗口换个位置 —— 没意义
        if (target is null && _floatingOwner is not null && area.Groups.Sum(g => g.Tabs.Count) <= 1)
        {
            return;
        }

        if (area.Detach(session.TabId) is not { } tab)
        {
            return;
        }

        if (target is not null)
        {
            target.Area!.Attach(tab);
        }
        else
        {
            FloatOut(tab, screen, session.HoldDelta);
        }

        // 源头空了：浮窗就没有存在的必要了（主编辑区空着是正常状态，不动它）
        CloseIfEmpty();
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

}
