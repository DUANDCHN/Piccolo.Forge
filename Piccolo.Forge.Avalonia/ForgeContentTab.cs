using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 标题条里的一个内容 tab。
/// </summary>
/// <remarks>
/// 对应 <c>platform/platform-impl/src/com/intellij/openapi/wm/impl/content/ContentTabLabel.java</c>
/// 与它的画法 <c>TabContentLayout.java:386-410</c> → <c>JBDefaultTabPainter.kt:62-96</c>。
/// 度量见 <c>工具窗口模型.md</c> §13.1。
/// </remarks>
public class ForgeContentTab : TabStripItem
{
    /// <summary>Creates a self-rendered tab item independent of the host theme.</summary>
    public ForgeContentTab()
    {
        Template = new global::Avalonia.Controls.Templates.FuncControlTemplate<ForgeContentTab>((_, _) => new Border());
    }
    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeContentTab, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    /// <summary><see cref="Title"/> 的属性定义。</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ForgeContentTab, string?>(nameof(Title));

    /// <summary><see cref="IsWindowActive"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsWindowActiveProperty =
        AvaloniaProperty.Register<ForgeContentTab, bool>(nameof(IsWindowActive));

    /// <summary><see cref="IsSoleContent"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsSoleContentProperty =
        AvaloniaProperty.Register<ForgeContentTab, bool>(nameof(IsSoleContent));

    /// <summary><see cref="CanBeClosed"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> CanBeClosedProperty =
        AvaloniaProperty.Register<ForgeContentTab, bool>(nameof(CanBeClosed));

    /// <summary><see cref="IsPinned"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsPinnedProperty =
        AvaloniaProperty.Register<ForgeContentTab, bool>(nameof(IsPinned));

    static ForgeContentTab()
    {
        AffectsRender<ForgeContentTab>(
            PaletteProperty, TitleProperty, IsSelectedProperty, IsWindowActiveProperty, IsSoleContentProperty);
        AffectsMeasure<ForgeContentTab>(TitleProperty, CanBeClosedProperty);
        AffectsRender<ForgeContentTab>(CanBeClosedProperty, IsPinnedProperty);
    }

    /// <summary>配色。</summary>
    public ForgePalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>tab 上的字。</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>所属工具窗口是不是活跃的（决定下划线用哪个颜色）。</summary>
    public bool IsWindowActive
    {
        get => GetValue(IsWindowActiveProperty);
        set => SetValue(IsWindowActiveProperty, value);
    }

    /// <summary>
    /// 这个工具窗口是不是只有这一块内容。
    /// </summary>
    /// <remarks>
    /// 只有内容超过一个时，选中的 tab 才换成 <c>underlinedTabForeground</c>
    /// （<c>ContentTabLabel.java:199-203</c>）。
    /// </remarks>
    public bool IsSoleContent
    {
        get => GetValue(IsSoleContentProperty);
        set => SetValue(IsSoleContentProperty, value);
    }

    /// <summary>这块内容能不能关（决定右边那个图标出不出现）。</summary>
    /// <remarks>条件是「内容可关 **且** 工具窗口允许关内容」（<c>ContentTabLabel.java:169-171</c>）。</remarks>
    public bool CanBeClosed
    {
        get => GetValue(CanBeClosedProperty);
        set => SetValue(CanBeClosedProperty, value);
    }

    /// <summary>是不是被固定住了。</summary>
    /// <remarks>
    /// **固定住时那个位置画的是图钉而不是叉**，点它是解固定
    /// （<c>ContentTabLabel.java:157-159</c>、<c>:250-254</c>）。
    /// </remarks>
    public bool IsPinned
    {
        get => GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    /// <summary>右边那个图标占的宽度：图标 + 间距 3 + 右缘 4。</summary>
    /// <remarks>对应 <c>ContentLabel.java:252-257</c>。</remarks>
    public double IconAreaWidth
        => CanBeClosed ? IconSize + ForgeMetrics.TabIconsGap + ForgeMetrics.TabRightIconInset : 0;

    /// <summary>右边那个小图标的边长。</summary>
    private static double IconSize => 12;

    /// <summary>左右内边距 12（<c>ToolWindow.HeaderTab.leftRightInsets</c>）。</summary>
    public static double Inset => ForgeMetrics.HeaderTabInset;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var text = MeasureText();
        var width = Math.Min(
            ForgeMetrics.TabMaxWidth,
            (text?.Width ?? 0) + (Inset * 2) + IconAreaWidth);

        return new Size(width, double.IsInfinity(availableSize.Height) ? ForgeMetrics.HeaderHeight : availableSize.Height);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        // 整块都能点，不然只有画了东西的地方收得到鼠标
        ForgeHitArea.Fill(context, Bounds.Size);

        if (IsPointerOver && !IsSelected)
        {
            context.FillRectangle(new SolidColorBrush(Palette.HeaderTabHoverBackground), new Rect(Bounds.Size));
        }

        if (IsSelected)
        {
            RenderUnderline(context);
        }

        if (MeasureText() is { } text)
        {
            context.DrawText(text, new Point(Inset, (Bounds.Height - text.Height) / 2));
        }

        if (CanBeClosed)
        {
            RenderTabIcon(context);
        }
    }

    /// <summary>
    /// 右边那个图标：固定住画图钉，否则画叉。
    /// </summary>
    /// <remarks>位置对应 <c>ContentLabel.java:252-257</c>：起点是 <c>宽度 - 4 - 图标宽</c>。</remarks>
    private void RenderTabIcon(DrawingContext context)
    {
        var brush = new SolidColorBrush(Palette.ButtonForeground);
        var x = Bounds.Width - ForgeMetrics.TabRightIconInset - IconSize;
        var rect = new Rect(x, (Bounds.Height - IconSize) / 2, IconSize, IconSize);

        // 固定住的画图钉而不是叉（ContentTabLabel.java:157-159）
        ForgeIcons.Draw(context, IsPinned ? ForgeIcons.Pin : ForgeIcons.Close, rect, brush, filled: IsPinned);
    }

    /// <summary>
    /// 下划线的矩形。
    /// </summary>
    /// <remarks>
    /// 对应 <c>JBDefaultTabPainter.kt:92-96 · underlineRectangle</c>：
    /// <c>(x, y + height - thickness, width, thickness)</c>。
    /// 工具窗口这一版还会把整体下移 1 并相应减高（<c>ToolWindowTabPainter.kt:35-36</c>），
    /// 所以底边实际上抬高了 1。
    /// </remarks>
    /// <param name="bounds">tab 的范围。</param>
    /// <returns>下划线矩形。</returns>
    public static Rect ComputeUnderlineRect(Rect bounds)
    {
        var border = ForgeMetrics.TabBorderThickness;
        var height = bounds.Height - border;
        var thickness = ForgeMetrics.HeaderTabUnderlineHeight;

        return new Rect(bounds.X, bounds.Y + border + height - thickness, bounds.Width, thickness);
    }

    private void RenderUnderline(DrawingContext context)
    {
        var color = IsWindowActive ? Palette.HeaderTabUnderline : Palette.HeaderTabInactiveUnderline;
        var rect = ComputeUnderlineRect(new Rect(Bounds.Size));

        // arc > 0 就画圆角矩形（JBDefaultTabPainter.kt:81-85）。
        // ⚠ 主题里的 arc 是**直径**，画的时候折半 —— 见 ForgeMetrics.StripeButtonArc 的说明。
        context.DrawRectangle(
            new SolidColorBrush(color),
            null,
            new RoundedRect(rect, ForgeMetrics.HeaderTabUnderlineArc / 2));
    }

    private FormattedText? MeasureText()
    {
        if (string.IsNullOrEmpty(Title))
        {
            return null;
        }

        // 单块内容时选中态不换前景（ContentTabLabel.java:199-203）
        var color = IsSelected && !IsSoleContent ? Palette.HeaderTabUnderline : Palette.Foreground;

        return new FormattedText(
            Title,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            12,
            new SolidColorBrush(color));
    }

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
