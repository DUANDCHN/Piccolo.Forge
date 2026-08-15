using Avalonia;
using Avalonia.Controls;
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
            IsPinnedProperty, CanBeClosedProperty, IconProperty);
        AffectsMeasure<ForgeEditorTab>(TitleProperty, CanBeClosedProperty, IconProperty);
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

    /// <summary><see cref="Icon"/> 的属性定义。</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<ForgeEditorTab, object?>(nameof(Icon));

    /// <summary>
    /// 左边那个文件图标：<see cref="Geometry"/> 直接画，<see cref="Control"/> 挂成子元素，
    /// <see langword="null"/> 就不画（编辑区标签页<b>没有</b>默认图标——文件是什么类型只有宿主知道）。
    /// </summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>左边文件图标占的宽度（含与标题的间距）。</summary>
    private double LeadingIconWidth
        => Icon is null ? 0 : ForgeMetrics.EditorTabIconSize + ForgeMetrics.EditorTabIconTextGap;

    /// <summary>右边那个小图标占的宽度。</summary>
    public double IconAreaWidth => CanBeClosed || IsPinned ? IconSize + ForgeMetrics.TabIconsGap : 0;

    private static double IconSize => 12;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var text = MeasureText();
        var width = (text?.Width ?? 0)
                    + (ForgeMetrics.EditorTabInsetHorizontal * 2)
                    + LeadingIconWidth
                    + IconAreaWidth;

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
                new Point(
                    ForgeMetrics.EditorTabInsetHorizontal + LeadingIconWidth,
                    (Bounds.Height - text.Height) / 2));
        }

        if (Icon is Geometry geometry)
        {
            ForgeIcons.Draw(context, geometry, LeadingIconRect(Bounds.Size), new SolidColorBrush(Palette.Foreground));
        }

        if (CanBeClosed || IsPinned)
        {
            RenderTabIcon(context);
        }
    }

    /// <summary>宿主塞进来的图标控件；理由同 <c>ForgeStripeButton</c>。</summary>
    private Control? _iconControl;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != IconProperty)
        {
            return;
        }

        var control = change.GetNewValue<object?>() as Control;
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
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        if (_iconControl is not null)
        {
            var rect = LeadingIconRect(finalSize);
            _iconControl.Measure(rect.Size);
            _iconControl.Arrange(rect);
        }

        return size;
    }

    private static Rect LeadingIconRect(Size size)
        => new(
            ForgeMetrics.EditorTabInsetHorizontal,
            (size.Height - ForgeMetrics.EditorTabIconSize) / 2,
            ForgeMetrics.EditorTabIconSize,
            ForgeMetrics.EditorTabIconSize);

    private void RenderTabIcon(DrawingContext context)
    {
        var brush = new SolidColorBrush(Palette.ButtonForeground);
        var x = Bounds.Width - ForgeMetrics.TabIconsGap - IconSize;
        var rect = new Rect(x, (Bounds.Height - IconSize) / 2, IconSize, IconSize);

        ForgeIcons.Draw(context, IsPinned ? ForgeIcons.Pin : ForgeIcons.Close, rect, brush, filled: IsPinned);
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
