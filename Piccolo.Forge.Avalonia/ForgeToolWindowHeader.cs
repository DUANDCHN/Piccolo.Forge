using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
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

    private readonly ForgeContentTabStrip _tabs = new();

    /// <summary>建一个标题条。</summary>
    public ForgeToolWindowHeader()
    {
        VisualChildren.Add(_tabs);
        LogicalChildren.Add(_tabs);
    }

    static ForgeToolWindowHeader()
    {
        AffectsRender<ForgeToolWindowHeader>(PaletteProperty, TitleProperty, IsActiveProperty);
        AffectsMeasure<ForgeToolWindowHeader>(IsCompactProperty);
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

    /// <summary>标题条高度：紧凑 31，否则 41。</summary>
    public double HeaderHeight => IsCompact ? ForgeCompactMetrics.HeaderHeight : ForgeMetrics.HeaderHeight;

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

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        _tabs.Measure(availableSize);
        return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, HeaderHeight);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = ComputeTabsStart();
        _tabs.Arrange(new Rect(x, 0, Math.Max(0, finalSize.Width - x), finalSize.Height));
        return finalSize;
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
