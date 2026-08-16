using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Piccolo.Forge.Model;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 工具窗口的标题条。
/// </summary>
/// <remarks>
/// 度量对应 <c>度量与主题键.md</c> §3：高 41（紧凑 31），
/// 标题左右内边距 (0,12,0,16)，工具栏左右内边距 (0,12,0,8)。
/// 激活/非激活两种背景来自 `ToolWindow.Header.background` / `...inactiveBackground`
/// （<c>JBUI.java:1154-1169</c>）。
/// </remarks>
public class ForgeToolWindowHeader : TemplatedControl
{
    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    /// <summary><see cref="Title"/> 的属性定义。</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, string?>(nameof(Title));

    /// <summary><see cref="IsActive"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, bool>(nameof(IsActive));

    /// <summary><see cref="IsCompact"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, bool>(nameof(IsCompact));

    /// <summary><see cref="ContentUiType"/> 的属性定义。</summary>
    public static readonly StyledProperty<ToolWindowContentUiType> ContentUiTypeProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, ToolWindowContentUiType>(nameof(ContentUiType));

    /// <summary><see cref="ShowButtons"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> ShowButtonsProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, bool>(nameof(ShowButtons), defaultValue: true);

    /// <summary><see cref="ShowDockButton"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> ShowDockButtonProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, bool>(nameof(ShowDockButton));

    private readonly ForgeContentTabStrip _tabs = new();
    private readonly ForgeHeaderButton _dock = new() { Icon = ForgeIcons.Dock, IsFilledIcon = false };
    private readonly ForgeHeaderButton _options = new() { Icon = ForgeIcons.MoreVertical };
    private readonly ForgeHeaderButton _hide = new() { Icon = ForgeIcons.Hide, IsFilledIcon = false };
    private readonly List<Control> _extra = [];

    /// <summary>建一个标题条。</summary>
    public ForgeToolWindowHeader()
    {
        VisualChildren.Add(_tabs);
        LogicalChildren.Add(_tabs);

        // 拖动窗口：子控件吃掉的按下也要收得到（理由见 OnHeaderPressed）
        AddHandler(PointerPressedEvent, OnHeaderPressed, RoutingStrategies.Bubble, handledEventsToo: true);

        // 客户区延伸到装饰区的窗口（宿主自绘 chrome 那种），装饰层**盖在内容之上**，
        // 自绘的 PointerPressed 根本收不到按下 —— 拖动得靠命中角色交给系统
        // （Win32 映射为 HTCAPTION，还顺带拿到双击最大化）。
        // ⚠ 角色只在**这条标题条兼任窗口标题栏**时才给（见 IsWindowTitleBar）：
        //   主窗口自己也是自绘 chrome，无条件给的话，每块停靠面板的标题条
        //   都会变成主窗口的拖动区 —— 拖谁都在搬主窗口。
        WindowDecorationProperties.SetElementRole(_tabs, WindowDecorationsElementRole.User);
        foreach (var button in new Control[] { _dock, _options, _hide })
        {
            WindowDecorationProperties.SetElementRole(button, WindowDecorationsElementRole.User);
        }

        _dock.Click += (_, e) => DockRequested?.Invoke(this, e);
        _options.Click += (_, e) => OptionsRequested?.Invoke(this, e);
        _hide.Click += (_, e) => HideRequested?.Invoke(this, e);

        foreach (var button in new Control[] { _dock, _options, _hide })
        {
            VisualChildren.Add(button);
            LogicalChildren.Add(button);
        }
    }

    /// <summary>
    /// 标题条右侧那两个按钮显不显示。
    /// </summary>
    /// <remarks>
    /// 上游恒显示（<c>ToolWindowHeader.kt:119</c> 的 <c>ShowOptionsAction</c> + <c>HideAction</c>），
    /// 这里给一个开关：嵌在别处的宿主可能自己管这两件事。默认开。
    /// </remarks>
    public bool ShowButtons
    {
        get => GetValue(ShowButtonsProperty);
        set => SetValue(ShowButtonsProperty, value);
    }

    /// <summary>
    /// 显不显示「停靠回去」那个按钮。
    /// </summary>
    /// <remarks>
    /// 上游那组动作的第一个就是 <c>DockToolWindowAction</c>（<c>ToolWindowHeader.kt:119</c>），
    /// **只在面板处于外部形态（浮动 / 独立窗口）时出现** —— 已经停靠着的面板没有"停回去"这回事。
    /// </remarks>
    public bool ShowDockButton
    {
        get => GetValue(ShowDockButtonProperty);
        set => SetValue(ShowDockButtonProperty, value);
    }

    /// <summary>「停靠回去」被点了。</summary>
    public event EventHandler<RoutedEventArgs>? DockRequested;

    /// <summary>⋮ 被点了 —— 宿主在这里弹「选项」菜单。</summary>
    public event EventHandler<RoutedEventArgs>? OptionsRequested;

    /// <summary>— 被点了 —— 宿主在这里收起这个面板。</summary>
    public event EventHandler<RoutedEventArgs>? HideRequested;

    /// <summary>⋮ 那个按钮（宿主要挂菜单时用得着它的位置）。</summary>
    public ForgeHeaderButton OptionsButton => _options;

    /// <summary>
    /// 宿主自己的按钮，排在内置那两个的**左边**。
    /// </summary>
    /// <remarks>
    /// 对应上游的 <c>ToolWindow.setTitleActions(...)</c>：标题条右侧那条工具栏本来就允许
    /// 每个工具窗口追加自己的动作（刷新、设置、清空…）。库不规定它们长什么样，
    /// 给 <see cref="ForgeHeaderButton"/> 也行、给任意 <see cref="Control"/> 也行。
    /// </remarks>
    /// <param name="buttons">要追加的按钮，按从左到右。</param>
    public void SetExtraButtons(IEnumerable<Control>? buttons)
    {
        foreach (var old in _extra)
        {
            VisualChildren.Remove(old);
            LogicalChildren.Remove(old);
        }

        _extra.Clear();

        if (buttons is not null)
        {
            _extra.AddRange(buttons);
        }

        foreach (var button in _extra)
        {
            // 同内置按钮：标题条整条是窗口拖动区，按钮得标出来才收得到点击（见构造里那段）
            WindowDecorationProperties.SetElementRole(button, WindowDecorationsElementRole.User);
            VisualChildren.Add(button);
            LogicalChildren.Add(button);
        }

        InvalidateMeasure();
    }

    /// <summary>右侧那排按钮，从左到右：宿主的 → ⿴ 停靠 → ⋮ → —。</summary>
    /// <remarks>次序照上游 <c>DefaultActionGroup(DockToolWindowAction(), ShowOptionsAction(), HideAction())</c>。</remarks>
    private IEnumerable<Control> Buttons =>
        ShowDockButton ? [.. _extra, _dock, _options, _hide] : [.. _extra, _options, _hide];

    static ForgeToolWindowHeader()
    {
        AffectsRender<ForgeToolWindowHeader>(PaletteProperty, TitleProperty, IsActiveProperty);
        AffectsMeasure<ForgeToolWindowHeader>(
            IsCompactProperty,
            ShowButtonsProperty,
            ShowDockButtonProperty,
            TitleBarHeightProperty,
            CaptionButtonsWidthProperty);
        ContentUiTypeProperty.Changed.AddClassHandler<ForgeToolWindowHeader>((h, _) => h.InvalidateMeasure());
    }

    /// <summary>配色。</summary>
    public ForgePalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>标题。</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>这个工具窗口是不是当前活跃的那个。</summary>
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>是否紧凑模式。</summary>
    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    /// <summary>
    /// 内容区是一排 tab 还是一个下拉。
    /// </summary>
    /// <remarks>
    /// 对应 <see cref="WindowInfo.ContentUiType"/>，两套布局见 <c>工具窗口模型.md</c> §13。
    /// </remarks>
    public ToolWindowContentUiType ContentUiType
    {
        get => GetValue(ContentUiTypeProperty);
        set => SetValue(ContentUiTypeProperty, value);
    }

    /// <summary>放 tab 的地方（横排）。</summary>
    /// <remarks>
    /// 上游把 <c>contentUi.tabComponent</c> 放进标题条的 westPanel
    /// （<c>ToolWindowHeader.kt:99</c>、<c>:115</c>）—— 标题和 tab 是**同一行**。
    /// </remarks>
    public ForgeContentTabStrip Tabs => _tabs;

    /// <summary>
    /// 这条标题条**兼任窗口标题栏**时的高度；<see cref="double.NaN"/> 表示不兼任，按面板的高度走。
    /// </summary>
    /// <remarks>
    /// 浮动形态没有系统标题栏（见 <see cref="IsWindowDragArea"/>），这条就是那个窗口的标题栏 ——
    /// 那它该按**窗口标题栏**的高度长，而不是面板标题条的 41
    /// （<c>JBUI.java:1178-1180</c> 那个高度是给停靠着的面板定的，摞在窗口顶上会明显偏高）。
    /// 具体多高由宿主定（<c>ForgeToolWindowFrame.TitleBarHeight</c>），因为"这个应用的窗口标题栏多高"
    /// 是宿主的事。
    /// </remarks>
    public static readonly StyledProperty<double> TitleBarHeightProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, double>(nameof(TitleBarHeight), double.NaN);

    /// <summary>兼任窗口标题栏时的高度，NaN = 不兼任。</summary>
    public double TitleBarHeight
    {
        get => GetValue(TitleBarHeightProperty);
        set => SetValue(TitleBarHeightProperty, value);
    }

    /// <summary>
    /// 右端要给窗口的系统按钮（最小化 / 最大化 / 关闭）留出多宽。
    /// </summary>
    /// <remarks>
    /// 独立窗口形态下，标题条与窗口的标题栏是**同一行**：左边是面板的标题和标签，
    /// 中间偏右是面板自己的按钮（⿴ ⋮ —），最右边才是窗口的三个系统按钮。
    /// 那三个按钮由宿主的 chrome 画在客户区上层，布局里看不见，所以得在这里留出它们的宽度，
    /// 否则面板的按钮会被压在它们底下。
    /// <para>具体多宽只有宿主知道（各家 chrome 不一样），由 <c>ForgeToolWindowFrame.CaptionButtonsWidth</c> 给。</para>
    /// </remarks>
    public static readonly StyledProperty<double> CaptionButtonsWidthProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, double>(nameof(CaptionButtonsWidth));

    /// <summary>右端给系统按钮留的宽度；0 = 不留（这个窗口的标题栏不在这一行）。</summary>
    public double CaptionButtonsWidth
    {
        get => GetValue(CaptionButtonsWidthProperty);
        set => SetValue(CaptionButtonsWidthProperty, value);
    }

    /// <summary>
    /// 这条标题条是不是**兼任所在窗口的标题栏**。
    /// </summary>
    /// <remarks>
    /// 只有浮动形态是（宿主窗体没有标题栏那一行，由它顶上，见 <see cref="TitleBarHeight"/>）。
    /// 停靠着的、以及独立窗口形态（那种窗体自带标题栏）都不是 —— 那时这条只是面板标题条，
    /// 按住它不该搬动任何窗口。
    /// <para>
    /// ⚠ 判据必须是**这条标题条自己**，不能问"这个窗口有没有系统标题栏"：
    /// 主窗口往往也是自绘 chrome（客户区延伸），那样问的话每块停靠面板的标题条
    /// 都会变成主窗口的拖动区。
    /// </para>
    /// </remarks>
    public bool IsWindowTitleBar => !double.IsNaN(TitleBarHeight);

    /// <summary>标题条高度：兼任窗口标题栏时按 <see cref="TitleBarHeight"/>，否则紧凑 31 / 常规 41。</summary>
    public double HeaderHeight => double.IsNaN(TitleBarHeight)
        ? (IsCompact ? ForgeCompactMetrics.HeaderHeight : ForgeMetrics.HeaderHeight)
        : TitleBarHeight;

    /// <summary>
    /// 标题在左边占多宽 —— tab 从这里往右排。
    /// </summary>
    /// <remarks>
    /// 上游是 <c>TAB_LAYOUT_START</c>(4) + idLabel 的宽度
    /// （<c>TabContentLayout.java:178-184</c>）。这里 idLabel 就是标题。
    /// </remarks>
    /// <returns>tab 的起始横坐标。</returns>
    public double ComputeTabsStart()
    {
        var title = MeasureTitle();
        return ForgeMetrics.TabLayoutStart
               + ForgeMetrics.HeaderLabelInsetLeft
               + (title?.Width ?? 0)
               + (title is null ? 0 : ForgeMetrics.HeaderLabelInsetRight);
    }

    /// <summary>右侧那排按钮一共占多宽（含左右内边距）；不显示时为 0。</summary>
    /// <remarks>
    /// 内边距取上游标题条工具栏那两个键（<c>HeaderToolbarInsetLeft/Right</c>，
    /// <c>JBUI.java:1186-1188</c>）。
    /// </remarks>
    public double ButtonsWidth
    {
        get
        {
            if (!ShowButtons)
            {
                return 0;
            }

            var count = Buttons.Count();
            return CaptionButtonsWidth + (count == 0
                ? 0
                : ForgeMetrics.HeaderToolbarInsetLeft
                  + (count * ForgeMetrics.HeaderButtonSize)
                  + ForgeMetrics.HeaderToolbarInsetRight);
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        _dock.IsVisible = ShowButtons && ShowDockButton;

        foreach (var button in Buttons)
        {
            button.IsVisible = ShowButtons;
            button.Measure(new Size(ForgeMetrics.HeaderButtonSize, ForgeMetrics.HeaderButtonSize));
        }

        _tabs.Measure(availableSize);
        return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, HeaderHeight);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        // 按钮靠右（上游是 BorderLayout.EAST，ToolWindowHeader.kt:202），标签页排在标题与按钮之间；
        // 独立窗口形态下再往左让出窗口那三个系统按钮的地方（见 CaptionButtonsWidth）
        var x = finalSize.Width - ForgeMetrics.HeaderToolbarInsetRight - CaptionButtonsWidth;
        foreach (var button in Buttons.Reverse())
        {
            if (!ShowButtons)
            {
                button.Arrange(default);
                continue;
            }

            x -= ForgeMetrics.HeaderButtonSize;
            button.Arrange(new Rect(
                x,
                (finalSize.Height - ForgeMetrics.HeaderButtonSize) / 2,
                ForgeMetrics.HeaderButtonSize,
                ForgeMetrics.HeaderButtonSize));
        }

        var start = ComputeTabsStart();
        var width = Math.Max(0, finalSize.Width - start - ButtonsWidth);
        _tabs.Arrange(new Rect(start, 0, width, finalSize.Height));
        return finalSize;
    }

    /// <summary>
    /// 按住标题条拖动整个窗口。
    /// </summary>
    /// <remarks>
    /// 浮动形态没有系统标题栏（上游 <c>FloatingDecorator.java:102 · setUndecorated(true)</c>），
    /// 所以标题条自己得担起这件事 —— 同主窗口的做法。
    /// 判据是**这个窗口还有没有系统标题栏**：客户区延伸到装饰区、或者压根没装饰，就说明标题栏归自绘，
    /// 该我们担；独立窗口形态留着系统标题条，它自己会拖，别抢。
    /// <para>
    /// ⚠ 不能只覆写 <c>OnPointerPressed</c>：标题条上摆着标签条等子控件，
    /// 落在它们身上的按下**到不了**这里（被它们标成已处理），于是"标题条上有些地方拖不动"。
    /// 所以走 <c>handledEventsToo</c> 的冒泡处理器，只把真正有别的用处的地方让出去：
    /// 按钮（点它们是执行动作）和标签（点它是切内容、拖它是搬标签页）。
    /// </para>
    /// </remarks>
    /// <param name="sender">事件源，未用。</param>
    /// <param name="e">按下事件。</param>
    private void OnHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsWindowTitleBar
            || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            || TopLevel.GetTopLevel(this) is not Window window)
        {
            return;
        }

        if (!IsWindowDragArea(e.Source as Visual))
        {
            return;
        }

        window.BeginMoveDrag(e);
    }

    /// <summary>
    /// 标题条上这个位置能不能用来拖窗口。
    /// </summary>
    /// <remarks>
    /// 除了两类地方，标题条**处处**都能拖：按钮（点它们是执行动作）和标签（点它是切内容、
    /// 拖它是搬标签页）。其余部分——标题文字、空白、标签条里没被标签占到的地方——都算把手。
    /// </remarks>
    /// <param name="source">按下落在哪个控件上。</param>
    /// <returns>能拖为 <see langword="true"/>。</returns>
    public bool IsWindowDragArea(Visual? source)
        => source is not null
           && !source.GetSelfAndVisualAncestors()
               .TakeWhile(x => !ReferenceEquals(x, this))
               .Any(x => x is Button or ForgeContentTab);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TitleBarHeightProperty)
        {
            // 兼任窗口标题栏时才把整条交给系统做拖动区（见 IsWindowTitleBar）
            WindowDecorationProperties.SetElementRole(
                this,
                IsWindowTitleBar ? WindowDecorationsElementRole.TitleBar : WindowDecorationsElementRole.None);
        }

        if (change.Property == PaletteProperty)
        {
            foreach (var button in Buttons.OfType<ForgeHeaderButton>())
            {
                button.Palette = Palette;
            }
        }
    }

    private FormattedText? MeasureTitle()
        => string.IsNullOrEmpty(Title)
            ? null
            : new FormattedText(
                Title,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                12,
                new SolidColorBrush(Palette.Foreground));

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        // 活动与非活动**同色**：ToolWindowHeader.kt:312 是 `val active = !isNewUi && isActive`，
        // 新 UI 下恒为 false，drawHeader 永远走 headerBackground()。
        // 「活动标题条」那条高亮是老 UI 的东西，照它画一眼就不像。
        context.FillRectangle(new SolidColorBrush(Palette.HeaderBackground), new Rect(Bounds.Size));

        // 下边一条 1px 分隔线
        context.FillRectangle(
            new SolidColorBrush(Palette.HeaderBorder),
            new Rect(0, Bounds.Height - 1, Bounds.Width, 1));

        if (MeasureTitle() is { } text)
        {
            context.DrawText(
                text,
                new Point(
                    ForgeMetrics.TabLayoutStart + ForgeMetrics.HeaderLabelInsetLeft,
                    (Bounds.Height - text.Height) / 2));
        }
    }
}
