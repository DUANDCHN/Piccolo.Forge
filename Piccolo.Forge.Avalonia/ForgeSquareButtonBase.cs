using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 条带上方形按钮的共同部分：度量、状态色、悬停。
/// </summary>
/// <remarks>
/// 对应上游的 <c>AbstractSquareStripeButton</c>（<c>SquareStripeButton.kt:62-80</c>）——
/// 工具窗口按钮和「更多」按钮在上游是**兄弟**，都继承它，
/// 而不是后者继承前者。这里保持同样的层级。
/// </remarks>
public abstract class ForgeSquareButtonBase : TemplatedControl
{
    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeSquareButtonBase, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    /// <summary><see cref="IsCompact"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<ForgeSquareButtonBase, bool>(nameof(IsCompact));

    static ForgeSquareButtonBase()
    {
        AffectsRender<ForgeSquareButtonBase>(PaletteProperty, IsCompactProperty, IsLeftStripeProperty);
        // 换族会改按钮宽（40 ↔ 37），所以 Palette 也要触发重新测量，不只是重画
        AffectsMeasure<ForgeSquareButtonBase>(IsCompactProperty, PaletteProperty, IsLeftStripeProperty);
    }

    /// <summary>
    /// 背景高亮的矩形：**按钮矩形去掉图标内边距**。
    /// </summary>
    /// <remarks>
    /// 照抄 <c>SquareStripeButtonLook.kt:64-70 · paintBackground</c>——
    /// 它从组件矩形里先后减掉 <c>insets</c> 与 <c>getIconPadding()</c> 再填色。
    /// 两套主题算下来都是 30×30（expUI <c>40-5-5</c>、Islands <c>37-5-2</c>），
    /// Islands 只是把这块整体推向条带外缘。
    /// <para>
    /// 早先这里画的是**整个按钮矩形**，于是 expUI 下高亮是 40×40 而不是 30×30——
    /// 贴着相邻按钮、也贴着条带边缘，看起来比 Rider 胖一圈。
    /// </para>
    /// </remarks>
    /// <param name="cell">
    /// 按钮那一格的矩形。不显名时就是整个 <c>Bounds</c>；
    /// 显名时按钮被名字撑高，这里传的是上面那一格（图标所在的 <see cref="ButtonWidth"/> × <see cref="ButtonHeight"/>）。
    /// </param>
    /// <returns>该填色的矩形。</returns>
    public Rect BackgroundRect(Rect cell)
    {
        var padding = IconPadding;
        return new Rect(
            cell.X + padding.Left,
            cell.Y + padding.Top,
            Math.Max(0, cell.Width - padding.Horizontal),
            Math.Max(0, cell.Height - padding.Vertical));
    }

    /// <summary>建一个方形按钮。</summary>
    protected ForgeSquareButtonBase()
    {
        Focusable = true;
    }

    /// <summary>配色。</summary>
    public ForgePalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>是否紧凑模式（<c>度量与主题键.md</c> §9）。</summary>
    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    /// <summary><see cref="IsLeftStripe"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsLeftStripeProperty =
        AvaloniaProperty.Register<ForgeSquareButtonBase, bool>(nameof(IsLeftStripe), defaultValue: true);

    /// <summary>
    /// 这个按钮在左条带还是右条带。默认左。
    /// </summary>
    /// <remarks>
    /// 内边距**按左右取不同的键**（<c>JBUI.java:1343</c> 把 left/right 拼进键名）。
    /// Islands 下两侧刻意镜像不对称——靠窗口内侧留 2、外侧留 5，
    /// 设错边的结果是图标朝窗口里侧挤，而不是贴着窗口边缘。
    /// </remarks>
    public bool IsLeftStripe
    {
        get => GetValue(IsLeftStripeProperty);
        set => SetValue(IsLeftStripeProperty, value);
    }

    /// <summary>「显示名字」模式——内边距另有一套键，派生类按自己的情况覆盖。</summary>
    protected virtual bool ShowsName => false;

    /// <summary>当前该用的条带按钮度量（随配色族 × 密度）。</summary>
    public ForgeStripeButtonMetrics StripeMetrics
        => ForgeStripeButtonMetrics.ForTheme(Palette.Family, IsCompact);

    /// <summary>按钮宽：expUI 40（紧凑 32），Islands 常规档 37。</summary>
    public double ButtonWidth => StripeMetrics.Width;

    /// <summary>按钮高：40（紧凑 32）。</summary>
    public double ButtonHeight => StripeMetrics.Height;

    /// <summary>图标边长：紧凑 16，否则 20。</summary>
    public double IconSize => StripeMetrics.IconSize;

    /// <summary>图标四周 padding，按左右条带与是否显名取。</summary>
    public ForgeInsets IconPadding => StripeMetrics.IconPadding(IsLeftStripe, ShowsName);

    /// <summary>圆角：紧凑 8，否则 12。</summary>
    public double Arc => ArcOverride ?? (IsCompact ? ForgeCompactMetrics.StripeButtonArc : ForgeMetrics.StripeButtonArc);

    /// <summary>
    /// 画圆角用的**半径** = <see cref="Arc"/> 的一半。
    /// </summary>
    /// <remarks>
    /// ⚠ <see cref="Arc"/> 是上游语义的**直径**（<c>IdeaActionButtonLook.java:36-37</c> 把它当
    /// <c>RoundRectangle2D</c> 的 <c>arcw/arch</c> 用），而 Avalonia 的 <c>RoundedRect</c> 收半径。
    /// 画的时候一律用这个属性，别直接用 <see cref="Arc"/> —— 那样圆角会大一倍。
    /// <para>
    /// 名字**不叫 <c>CornerRadius</c>**：那会隐藏 <see cref="TemplatedControl"/> 的同名属性，
    /// 编译器只给个警告，而两者语义不同（那个是控件自身边框的圆角）。
    /// </para>
    /// </remarks>
    public double ArcRadius => Arc / 2;

    /// <summary>宿主指定的圆角**直径**；<see langword="null"/> 表示按主题的默认值。</summary>
    public double? ArcOverride { get; set; }

    /// <summary>
    /// 当前该用哪个背景。
    /// </summary>
    /// <remarks>
    /// 优先级照抄 <c>SquareStripeButton.kt:208-219 · getBackgroundColor</c>：
    /// **焦点 &gt; 窗口可见 &gt; 悬停 &gt; 无**。
    /// 注意「窗口可见」压过「悬停」——鼠标划过一个已经打开的工具窗口按钮时，
    /// 显示的是 pressed 背景而不是 hover 背景。
    /// </remarks>
    /// <returns>背景色；都不满足时是 <see langword="null"/>（不画）。</returns>
    public virtual Color? ResolveBackground()
        => IsPointerOver ? Palette.ButtonHoverBackground : null;

    /// <summary>当前该用哪个前景。</summary>
    /// <returns>前景色。</returns>
    public virtual Color ResolveForeground() => Palette.ButtonForeground;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(ButtonWidth, ButtonHeight);

    /// <inheritdoc />
    protected override void OnPointerEntered(global::Avalonia.Input.PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(global::Avalonia.Input.PointerEventArgs e)
    {
        base.OnPointerExited(e);
        InvalidateVisual();
    }
}
