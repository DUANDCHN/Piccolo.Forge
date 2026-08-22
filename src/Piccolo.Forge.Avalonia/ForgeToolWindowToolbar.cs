using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
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

        AffectsMeasure<ForgeToolWindowToolbar>(CustomWidthProperty, BorderOverlaysContentProperty);
    }

    /// <summary>建一根条带。</summary>
    public ForgeToolWindowToolbar()
    {
        DockPanel.SetDock(_topGroup, Dock.Top);
        DockPanel.SetDock(_bottomGroup, Dock.Bottom);
        _root.Children.Add(_topGroup);
        _root.Children.Add(_bottomGroup);

        // ⚠ 两组按钮**不再**挂在这个控件的 Visual/LogicalChildren 上（REQ-067 模板化）：
        //   ApplyTemplate 会把 VisualChildren 清空，挂在这儿等于每次套模板被扫一次；
        //   而 LogicalChildren 也不能留 —— 槽（Decorator.Child）自己会认领逻辑父，
        //   两处都认就当场抛「The Control already has a parent」。
        //   它们改为放进模板里那个槽（PART_Groups），见 OnApplyTemplate。
    }

    /// <inheritdoc />
    /// <remarks>
    /// 把两组按钮塞进模板的槽里。
    /// <para>
    /// ⚠ <b>先摘再挂</b>：一个控件只能有一个父，重新套模板时上一份模板里的槽还攥着 <c>_root</c>，
    /// 不摘就当场抛「already has a visual parent」。
    /// </para>
    /// </remarks>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_root.GetVisualParent() is Decorator previous)
        {
            previous.Child = null;
        }

        if (e.NameScope.Find<Decorator>("PART_Groups") is { } slot)
        {
            slot.Child = _root;
        }
    }

    /// <summary><see cref="BorderOverlaysContent"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> BorderOverlaysContentProperty =
        AvaloniaProperty.Register<ForgeToolWindowToolbar, bool>(nameof(BorderOverlaysContent));

    /// <summary>
    /// 内侧那条 1px 线**压在内容上**，不再单独占宽。
    /// </summary>
    /// <remarks>
    /// 默认 <see langword="false"/>（照上游：线占 1px，条带 = 按钮 + 1）。
    /// <para>
    /// ★ 宿主自己定了条带宽度时要打开它，否则两件事对不上：
    /// 总宽会变成"给的宽 + 1"，而且**图标在总宽里落在半像素上** ——
    /// 按钮在内容区里居中是整数，可总宽比内容多 1，取整后左右就差 1px（实机看出来的"偏左"）。
    /// 线压在内容上之后，按钮就是整条条带宽，图标居中即整数。
    /// 线本身画在条带边缘，而图标离边缘还有七八个像素，压不到任何东西。
    /// </para>
    /// </remarks>
    public bool BorderOverlaysContent
    {
        get => GetValue(BorderOverlaysContentProperty);
        set => SetValue(BorderOverlaysContentProperty, value);
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
    /// <remarks>
    /// 算式没变，只是**换了谁在算**（REQ-067）：内侧那条 1px 线占的宽，
    /// 现在由模板里那个槽的 <c>Margin</c> 表达（左条带右让 1、右条带左让 1），
    /// 于是模板根量出来的就已经是"两组按钮 + 1"。
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return new Size(CustomWidth > 0 ? CustomWidth : size.Width, size.Height);
    }

    /// <summary><see cref="StripeBackgroundBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> StripeBackgroundBrushProperty =
        AvaloniaProperty.Register<ForgeToolWindowToolbar, IBrush?>(nameof(StripeBackgroundBrush));

    /// <summary><see cref="StripeBorderBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> StripeBorderBrushProperty =
        AvaloniaProperty.Register<ForgeToolWindowToolbar, IBrush?>(nameof(StripeBorderBrush));

    /// <summary>条带的底色，由 <c>ControlTheme</c> 从资源键给（REQ-067）。</summary>
    /// <remarks>
    /// ★ 这个控件**已经是真模板**：两组按钮住在模板里那个槽（<c>PART_Groups</c>）中，
    /// 由 <see cref="OnApplyTemplate"/> 塞进去 —— 这样 <c>ApplyTemplate</c> 清空
    /// <c>VisualChildren</c> 就不再是问题（早先正是这条把它挡在模板之外）。
    /// 底与那条 1px 线都是模板里的 <c>Border</c>，宿主重写模板即可改形状。
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

    /// <summary>透底时不画条带底 —— 写成伪类，模板据此把那层 <c>Border</c> 收起来。</summary>
    /// <remarks>
    /// 它和停靠区底是同一片「海」，只画一半会出现一条硬边。
    /// ⚠ <b>收起来的只是底色那层</b>：模板根仍是 <c>Background="Transparent"</c> 的 <c>Panel</c>，
    /// 否则条带空白处收不到鼠标（那里有右键菜单）。
    /// </remarks>
    /// <param name="change">属性变化。</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TransparentBackdropProperty)
        {
            PseudoClasses.Set(":transparent-backdrop", change.GetNewValue<bool>());
        }

        if (change.Property == BorderOverlaysContentProperty)
        {
            PseudoClasses.Set(":border-overlay", change.GetNewValue<bool>());
        }

        if (change.Property == SideProperty)
        {
            // 左条带的线在右缘，右条带的线在左缘；槽也跟着往另一边让出那 1px
            PseudoClasses.Set(":right-side", change.GetNewValue<StripeSide>() == StripeSide.Right);
        }
    }
}
