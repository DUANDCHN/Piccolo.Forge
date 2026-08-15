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
            IsToolWindowVisibleProperty, IsToolWindowActiveProperty, ShowNameProperty, TitleProperty);
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
    protected override Size MeasureOverride(Size availableSize)
    {
        if (!ShowName)
        {
            return new Size(ButtonSize, ButtonSize);
        }

        // 高度 = 基础高 + 间距 + 行数 × 行高（SquareStripeButton.kt:320-327）
        var lines = SplitTitle(Title).Count;
        var height = ButtonSize + (lines > 0 ? ForgeMetrics.StripeIconLabelGap + (lines * NameFontSize * 1.4) : 0);
        var width = StripeWidth > 0 ? StripeWidth : ButtonSize;

        return new Size(width, height);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var background = ResolveBackground();
        if (background is { } color)
        {
            var backgroundRect = ShowName
                ? new Rect((Bounds.Width - ButtonSize) / 2, 0, ButtonSize, ButtonSize)
                : new Rect(Bounds.Size);

            context.DrawRectangle(new SolidColorBrush(color), null, new RoundedRect(backgroundRect, Arc));
        }

        // 图标位：按 padding 内缩，正好留出 IconSize
        // 显示名字时，图标只占上面那一块（SquareStripeButton.kt:240 的 buttonWrapper 就是干这个）
        var iconAreaHeight = ShowName ? ButtonSize : Bounds.Height;
        var iconRect = new Rect(
            IconPadding,
            IconPadding,
            Math.Max(0, Bounds.Width - (IconPadding * 2)),
            Math.Max(0, iconAreaHeight - (IconPadding * 2)));

        if (ShowName)
        {
            RenderName(context);
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
        var y = ButtonSize + ForgeMetrics.StripeIconLabelGap;

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
