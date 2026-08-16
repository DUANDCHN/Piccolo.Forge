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

    /// <summary><see cref="ThumbImage"/> 的属性定义。</summary>
    public static readonly StyledProperty<IImage?> ThumbImageProperty =
        AvaloniaProperty.Register<ForgeDragOverlay, IImage?>(nameof(ThumbImage));

    /// <summary><see cref="ThumbRect"/> 的属性定义。</summary>
    public static readonly StyledProperty<Rect?> ThumbRectProperty =
        AvaloniaProperty.Register<ForgeDragOverlay, Rect?>(nameof(ThumbRect));

    static ForgeDragOverlay()
    {
        AffectsRender<ForgeDragOverlay>(
            PaletteProperty,
            AreaRectProperty,
            DropRectProperty,
            SplitBoxRectProperty,
            IsCompactProperty,
            ThumbImageProperty,
            ThumbRectProperty);
    }

    /// <summary>
    /// 跟着光标走的拖拽虚影：被拖按钮在**拖拽开始那一刻**的位图快照。
    /// </summary>
    /// <remarks>
    /// 对应上游 <c>ToolWindowDragHelper.kt:820-833 · DragImageDialog</c>：
    /// 一个 <c>opacity = THUMB_OPACITY(0.85)</c> 的无边框置顶小窗，内容是
    /// <c>createStripeButtonDragImage</c> 拍下来的 <c>BufferedImage</c>。
    /// <para>
    /// <b>实现差异（有意）</b>：不开系统级小窗，在覆盖层里画。
    /// Forge 的拖拽本来就只发生在外壳内部，开真窗口除了跨平台的透明/置顶麻烦拿不到什么。
    /// </para>
    /// <para>
    /// ⚠ **是快照，不是对活控件的引用**（曾用 <c>VisualBrush</c> 引原控件）。
    /// 那样虚影与被拖控件的排布强耦合：控件一旦被藏起来（拖拽期它本来就该从条带里消失），
    /// 虚影跟着一起没了；于是只能把它"排到面板外面"留着，又导致它画在面板上方露出来。
    /// 拍成位图之后两件事彻底解耦，控件可以老老实实排成零尺寸。
    /// 快照也顺带保住了宿主塞的任意控件图标（LDVision 用的是 Material.Icons）。
    /// </para>
    /// </remarks>
    public IImage? ThumbImage
    {
        get => GetValue(ThumbImageProperty);
        set => SetValue(ThumbImageProperty, value);
    }

    /// <summary>虚影画在哪；<see langword="null"/> 表示不画。</summary>
    public Rect? ThumbRect
    {
        get => GetValue(ThumbRectProperty);
        set => SetValue(ThumbRectProperty, value);
    }

    /// <summary>拖拽虚影的透明度（<c>ToolWindowDragHelper.kt:94 · THUMB_OPACITY</c>）。</summary>
    public const double ThumbOpacity = 0.85;

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

    /// <summary>
    /// 把一个控件当前的样子拍成位图，给 <see cref="ThumbImage"/> 用。
    /// </summary>
    /// <remarks>
    /// 必须在它**还排在原位**时拍 —— 拖拽一开始它可能就被排成零尺寸，那时再拍是空的。
    /// 尺寸取整：<see cref="global::Avalonia.Media.Imaging.RenderTargetBitmap"/> 要像素尺寸。
    /// </remarks>
    /// <param name="control">要拍的控件。</param>
    /// <returns>位图；控件小于 1px 时为 <see langword="null"/>。</returns>
    public static global::Avalonia.Media.Imaging.RenderTargetBitmap? CaptureThumb(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        var size = control.Bounds.Size;
        if (size.Width < 1 || size.Height < 1)
        {
            return null;
        }

        var bitmap = new global::Avalonia.Media.Imaging.RenderTargetBitmap(
            new PixelSize((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height)));
        bitmap.Render(control);
        return bitmap;
    }

    /// <summary>什么都不用画的时候，别让它白占一层渲染。</summary>
    public bool IsIdle => AreaRect is null && DropRect is null && SplitBoxRect is null;

    /// <summary>一次性清掉三样东西。</summary>
    public void Clear()
    {
        AreaRect = null;
        DropRect = null;
        SplitBoxRect = null;
        ThumbImage = null;
        ThumbRect = null;
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
            // 落点方块与真按钮同圆角（AbstractDroppableStripe.kt:639 取的就是 stripeButtonArc）。
            // ⚠ 那是**直径**，画的时候要折半 —— 见 ForgeMetrics.StripeButtonArc 的说明。
            var arc = IsCompact ? ForgeCompactMetrics.StripeButtonArc : ForgeMetrics.StripeButtonArc;
            context.DrawRectangle(
                new SolidColorBrush(Palette.ButtonDropBackground), null, new RoundedRect(drop, arc / 2));
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

        // 虚影画在最后 —— 它要压在落点方块与区域高亮之上，跟着光标走的东西不该被别的盖住
        if (ThumbImage is { } image && ThumbRect is { } thumb)
        {
            using (context.PushOpacity(ThumbOpacity))
            {
                context.DrawImage(image, new Rect(image.Size), thumb);
            }
        }
    }
}
