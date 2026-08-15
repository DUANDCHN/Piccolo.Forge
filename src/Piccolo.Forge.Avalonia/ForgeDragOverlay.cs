using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 拖动工具窗口时的那层反馈：目标区域高亮 + 条带上的落点方块 + 「另一半」的虚线框。
/// </summary>
/// <remarks>
/// 三样东西在上游分属两处：
/// <list type="bullet">
/// <item>目标停靠区的整片高亮 —— <c>ToolWindowDragHelper.kt:126-134 ·
///       createDropTargetHighlightComponent</c>，新 UI 填
///       <c>ToolWindow.DragAndDrop.areaBackground</c>；</item>
/// <item>条带上的落点方块与虚线框 —— <c>AbstractDroppableStripe.kt:627-660 ·
///       paintComponent</c>（那段注释也写明「条带按钮的高亮由条带自己画」）。</item>
/// </list>
/// <para>
/// 做成一层覆盖控件而不是让各控件自己画：这三样都要盖在别的控件<b>之上</b>，
/// 而 Avalonia 里父控件的 <c>Render</c> 在子控件之下。
/// </para>
/// </remarks>
public class ForgeDragOverlay : Control
{
    /// <summary><see cref="Palette"/> 的属性定义。</summary>
    public static readonly StyledProperty<ForgePalette> PaletteProperty =
        AvaloniaProperty.Register<ForgeDragOverlay, ForgePalette>(nameof(Palette), ForgePalette.ExpUiDark);

    /// <summary><see cref="AreaRect"/> 的属性定义。</summary>
    public static readonly StyledProperty<Rect?> AreaRectProperty =
        AvaloniaProperty.Register<ForgeDragOverlay, Rect?>(nameof(AreaRect));

    /// <summary><see cref="DropRect"/> 的属性定义。</summary>
    public static readonly StyledProperty<Rect?> DropRectProperty =
        AvaloniaProperty.Register<ForgeDragOverlay, Rect?>(nameof(DropRect));

    /// <summary><see cref="SplitBoxRect"/> 的属性定义。</summary>
    public static readonly StyledProperty<Rect?> SplitBoxRectProperty =
        AvaloniaProperty.Register<ForgeDragOverlay, Rect?>(nameof(SplitBoxRect));

    /// <summary><see cref="IsCompact"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<ForgeDragOverlay, bool>(nameof(IsCompact));

    static ForgeDragOverlay()
    {
        AffectsRender<ForgeDragOverlay>(
            PaletteProperty, AreaRectProperty, DropRectProperty, SplitBoxRectProperty, IsCompactProperty);
    }

    /// <summary>建一层拖拽反馈。</summary>
    public ForgeDragOverlay()
    {
        // 它只负责画，别挡住底下控件的鼠标
        IsHitTestVisible = false;
    }

    /// <summary>配色。</summary>
    public ForgePalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>目标停靠区的整片高亮；<see langword="null"/> 表示不画。</summary>
    public Rect? AreaRect
    {
        get => GetValue(AreaRectProperty);
        set => SetValue(AreaRectProperty, value);
    }

    /// <summary>条带上那个落点方块；<see langword="null"/> 表示不画。</summary>
    public Rect? DropRect
    {
        get => GetValue(DropRectProperty);
        set => SetValue(DropRectProperty, value);
    }

    /// <summary>「拖到另一半」的虚线方框；<see langword="null"/> 表示不画。</summary>
    public Rect? SplitBoxRect
    {
        get => GetValue(SplitBoxRectProperty);
        set => SetValue(SplitBoxRectProperty, value);
    }

    /// <summary>紧凑模式（只影响落点方块的圆角）。</summary>
    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    /// <summary>什么都不用画的时候，别让它白占一层渲染。</summary>
    public bool IsIdle => AreaRect is null && DropRect is null && SplitBoxRect is null;

    /// <summary>一次性清掉三样东西。</summary>
    public void Clear()
    {
        AreaRect = null;
        DropRect = null;
        SplitBoxRect = null;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (AreaRect is { } area)
        {
            context.FillRectangle(new SolidColorBrush(Palette.DragAreaBackground), area);
        }

        if (DropRect is { } drop)
        {
            var arc = IsCompact ? ForgeMetrics.StripeButtonArcCompact : ForgeMetrics.StripeButtonArc;
            context.DrawRectangle(new SolidColorBrush(Palette.ButtonDropBackground), null, new RoundedRect(drop, arc));
        }

        if (SplitBoxRect is { } box)
        {
            // BasicStroke(1f, CAP_SQUARE, JOIN_MITER, 10f, dash = [4, 4], 0f)
            var pen = new Pen(
                new SolidColorBrush(Palette.ButtonDropBorder),
                thickness: 1,
                dashStyle: new DashStyle([4, 4], 0),
                lineCap: PenLineCap.Square,
                lineJoin: PenLineJoin.Miter,
                miterLimit: 10);

            context.DrawRectangle(null, pen, box);
        }
    }
}
