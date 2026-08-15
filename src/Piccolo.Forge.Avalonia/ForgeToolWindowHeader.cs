using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
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

    static ForgeToolWindowHeader()
    {
        AffectsRender<ForgeToolWindowHeader>(PaletteProperty, TitleProperty, IsActiveProperty);
        AffectsMeasure<ForgeToolWindowHeader>(IsCompactProperty);
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

    /// <summary>标题条高度：紧凑 31，否则 41。</summary>
    public double HeaderHeight => IsCompact ? ForgeCompactMetrics.HeaderHeight : ForgeMetrics.HeaderHeight;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
        => new(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, HeaderHeight);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var background = IsActive ? Palette.HeaderActiveBackground : Palette.HeaderInactiveBackground;
        context.FillRectangle(new SolidColorBrush(background), new Rect(Bounds.Size));

        // 下边一条 1px 分隔线
        context.FillRectangle(
            new SolidColorBrush(Palette.HeaderBorder),
            new Rect(0, Bounds.Height - 1, Bounds.Width, 1));

        if (string.IsNullOrEmpty(Title))
        {
            return;
        }

        var text = new FormattedText(
            Title,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            12,
            new SolidColorBrush(Palette.Foreground));

        context.DrawText(
            text,
            new Point(
                ForgeMetrics.HeaderLabelInsetLeft,
                (Bounds.Height - text.Height) / 2));
    }
}
