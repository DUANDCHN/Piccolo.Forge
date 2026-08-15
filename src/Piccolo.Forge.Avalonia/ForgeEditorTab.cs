using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 编辑区里的一个标签页。
/// </summary>
/// <remarks>
/// 度量见 <c>docs/upstream/编辑区.md</c> §5：高 28、左右内边距 8、
/// 选中下划线高 3 且**圆角 0**（工具窗口标题条那边是 4，两处不一致是真的）。
/// </remarks>
public class ForgeEditorTab : TemplatedControl
{
    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeEditorTab, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    /// <summary><see cref="Title"/> 的属性定义。</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ForgeEditorTab, string?>(nameof(Title));

    /// <summary><see cref="IsSelected"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<ForgeEditorTab, bool>(nameof(IsSelected));

    /// <summary><see cref="IsGroupActive"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsGroupActiveProperty =
        AvaloniaProperty.Register<ForgeEditorTab, bool>(nameof(IsGroupActive));

    /// <summary><see cref="IsPinned"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsPinnedProperty =
        AvaloniaProperty.Register<ForgeEditorTab, bool>(nameof(IsPinned));

    /// <summary><see cref="CanBeClosed"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> CanBeClosedProperty =
        AvaloniaProperty.Register<ForgeEditorTab, bool>(nameof(CanBeClosed), defaultValue: true);

    static ForgeEditorTab()
    {
        AffectsRender<ForgeEditorTab>(
            PaletteProperty, TitleProperty, IsSelectedProperty, IsGroupActiveProperty,
            IsPinnedProperty, CanBeClosedProperty);
        AffectsMeasure<ForgeEditorTab>(TitleProperty, CanBeClosedProperty);
    }

    /// <summary>配色。</summary>
    public ForgePalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>标签上的字。</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>是不是这一组里选中的那个。</summary>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>所在的组是不是当前组（决定下划线用哪个颜色）。</summary>
    public bool IsGroupActive
    {
        get => GetValue(IsGroupActiveProperty);
        set => SetValue(IsGroupActiveProperty, value);
    }

    /// <summary>固定住了 —— 画图钉而不是叉。</summary>
    public bool IsPinned
    {
        get => GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    /// <summary>能不能关。</summary>
    public bool CanBeClosed
    {
        get => GetValue(CanBeClosedProperty);
        set => SetValue(CanBeClosedProperty, value);
    }

    /// <summary>右边那个小图标占的宽度。</summary>
    public double IconAreaWidth => CanBeClosed || IsPinned ? IconSize + ForgeMetrics.TabIconsGap : 0;

    private static double IconSize => 12;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var text = MeasureText();
        var width = (text?.Width ?? 0) + (ForgeMetrics.EditorTabInsetHorizontal * 2) + IconAreaWidth;

        return new Size(width, ForgeMetrics.EditorTabHeight);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (IsPointerOver && !IsSelected)
        {
            context.FillRectangle(new SolidColorBrush(Palette.HeaderTabHoverBackground), new Rect(Bounds.Size));
        }

        if (IsSelected)
        {
            // 下划线：(x, y+height-thickness, w, 3)，圆角 0
            var color = IsGroupActive ? Palette.HeaderTabUnderline : Palette.HeaderTabInactiveUnderline;
            var thickness = ForgeMetrics.HeaderTabUnderlineHeight;

            context.DrawRectangle(
                new SolidColorBrush(color),
                null,
                new RoundedRect(
                    new Rect(0, Bounds.Height - thickness, Bounds.Width, thickness),
                    ForgeMetrics.EditorTabUnderlineArc));
        }

        if (MeasureText() is { } text)
        {
            context.DrawText(
                text,
                new Point(ForgeMetrics.EditorTabInsetHorizontal, (Bounds.Height - text.Height) / 2));
        }

        if (CanBeClosed || IsPinned)
        {
            RenderTabIcon(context);
        }
    }

    private void RenderTabIcon(DrawingContext context)
    {
        var brush = new SolidColorBrush(Palette.ButtonForeground);
        var pen = new Pen(brush, 1);
        var x = Bounds.Width - ForgeMetrics.TabIconsGap - IconSize;
        var rect = new Rect(x, (Bounds.Height - IconSize) / 2, IconSize, IconSize);

        if (IsPinned)
        {
            var head = new Rect(rect.X + 3, rect.Y + 1, IconSize - 6, IconSize / 2);
            context.FillRectangle(brush, head);
            context.DrawLine(pen, new Point(rect.Center.X, head.Bottom), new Point(rect.Center.X, rect.Bottom));
            return;
        }

        var pad = 3.0;
        context.DrawLine(pen, new Point(rect.X + pad, rect.Y + pad), new Point(rect.Right - pad, rect.Bottom - pad));
        context.DrawLine(pen, new Point(rect.Right - pad, rect.Y + pad), new Point(rect.X + pad, rect.Bottom - pad));
    }

    private FormattedText? MeasureText()
        => string.IsNullOrEmpty(Title)
            ? null
            : new FormattedText(
                Title,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                12,
                new SolidColorBrush(IsSelected ? Palette.HeaderTabUnderline : Palette.Foreground));

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
