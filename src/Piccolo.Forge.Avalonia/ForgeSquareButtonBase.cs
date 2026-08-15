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
        AffectsRender<ForgeSquareButtonBase>(PaletteProperty, IsCompactProperty);
        AffectsMeasure<ForgeSquareButtonBase>(IsCompactProperty);
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

    /// <summary>按钮边长：紧凑 32，否则 40。</summary>
    public double ButtonSize => IsCompact ? ForgeCompactMetrics.StripeButtonSize : ForgeMetrics.StripeButtonSize;

    /// <summary>图标边长：紧凑 16，否则 20。</summary>
    public double IconSize => IsCompact ? ForgeCompactMetrics.StripeButtonIconSize : ForgeMetrics.StripeButtonIconSize;

    /// <summary>图标四周 padding：紧凑 4，否则 5。</summary>
    public double IconPadding
        => IsCompact ? ForgeCompactMetrics.StripeButtonIconPadding : ForgeMetrics.StripeButtonIconPadding;

    /// <summary>圆角：紧凑 8，否则 12。</summary>
    public double Arc => IsCompact ? ForgeCompactMetrics.StripeButtonArc : ForgeMetrics.StripeButtonArc;

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
    protected override Size MeasureOverride(Size availableSize) => new(ButtonSize, ButtonSize);

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
