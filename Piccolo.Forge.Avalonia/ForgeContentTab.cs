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
    // ⚠ 这里曾经在构造里赋 Template（一个空 Border，只为盖掉 TabStripItem 自带的模板）。
    //   那是 local value，**会盖过 ControlTheme 里的模板**，症状是"主题写了一点不生效"且不报错。
    //   模板现在写在 Themes/ForgeContentTab.axaml 的 ControlTheme 里（REQ-067）。
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
        AffectsMeasure<ForgeContentTab>(TitleProperty, CanBeClosedProperty, TitleFontSizeProperty, IconSizeProperty);

        // ⚠ 这里原来还有一串 AffectsRender（Palette / Title / IsSelected / …）。
        //   现在 Render 只剩铺命中区，画面全由模板子元素出，重画由**伪类变化触发样式重算**驱动，
        //   那串已经不起作用（REQ-067）。
        //
        // 状态与族写进伪类，模板据此选色 / 决定显不显示。
        // 用伪类而不是样式类：它们是控件自己的状态，不该让宿主用 Classes 去改。
        // （`:selected` / `:pointerover` 由 TabStripItem 与 InputElement 自带，不必在这里设。）
        IsWindowActiveProperty.Changed.AddClassHandler<ForgeContentTab>(
            (x, e) => x.PseudoClasses.Set(":window-active", e.GetNewValue<bool>()));
        IsSoleContentProperty.Changed.AddClassHandler<ForgeContentTab>(
            (x, e) => x.PseudoClasses.Set(":sole-content", e.GetNewValue<bool>()));
        CanBeClosedProperty.Changed.AddClassHandler<ForgeContentTab>(
            (x, e) => x.PseudoClasses.Set(":closeable", e.GetNewValue<bool>()));
        IsPinnedProperty.Changed.AddClassHandler<ForgeContentTab>(
            (x, e) => x.PseudoClasses.Set(":pinned", e.GetNewValue<bool>()));
        PaletteProperty.Changed.AddClassHandler<ForgeContentTab>(
            (x, _) => x.PseudoClasses.Set(":islands", x.Palette.Island is not null));
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

    /// <summary>左右内边距 12（<c>ToolWindow.HeaderTab.leftRightInsets</c>）。</summary>
    public static double Inset => ForgeMetrics.HeaderTabInset;

    // ── 模板要用的派生值（REQ-067）──
    // 全部做成 Avalonia 属性：TemplateBinding **只认 Avalonia 属性**，普通 C# 计算属性
    // 绑不了（编译期 AVLN3000）。默认值就是原先写死在 Render 里的那些度量，
    // 所以「一个键都不覆盖时长相不变」由默认值本身保证，不依赖任何同步方法先跑过。

    /// <summary><see cref="TitleFontSize"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> TitleFontSizeProperty =
        AvaloniaProperty.Register<ForgeContentTab, double>(nameof(TitleFontSize), 12);

    /// <summary><see cref="TitleMargin"/> 的属性定义。</summary>
    public static readonly StyledProperty<Thickness> TitleMarginProperty =
        AvaloniaProperty.Register<ForgeContentTab, Thickness>(
            nameof(TitleMargin), new Thickness(ForgeMetrics.HeaderTabInset, 0, 0, 0));

    /// <summary><see cref="IconSize"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<ForgeContentTab, double>(nameof(IconSize), 12);

    /// <summary><see cref="IconMargin"/> 的属性定义。</summary>
    public static readonly StyledProperty<Thickness> IconMarginProperty =
        AvaloniaProperty.Register<ForgeContentTab, Thickness>(
            nameof(IconMargin), new Thickness(0, 0, ForgeMetrics.TabRightIconInset, 0));

    /// <summary><see cref="UnderlineMargin"/> 的属性定义。</summary>
    public static readonly StyledProperty<Thickness> UnderlineMarginProperty =
        AvaloniaProperty.Register<ForgeContentTab, Thickness>(nameof(UnderlineMargin));

    /// <summary><see cref="UnderlineHeight"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> UnderlineHeightProperty =
        AvaloniaProperty.Register<ForgeContentTab, double>(
            nameof(UnderlineHeight), ForgeMetrics.HeaderTabUnderlineHeight);

    /// <summary><see cref="UnderlineCornerRadius"/> 的属性定义。</summary>
    public static readonly StyledProperty<CornerRadius> UnderlineCornerRadiusProperty =
        AvaloniaProperty.Register<ForgeContentTab, CornerRadius>(
            nameof(UnderlineCornerRadius), new CornerRadius(ForgeMetrics.HeaderTabUnderlineArc / 2));

    /// <summary>标题字号。</summary>
    /// <remarks>
    /// 12 是原先自绘时写死的值。它同时喂给模板里那个 <c>TextBlock</c> 和
    /// <see cref="MeasureTitle"/> —— 两处必须是同一个来源，否则量出来的宽度与画出来的字不一致。
    /// </remarks>
    public double TitleFontSize
    {
        get => GetValue(TitleFontSizeProperty);
        set => SetValue(TitleFontSizeProperty, value);
    }

    /// <summary>标题相对 tab 的位置：左边留 <see cref="Inset"/>，竖直居中。</summary>
    /// <remarks>
    /// 只给左内边距、<b>右边不设界</b> —— 照抄原先 <c>DrawText(text, new Point(Inset, …))</c>：
    /// 那时文字也没有右边界，长标题是直接压过图标区的（宽度上限由
    /// <see cref="ForgeMetrics.TabMaxWidth"/> 在测量时兜着）。
    /// </remarks>
    public Thickness TitleMargin
    {
        get => GetValue(TitleMarginProperty);
        set => SetValue(TitleMarginProperty, value);
    }

    /// <summary>右边那个小图标的边长。</summary>
    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>右边那个小图标的位置：贴右缘 <see cref="ForgeMetrics.TabRightIconInset"/>，竖直居中。</summary>
    /// <remarks>对应 <c>ContentLabel.java:252-257</c>：起点是 <c>宽度 - 4 - 图标宽</c>。</remarks>
    public Thickness IconMargin
    {
        get => GetValue(IconMarginProperty);
        set => SetValue(IconMarginProperty, value);
    }

    /// <summary>
    /// 下划线在 tab 里的位置 —— 模板里的 <c>Border</c> 拿它当 <c>Margin</c>。
    /// </summary>
    /// <remarks>
    /// 值仍出自 <see cref="ComputeUnderlineRect"/>（<b>出处注释在那里，别在这里另算一份</b>），
    /// 由 <see cref="ArrangeOverride"/> 在知道最终尺寸之后刷进来。
    /// </remarks>
    public Thickness UnderlineMargin
    {
        get => GetValue(UnderlineMarginProperty);
        private set => SetValue(UnderlineMarginProperty, value);
    }

    /// <summary>下划线的粗细，与 <see cref="ComputeUnderlineRect"/> 同一个度量。</summary>
    public double UnderlineHeight
    {
        get => GetValue(UnderlineHeightProperty);
        set => SetValue(UnderlineHeightProperty, value);
    }

    /// <summary>下划线的圆角。</summary>
    /// <remarks>
    /// ⚠ 主题里的 arc（<see cref="ForgeMetrics.HeaderTabUnderlineArc"/> = 4）是<b>直径</b>，
    /// 给 Avalonia 要折半 —— 见 <see cref="ForgeMetrics.StripeButtonArc"/> 的说明。
    /// </remarks>
    public CornerRadius UnderlineCornerRadius
    {
        get => GetValue(UnderlineCornerRadiusProperty);
        set => SetValue(UnderlineCornerRadiusProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var text = MeasureTitle();
        var width = Math.Min(
            ForgeMetrics.TabMaxWidth,
            (text?.Width ?? 0) + (Inset * 2) + IconAreaWidth);

        return new Size(width, double.IsInfinity(availableSize.Height) ? ForgeMetrics.HeaderHeight : availableSize.Height);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 下划线的位置要等到知道最终尺寸才算得出来（它贴着底边），所以在这里刷
    /// —— 与 <see cref="ForgeStripeSeparator"/> 那条线同一个做法。
    /// </remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var underline = ComputeUnderlineRect(new Rect(finalSize));
        UnderlineMargin = new Thickness(underline.X, underline.Y, 0, 0);

        return base.ArrangeOverride(finalSize);
    }

    /// <inheritdoc />
    /// <remarks>
    /// ★ 只剩铺命中区（REQ-067）：悬停底、下划线、标题、图标全部搬进了模板
    /// （<c>Themes/ForgeContentTab.axaml</c> 里的 <c>ControlTheme</c>）。
    /// <para>
    /// <b>必须一次搬干净</b>：控件自身的 <c>Render</c> 画在**模板子元素之下**，
    /// 只要还有一样东西留在这里画，模板里就不能放任何不透明的元素（放了就盖住它）
    /// —— 条带按钮那次是实机撞出来的，症状是选中的按钮只剩图标、名字没了。
    /// </para>
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        // 整块都能点，不然只有画了东西的地方收得到鼠标
        ForgeHitArea.Fill(context, Bounds.Size);
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

    /// <summary><see cref="HoverBackgroundBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> HoverBackgroundBrushProperty =
        AvaloniaProperty.Register<ForgeContentTab, IBrush?>(nameof(HoverBackgroundBrush));

    /// <summary><see cref="UnderlineBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> UnderlineBrushProperty =
        AvaloniaProperty.Register<ForgeContentTab, IBrush?>(nameof(UnderlineBrush));

    /// <summary><see cref="InactiveUnderlineBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> InactiveUnderlineBrushProperty =
        AvaloniaProperty.Register<ForgeContentTab, IBrush?>(nameof(InactiveUnderlineBrush));

    /// <summary><see cref="IconBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> IconBrushProperty =
        AvaloniaProperty.Register<ForgeContentTab, IBrush?>(nameof(IconBrush));

    /// <summary>
    /// 右边那个关闭 / 图钉图标的颜色。
    /// </summary>
    /// <remarks>
    /// ★ 与标题分开的原因：它们本来就是**两个颜色** ——
    /// 图标走 <c>ToolWindow.Button.foreground</c>，标题走主题的 <c>foreground</c>
    /// （选中且不止一块内容时还要换成下划线那档，<c>ContentTabLabel.java:199-203</c>）。
    /// <para>
    /// 所以 <see cref="TemplatedControl.Foreground"/> 留给**标题**（模板里的 <c>TextBlock</c>
    /// 本来就继承它，这也是 <c>Foreground</c> 该有的含义），图标另给这一个。
    /// 模板化之前 <c>Foreground</c> 是喂给图标的 —— 那时标题的颜色根本没经过主题，
    /// 是直接从 <see cref="Palette"/> 取的。
    /// </para>
    /// </remarks>
    public IBrush? IconBrush
    {
        get => GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    /// <summary>悬停底，由 <c>ControlTheme</c> 从资源键给（REQ-067）。</summary>
    public IBrush? HoverBackgroundBrush
    {
        get => GetValue(HoverBackgroundBrushProperty);
        set => SetValue(HoverBackgroundBrushProperty, value);
    }

    /// <summary>选中下划线（窗口活动时）。</summary>
    public IBrush? UnderlineBrush
    {
        get => GetValue(UnderlineBrushProperty);
        set => SetValue(UnderlineBrushProperty, value);
    }

    /// <summary>选中下划线（窗口失焦时）。</summary>
    public IBrush? InactiveUnderlineBrush
    {
        get => GetValue(InactiveUnderlineBrushProperty);
        set => SetValue(InactiveUnderlineBrushProperty, value);
    }

    /// <summary>
    /// 量标题有多宽 —— <b>只为测量</b>，画是模板里那个 <c>TextBlock</c> 的事。
    /// </summary>
    /// <remarks>
    /// tab 的宽度是<b>按标题实际宽度</b>算的（<c>ContentLabel.java:265-269</c>），
    /// 所以 <see cref="MeasureOverride"/> 必须有一个"这行字多宽"的答案。
    /// 不能改成去问模板里那个 <c>TextBlock</c>：测量可能发生在模板套上之前
    /// （控件刚建好、还没进可视树），那时问到的是 0，tab 会全部缩成两个内边距宽。
    /// <para>
    /// 字形与字号必须与模板里那个 <c>TextBlock</c> 同源（<see cref="ResolveTypeface"/> /
    /// <see cref="TitleFontSize"/>），否则量出来的和画出来的对不上。
    /// 颜色在这里无所谓（量宽度用不到），给个透明的占位。
    /// </para>
    /// </remarks>
    /// <returns>标题的排版结果；没有标题时是 <see langword="null"/>。</returns>
    private FormattedText? MeasureTitle()
    {
        if (string.IsNullOrEmpty(Title))
        {
            return null;
        }

        return new FormattedText(
            Title,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            ResolveTypeface(),
            TitleFontSize,
            Brushes.Transparent);
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
