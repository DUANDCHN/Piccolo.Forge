using Avalonia;
using Avalonia.Automation;
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
    private Control? _titleAdornment;
    private TextBlock? _titleText;
    private double _titleMaxWidth = double.PositiveInfinity;

    /// <summary>建一个标题条。</summary>
    public ForgeToolWindowHeader()
    {
        // 无障碍名：三颗内置按钮只有图标，读屏器念 AutomationProperties.Name（文案走 ForgeStrings）
        AutomationProperties.SetName(_dock, ForgeStrings.Current.DockButton);
        AutomationProperties.SetName(_options, ForgeStrings.Current.OptionsButton);
        AutomationProperties.SetName(_hide, ForgeStrings.Current.HideButton);

        // ⚠ 子控件**不再**挂在这个控件身上（REQ-067 模板化）：ApplyTemplate 会清空
        //   VisualChildren，挂这儿等于每套一次模板被扫一次；LogicalChildren 也不能留
        //   —— 槽（Panel.Children）自己会认领逻辑父，两处都认就抛「already has a parent」。
        //   它们改为住在模板里那个槽（PART_Content）中，见 OnApplyTemplate / SyncSlot。

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

    }

    private ForgeHeaderContentHost? _slot;

    /// <inheritdoc />
    /// <remarks>把标签条与按钮放进模板的槽里；重新套模板时先从上一份槽里摘走。</remarks>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _slot?.Children.Clear();
        _titleText = e.NameScope.Find<TextBlock>("PART_Title");
        if (_titleText is not null)
        {
            _titleText.MaxWidth = _titleMaxWidth;
        }
        _slot = e.NameScope.Find<ForgeHeaderContentHost>("PART_Content");
        SyncSlot();
    }

    /// <summary>把当前该出现的子控件同步进槽 —— 次序即绘制次序（标签条在按钮之下）。</summary>
    private void SyncSlot()
    {
        if (_slot is null)
        {
            return;
        }

        _slot.Children.Clear();
        if (_titleAdornment is not null)
        {
            _slot.Children.Add(_titleAdornment);
        }

        _slot.Children.Add(_tabs);
        foreach (var button in Buttons)
        {
            _slot.Children.Add(button);
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
        _extra.Clear();

        if (buttons is not null)
        {
            _extra.AddRange(buttons);
        }

        foreach (var button in _extra)
        {
            // 同内置按钮：标题条整条是窗口拖动区，按钮得标出来才收得到点击（见构造里那段）
            WindowDecorationProperties.SetElementRole(button, WindowDecorationsElementRole.User);
        }

        SyncSlot();
        InvalidateMeasure();
    }

    /// <summary>
    /// 宿主挂在**标题后面**的一个控件（只读标识、角标之类），排在标题与内容标签之间。
    /// </summary>
    /// <remarks>
    /// 此槽用于标题来源标识，排在标题后、内容标签前；<see cref="SetExtraButtons"/> 是右侧动作工具栏。
    /// 工具栏的图标按钮采用宿主配置的宽度，分隔线等非按钮按期望宽度排布；标题标识应使用本槽，
    /// 由库限制其可用宽度，并把内容标签的起点往右推。
    /// <para>
    /// 位置的依据是上游 <c>ToolWindowHeader</c> 的 westPanel —— 标题与内容标签本来就在同一行
    /// （<c>ToolWindowHeader.kt:99</c>、<c>:115</c>、<c>:261</c>，见 <c>docs/upstream/工具窗口模型.md</c> §13），
    /// 这个槽就插在这两者中间。
    /// ⚠ <b>它对位上游哪一个 API 尚未核对</b>（疑似 <c>ToolWindowEx.setTabActions</c>）：
    /// 本机没有 <c>third_party/intellij-community</c> 克隆，行号待补 —— 按 CONTRIBUTING 的规矩，
    /// 这一条是欠着的账，不是已经审明白的结论。
    /// </para>
    /// <para>
    /// <b>宿主要自己缓存实例</b>：本方法每次同步都会被调用，每次给一个新控件等于每帧重建。
    /// 与 <see cref="SetExtraButtons"/> 同一条约定。
    /// </para>
    /// <para>
    /// <b>面板窄的时候它按"剩下多少"量</b>：可用宽度 = 标题右边到右侧按钮左边之间还剩的部分。
    /// 所以挂上来的控件要**自己会缩**，写死 <c>Width</c> 的在窄面板上会坚持自己的宽度并溢出
    /// （Avalonia 的常规行为，不是这里特判的）。
    /// ⚠ <b>别拿横排 <c>StackPanel</c> 当容器</b>：它给子控件的可用宽度是<b>无限</b>，
    /// 会把这里传下去的"还剩多少"整个吃掉；给里面的文字设 <c>MaxWidth</c> 来刹车是假的
    /// —— 那个数既会在宽面板上白白切短内容，又在窄到不足它时照样兜不住。
    /// 用 <c>DockPanel</c>（图标 Dock 左、文字填满）或带约束的 <c>Grid</c>。
    /// 另外它排在内容标签**之前**：两者都在的窄面板上，标签能用的地方会相应变少。
    /// </para>
    /// </remarks>
    /// <param name="adornment">要挂的控件；<see langword="null"/> = 不挂。</param>
    public void SetTitleAdornment(Control? adornment)
    {
        if (ReferenceEquals(_titleAdornment, adornment))
        {
            return;
        }

        _titleAdornment = adornment;

        if (_titleAdornment is not null)
        {
            // 同按钮：标题条整条是窗口拖动区，不标出来的话它自己的 ToolTip / 命中会被拖动吃掉
            WindowDecorationProperties.SetElementRole(_titleAdornment, WindowDecorationsElementRole.User);
        }

        SyncSlot();
        InvalidateMeasure();
    }

    /// <summary>右侧那排按钮，从左到右：宿主的 → ⿴ 停靠 → ⋮ → —。</summary>
    /// <remarks>次序照上游 <c>DefaultActionGroup(DockToolWindowAction(), ShowOptionsAction(), HideAction())</c>。</remarks>
    private IEnumerable<Control> Buttons =>
        ShowDockButton ? [.. _extra, _dock, _options, _hide] : [.. _extra, _options, _hide];

    static ForgeToolWindowHeader()
    {
        AffectsRender<ForgeToolWindowHeader>(
            PaletteProperty, TitleProperty, IsActiveProperty, TopCornerRadiusProperty);
        AffectsMeasure<ForgeToolWindowHeader>(TitleFontWeightProperty);
        AffectsMeasure<ForgeToolWindowHeader>(
            IsCompactProperty,
            ShowButtonsProperty,
            ShowDockButtonProperty,
            TitleBarHeightProperty,
            HeightOverrideProperty,
            CaptionButtonsWidthProperty);
        ContentUiTypeProperty.Changed.AddClassHandler<ForgeToolWindowHeader>((h, _) => h.InvalidateMeasure());
        AffectsRender<ForgeToolWindowHeader>(HeaderBackgroundBrushProperty, HeaderBorderBrushProperty);


        // 上面两个角跟着岛磨圆，下面两个角照旧是方的 —— 模板里那层 Border 拿 HeaderCornerRadius
        TopCornerRadiusProperty.Changed.AddClassHandler<ForgeToolWindowHeader>(
            (h, e) => h.HeaderCornerRadius = new CornerRadius(e.GetNewValue<double>(), e.GetNewValue<double>(), 0, 0));

        // 标题左内边距一处给、两处用：模板里那个 TextBlock 的 Margin + 标签的起点
        TitleInsetLeftProperty.Changed.AddClassHandler<ForgeToolWindowHeader>((h, _) => h.SyncTitleMargin());
        AffectsMeasure<ForgeToolWindowHeader>(TitleInsetLeftProperty, TitleInsetRightProperty,
            ToolbarInsetLeftProperty, ToolbarInsetRightProperty, ButtonWidthProperty, TitleTextTrimmingProperty);
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
    /// 具体多高由宿主定（<c>ForgeControl.TitleBarHeight</c>），因为"这个应用的窗口标题栏多高"
    /// 是宿主的事。
    /// </remarks>
    public static readonly StyledProperty<double> TitleBarHeightProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, double>(nameof(TitleBarHeight), double.NaN);

    /// <summary>
    /// 标题文字的字重。
    /// </summary>
    /// <remarks>
    /// 上游那条标题是常规字重，所以默认 <see cref="FontWeight.Normal"/>；
    /// 想让面板名更立得住的项目自己调（<c>ForgeControl.HeaderTitleFontWeight</c>）。
    /// </remarks>
    public static readonly StyledProperty<FontWeight> TitleFontWeightProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, FontWeight>(nameof(TitleFontWeight), FontWeight.Normal);

    /// <summary>标题文字的字重。</summary>
    public FontWeight TitleFontWeight
    {
        get => GetValue(TitleFontWeightProperty);
        set => SetValue(TitleFontWeightProperty, value);
    }

    /// <summary>兼任窗口标题栏时的高度，NaN = 不兼任。</summary>
    public double TitleBarHeight
    {
        get => GetValue(TitleBarHeightProperty);
        set => SetValue(TitleBarHeightProperty, value);
    }

    /// <summary>
    /// 宿主指定的标题条高度；<see cref="double.NaN"/> = 不指定，按上游那两档（紧凑 31 / 常规 41）走。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="IsCompact"/> **不是一回事**：紧凑是上游的一整档密度（条带按钮、字号、圆角一起变小），
    /// 这里只改标题条这一行的高度，别的度量不动 —— "面板标题条该多高"是宿主的版面取舍，
    /// 它不该被迫连同整档密度一起切换。
    /// <para>
    /// 兼任窗口标题栏时（<see cref="IsWindowTitleBar"/>）不吃这个值：那种形态下这一行是**窗口的**标题栏，
    /// 高度得跟应用里其它窗体对齐（<see cref="TitleBarHeight"/>），与面板标题条多高无关。
    /// </para>
    /// </remarks>
    public static readonly StyledProperty<double> HeightOverrideProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, double>(nameof(HeightOverride), double.NaN);

    /// <summary>宿主指定的标题条高度，NaN = 走上游默认。</summary>
    public double HeightOverride
    {
        get => GetValue(HeightOverrideProperty);
        set => SetValue(HeightOverrideProperty, value);
    }

    /// <summary>
    /// 右端要给窗口的系统按钮（最小化 / 最大化 / 关闭）留出多宽。
    /// </summary>
    /// <remarks>
    /// 独立窗口形态下，标题条与窗口的标题栏是**同一行**：左边是面板的标题和标签，
    /// 中间偏右是面板自己的按钮（⿴ ⋮ —），最右边才是窗口的三个系统按钮。
    /// 那三个按钮由宿主的 chrome 画在客户区上层，布局里看不见，所以得在这里留出它们的宽度，
    /// 否则面板的按钮会被压在它们底下。
    /// <para>具体多宽只有宿主知道（各家 chrome 不一样），由 <c>ForgeControl.CaptionButtonsWidth</c> 给。</para>
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

    /// <summary>
    /// 上面两个角的圆角半径 —— 标题条坐在岛的顶上时，得跟着岛一起磨圆。
    /// </summary>
    /// <remarks>
    /// 标题条是不透明的：它要是方角，就会把岛顶上那两段圆角**盖掉**，
    /// 表现成"下面的圆角好好的，上面的断了"。岛的描边画在标题条之下，压不住。
    /// </remarks>
    public static readonly StyledProperty<double> TopCornerRadiusProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, double>(nameof(TopCornerRadius));

    /// <summary>上面两个角的圆角半径。</summary>
    public double TopCornerRadius
    {
        get => GetValue(TopCornerRadiusProperty);
        set => SetValue(TopCornerRadiusProperty, value);
    }

    /// <summary>
    /// 标题条高度：兼任窗口标题栏时按 <see cref="TitleBarHeight"/>，
    /// 否则宿主给了 <see cref="HeightOverride"/> 就按它，再否则紧凑 31 / 常规 41。
    /// </summary>
    public double HeaderHeight
    {
        get
        {
            if (!double.IsNaN(TitleBarHeight))
            {
                return TitleBarHeight;
            }

            return double.IsNaN(HeightOverride)
                ? (IsCompact ? ForgeCompactMetrics.HeaderHeight : ForgeMetrics.HeaderHeight)
                : HeightOverride;
        }
    }

    /// <summary>
    /// 标题（以及标题后缀，若有）在左边占多宽 —— tab 从这里往右排。
    /// </summary>
    /// <remarks>
    /// 上游是 <c>TAB_LAYOUT_START</c>(4) + idLabel 的宽度
    /// （<c>TabContentLayout.java:178-184</c>）。这里 idLabel 就是标题。
    /// <para>宿主挂了 <see cref="SetTitleAdornment"/> 时还要再让过它 —— 不让就是让标签压在它上面。</para>
    /// </remarks>
    /// <returns>tab 的起始横坐标。</returns>
    public double ComputeTabsStart()
        => TitleAdornmentStart + AdornmentAdvance;

    /// <summary>标题后缀（<see cref="SetTitleAdornment"/>）从哪儿开始 —— 也就是没有后缀时标签的起点。</summary>
    private double TitleAdornmentStart
    {
        get
        {
            var title = MeasureTitle();
            return ResolvedTitleInset
                   + Math.Min(title?.Width ?? 0, _titleMaxWidth)
                   + (title is null ? 0 : TitleInsetRight);
        }
    }

    /// <summary>标题后缀把标签起点往右推多少：它的宽度 + 一个与标题同款的间距；没有后缀时 0。</summary>
    /// <remarks>
    /// 用控件量出来的 <c>DesiredSize</c> 而不是它的 <c>Width</c>：这个位的意义就是
    /// "宽度由控件自己说了算"。还没量过（DesiredSize 为 0）时推 0，下一轮量完自然对上。
    /// </remarks>
    private double AdornmentAdvance
        => _titleAdornment is { IsVisible: true } adornment && adornment.DesiredSize.Width > 0
            ? adornment.DesiredSize.Width + TitleInsetRight
            : 0;

    /// <summary>按钮宽度由宿主配置；分隔线等非按钮控件使用含 Margin 的期望宽度。</summary>
    private double ButtonAdvance(Control control)
        => control is ForgeHeaderButton ? ButtonWidth : control.DesiredSize.Width;

    /// <summary>先量工具栏再分配标题空间，避免首轮布局或分隔线宽度改变时提前截断标题。</summary>
    private void MeasureButtons()
    {
        _dock.IsVisible = ShowButtons && ShowDockButton;
        foreach (var button in Buttons)
        {
            button.IsVisible = ShowButtons;
            button.Measure(new Size(
                button is ForgeHeaderButton ? ButtonWidth : double.PositiveInfinity,
                ForgeMetrics.HeaderButtonSize));
        }
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

            var buttons = Buttons.ToArray();
            return CaptionButtonsWidth + (buttons.Length == 0
                ? 0
                : ToolbarInsetLeft
                  + buttons.Sum(ButtonAdvance)
                  + ToolbarInsetRight);
        }
    }

    /// <inheritdoc />
    /// <remarks>模板量完（其中槽会回头调 <see cref="MeasureContent"/>），这一行的高度仍由自己定。</remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        // 先量工具栏：分隔线等非按钮控件按实际期望宽度计入预算，首轮布局也不能按按钮个数估算。
        MeasureButtons();

        // 右侧动作与 Rider 一样具有更高的布局优先级；面板变窄时标题只能使用按钮左侧的剩余空间。
        // 模板按宿主选择显示省略号或直接裁切，ClipToBounds 始终阻止文字绘制到按钮区域。
        _titleMaxWidth = double.IsInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : Math.Max(0, availableSize.Width - ResolvedTitleInset - ButtonsWidth - TitleInsetRight);
        if (_titleText is not null)
        {
            _titleText.MaxWidth = _titleMaxWidth;
        }

        base.MeasureOverride(availableSize.WithHeight(HeaderHeight));
        return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, HeaderHeight);
    }

    /// <summary>
    /// 量槽里的子控件 —— <b>算式与模板化之前逐字一致</b>，只是改由槽触发（REQ-067）。
    /// </summary>
    /// <param name="availableSize">槽拿到的可用空间。</param>
    /// <returns>槽的期望尺寸（铺满即可，真正的排布在 <see cref="ArrangeContent"/>）。</returns>
    internal Size MeasureContent(Size availableSize)
    {
        MeasureButtons();

        // 标题后缀先量：标签的起点要用它量出来的宽度（ComputeTabsStart → AdornmentAdvance），
        // 顺序反了这一轮标签会压在它上面。
        // 给的宽度是"标题右边到按钮左边之间还剩多少"，不是无限 —— 相机名之类的东西长度不可控，
        // 给无限它就会一路量下去，把标签挤没。
        if (_titleAdornment is { IsVisible: true } adornment)
        {
            var room = double.IsInfinity(availableSize.Width)
                ? double.PositiveInfinity
                : Math.Max(0, availableSize.Width - TitleAdornmentStart - ButtonsWidth - TitleInsetRight);
            adornment.Measure(new Size(room, HeaderHeight));
        }

        // 标签按**这一行最终的高度**量，别把外面给的（可能是无限）原样传下去 ——
        // ForgeContentTab 量到无限高时会退回上游那个 41，标题条却按 HeaderHeight 排，两处对不上。
        _tabs.Measure(availableSize.WithHeight(HeaderHeight));
        return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, HeaderHeight);
    }

    /// <summary>
    /// 排槽里的子控件 —— <b>算式与模板化之前逐字一致</b>。
    /// </summary>
    /// <param name="finalSize">槽的最终尺寸（模板根铺满标题条，所以它就是标题条的尺寸）。</param>
    /// <returns>用掉的尺寸。</returns>
    internal Size ArrangeContent(Size finalSize)
    {
        // 按钮靠右（上游是 BorderLayout.EAST，ToolWindowHeader.kt:202），标签页排在标题与按钮之间；
        // 独立窗口形态下再往左让出窗口那三个系统按钮的地方（见 CaptionButtonsWidth）
        var x = finalSize.Width - ToolbarInsetRight - CaptionButtonsWidth;
        foreach (var button in Buttons.Reverse())
        {
            if (!ShowButtons)
            {
                button.Arrange(default);
                continue;
            }

            // 与 ButtonsWidth 使用同一宽度，分隔线不能再占据整颗按钮的槽位。
            var buttonWidth = ButtonAdvance(button);
            x -= buttonWidth;
            button.Arrange(new Rect(
                x,
                (finalSize.Height - ForgeMetrics.HeaderButtonSize) / 2,
                buttonWidth,
                ForgeMetrics.HeaderButtonSize));
        }

        if (_titleAdornment is { IsVisible: true } adornment)
        {
            var height = Math.Min(adornment.DesiredSize.Height, finalSize.Height);
            adornment.Arrange(new Rect(
                TitleAdornmentStart,
                (finalSize.Height - height) / 2,
                adornment.DesiredSize.Width,
                height));
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
                ResolveTypeface(TitleFontWeight),
                TitleFontSize,
                // ★ **只为量宽度**（REQ-067）：标题的画由模板里那个 TextBlock 负责。
                //   ComputeTabsStart 要"这行字多宽"才知道标签从哪开始 —— 这就是需求开头
                //   「风险 1：布局算式依赖测出来的文字宽度」的答案：**测量留在 FormattedText，
                //   绘制归模板**，两边字体/字号/字重同源即可。颜色在这里用不到。
                Brushes.Transparent);

    /// <inheritdoc />
    /// <summary><see cref="HeaderBackgroundBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> HeaderBackgroundBrushProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, IBrush?>(nameof(HeaderBackgroundBrush));

    /// <summary><see cref="HeaderBorderBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> HeaderBorderBrushProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, IBrush?>(nameof(HeaderBorderBrush));

    /// <summary>标题条底色，由 <c>ControlTheme</c> 从资源键给（REQ-067）。</summary>
    /// <remarks>
    /// <b>这个控件不套模板</b>：它自己往 <c>VisualChildren</c> 里挂标签条与三个按钮，
    /// 而 <c>ApplyTemplate</c> 会把 <c>VisualChildren</c> 清空 —— 给它模板等于把那些子控件扫掉。
    /// 走"画笔属性由主题给、绘制仍自己来"这条；对宿主而言用法一样。
    /// <para>兜底回配色 —— 控件没进树时拿不到主题。</para>
    /// </remarks>
    public IBrush? HeaderBackgroundBrush
    {
        get => GetValue(HeaderBackgroundBrushProperty);
        set => SetValue(HeaderBackgroundBrushProperty, value);
    }

    /// <summary>标题条底下那条 1px 分隔线的颜色，来源同 <see cref="HeaderBackgroundBrush"/>。</summary>
    public IBrush? HeaderBorderBrush
    {
        get => GetValue(HeaderBorderBrushProperty);
        set => SetValue(HeaderBorderBrushProperty, value);
    }

    /// <summary><see cref="HeaderCornerRadius"/> 的属性定义。</summary>
    public static readonly StyledProperty<CornerRadius> HeaderCornerRadiusProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, CornerRadius>(nameof(HeaderCornerRadius));

    /// <summary><see cref="TitleMargin"/> 的属性定义。</summary>
    public static readonly StyledProperty<Thickness> TitleMarginProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, Thickness>(
            nameof(TitleMargin),
            new Thickness(ForgeMetrics.TabLayoutStart + ForgeMetrics.HeaderLabelInsetLeft, 0, 0, 0));

    /// <summary><see cref="TitleInsetLeft"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> TitleInsetLeftProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, double>(nameof(TitleInsetLeft), double.NaN);

    /// <summary>
    /// 标题距左缘多远；<see cref="double.NaN"/> = 走上游
    /// （<see cref="ForgeMetrics.TabLayoutStart"/> + <see cref="ForgeMetrics.HeaderLabelInsetLeft"/>）。
    /// </summary>
    /// <remarks>
    /// ⚠ <b>一处给、两处用</b>：它既是 <see cref="TitleMargin"/> 的左边距，
    /// 也是 <see cref="ComputeTabsStart"/> 的起点 —— 标签从"标题右缘"往后排，
    /// 只挪标题不挪起点会在两者之间留一段空。
    /// </remarks>
    public double TitleInsetLeft
    {
        get => GetValue(TitleInsetLeftProperty);
        set => SetValue(TitleInsetLeftProperty, value);
    }

    /// <summary><see cref="TitleInsetRight"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> TitleInsetRightProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, double>(nameof(TitleInsetRight), ForgeMetrics.HeaderLabelInsetRight,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>标题及标题后缀右侧的间距；默认采用上游度量，宿主可选择紧凑布局。</summary>
    public double TitleInsetRight
    {
        get => GetValue(TitleInsetRightProperty);
        set => SetValue(TitleInsetRightProperty, value);
    }

    /// <summary><see cref="ToolbarInsetLeft"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> ToolbarInsetLeftProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, double>(nameof(ToolbarInsetLeft), ForgeMetrics.HeaderToolbarInsetLeft,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>右侧工具栏的左内边距；默认采用上游度量，宿主可选择紧凑布局。</summary>
    public double ToolbarInsetLeft
    {
        get => GetValue(ToolbarInsetLeftProperty);
        set => SetValue(ToolbarInsetLeftProperty, value);
    }

    /// <summary><see cref="ToolbarInsetRight"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> ToolbarInsetRightProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, double>(nameof(ToolbarInsetRight), ForgeMetrics.HeaderToolbarInsetRight,
            validate: value => double.IsFinite(value) && value >= 0);

    /// <summary>右侧工具栏的右内边距；默认采用上游度量，宿主可选择紧凑布局。</summary>
    public double ToolbarInsetRight
    {
        get => GetValue(ToolbarInsetRightProperty);
        set => SetValue(ToolbarInsetRightProperty, value);
    }

    /// <summary><see cref="ButtonWidth"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> ButtonWidthProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, double>(nameof(ButtonWidth), ForgeMetrics.HeaderButtonSize,
            validate: value => double.IsFinite(value) && value >= ForgeMetrics.HeaderButtonIconSlot);

    /// <summary>工具栏图标按钮的宽度，默认 24；最小 20，保证模板图标槽完整，高度不变。</summary>
    /// <remarks>宿主布局覆盖项；不缩放图标，分隔线仍按自身的期望宽度排列。</remarks>
    public double ButtonWidth
    {
        get => GetValue(ButtonWidthProperty);
        set => SetValue(ButtonWidthProperty, value);
    }

    /// <summary><see cref="TitleTextTrimming"/> 的属性定义。</summary>
    public static readonly StyledProperty<TextTrimming> TitleTextTrimmingProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, TextTrimming>(nameof(TitleTextTrimming), TextTrimming.CharacterEllipsis);

    /// <summary>标题溢出显示方式；默认省略号，None 保持完整字形排版并在可用宽度边界连续裁切。</summary>
    /// <remarks>只作用于面板名；不改变按钮预算、标题来源标识或文档标签的显示方式。</remarks>
    public TextTrimming TitleTextTrimming
    {
        get => GetValue(TitleTextTrimmingProperty);
        set => SetValue(TitleTextTrimmingProperty, value);
    }

    /// <summary>标题实际用的左内边距 —— 宿主没指定就走上游那两个度量之和。</summary>
    private double ResolvedTitleInset
        => double.IsNaN(TitleInsetLeft)
            ? ForgeMetrics.TabLayoutStart + ForgeMetrics.HeaderLabelInsetLeft
            : TitleInsetLeft;

    /// <summary><see cref="TitleFontSize"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> TitleFontSizeProperty =
        AvaloniaProperty.Register<ForgeToolWindowHeader, double>(nameof(TitleFontSize), 12);

    /// <summary>标题条底那块的圆角：**只磨上面两个角**，下面接着正文。</summary>
    /// <remarks>由 <see cref="TopCornerRadius"/> 算出（跟着岛走），模板里那层 <c>Border</c> 拿它。</remarks>
    public CornerRadius HeaderCornerRadius
    {
        get => GetValue(HeaderCornerRadiusProperty);
        private set => SetValue(HeaderCornerRadiusProperty, value);
    }

    /// <summary>标题文字的位置：左边让开 <c>TabLayoutStart + HeaderLabelInsetLeft</c>，竖直居中。</summary>
    public Thickness TitleMargin
    {
        get => GetValue(TitleMarginProperty);
        set => SetValue(TitleMarginProperty, value);
    }

    /// <summary>把标题的左内边距刷进模板绑的那个 <see cref="TitleMargin"/>。</summary>
    private void SyncTitleMargin() => TitleMargin = new Thickness(ResolvedTitleInset, 0, 0, 0);

    /// <summary>标题字号 —— 与 <see cref="MeasureTitle"/> 必须同源（标签起点按量出来的标题宽算）。</summary>
    public double TitleFontSize
    {
        get => GetValue(TitleFontSizeProperty);
        set => SetValue(TitleFontSizeProperty, value);
    }

    /// <inheritdoc />
    /// <remarks>
    /// ★ 只剩铺命中区（REQ-067）：底、下边那条 1px 线、标题文字全部搬进了模板。
    /// <para>
    /// 活动与非活动**同色**：<c>ToolWindowHeader.kt:312</c> 是
    /// <c>val active = !isNewUi &amp;&amp; isActive</c>，新 UI 下恒为 false，
    /// <c>drawHeader</c> 永远走 <c>headerBackground()</c>。「活动标题条」那条高亮是老 UI 的东西。
    /// </para>
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        // 整条都要能按下（拖窗口、右键菜单），不然只有画了东西的地方收得到鼠标
        ForgeHitArea.Fill(context, Bounds.Size);
    }
    /// <summary>
    /// 画文字用的字形 —— **走控件自己继承下来的字体**，不是 <see cref="Typeface.Default"/>。
    /// </summary>
    /// <remarks>
    /// 这些文字是自绘的，不像 <c>TextBlock</c> 那样自动吃到宿主的全局字体样式；
    /// 用 <see cref="Typeface.Default"/> 的话，整个外壳（面板标题、条带名字、标签页）
    /// 会与宿主其余部分**用两种字体**，宿主内嵌了字体时尤其明显。
    /// <see cref="TemplatedControl.FontFamily"/> 是可继承属性，读它就跟着宿主走。
    /// </remarks>
    private Typeface ResolveTypeface(FontWeight weight = FontWeight.Normal)
        => new(FontFamily, FontStyle.Normal, weight);
}
