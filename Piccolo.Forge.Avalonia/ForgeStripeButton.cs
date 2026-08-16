using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Piccolo.Forge.Model;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 条带上的一个工具窗口按钮。
/// </summary>
/// <remarks>
/// 度量对应 <c>JBUI.CurrentTheme.Toolbar</c> 那一族（<c>度量与主题键.md</c> §2）：
/// 40×40、图标 20、图标四周 padding 5、圆角 12（紧凑 8）。
/// 背景优先级照抄 <c>SquareStripeButton.kt:208-219 · getBackgroundColor</c>。
/// </remarks>
public class ForgeStripeButton : ForgeSquareButtonBase
{
    /// <summary><see cref="IsToolWindowVisible"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsToolWindowVisibleProperty =
        AvaloniaProperty.Register<ForgeStripeButton, bool>(nameof(IsToolWindowVisible));

    /// <summary><see cref="IsToolWindowActive"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsToolWindowActiveProperty =
        AvaloniaProperty.Register<ForgeStripeButton, bool>(nameof(IsToolWindowActive));

    /// <summary><see cref="Text"/> 的属性定义。</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ForgeStripeButton, string?>(nameof(Text));

    /// <summary><see cref="Icon"/> 的属性定义。</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<ForgeStripeButton, object?>(nameof(Icon));

    /// <summary><see cref="ShowName"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> ShowNameProperty =
        AvaloniaProperty.Register<ForgeStripeButton, bool>(nameof(ShowName));

    /// <summary><see cref="Title"/> 的属性定义。</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ForgeStripeButton, string?>(nameof(Title));

    /// <summary><see cref="StripeWidth"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> StripeWidthProperty =
        AvaloniaProperty.Register<ForgeStripeButton, double>(nameof(StripeWidth));

    static ForgeStripeButton()
    {
        AffectsRender<ForgeStripeButton>(
            IsToolWindowVisibleProperty, IsToolWindowActiveProperty, ShowNameProperty, TitleProperty, IconProperty);
        AffectsMeasure<ForgeStripeButton>(ShowNameProperty, StripeWidthProperty, TitleProperty);
    }

    /// <summary>对应的工具窗口当前是否可见。</summary>
    public bool IsToolWindowVisible
    {
        get => GetValue(IsToolWindowVisibleProperty);
        set => SetValue(IsToolWindowVisibleProperty, value);
    }

    /// <summary>对应的工具窗口当前是否持有焦点。</summary>
    public bool IsToolWindowActive
    {
        get => GetValue(IsToolWindowActiveProperty);
        set => SetValue(IsToolWindowActiveProperty, value);
    }

    /// <summary>
    /// 条带按钮上的图标。
    /// </summary>
    /// <remarks>
    /// 是 <see cref="object"/>：宿主塞控件、几何图形或字符都行。
    /// Forge **不带图标资源**（上游的图标是 JetBrains 的美术资产，
    /// 原因见 <c>工具窗口模型.md</c> §17）；这里只按审出来的尺寸留位置：
    /// 图标区 <see cref="ForgeSquareButtonBase.IconSize"/>、四周 <see cref="ForgeSquareButtonBase.IconPadding"/>。
    /// </remarks>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>没有图标时画的占位字符。</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>是否在图标下面显示工具窗口的名字。</summary>
    /// <remarks>
    /// 开关是 `UISettings.showToolWindowsNames`，默认 false
    /// （<c>UISettingsState.kt:83</c>）；打开时条带宽度变成
    /// <see cref="ForgeMetrics.StripeNamedDefaultWidth"/>（<c>ResizeStripeManager.kt:215-218</c>）。
    /// </remarks>
    public bool ShowName
    {
        get => GetValue(ShowNameProperty);
        set => SetValue(ShowNameProperty, value);
    }

    /// <summary>工具窗口的名字，<see cref="ShowName"/> 打开时画在图标下面。</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>条带的自定义宽度；<c>0</c> 表示按按钮自身尺寸走。</summary>
    public double StripeWidth
    {
        get => GetValue(StripeWidthProperty);
        set => SetValue(StripeWidthProperty, value);
    }

    /// <summary>名字左右的 padding：紧凑 4，否则 6。</summary>
    public double TextPadding
        => IsCompact ? ForgeCompactMetrics.StripeButtonTextPadding : ForgeMetrics.StripeButtonTextPadding;

    /// <summary>
    /// 当前该用哪个背景。
    /// </summary>
    /// <remarks>
    /// 优先级照抄 <c>SquareStripeButton.kt:208-219</c>：
    /// **焦点 &gt; 窗口可见 &gt; 悬停 &gt; 无**。
    /// 注意「窗口可见」压过「悬停」——鼠标划过一个已经打开的工具窗口按钮时，
    /// 显示的是 pressed 背景而不是 hover 背景。
    /// </remarks>
    /// <returns>背景色；都不满足时是 <see langword="null"/>（不画）。</returns>
    public override Color? ResolveBackground()
    {
        if (IsToolWindowActive)
        {
            return Palette.ButtonSelectedBackground;
        }

        if (IsToolWindowVisible)
        {
            return Palette.ButtonPressedBackground;
        }

        return base.ResolveBackground();
    }

    /// <summary>当前该用哪个前景。</summary>
    /// <remarks>激活时是 <c>ToolWindow.Button.selectedForeground</c>，否则 <c>ToolWindow.Button.foreground</c>。</remarks>
    /// <returns>前景色。</returns>
    public override Color ResolveForeground()
        => IsToolWindowActive ? Palette.ButtonSelectedForeground : Palette.ButtonForeground;

    /// <summary>
    /// 名字按第一个空格拆成最多两行。
    /// </summary>
    /// <remarks>对应 <c>SquareStripeButton.kt:331-334 · getStripeSplitText</c>：<c>split(" ", limit = 2)</c>。</remarks>
    /// <param name="title">工具窗口的名字。</param>
    /// <returns>拆出来的行。</returns>
    public static IReadOnlyList<string> SplitTitle(string? title)
    {
        var text = title?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var space = text.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? [text] : [text[..space], text[(space + 1)..]];
    }

    /// <summary>「显示名字」时字号：比正文小一号。</summary>
    /// <remarks>上游是 <c>RelativeFont.TINY.derive(button.font)</c>（<c>SquareStripeButton.kt:329</c>）。</remarks>
    private double NameFontSize => Math.Max(8, IconSize * 0.5);

    /// <inheritdoc />
    /// <remarks>
    /// 显名与否会换一整套内边距键（<c>…StripeIconWithName.padding</c>），
    /// 所以基类要能问到它。
    /// </remarks>
    protected override bool ShowsName => ShowName;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (!ShowName)
        {
            return new Size(ButtonWidth, ButtonHeight);
        }

        // 高度 = 基础高 + 间距 + 行数 × 行高（SquareStripeButton.kt:320-327）
        var lines = SplitTitle(Title).Count;
        var height = ButtonHeight + (lines > 0 ? ForgeMetrics.StripeIconLabelGap + (lines * NameFontSize * 1.4) : 0);
        var width = StripeWidth > 0 ? StripeWidth : ButtonWidth;

        return new Size(width, height);
    }

    /// <summary>
    /// 宿主塞进来的图标控件。
    /// </summary>
    /// <remarks>
    /// <see cref="Icon"/> 是 <see cref="object"/>，允许三种东西：
    /// <list type="bullet">
    /// <item><see cref="Geometry"/> —— 库自绘那一套（<c>ForgeIcons</c>），直接画；</item>
    /// <item><see cref="Control"/> —— 宿主自己的图标控件（Material.Icons、Image…），
    ///       挂成子元素，由它自己画；</item>
    /// <item><see langword="null"/> —— 画默认图形。</item>
    /// </list>
    /// 第二种是这个字段存在的理由：库<b>不带图标资源</b>（上游那套是 JetBrains 的美术资产），
    /// 也不想强迫宿主改用某个图标库，所以必须能收下一个现成的控件。
    /// </remarks>
    private Control? _iconControl;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconProperty)
        {
            AttachIconControl(change.GetNewValue<object?>() as Control);
        }

        if (change.Property == IconProperty
            || change.Property == IsToolWindowActiveProperty
            || change.Property == PaletteProperty)
        {
            UpdateIconForeground();
        }
    }

    /// <summary>
    /// 把当前前景色写到 <see cref="TemplatedControl.Foreground"/> 上。
    /// </summary>
    /// <remarks>
    /// 宿主塞进来的图标控件（Material.Icons 那类）默认<b>继承</b> Foreground，
    /// 写在这里它就跟着主题明暗与激活态一起变 —— 否则浅色主题下会出现一排
    /// 看不见的白图标，而库自绘的那套图标是好的，很难猜到是这个原因。
    /// </remarks>
    private void UpdateIconForeground()
    {
        if (_iconControl is null)
        {
            return;
        }

        SetCurrentValue(ForegroundProperty, new SolidColorBrush(ResolveForeground()));
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        if (_iconControl is not null)
        {
            var rect = IconRect(finalSize);
            _iconControl.Measure(rect.Size);
            _iconControl.Arrange(rect);
        }

        return size;
    }

    /// <summary>
    /// 图标占的那块地：<see cref="ForgeSquareButtonBase.IconSize"/> 见方，居中。
    /// </summary>
    /// <remarks>
    /// <b>图标尺寸是定值，不是「按钮减 padding」</b>：上游
    /// <c>SquareStripeButton.kt:356-367 · scaleIcon</c> 把图标直接缩放成
    /// <c>getStripeToolbarButtonIconSize()</c>（20，紧凑 16），再由 <c>ActionButton</c>
    /// 按 insets(5) 排进 40×40 的按钮里。
    /// <para>
    /// 第一版按 <c>Bounds - IconPadding × 2</c> 算，得 30×30 —— 图标被放大到 1.5 倍，
    /// 整条条带看着都糊。padding 决定的是<b>按钮内容区</b>，不是图标大小。
    /// </para>
    /// <para>
    /// 显示名字时图标只占上面那一块（<c>:240</c> 的 <c>buttonWrapper</c> 就是干这个）。
    /// </para>
    /// <para>
    /// <b>居中的基准是<see cref="ForgeSquareButtonBase.BackgroundRect"/>，不是整个按钮</b>——
    /// 上游 <c>SquareStripeButtonLook.kt:112-118 · getIconPosition</c> 与 <c>paintBackground</c>
    /// 扣的是**同一份** <c>iconPadding</c>，图标与高亮共用一个矩形。
    /// expUI 的内边距左右对称，两种算法恰好重合，所以按整块按钮居中一直看不出错；
    /// Islands 是 5/2 不对称，图标就会比高亮偏 1.5px。
    /// </para>
    /// </remarks>
    /// <param name="size">按钮自己的尺寸。</param>
    /// <returns>图标矩形。</returns>
    private Rect IconRect(Size size)
    {
        // 显名时按钮被名字撑高，图标只在上面那一格里排
        var cell = ShowName
            ? new Rect((size.Width - ButtonWidth) / 2, 0, ButtonWidth, ButtonHeight)
            : new Rect(size);

        var area = BackgroundRect(cell);
        return new Rect(
            area.X + ((area.Width - IconSize) / 2),
            area.Y + ((area.Height - IconSize) / 2),
            IconSize,
            IconSize);
    }

    private void AttachIconControl(Control? control)
    {
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
    public override void Render(DrawingContext context)
    {
        // 整块都能点，不然只有画了东西的地方收得到鼠标
        ForgeHitArea.Fill(context, Bounds.Size);

        var background = ResolveBackground();
        if (background is { } color)
        {
            // 高亮画的是「按钮矩形 − 图标内边距」（SquareStripeButtonLook.kt:64-70：
            // Rectangle(component.size) 减 insets 再减 iconPadding）。
            //
            // ⚠ **显名时也是整块按钮，含名字那一截** —— 上游 SquareStripeButton.kt:196-198
            // 的 paintButtonLook 里，paintBackground 收到的就是 button 本身（它的 size 已经
            // 包含名字的高度），只有 paintIcon 才把高度砍掉一截去摆图标（:227-242 那个
            // buttonWrapper，注释原话是 "we need change height for right icon layout"）。
            // 之前这里跟着图标一起砍，于是高亮只包住图标、名字露在背景板外面 ——
            // 看着就是"选中的是图标，不是这一项"。
            context.DrawRectangle(
                new SolidColorBrush(color),
                null,
                new RoundedRect(BackgroundRect(new Rect(Bounds.Size)), ArcRadius));
        }

        var iconRect = IconRect(Bounds.Size);

        if (ShowName)
        {
            RenderName(context);
        }

        // 宿主给的是控件（MaterialIcon、Image、任何 Control）——它自己会画，这里什么都不做。
        // 位置在 ArrangeOverride 里安排好了。
        if (_iconControl is not null)
        {
            return;
        }

        // 图标优先：宿主给了 Geometry 就画它，什么都没给就画默认图形
        if (Icon is Geometry geometry)
        {
            ForgeIcons.Draw(context, geometry, iconRect, new SolidColorBrush(ResolveForeground()));
            return;
        }

        if (string.IsNullOrEmpty(Text))
        {
            if (Icon is null)
            {
                ForgeIcons.Draw(
                    context, ForgeIcons.DefaultToolWindow, iconRect, new SolidColorBrush(ResolveForeground()));
            }

            return;
        }

        var text = new FormattedText(
            Text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            IconSize * 0.7,
            new SolidColorBrush(ResolveForeground()));

        context.DrawText(
            text,
            new Point(
                iconRect.X + ((iconRect.Width - text.Width) / 2),
                iconRect.Y + ((iconRect.Height - text.Height) / 2)));
    }

    private void RenderName(DrawingContext context)
    {
        var lines = SplitTitle(Title);
        if (lines.Count == 0)
        {
            return;
        }

        var totalWidth = Math.Max(0, Bounds.Width - (TextPadding * 2));
        var y = ButtonHeight + ForgeMetrics.StripeIconLabelGap;

        foreach (var line in lines)
        {
            var text = new FormattedText(
                line,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                NameFontSize,
                new SolidColorBrush(ResolveForeground()));

            context.DrawText(text, new Point(TextPadding + ((totalWidth - text.Width) / 2), y));
            y += NameFontSize * 1.4;
        }
    }

}

/// <summary>
/// 条带上分组之间的那条分隔符。
/// </summary>
/// <remarks>
/// 对应 <c>platform/platform-impl/src/com/intellij/openapi/wm/impl/StripeButtonSeparator.kt</c>：
/// 占位 32×11，中间画一条 24×1 的线，横向居中于**去掉 padding 后**的区域、纵向在正中。
/// </remarks>
public class ForgeStripeSeparator : Control
{
    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeStripeSeparator, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    /// <summary><see cref="IsDragAndDrop"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsDragAndDropProperty =
        AvaloniaProperty.Register<ForgeStripeSeparator, bool>(nameof(IsDragAndDrop));

    static ForgeStripeSeparator()
    {
        AffectsRender<ForgeStripeSeparator>(PaletteProperty, IsDragAndDropProperty);
    }

    /// <summary>配色。</summary>
    public ForgePalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>是否处于拖拽态（换成 DragAndDrop 那个颜色）。</summary>
    public bool IsDragAndDrop
    {
        get => GetValue(IsDragAndDropProperty);
        set => SetValue(IsDragAndDropProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
        => new(ForgeMetrics.StripeSeparatorWidth, ForgeMetrics.StripeSeparatorHeight);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var padding = ForgeMetrics.StripeButtonIconPadding;
        var paintAreaWidth = Bounds.Width - (padding * 2);
        var x = padding + ((paintAreaWidth - ForgeMetrics.StripeSeparatorLineWidth) / 2);
        var y = Bounds.Height / 2;

        var color = IsDragAndDrop ? Palette.StripeSeparatorDragAndDrop : Palette.StripeSeparator;
        context.FillRectangle(
            new SolidColorBrush(color),
            new Rect(x, y, ForgeMetrics.StripeSeparatorLineWidth, ForgeMetrics.StripeSeparatorLineHeight));
    }
}
