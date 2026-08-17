using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Piccolo.Forge.Model;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 一根条带：上下两组按钮，外加一条内侧边框。
/// </summary>
/// <remarks>
/// 对应 <c>platform/platform-impl/src/com/intellij/toolWindow/ToolWindowToolbar.kt</c>
/// 及其左右两个子类：
/// <list type="bullet">
/// <item>上组在 NORTH、下组在 SOUTH（<c>ToolWindowToolbar.kt:71-75</c>）；</item>
/// <item>左条带右侧 1px 线、右条带左侧 1px 线
/// （<c>ToolWindowLeftToolbar.kt:28</c>、<c>ToolWindowRightToolbar.kt:27</c>）；</item>
/// <item>背景 `ToolWindow.Stripe.background`（<c>ToolWindowToolbar.kt:57</c>）。</item>
/// </list>
/// 落位规则不在这里，在 <see cref="StripePlacementResolver"/>。
/// </remarks>
public class ForgeToolWindowToolbar : TemplatedControl
{
    private readonly StripeItemsPanel _topGroup = new();
    private readonly StripeItemsPanel _bottomGroup = new();
    private readonly DockPanel _root = new() { LastChildFill = false };

    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeToolWindowToolbar, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    /// <summary><see cref="Side"/> 的属性定义。</summary>
    public static readonly StyledProperty<StripeSide> SideProperty =
        AvaloniaProperty.Register<ForgeToolWindowToolbar, StripeSide>(nameof(Side));

    /// <summary><see cref="CustomWidth"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> CustomWidthProperty =
        AvaloniaProperty.Register<ForgeToolWindowToolbar, double>(nameof(CustomWidth));

    /// <summary><see cref="TransparentBackdrop"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> TransparentBackdropProperty =
        AvaloniaProperty.Register<ForgeToolWindowToolbar, bool>(nameof(TransparentBackdrop));

    /// <summary>
    /// 不画条带底，让窗口背后的东西透上来。由 <see cref="ForgeControl"/> 下发，别单独设。
    /// </summary>
    public bool TransparentBackdrop
    {
        get => GetValue(TransparentBackdropProperty);
        set => SetValue(TransparentBackdropProperty, value);
    }

    static ForgeToolWindowToolbar()
    {
        AffectsRender<ForgeToolWindowToolbar>(
            PaletteProperty, SideProperty, TransparentBackdropProperty,
            StripeBackgroundBrushProperty, StripeBorderBrushProperty);

        // 族写进伪类，ControlTheme 据此换一套颜色键（REQ-067）
        PaletteProperty.Changed.AddClassHandler<ForgeToolWindowToolbar>(
            (x, _) => x.PseudoClasses.Set(":islands", x.Palette.Island is not null));
        AffectsMeasure<ForgeToolWindowToolbar>(CustomWidthProperty);
    }

    /// <summary>建一根条带。</summary>
    public ForgeToolWindowToolbar()
    {
        DockPanel.SetDock(_topGroup, Dock.Top);
        DockPanel.SetDock(_bottomGroup, Dock.Bottom);
        _root.Children.Add(_topGroup);
        _root.Children.Add(_bottomGroup);
        VisualChildren.Add(_root);
        LogicalChildren.Add(_root);
    }

    /// <summary>配色。</summary>
    public ForgePalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>这是左条带还是右条带。</summary>
    public StripeSide Side
    {
        get => GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    /// <summary>
    /// 条带的自定义宽度；<c>0</c> 表示按按钮自身尺寸走。
    /// </summary>
    /// <remarks>
    /// 打开「显示名字」时是 <see cref="ForgeMetrics.StripeNamedDefaultWidth"/>=59
    /// （<c>ResizeStripeManager.kt:215-218</c>），可拖动，夹在
    /// [<see cref="ForgeMetrics.StripeMinWidth"/>, <see cref="ForgeMetrics.StripeMaxWidth"/>] 之内
    /// （<c>:138-146</c>）。
    /// </remarks>
    public double CustomWidth
    {
        get => GetValue(CustomWidthProperty);
        set => SetValue(CustomWidthProperty, value);
    }

    /// <summary>
    /// 把宽度夹到上游允许的范围。
    /// </summary>
    /// <remarks>对应 <c>ResizeStripeManager.kt:138-149 · checkMinMax</c>。</remarks>
    /// <param name="width">想要的宽度。</param>
    /// <param name="isCompact">是否紧凑模式。</param>
    /// <returns>夹住之后的宽度。</returns>
    public static double ClampCustomWidth(double width, bool isCompact = false)
    {
        var min = isCompact ? ForgeCompactMetrics.StripeMinWidth : ForgeMetrics.StripeMinWidth;
        return Math.Clamp(width, min, ForgeMetrics.StripeMaxWidth);
    }

    /// <summary>上组：放 LEFT / RIGHT 的按钮。</summary>
    public StripeItemsPanel TopGroup => _topGroup;

    /// <summary>下组：放 BOTTOM 的按钮。</summary>
    public StripeItemsPanel BottomGroup => _bottomGroup;

    /// <summary>
    /// 这根条带上有没有任何按钮。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowToolbar.kt:151 · hasButtons</c>。
    /// 一根没有可见按钮的条带整根隐藏
    /// （<c>ToolWindowPaneNewButtonManager.kt:76-99 · updateToolStripesVisibility</c>）。
    /// </remarks>
    public bool HasButtons => _topGroup.Children.Count > 0 || _bottomGroup.Children.Count > 0;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        _root.Measure(availableSize);
        var size = _root.DesiredSize;

        // 内侧那条 1px 线占宽
        var width = CustomWidth > 0 ? CustomWidth : size.Width + 1;
        return new Size(width, size.Height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var contentWidth = Math.Max(0, finalSize.Width - 1);
        var x = Side == StripeSide.Left ? 0 : 1;
        _root.Arrange(new Rect(x, 0, contentWidth, finalSize.Height));
        return finalSize;
    }

    /// <summary><see cref="StripeBackgroundBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> StripeBackgroundBrushProperty =
        AvaloniaProperty.Register<ForgeToolWindowToolbar, IBrush?>(nameof(StripeBackgroundBrush));

    /// <summary><see cref="StripeBorderBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> StripeBorderBrushProperty =
        AvaloniaProperty.Register<ForgeToolWindowToolbar, IBrush?>(nameof(StripeBorderBrush));

    /// <summary>条带的底色，由 <c>ControlTheme</c> 从资源键给（REQ-067）。</summary>
    /// <remarks>
    /// ⚠ <b>这个控件不套模板</b>：它自己往 <c>VisualChildren</c> 里挂条带分组
    /// （<c>_root</c>），而 <c>ApplyTemplate</c> 会把 <c>VisualChildren</c> 清空 ——
    /// 给它一份模板等于把两组按钮扫掉。所以走"画笔属性由主题给、绘制仍自己来"这条：
    /// 对宿主而言用法一样（重定义同名键、明暗照常跟随），只是库内部不经模板。
    /// </remarks>
    public IBrush? StripeBackgroundBrush
    {
        get => GetValue(StripeBackgroundBrushProperty);
        set => SetValue(StripeBackgroundBrushProperty, value);
    }

    /// <summary>条带边缘那条 1px 线的颜色，来源同 <see cref="StripeBackgroundBrush"/>。</summary>
    public IBrush? StripeBorderBrush
    {
        get => GetValue(StripeBorderBrushProperty);
        set => SetValue(StripeBorderBrushProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        // 透底时条带底也不画 —— 它和停靠区底是同一片「海」，只画一半会出现一条硬边。
        // 仍要 Fill 而不是不画：条带空白处有右键菜单，不铺就收不到鼠标。
        // 兜底回配色：控件还没进树时拿不到主题（见 ThemeResourceSpikeTests）。
        var background = TransparentBackdrop
            ? Brushes.Transparent
            : StripeBackgroundBrush ?? new SolidColorBrush(Palette.StripeBackground);
        context.FillRectangle(background, new Rect(Bounds.Size));

        // 左条带的线在右缘，右条带的线在左缘
        var x = Side == StripeSide.Left ? Bounds.Width - 1 : 0;
        context.FillRectangle(
            StripeBorderBrush ?? new SolidColorBrush(Palette.StripeBorder),
            new Rect(x, 0, 1, Bounds.Height));
    }
}
