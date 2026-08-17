using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 编辑区里的一个标签页。
/// </summary>
/// <remarks>
/// 度量见 <c>docs/upstream/编辑区.md</c> §5：高 28、左右内边距 8、
/// 选中下划线高 3 且**圆角 0**（工具窗口标题条那边是 4，两处不一致是真的）。
/// </remarks>
public class ForgeEditorTab : TabStripItem
{
    // 这里曾经在构造里赋 Template（一个空 Border，只为盖掉 TabStripItem 自带的模板）。
    // 那是 local value，会盖过 ControlTheme 里的模板与 Setter，
    // 症状是「主题写了一点不生效」且不报错（REQ-067）。

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        // Applying a template rebuilds VisualChildren. A host-supplied Control
        // icon is deliberately outside the empty container template, so restore it.
        if (_iconControl is not null && _iconControl.GetVisualParent() is null)
        {
            VisualChildren.Add(_iconControl);
        }
    }
    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeEditorTab, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    /// <summary>
    /// 标签文字的字重。
    /// </summary>
    /// <remarks>
    /// 上游是常规字重，所以默认 <see cref="FontWeight.Normal"/>；
    /// 由 <c>ForgeEditorControl.TabTitleFontWeight</c> 统一给值。
    /// </remarks>
    public static readonly StyledProperty<FontWeight> TitleFontWeightProperty =
        AvaloniaProperty.Register<ForgeEditorTab, FontWeight>(nameof(TitleFontWeight), FontWeight.Normal);

    /// <summary>标签文字的字重。</summary>
    public FontWeight TitleFontWeight
    {
        get => GetValue(TitleFontWeightProperty);
        set => SetValue(TitleFontWeightProperty, value);
    }


    /// <summary><see cref="Title"/> 的属性定义。</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ForgeEditorTab, string?>(nameof(Title));

    /// <summary><see cref="IsGroupActive"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsGroupActiveProperty =
        AvaloniaProperty.Register<ForgeEditorTab, bool>(nameof(IsGroupActive));

    /// <summary><see cref="IsPinned"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsPinnedProperty =
        AvaloniaProperty.Register<ForgeEditorTab, bool>(nameof(IsPinned));

    /// <summary><see cref="CanBeClosed"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> CanBeClosedProperty =
        AvaloniaProperty.Register<ForgeEditorTab, bool>(nameof(CanBeClosed), defaultValue: true);

    /// <summary>
    /// 宿主指定的标签行高；<see cref="double.NaN"/> = 不指定，按
    /// <see cref="ForgeMetrics.EditorTabRowHeight"/>（Islands 40 / expUI 42）走。
    /// </summary>
    /// <remarks>
    /// 行高变了，Islands 那个内缩圆角块跟着自适应 —— 它的高度是
    /// <c>行高 - max(行高 - 28, 8)</c>（见 <see cref="IslandsBlockRect"/>），
    /// 行高压到 31 时块高 23、上下各留 4，不会顶出去。
    /// </remarks>
    public static readonly StyledProperty<double> RowHeightOverrideProperty =
        AvaloniaProperty.Register<ForgeEditorTab, double>(nameof(RowHeightOverride), double.NaN);

    /// <summary>宿主指定的标签行高，NaN = 走上游默认。</summary>
    public double RowHeightOverride
    {
        get => GetValue(RowHeightOverrideProperty);
        set => SetValue(RowHeightOverrideProperty, value);
    }

    /// <summary>五个状态各自的底与描边，由 ControlTheme 从资源键给（REQ-067）。</summary>
    /// <remarks>
    /// <b>这个控件不套模板</b>，理由是判定本身：底与描边有 <b>5 个状态 × 2 个族</b>，
    /// 两族还是**两个 painter**（expUI 整条贴边 + 下划线；Islands 改画内缩圆角块、完全不画下划线），
    /// 另有若干状态的填充或描边是**空**（不画）。用样式选择器表达要写 20 条上下，
    /// 而这套判定现在集中在 <see cref="ResolveColors"/> 里、每一档都带着上游出处
    /// （<c>IslandsTabPainter.kt:254-268</c>）。拆进选择器出错概率高于收益 ——
    /// 真要拆该等「宿主想换标签形状」这个需求出现。
    /// <para>颜色仍然全部可主题化：宿主重定义同名键即可，明暗照常跟随。</para>
    /// </remarks>
    public static readonly StyledProperty<IBrush?> SelectedBackgroundBrushProperty =
        AvaloniaProperty.Register<ForgeEditorTab, IBrush?>(nameof(SelectedBackgroundBrush));

    /// <summary>选中且所在组活动时的描边。</summary>
    public static readonly StyledProperty<IBrush?> SelectedBorderBrushProperty =
        AvaloniaProperty.Register<ForgeEditorTab, IBrush?>(nameof(SelectedBorderBrush));

    /// <summary>悬停底。</summary>
    public static readonly StyledProperty<IBrush?> HoverBackgroundBrushProperty =
        AvaloniaProperty.Register<ForgeEditorTab, IBrush?>(nameof(HoverBackgroundBrush));

    /// <summary>选中但失焦时的底。</summary>
    public static readonly StyledProperty<IBrush?> InactiveSelectedBackgroundBrushProperty =
        AvaloniaProperty.Register<ForgeEditorTab, IBrush?>(nameof(InactiveSelectedBackgroundBrush));

    /// <summary>选中但失焦时的描边。</summary>
    public static readonly StyledProperty<IBrush?> InactiveSelectedBorderBrushProperty =
        AvaloniaProperty.Register<ForgeEditorTab, IBrush?>(nameof(InactiveSelectedBorderBrush));

    /// <summary>下划线（活动组）。</summary>
    public static readonly StyledProperty<IBrush?> UnderlineBrushProperty =
        AvaloniaProperty.Register<ForgeEditorTab, IBrush?>(nameof(UnderlineBrush));

    /// <summary>下划线（失焦组）。</summary>
    public static readonly StyledProperty<IBrush?> InactiveUnderlineBrushProperty =
        AvaloniaProperty.Register<ForgeEditorTab, IBrush?>(nameof(InactiveUnderlineBrush));

    /// <summary>选中且活动时的底。</summary>
    public IBrush? SelectedBackgroundBrush
    {
        get => GetValue(SelectedBackgroundBrushProperty);
        set => SetValue(SelectedBackgroundBrushProperty, value);
    }

    /// <summary>选中且活动时的描边。</summary>
    public IBrush? SelectedBorderBrush
    {
        get => GetValue(SelectedBorderBrushProperty);
        set => SetValue(SelectedBorderBrushProperty, value);
    }

    /// <summary>悬停底。</summary>
    public IBrush? HoverBackgroundBrush
    {
        get => GetValue(HoverBackgroundBrushProperty);
        set => SetValue(HoverBackgroundBrushProperty, value);
    }

    /// <summary>选中但失焦时的底。</summary>
    public IBrush? InactiveSelectedBackgroundBrush
    {
        get => GetValue(InactiveSelectedBackgroundBrushProperty);
        set => SetValue(InactiveSelectedBackgroundBrushProperty, value);
    }

    /// <summary>选中但失焦时的描边。</summary>
    public IBrush? InactiveSelectedBorderBrush
    {
        get => GetValue(InactiveSelectedBorderBrushProperty);
        set => SetValue(InactiveSelectedBorderBrushProperty, value);
    }

    /// <summary>下划线（活动组）。</summary>
    public IBrush? UnderlineBrush
    {
        get => GetValue(UnderlineBrushProperty);
        set => SetValue(UnderlineBrushProperty, value);
    }

    /// <summary>下划线（失焦组）。</summary>
    public IBrush? InactiveUnderlineBrush
    {
        get => GetValue(InactiveUnderlineBrushProperty);
        set => SetValue(InactiveUnderlineBrushProperty, value);
    }

    static ForgeEditorTab()
    {
        AffectsRender<ForgeEditorTab>(
            SelectedBackgroundBrushProperty, SelectedBorderBrushProperty, HoverBackgroundBrushProperty,
            InactiveSelectedBackgroundBrushProperty, InactiveSelectedBorderBrushProperty,
            UnderlineBrushProperty, InactiveUnderlineBrushProperty);

        PaletteProperty.Changed.AddClassHandler<ForgeEditorTab>(
            (x, _) => x.PseudoClasses.Set(":islands", x.Palette.Island is not null));

        AffectsRender<ForgeEditorTab>(
            PaletteProperty, TitleProperty, IsSelectedProperty, IsGroupActiveProperty,
            IsPinnedProperty, CanBeClosedProperty, IconProperty, IsCompactProperty);
        AffectsMeasure<ForgeEditorTab>(
            TitleProperty, CanBeClosedProperty, IconProperty, TitleFontWeightProperty,
            RowHeightOverrideProperty);
    }

    /// <summary>配色。</summary>
    public ForgePalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>标签上的字。</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>所在的组是不是当前组（决定下划线用哪个颜色）。</summary>
    public bool IsGroupActive
    {
        get => GetValue(IsGroupActiveProperty);
        set => SetValue(IsGroupActiveProperty, value);
    }

    /// <summary>固定住了 —— 画图钉而不是叉。</summary>
    public bool IsPinned
    {
        get => GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    /// <summary>能不能关。</summary>
    public bool CanBeClosed
    {
        get => GetValue(CanBeClosedProperty);
        set => SetValue(CanBeClosedProperty, value);
    }

    /// <summary><see cref="IsCompact"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<ForgeEditorTab, bool>(nameof(IsCompact));

    /// <summary>是否紧凑模式（只影响 Islands 那个选中块的尺寸与内缩）。</summary>
    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    /// <summary><see cref="Icon"/> 的属性定义。</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<ForgeEditorTab, object?>(nameof(Icon));

    /// <summary>
    /// 左边那个文件图标：<see cref="Geometry"/> 直接画，<see cref="Control"/> 挂成子元素，
    /// <see langword="null"/> 就不画（编辑区标签页<b>没有</b>默认图标——文件是什么类型只有宿主知道）。
    /// </summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>左边文件图标占的宽度（含与标题的间距）。</summary>
    private double LeadingIconWidth
        => Icon is null ? 0 : ForgeMetrics.EditorTabIconSize + ForgeMetrics.EditorTabIconTextGap;

    /// <summary>右边那个小图标占的宽度。</summary>
    public double IconAreaWidth => CanBeClosed || IsPinned ? IconSize + ForgeMetrics.EditorTabIconTextGap : 0;

    private static double IconSize => 12;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var text = MeasureText();
        var islands = Palette.Island is not null;
        var width = (text?.Width ?? 0)
                    + ForgeMetrics.EditorTabTextInsetLeft
                    + ForgeMetrics.EditorTabInsetRight(islands)
                    + LeadingIconWidth
                    + IconAreaWidth;

        var rowHeight = double.IsNaN(RowHeightOverride)
            ? ForgeMetrics.EditorTabRowHeight(islands)
            : RowHeightOverride;

        return new Size(width, rowHeight);
    }

    /// <summary>
    /// 当前状态该用什么底色与描边。
    /// </summary>
    /// <remarks>
    /// 照抄 <c>IslandsTabPainter.kt:254-268 · getColors</c> —— **五种状态**，
    /// 不是「选中 / 没选中」两种：
    /// <list type="bullet">
    /// <item>选中 + 活动 → <c>underlinedTabBackground</c> / <c>underlinedBorderColor</c></item>
    /// <item>选中 + 失焦 + 悬停 → <c>hoverBackground</c> / <c>inactiveUnderlinedTabBorderColor</c></item>
    /// <item>选中 + 失焦 → <c>inactiveUnderlinedTabBackground</c> / <c>inactiveUnderlinedTabBorderColor</c></item>
    /// <item>悬停 → <c>hoverBackground</c> / <c>hoverBorderColor</c>（Islands 没定义描边 → 不画）</item>
    /// <item>常规 → <c>regularBackground</c> / <c>regularBorderColor</c>（两族都没定义 → 什么都不画）</item>
    /// </list>
    /// </remarks>
    private (IBrush? Fill, IBrush? Border) ResolveColors()
    {
        // 每一档先取主题给的画刷，取不到（控件还没进树）再兜底回配色 —— 两者同源
        if (IsSelected)
        {
            if (IsGroupActive)
            {
                return (
                    SelectedBackgroundBrush ?? Solid(Palette.EditorTabSelectedBackground),
                    SelectedBorderBrush ?? Solid(Palette.EditorTabSelectedBorder));
            }

            return IsPointerOver
                ? (HoverBackgroundBrush ?? Solid(Palette.EditorTabHoverBackground),
                   InactiveSelectedBorderBrush ?? Solid(Palette.EditorTabInactiveSelectedBorder))
                : (InactiveSelectedBackgroundBrush ?? Solid(Palette.EditorTabInactiveSelectedBackground),
                   InactiveSelectedBorderBrush ?? Solid(Palette.EditorTabInactiveSelectedBorder));
        }

        // 非选中：只有悬停时有底，常规态两族都没定义（regularBackground/BorderColor 为空）
        return IsPointerOver
            ? (HoverBackgroundBrush ?? Solid(Palette.EditorTabHoverBackground), null)
            : (null, null);
    }

    private static IBrush Solid(Color color) => new SolidColorBrush(color);

    /// <summary>
    /// Islands 下那个内缩圆角块的矩形。
    /// </summary>
    /// <remarks>
    /// 照抄 <c>IslandsTabPainter.kt:171-183</c>：
    /// <code>
    /// hOffset  = 4（紧凑 2）           :271-276 的非 side 分支
    /// minVOff  = 8（紧凑 4）           :173
    /// fullH    = 28（紧凑 24）         :175
    /// vOffset  = max(行高 - fullH, minVOff)
    /// y = floor(vOffset / 2)，height = 行高 - vOffset
    /// </code>
    /// 行高是 40（见 <see cref="ForgeMetrics.EditorTabRowHeight"/>），所以
    /// <c>vOffset = 12</c>、块高正好 <b>28</b>、上下各留 6。
    /// </remarks>
    private Rect IslandsBlockRect()
    {
        var hOffset = IsCompact
            ? ForgeMetrics.IslandsEditorTabInsetHorizontalCompact
            : ForgeMetrics.IslandsEditorTabInsetHorizontal;
        var minVOffset = IsCompact
            ? ForgeMetrics.IslandsEditorTabMinInsetVerticalCompact
            : ForgeMetrics.IslandsEditorTabMinInsetVertical;
        var fullHeight = IsCompact
            ? ForgeMetrics.IslandsEditorTabHeightCompact
            : ForgeMetrics.IslandsEditorTabHeight;

        var vOffset = Math.Max(Bounds.Height - fullHeight, minVOffset);
        return new Rect(
            hOffset,
            Math.Floor(vOffset / 2),
            Math.Max(0, Bounds.Width - (hOffset * 2)),
            Math.Max(0, Bounds.Height - vOffset));
    }

    /// <summary>
    /// 画底与描边。**两个族是两个 painter，不是同一套加减参数。**
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><b>expUI</b>：整条贴边 + 一条 <c>underlineHeight</c>=4 的下划线。</item>
    /// <item><b>Islands</b>：**完全不画下划线**（<c>IslandsTabPainter.kt:109</c> 把
    /// <c>underlineHeight</c> 覆盖成 0、<c>:150 · paintUnderline</c> 是空实现），
    /// 改画 <see cref="IslandsBlockRect"/> 那个内缩圆角块。</item>
    /// </list>
    /// </remarks>
    private void RenderDecoration(DrawingContext context)
    {
        var (brush, border) = ResolveColors();
        if (brush is null && border is null)
        {
            return;
        }

        var pen = border is { } b ? new Pen(b, 1) : null;

        if (Palette.Island is null)
        {
            context.DrawRectangle(brush, pen, new Rect(Bounds.Size));

            if (IsSelected)
            {
                var underline = IsGroupActive
                    ? UnderlineBrush ?? Solid(Palette.HeaderTabUnderline)
                    : InactiveUnderlineBrush ?? Solid(Palette.HeaderTabInactiveUnderline);
                var thickness = ForgeMetrics.EditorTabUnderlineHeight;
                context.DrawRectangle(
                    underline,
                    null,
                    new RoundedRect(
                        new Rect(0, Bounds.Height - thickness, Bounds.Width, thickness),
                        ForgeMetrics.EditorTabUnderlineArc / 2));
            }

            return;
        }

        // arc 取的是主工具栏按钮的圆角（:169 原文就是 MainToolbar.Button.hoverArc），仍是直径
        context.DrawRectangle(
            brush, pen, new RoundedRect(IslandsBlockRect(), ForgeMetrics.IslandsEditorTabArc / 2));
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        // 整块都能点，不然只有画了东西的地方收得到鼠标
        ForgeHitArea.Fill(context, Bounds.Size);

        // ⚠ 编辑区的标签页走 **EditorTabs.\*** 这一族键，不是工具窗口标题条的
        //   ToolWindow.HeaderTab.\*；且底与描边是**五态**的，见 ResolveColors。
        RenderDecoration(context);

        if (MeasureText() is { } text)
        {
            context.DrawText(
                text,
                new Point(
                    ForgeMetrics.EditorTabTextInsetLeft + LeadingIconWidth,
                    (Bounds.Height - text.Height) / 2));
        }

        if (Icon is Geometry geometry)
        {
            ForgeIcons.Draw(context, geometry, LeadingIconRect(Bounds.Size), Foreground ?? Solid(Palette.Foreground));
        }

        if (CanBeClosed || IsPinned)
        {
            RenderTabIcon(context);
        }
    }

    /// <summary>宿主塞进来的图标控件；理由同 <c>ForgeStripeButton</c>。</summary>
    private Control? _iconControl;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != IconProperty)
        {
            return;
        }

        var control = change.GetNewValue<object?>() as Control;
        if (ReferenceEquals(control, _iconControl))
        {
            return;
        }

        ReleaseIcon();

        // ★ 宿主给的若是**控件实例**，它可能还挂在上一个标签容器上 —— 先摘再收养。
        //
        //   一个 Avalonia 控件只能有一个父，第二个容器收养它时当场抛
        //   「already has a visual parent」。而这一次收养发生在标签集合变化的**同步回调**里
        //   （TabStrip 的 PrepareContainerForItem），异常会顺着 EditorArea.Changed 一路炸穿
        //   编辑区 VM 的同步 —— 表现不是「图标没了」，而是**整块编辑区停在上一版、点什么都没反应**，
        //   再点一次才在半同步的状态上凑合建出来（于是少一个标签页）。
        //   触发它的日常动作就是「宫格收回单栏」：旧那一格的标签条被整条丢弃，
        //   它的容器不走 ClearContainerForItem，图标就一直攥在死容器手里。
        //
        //   同一页真的同时开在两个分屏时，实例只能跟着最后同步的那个走 ——
        //   这是「传实例」本身的代价（宿主传 Func<Control> 工厂即可两边都有，
        //   见 ForgeEditorControl.ResolveIcon）。但**无论如何不能让它打断同步**。
        if (control?.GetVisualParent() is ForgeEditorTab previous)
        {
            previous.ReleaseIcon();
        }

        _iconControl = control;

        if (_iconControl is not null)
        {
            VisualChildren.Add(_iconControl);
            LogicalChildren.Add(_iconControl);
        }

        InvalidateMeasure();
    }

    /// <summary>松开攥着的图标控件，好让别的标签容器收养它。</summary>
    private void ReleaseIcon()
    {
        if (_iconControl is null)
        {
            return;
        }

        VisualChildren.Remove(_iconControl);
        LogicalChildren.Remove(_iconControl);
        _iconControl = null;
        InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        if (_iconControl is not null)
        {
            var rect = LeadingIconRect(finalSize);
            _iconControl.Measure(rect.Size);
            _iconControl.Arrange(rect);
        }

        return size;
    }

    private static Rect LeadingIconRect(Size size)
        => new(
            ForgeMetrics.EditorTabTextInsetLeft,
            (size.Height - ForgeMetrics.EditorTabIconSize) / 2,
            ForgeMetrics.EditorTabIconSize,
            ForgeMetrics.EditorTabIconSize);

    /// <summary>
    /// 右边那个关闭 / 固定的小图标，鼠标压在它上面时垫一层圆角底。
    /// </summary>
    /// <remarks>
    /// ⚠ <b>悬停底是本库自己定的，上游不是这么做的。</b>
    /// 上游 <c>ActionButton.java:125</c> 走的是 <c>setIcons(icon, disabledIcon, hoveredIcon)</c> ——
    /// 悬停时**换一张图标**，而那是 JetBrains 的美术资产，本项目按审出的尺寸自己画
    /// （见 README「有意不做」）。既然拿不到那张图，就用同一套语义最近的
    /// <c>ActionButton.hoverBackground</c> 垫个底，观感对得上，且用的仍是上游的键。
    /// </remarks>
    private void RenderTabIcon(DrawingContext context)
    {
        var brush = Foreground ?? Solid(Palette.ButtonForeground);
        var x = Bounds.Width - ForgeMetrics.EditorTabInsetRight(Palette.Island is not null) - IconSize;
        var rect = new Rect(x, (Bounds.Height - IconSize) / 2, IconSize, IconSize);

        if (_iconHovered)
        {
            // 底比图标大一圈，四周各留 3，圆角取条带按钮那套的一半（同为小方按钮）
            var pad = 3;
            context.DrawRectangle(
                HoverBackgroundBrush ?? Solid(Palette.ButtonHoverBackground),
                null,
                new RoundedRect(rect.Inflate(pad), ForgeMetrics.StripeButtonArcCompact / 2));
        }

        ForgeIcons.Draw(context, IsPinned ? ForgeIcons.Pin : ForgeIcons.Close, rect, brush, filled: IsPinned);
    }

    private bool _iconHovered;

    /// <summary>
    /// 关闭 / 固定图标的命中矩形（比图标大一圈，好点中）。
    /// </summary>
    /// <remarks>
    /// ⚠ <b>点击判定与悬停判定必须用同一个矩形。</b>早先视图那边用的是
    /// 「x 大于 <c>Bounds.Width - IconAreaWidth</c>」，而图标本身按
    /// <see cref="ForgeMetrics.EditorTabInsetRight"/> 往里缩了 —— 两块区域只重叠一半：
    /// 压在图标左半边点不动，图标右边的空白反而能点。所以对外暴露这一个，
    /// 谁都别再自己算一遍。
    /// </remarks>
    public Rect TabIconHitRect()
    {
        var x = Bounds.Width - ForgeMetrics.EditorTabInsetRight(Palette.Island is not null) - IconSize;
        return new Rect(x, (Bounds.Height - IconSize) / 2, IconSize, IconSize).Inflate(3);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(global::Avalonia.Input.PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var hovered = (CanBeClosed || IsPinned) && TabIconHitRect().Contains(e.GetPosition(this));
        if (hovered == _iconHovered)
        {
            return;
        }

        _iconHovered = hovered;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(global::Avalonia.Input.PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _iconHovered = false;

        // 离开时无条件重画：标签页整体的悬停态（ResolveColors 里的 hovered 分支）也要跟着撤
        InvalidateVisual();
    }

    private FormattedText? MeasureText()
        => string.IsNullOrEmpty(Title)
            ? null
            : new FormattedText(
                Title,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                ResolveTypeface(TitleFontWeight),
                12,
                // 选中态**不**换强调色：上游只有 underlinedTabForeground 一项，
                // 而两个族的主题都没定义它 —— 也就是与常规文字同色。
                Foreground ?? Solid(Palette.Foreground));

    /// <inheritdoc />
    protected override void OnPointerEntered(global::Avalonia.Input.PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        InvalidateVisual();
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
