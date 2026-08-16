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
    /// <summary>Creates a self-rendered tab item independent of the host theme.</summary>
    public ForgeEditorTab()
    {
        Template = new global::Avalonia.Controls.Templates.FuncControlTemplate<ForgeEditorTab>((_, _) => new Border());
    }

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

    static ForgeEditorTab()
    {
        AffectsRender<ForgeEditorTab>(
            PaletteProperty, TitleProperty, IsSelectedProperty, IsGroupActiveProperty,
            IsPinnedProperty, CanBeClosedProperty, IconProperty, IsCompactProperty);
        AffectsMeasure<ForgeEditorTab>(TitleProperty, CanBeClosedProperty, IconProperty);
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

        return new Size(width, ForgeMetrics.EditorTabRowHeight(Palette.Island is not null));
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
    private (Color? Fill, Color? Border) ResolveColors()
    {
        if (IsSelected)
        {
            if (IsGroupActive)
            {
                return (Palette.EditorTabSelectedBackground, Palette.EditorTabSelectedBorder);
            }

            return IsPointerOver
                ? (Palette.EditorTabHoverBackground, Palette.EditorTabInactiveSelectedBorder)
                : (Palette.EditorTabInactiveSelectedBackground, Palette.EditorTabInactiveSelectedBorder);
        }

        // 非选中：只有悬停时有底，常规态两族都没定义（regularBackground/BorderColor 为空）
        return IsPointerOver ? (Palette.EditorTabHoverBackground, (Color?)null) : (null, null);
    }

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
        var (fill, border) = ResolveColors();
        if (fill is null && border is null)
        {
            return;
        }

        var brush = fill is { } f ? new SolidColorBrush(f) : null;
        var pen = border is { } b ? new Pen(new SolidColorBrush(b), 1) : null;

        if (Palette.Island is null)
        {
            context.DrawRectangle(brush, pen, new Rect(Bounds.Size));

            if (IsSelected)
            {
                var color = IsGroupActive ? Palette.HeaderTabUnderline : Palette.HeaderTabInactiveUnderline;
                var thickness = ForgeMetrics.EditorTabUnderlineHeight;
                context.DrawRectangle(
                    new SolidColorBrush(color),
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
            ForgeIcons.Draw(context, geometry, LeadingIconRect(Bounds.Size), new SolidColorBrush(Palette.Foreground));
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

        if (_iconControl is not null)
        {
            VisualChildren.Remove(_iconControl);
            LogicalChildren.Remove(_iconControl);
        }

        _iconControl = control;

        if (_iconControl is not null)
        {
            VisualChildren.Add(_iconControl);
            LogicalChildren.Add(_iconControl);
        }

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
        var brush = new SolidColorBrush(Palette.ButtonForeground);
        var x = Bounds.Width - ForgeMetrics.EditorTabInsetRight(Palette.Island is not null) - IconSize;
        var rect = new Rect(x, (Bounds.Height - IconSize) / 2, IconSize, IconSize);

        if (_iconHovered)
        {
            // 底比图标大一圈，四周各留 3，圆角取条带按钮那套的一半（同为小方按钮）
            var pad = 3;
            context.DrawRectangle(
                new SolidColorBrush(Palette.ButtonHoverBackground),
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
                Typeface.Default,
                12,
                // 选中态**不**换强调色：上游只有 underlinedTabForeground 一项，
                // 而两个族的主题都没定义它 —— 也就是与常规文字同色。
                new SolidColorBrush(Palette.Foreground));

    /// <inheritdoc />
    protected override void OnPointerEntered(global::Avalonia.Input.PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        InvalidateVisual();
    }
}
