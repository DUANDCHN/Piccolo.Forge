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
public class ForgeEditorControl : TemplatedControl
{
    // 正文宿主的键是 (组, 标签页 id) 而不是单纯 id：分屏后同一个 id
    // 可同时存在于两个组（EditorWindow.kt:570-571）。
    private readonly Dictionary<(EditorGroupViewModel Group, string Id), ContentControl> _contents = [];
    private readonly Dictionary<EditorGroupViewModel, Control> _groupHosts = [];
    private readonly Dictionary<EditorGroupViewModel, ForgeEditorTabStrip> _groupStrips = [];
    private readonly Dictionary<EditorGroupViewModel, Border> _groupStripHosts = [];

    /// <summary>标签条底下那条 1px 线（**压在内容上、不占高度**，见建它的地方）。</summary>
    private readonly Dictionary<EditorGroupViewModel, Border> _groupStripLines = [];
    private readonly Dictionary<EditorGroupViewModel, ContentControl> _groupBodies = [];
    private readonly Dictionary<EditorGroupViewModel, StackPanel> _groupActions = [];
    // IntelliJ/Rider 的整个文档区是一座连续的 editor island；分屏叶子只是
    // 这座岛内部的 EditorWindow，不能各自再套 Margin/圆角，否则拆分后会
    // 变成两座互不相干的岛，也会在中间制造一条错误的“海”。
    private readonly Border _root = new();

    /// <summary>
    /// 岛的那圈边 —— 画在**内容之上**的一层，只有描边没有底。
    /// </summary>
    /// <remarks>
    /// 岛自己（<see cref="_root"/>）的描边画在子控件之下，而分屏叶子各自铺着不透明的底，
    /// 会把四角那几段弧盖掉 —— 就是"面板的圆角好了、文档区的还断着"。
    /// 面板那边靠标题条自己磨圆解决（只有它压在顶上）；文档区不行：分屏之后
    /// 四个角可能分属不同叶子，逐个磨圆既繁琐又会随分屏结构变。所以把边补画在最上层。
    /// <para>不吃鼠标：它铺满整个编辑区，参与命中测试的话点哪儿都是它。</para>
    /// </remarks>
    private readonly Border _islandRing = new() { IsHitTestVisible = false };


    private EditorAreaViewModel? _area;

    /// <summary>
    /// <see cref="Editor"/> 的属性定义。
    /// </summary>
    /// <remarks>
    /// 做成可绑定属性而不是只读 + <c>Attach()</c>，理由同
    /// <see cref="ForgeControl.ShellProperty"/>：整块外壳要能在 XAML 里声明。
    /// <para>
    /// ⚠ 叫 <c>Editor</c> 不叫 <c>Area</c>：这个控件本来就叫 <c>ForgeEditorControl</c>，
    /// 而它接的 VM 自己还有个 <c>Area</c>（那是模型 <see cref="EditorArea"/>）——
    /// 叫 Area 的话宿主看到的是 <c>control.Area.Area.Root</c>，一条链上三个 Area。
    /// </para>
    /// </remarks>
    public static readonly StyledProperty<EditorAreaViewModel?> EditorProperty =
        AvaloniaProperty.Register<ForgeEditorControl, EditorAreaViewModel?>(nameof(Editor));

    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeEditorControl, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    /// <summary><see cref="IslandBorderBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> IslandBorderBrushProperty =
        AvaloniaProperty.Register<ForgeEditorControl, IBrush?>(nameof(IslandBorderBrush));

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

    /// <summary><see cref="EditorBackgroundBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> EditorBackgroundBrushProperty =
        AvaloniaProperty.Register<ForgeEditorControl, IBrush?>(nameof(EditorBackgroundBrush));

    /// <summary>
    /// 编辑区底色的画刷，由 <c>ControlTheme</c> 从资源键给（同 <see cref="IslandBorderBrush"/> 那条路）。
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 兜底回 <see cref="ForgePalette.EditorBackground"/>（上游取值）。
    /// <para>
    /// 单独开这个口子的理由：上游 expUI 让编辑区比工具窗口**深一档**，那是为满屏文字的 IDE 定的；
    /// 宿主的编辑区若常是一整块空画布（图像视口这类），同一档差会读成一个黑洞。
    /// 改这个是**产品取舍**，不该逼宿主去改库里的配色 —— 重定义
    /// <c>Forge.ExpUi.EditorBackground</c> / <c>Forge.Islands.EditorBackground</c> 即可，明暗各盖各的。
    /// </para>
    /// </remarks>
    public IBrush? EditorBackgroundBrush
    {
        get => GetValue(EditorBackgroundBrushProperty);
        set => SetValue(EditorBackgroundBrushProperty, value);
    }

    static ForgeEditorControl()
    {
        // ⚠ 这些都是**纯外观**，不要走 Sync（那会重建整棵树，见 RefreshAppearance 的说明）
        PaletteProperty.Changed.AddClassHandler<ForgeEditorControl>((x, _) => x.RefreshAppearance());

        // 族写进伪类，ControlTheme 据此给岛描边选哪一套键（REQ-067）
        PaletteProperty.Changed.AddClassHandler<ForgeEditorControl>(
            (x, _) => x.PseudoClasses.Set(":islands", x.Palette.Island is not null));
        IslandBorderBrushProperty.Changed.AddClassHandler<ForgeEditorControl>((x, _) => x.RefreshAppearance());
        EditorBackgroundBrushProperty.Changed.AddClassHandler<ForgeEditorControl>((x, _) => x.RefreshAppearance());
        ShowTabActionsProperty.Changed.AddClassHandler<ForgeEditorControl>((x, _) => x.Sync());
        EditorProperty.Changed.AddClassHandler<ForgeEditorControl, EditorAreaViewModel?>(
            (x, e) => x.OnEditorChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));

        // 外观换了一份：重跑取色（可能换了族或取色回调），再把纯外观的部分刷一遍。
        // ⚠ 别把 _variantApplied 归零，理由见 ForgeControl 同处的注释
        // （会把「宿主自己管配色」那道探测重新武装，换族与跟随明暗一起坏掉）。
        // ⚠ 也别走 Sync —— 外观是纯外观，Sync 会重建整棵树（见 RefreshAppearance 的说明）。
        ForgeChrome.AppearanceProperty.Changed.AddClassHandler<ForgeEditorControl>((x, _) =>
        {
            x.ApplyThemeVariant();
            x.RefreshAppearance();
        });
    }

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
    /// 与 <c>ForgeControl.HeaderButtonsExtension</c> 同形，只是编辑区一组一条标签行，
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
        AvaloniaProperty.Register<ForgeEditorControl, bool>(nameof(ShowTabActions), defaultValue: true);

    /// <summary>
    /// 外观：配色族、取色、岛几何、标签行的字重和行高。
    /// </summary>
    /// <remarks>
    /// 摆在外框里用时**不必设** —— 外观是继承式附加属性，从外框自动流下来
    /// （见 <see cref="ForgeChrome.AppearanceProperty"/>），这正是「四边是岛、中间还是方角」
    /// 那类事故的根治。独立使用（浮出去的编辑窗口、没有外框的宿主）才需要自己给一份。
    /// </remarks>
    public ForgeAppearance Appearance
    {
        get => ForgeChrome.GetAppearance(this);
        set => ForgeChrome.SetAppearance(this, value);
    }

    /// <summary>建一个编辑区控件。</summary>
    public ForgeEditorControl()
    {
        VisualChildren.Add(_root);
        LogicalChildren.Add(_root);

        // 岛的那圈边：加在 _root **之后**，于是画在内容之上（见 _islandRing）
        VisualChildren.Add(_islandRing);
        LogicalChildren.Add(_islandRing);

        // 覆盖层加在 _root **之后** —— 父控件的 Render 在子控件之下，
        // 落点高亮必须盖在分屏与正文之上才看得见（同 ForgeControl）。
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

        // ── 点进编辑区 = 编辑区拿到焦点（上游：选中标签后焦点落在编辑器上）──
        //
        // 标签页的五种状态里有三种取决于「这一组是不是活动的」，而"活动"= 本组是当前组
        // **且编辑区握着键盘焦点**（见 SyncStrip）。宿主的文档内容不一定可获焦
        // ——图像视口、只读画布这类都不是——于是没有这一段的话，那三种状态在这些宿主里
        // **永远到不了**：选中标签的下划线一直是失焦那档灰，看着像"蓝条没生效"。
        //
        // 焦点落在**控件自己**身上（Focusable + IsTabStop=false）：库不知道宿主的内容里
        // 哪个才该收焦点，而"编辑区握着焦点"这个状态只需要焦点在这棵子树里。
        // ⚠ 走 **Bubble** 且只在没人接的时候才收：内容里若有输入框，它在冒到这里之前
        //   已经拿到焦点（IsKeyboardFocusWithin 为真），这一段就不插手 —— 用 Tunnel 会抢在它前面。
        Focusable = true;
        IsTabStop = false;
        AddHandler(PointerPressedEvent, OnEditorPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>点在编辑区里而焦点还在别处（工具面板等）→ 把焦点收进来。</summary>
    private void OnEditorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsKeyboardFocusWithin)
        {
            Focus(NavigationMethod.Pointer);
        }
    }

    private readonly ForgeDragOverlay _overlay = new();

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

    private readonly Dictionary<Window, ForgeEditorControl> _floatingWindows = [];
    private ForgeEditorControl? _floatingOwner;

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

        // ★ 幽灵窗**全程在**（起拖那一刻就有）：拖影就是它，"拖动中的小窗口"。
        //   曾经窗内画覆盖层缩略图、出窗才换幽灵窗 —— 两副长相，切换那一下还跳一帧；
        //   幽灵窗是置顶真窗口，画在窗内窗外都不会被裁，干脆全程用它。
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

        // 同 ToScreen 的教训：减 Bounds.Position 只补一层，要按可视树换算本控件的原点
        var origin = this.TranslatePoint(default, window) ?? default;
        var here = window.PointToClient(screen) - origin;
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
    private IEnumerable<ForgeEditorControl> Family()
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

    /// <remarks>
    /// ⚠ 必须走 <c>Visual.PointToScreen</c>（完整可视树变换）。曾经写成
    /// <c>window.PointToScreen(point + Bounds.Position)</c> —— 那只补了**一层**父级偏移，
    /// 而编辑区嵌在外壳好几层容器里，幽灵窗与光标就整体错开一截（实机："直接错位了"）。
    /// </remarks>
    private PixelPoint ToScreen(Point point)
        => TopLevel.GetTopLevel(this) is not null ? this.PointToScreen(point) : default;

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
        // ⚠ 浮出去的编辑区在**另一棵树**里，继承过不来，得显式给一份 ——
        //   给的是整个外观对象，所以不会像早先手抄字段那样抄漏
        //   （抄漏过取色回调 / 字重 / 行高，表现是浮窗里描边没了、字重和行高变回默认）。
        var control = new ForgeEditorControl
        {
            Appearance = Appearance,
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
                    owner.Editor?.Attach(back, select: false);
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
    private void ApplyThemeVariant()
    {
        if (!Appearance.FollowThemeVariant)
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

        var wanted = Appearance.PaletteResolver?.Invoke(Appearance.Family, ActualThemeVariant)
                     ?? ForgePalette.ForTheme(Appearance.Family, ActualThemeVariant);
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

    /// <summary>
    /// 接上的编辑区 VM；赋值即接上（同 <see cref="Attach"/>），可在 XAML 里绑定。
    /// </summary>
    public EditorAreaViewModel? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    /// <summary>接上一个编辑区 VM。</summary>
    /// <param name="area">编辑区 VM。</param>
    public void Attach(EditorAreaViewModel area)
    {
        ArgumentNullException.ThrowIfNull(area);
        Editor = area;
    }

    /// <summary>换了一个编辑区 VM：退订旧的、订上新的、按新状态同步一遍。</summary>
    /// <remarks>
    /// ⚠ 退订那一句是**必须的**：这里原先每次 Attach 都挂一个新的 lambda、从不摘旧的，
    /// 接第二次就会同一个变更同步两遍。以前只 Attach 一次所以没露头，
    /// 改成可绑定属性之后绑定随时可能重发，就藏不住了。
    /// </remarks>
    /// <param name="oldArea">上一个（可能没有）。</param>
    /// <param name="area">新的（<see langword="null"/> = 摘下来）。</param>
    private void OnEditorChanged(EditorAreaViewModel? oldArea, EditorAreaViewModel? area)
    {
        if (oldArea is not null)
        {
            oldArea.Area.Changed -= OnAreaModelChanged;
        }

        _area = area;
        if (area is not null)
        {
            area.Area.Changed += OnAreaModelChanged;
        }

        Sync();
    }

    private void OnAreaModelChanged(object? sender, EventArgs e) => Sync();

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

        // 与 _root **同一块矩形** —— 取它排完之后的实际 Bounds，而不是自己拿外边距算：
        // 排布会把尺寸对齐到整像素，自己算会差半个到一个像素，两条边就错开成"双线"。
        // 正好盖在根自己那条边上，外半边混的才是窗口底板而不是编辑区底色。
        _islandRing.Arrange(_root.Bounds);

        _overlay.Arrange(new Rect(finalSize));
        return finalSize;
    }

    private void Sync()
    {
        if (_area is null)
        {
            return;
        }

        ForgeIslandDecor.Apply(
            _root, Palette, anchor: null, Appearance.IslandMetrics, Palette.EditorBackground, IslandBorderBrush,
            EditorBackgroundBrush);

        // 那圈边与根同参；根自己那条画在内容之下、会被分屏叶子的不透明底盖住四角，
        // 所以这一层补在最上面（见 _islandRing）。
        _islandRing.CornerRadius = _root.CornerRadius;
        _islandRing.BorderThickness = _root.BorderThickness;
        _islandRing.BorderBrush = _root.BorderBrush;

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

                    // 内容 + 最上层补一圈岛的边（见 _islandRing）
                    var content = BuildNode(_area.Area.Root);

                    _root.Child = content;
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
        FocusOnNewlyOpenedTab();
    }

    private bool _rebuilding;
    private bool _rebuildAgain;

    /// <summary>见过的标签页 id —— 用来认出"这一轮新开了一张"。</summary>
    private readonly HashSet<string> _knownTabs = [];

    /// <summary>
    /// 新开一张文档 → 焦点进编辑区（上游 <c>openFile</c> 的 <c>focusEditor</c> 默认就是真）。
    /// </summary>
    /// <remarks>
    /// 只认**新出现的 id**，不认选中项变化：关掉一张会让选中落到左邻居，那不是"打开"，
    /// 焦点本来就在编辑区里，再抢一次没有意义。
    /// <para>
    /// ⚠ 焦点必须**等这一轮重建完**再收：Sync 会把组宿主换父容器，而换父容器会带走焦点 ——
    /// 在树还没搭完时收，收到的焦点转头就被自己弄丢。所以 Post 到下一轮。
    /// </para>
    /// <para>
    /// 已经在编辑区里就不动（比如从一张文档里打开另一张）—— 那时焦点可能正落在
    /// 内容里的某个输入控件上，抢过来等于把用户从他正在填的框里踢出去。
    /// </para>
    /// </remarks>
    private void FocusOnNewlyOpenedTab()
    {
        if (_area is null)
        {
            return;
        }

        var ids = _area.Groups.SelectMany(g => g.Tabs).Select(t => t.Id).ToList();
        var opened = ids.Exists(id => !_knownTabs.Contains(id));

        _knownTabs.Clear();
        foreach (var id in ids)
        {
            _knownTabs.Add(id);
        }

        // 还没进可视树时收不到焦点（也不该收）——那一轮只登记 id，等真正显示出来的那次
        if (!opened || IsKeyboardFocusWithin || !IsLoaded)
        {
            return;
        }

        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!IsKeyboardFocusWithin && IsLoaded)
            {
                Focus();
            }
        });
    }

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
            _groupStripLines.Remove(group);
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

        ForgeIslandDecor.Apply(
            _root, Palette, anchor: null, Appearance.IslandMetrics, Palette.EditorBackground, IslandBorderBrush,
            EditorBackgroundBrush);

        // 那圈边与根同参；根自己那条画在内容之下、会被分屏叶子的不透明底盖住四角，
        // 所以这一层补在最上面（见 _islandRing）。
        _islandRing.CornerRadius = _root.CornerRadius;
        _islandRing.BorderThickness = _root.BorderThickness;
        _islandRing.BorderBrush = _root.BorderBrush;

        foreach (var vm in _area.Groups)
        {
            if (_groupHosts.TryGetValue(vm, out var host))
            {
                ApplyEditorWindowDecor((Border)host);
            }

            if (_groupStripLines.TryGetValue(vm, out var stripLine))
            {
                stripLine.Background = new SolidColorBrush(Palette.EditorTabsBorder);
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

                    // 字重与行高也是外观：换一份外观（比如宿主改了行高）得当场生效，
                    // 而不是等到下一次 Sync 重建标签容器 —— 那可能永远不来。
                    control.TitleFontWeight = Appearance.TabTitleFontWeight;
                    control.RowHeightOverride = Appearance.TabRowHeight;
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

            // ⚠ 这条线**压在内容上、不占高度**：占高度的话标签行就是「行高 + 1」，
            //   比面板标题条高出 1px —— 宿主给的 HeaderHeight / TabRowHeight 是同一个数，
            //   意思就是这两行一样高（同 ForgeToolWindowToolbar.BorderOverlaysContent 那条边）。
            //   放在标签**之下**（先加 = z 序在下）：选中标签自己的下划线画在它上面、正好盖住那一段，
            //   与上游一致（蓝条压过标签栏底线，不是被它切掉 1px）。
            var stripLine = new Border { Height = 1, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Bottom };
            _groupStripLines[vm] = stripLine;

            var stripLayer = new Panel();
            stripLayer.Children.Add(stripLine);
            stripLayer.Children.Add(stripRow);

            var stripHost = new Border { Child = stripLayer };
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

        // 标签行什么时候不摆：
        //   · 一页都没开 —— 空的标签行只剩底下那条分隔线，凭空在编辑区顶上画一道；
        //   · 浮出去的窗口里只剩一页 —— 文档名写在**窗口标题**上，与 Rider 一致，
        //     一个窗口一份文档还挂个标签条是重复的。
        // 开了标签、或者分了屏，标签行自然回来。
        _groupStripHosts[vm].IsVisible = vm.Tabs.Count > 0 && !IsSingleDocumentWindow;

        _groupStripLines[vm].Background = new SolidColorBrush(Palette.EditorTabsBorder);

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
    /// <para>
    /// 主题给了 <see cref="EditorBackgroundBrush"/> 就用它 —— 分屏叶子与整块编辑区必须同色，
    /// 少改一处的症状是"分屏之后半边亮半边暗"。
    /// </para>
    /// </remarks>
    private void ApplyEditorWindowDecor(Border host)
    {
        host.Background = EditorBackgroundBrush ?? new SolidColorBrush(Palette.EditorBackground);
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
        control.TitleFontWeight = Appearance.TabTitleFontWeight;
        control.RowHeightOverride = Appearance.TabRowHeight;

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

    /// <summary>
    /// 判"拖出标签行"时上下各留的余量 <b>12</b>。
    /// </summary>
    /// <remarks>
    /// 不留余量的话，指针贴着行的上下边缘时会在"重排"与"分屏"之间来回横跳；
    /// 留得太多又会让"往下拖去分屏"变得费劲。12 ≈ 标签行高 31 的三分之一。
    /// </remarks>
    private const double DragOutMargin = 12;

    private ForgeEditorTab? _dragTab;
    private IPointer? _dragPointer;
    private EditorGroupViewModel? _dragGroup;
    private string? _dragId;
    private Point _pressPoint;
    private Vector _holdDelta;
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
        public ForgeEditorControl? ExternalTarget { get; set; }

        /// <summary>源组的标签条控件（区域一的缝判定与投影重绘要用）。</summary>
        public ForgeEditorTabStrip? SourceStripControl { get; init; }

        /// <summary>源组标签条的排布面板（摘起 / 缝都设在它身上）。</summary>
        public HeaderTabsPanel? SourcePanel { get; init; }

        /// <summary>被拖那一页的容器控件。</summary>
        public ForgeEditorTab? SourceTabControl { get; init; }

        /// <summary>起拖那一刻它的格子 —— <c>ResolveTarget</c> 的回滞基准。</summary>
        public Rect OriginalRec { get; init; }

        /// <summary>光标此刻在不在源标签行带里（区域一：松手 = 重排，绝不落到浮窗那条路）。</summary>
        public bool OverSourceRow { get; set; }

        /// <summary>区域一里算出的换位目标；<see langword="null"/> = 原地放下。</summary>
        public string? ReorderTargetId { get; set; }
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
        _pressPoint = e.GetPosition(strip);
        // Dock.Avalonia ItemDragHelper 在标签行内只保存按下状态，不主动捕获指针；
        // 离开标签条、正式进入 dock drag 时才把捕获交给整个 DockControl。
        // 这里保持同一边界，避免 TabStrip 的选中/容器生命周期从按下起就被编辑区劫持。
    }

    /// <summary>
    /// 拖动中（还没起拖时）：过了死区就把手势升级成**统一拖动会话**。
    /// </summary>
    /// <remarks>
    /// ★ <b>被拖的视觉全程是拖影（窗内画在覆盖层、出窗换幽灵窗），跟着光标走，永不吸附。</b>★
    /// 吸附的只有提示，三个区域按位置来回切（都在 <see cref="UpdateDockDrag"/> 里）：
    /// <list type="bullet">
    /// <item>源标签行带 → 缝上的投影，松手 = 重排；</item>
    /// <item>行下的内容区 → 分屏 / 并组的蓝框；</item>
    /// <item>行上、窗外 → 浮窗预示（幽灵窗本身就是"松手会得到多大一个窗"）。</item>
    /// </list>
    /// <para>
    /// 曾经是"行内用真控件摆位、出行才换拖影"两套：真控件出不了标签行（会被祖先裁掉半截），
    /// 于是要么夹在行里被读成"吸附"、要么放出去被裁 —— 结构性死路，整条删掉。
    /// </para>
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
        if (Math.Abs(moved.X) < ForgeEditorTabDragTarget.DragStartDeadZone
            && Math.Abs(moved.Y) < ForgeEditorTabDragTarget.DragStartDeadZone)
        {
            return;
        }

        // 抓握偏移：按在标签页内部什么位置，拖影就保持那个相对位置（DragHelper.java:178 myHoldDelta）
        _holdDelta = _pressPoint - origin.Position;

        var session = new EditorDockDragSession(
            _dragPointer!, group, _dragId, _holdDelta, origin.Size, ForgeDragOverlay.CaptureThumb(control))
        {
            SourceStripControl = stripControl,
            SourcePanel = strip,
            SourceTabControl = control,
            OriginalRec = origin,
        };

        // 把源标签"摘起来"：排布跳过它（缝由 DropIndex 表达）、自己被摆成空，视觉交给拖影
        strip.DraggedChild = control;
        ClearInlineDrag(releasePointer: false);
        _dockDrag = session;
        session.Pointer.Capture(this);
        UpdateDockDrag(session, e);
    }

    /// <summary>
    /// 区域一：光标在源标签行带里 —— 把"松手会落到哪条缝"的投影摆上去。
    /// </summary>
    /// <remarks>
    /// 行带 = 整条标签行（含右端按钮那一段），上下各留 <see cref="DragOutMargin"/> 的余量防抖。
    /// 换位目标用与行内拖拽同一套 <see cref="ForgeEditorTabDragTarget.ResolveTarget"/>（带回滞）。
    /// <para>
    /// ⚠ <b>缝序号恒等于 target，别"往右减一"。</b>松手走的 ReorderTab 是
    /// "先取目标索引 → 移除源 → 插回旧索引"：拿 [a,b,c] 把 a 拖到 c(2) 上 ——
    /// 移除 a 得 [b,c]，插回 2 → [b,c,a]，落点的"除源之外"序号 = 2 = target；往左同理。
    /// </para>
    /// </remarks>
    /// <param name="session">当前会话。</param>
    /// <param name="e">指针事件。</param>
    /// <returns>光标确实在行带里则为 <see langword="true"/>。</returns>
    private bool UpdateSourceRowReorder(EditorDockDragSession session, PointerEventArgs e)
    {
        if (session.SourceStripControl is not { } stripControl
            || session.SourcePanel is not { } strip
            || session.SourceTabControl is not { } control
            || !_groupStripHosts.TryGetValue(session.Source, out var rowHost)
            || !rowHost.IsVisible)
        {
            return false;
        }

        var p = e.GetPosition(rowHost);
        var inRow = p.X >= 0 && p.X <= rowHost.Bounds.Width
                    && p.Y >= -DragOutMargin && p.Y <= rowHost.Bounds.Height + DragOutMargin;
        if (!inRow)
        {
            return false;
        }

        // 落点判定用**夹进行内**的矩形（ResolveTarget 要的是行内坐标）——
        // 夹的只是判定用的数字，拖影本身不受它约束。
        var point = e.GetPosition(strip);
        var dragRec = ForgeEditorTabDragTarget.MoveToFit(
            new Rect(point.X - session.HoldDelta.X, point.Y - session.HoldDelta.Y,
                session.OriginalRec.Width, session.OriginalRec.Height),
            new Rect(strip.Bounds.Size));

        var order = Enumerable.Range(0, session.Source.Tabs.Count)
            .Select(i => stripControl.ContainerFromIndex(i) as ForgeEditorTab)
            .ToList();
        var rects = order.Select(c => c is not null ? strip.SlotOf(c) ?? default : default).ToList();
        var sourceIndex = order.IndexOf(control);
        var target = ForgeEditorTabDragTarget.ResolveTarget(dragRec, session.OriginalRec, rects, sourceIndex);

        session.OverSourceRow = true;
        session.ReorderTargetId = target >= 0 ? session.Source.Tabs[target].Id : null;

        int? gap = target >= 0 ? target : (sourceIndex >= 0 ? sourceIndex : (int?)null);
        strip.DropIndex = gap;
        strip.InvalidateArrange();
        stripControl.InvalidateVisual();   // 投影由 TabStrip 画（Panel.Render 是密封的）
        return true;
    }

    /// <summary>离开源标签行带：收掉缝上的投影与重排意图。</summary>
    private void ClearSourceRowHint(EditorDockDragSession session)
    {
        session.OverSourceRow = false;
        session.ReorderTargetId = null;

        if (session.SourcePanel is { } strip && strip.DropIndex is not null)
        {
            strip.DropIndex = null;
            strip.InvalidateArrange();
            session.SourceStripControl?.InvalidateVisual();
        }
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

        // ── 区域一：源标签行带 —— 缝上的投影（松手 = 重排）；其余区域走停靠判定 ──
        if (!UpdateSourceRowReorder(session, e))
        {
            ClearSourceRowHint(session);

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

        }

        UpdateCrossWindowFeedback(session, here);

        // 拖影由幽灵窗承担（见 UpdateCrossWindowFeedback）；覆盖层只画提示（蓝框等）
        _overlay.Palette = Palette;
        _overlay.ThumbImage = null;
        _overlay.ThumbRect = default;
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
        _ = group;
        if (!ReferenceEquals(_dragTab, control))
        {
            return;
        }

        // 到这儿 = 没过死区就松手 —— 过了死区手势早升级成统一会话（走 EndDockDrag），
        // 这里什么都没动过，清状态即可
        ClearInlineDrag(releasePointer: true);
    }

    private void ClearInlineDrag(bool releasePointer)
    {
        var pointer = _dragPointer;
        _dragTab = null;
        _dragPointer = null;
        _dragGroup = null;
        _dragId = null;

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

        // 源行的"摘起"状态在**任何**结束路径都要还原 —— 别把标签留在隐身态
        if (session.SourcePanel is { } sourcePanel)
        {
            sourcePanel.DraggedChild = null;
            sourcePanel.DropIndex = null;
            sourcePanel.InvalidateArrange();
            session.SourceStripControl?.InvalidateVisual();
        }

        // ── 区域一收尾：松手时光标在源行带里 → 重排（或原地放下），**绝不落到浮窗那条路** ──
        if (session.OverSourceRow)
        {
            if (commit && session.ReorderTargetId is { } reorderTarget)
            {
                _area!.ReorderTab(session.TabId, reorderTarget);
            }

            return;
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
            target.Editor!.Attach(tab);
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
