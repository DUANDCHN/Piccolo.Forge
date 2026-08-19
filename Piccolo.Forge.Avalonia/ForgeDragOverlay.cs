using Avalonia;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
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

    /// <summary><see cref="DropCornerRadius"/> 的属性定义。</summary>
    public static readonly StyledProperty<CornerRadius> DropCornerRadiusProperty =
        AvaloniaProperty.Register<ForgeDragOverlay, CornerRadius>(nameof(DropCornerRadius));

    /// <summary><see cref="IsCompact"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<ForgeDragOverlay, bool>(nameof(IsCompact));

    static ForgeDragOverlay()
    {
        AffectsRender<ForgeDragOverlay>(
            PaletteProperty,
            AreaRectProperty,
            DropRectProperty,
            DropCornerRadiusProperty,
            SplitBoxRectProperty,
            IsCompactProperty,
            DropBackgroundBrushProperty,
            DropBorderBrushProperty,
            DropAreaBrushProperty);

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

    /// <summary>
    /// 落点方块的圆角；<c>default</c> 表示按度量里的 <c>Arc</c> 折半。
    /// </summary>
    /// <remarks>
    /// 由宿主把**按钮自己的圆角**（<c>ForgeSquareButtonBase.ArcCornerRadius</c>）传进来：
    /// 落点方块画的就是那颗按钮松手后占的那块地，长相要跟图标按钮、跟拖影一致。
    /// 宿主改了圆角（本软件收成半径 4），这里不能还按库里的常量画。
    /// </remarks>
    public CornerRadius DropCornerRadius
    {
        get => GetValue(DropCornerRadiusProperty);
        set => SetValue(DropCornerRadiusProperty, value);
    }

    /// <summary>紧凑模式（只影响落点方块的圆角）。</summary>
    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    /// <summary>
    /// 放掉一张拖影快照 —— <see cref="CaptureThumb"/> 的对手戏。
    /// </summary>
    /// <remarks>
    /// ⚠ **不能当场 Dispose**。快照是拖影窗口 <c>Image.Source</c> 上挂着的那张，
    /// 而 <c>Window.Close()</c> 并不保证这一刻之后再没有一次绘制：平台那边还会回来敲一次
    /// <c>Paint</c>，这时候位图已经没了 ——
    /// <c>ObjectDisposedException: Ref&lt;IRenderTargetBitmapImpl&gt;</c>，
    /// 从原生 Paint 回调**同步**抛出，绕过 <c>Dispatcher.UIThread.UnhandledException</c>
    /// 那类兜底，直接把进程干掉（用户实机：拖侧边栏图标换位，松手即崩）。
    /// <para>
    /// 优先级必须**低于** <see cref="DispatcherPriority.Render"/>：默认的 Normal 比 Render 高，
    /// 「延后一拍」反而排在这一帧绘制之前，等于没延后。
    /// </para>
    /// </remarks>
    /// <param name="bitmap">要放掉的快照；<see langword="null"/> 直接返回。</param>
    public static void ReleaseThumb(global::Avalonia.Media.Imaging.RenderTargetBitmap? bitmap)
    {
        if (bitmap is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(bitmap.Dispose, DispatcherPriority.Background);
    }

    /// <summary>
    /// 把一个控件当前的样子拍成位图，给拖影（<see cref="ForgeDragGhost"/>）用。
    /// </summary>
    /// <remarks>
    /// 必须在它**还排在原位**时拍 —— 拖拽一开始它可能就被排成零尺寸，那时再拍是空的。
    /// 尺寸取整：<see cref="global::Avalonia.Media.Imaging.RenderTargetBitmap"/> 要像素尺寸。
    /// </remarks>
    /// <param name="control">要拍的控件。</param>
    /// <param name="foreground">
    /// 拍这一张时临时改用的前景；<see langword="null"/> = 用它此刻本来的颜色。
    /// <para>
    /// 条带图标要用它：**开着的**那个面板，它的按钮是激活态、图标是白的
    /// （<c>ToolWindow.Button.selectedForeground</c>，四套主题都是 <c>#FFFFFF</c>），
    /// 而拖影的背板是**悬停**那一档底色 —— 白图标落在浅色主题的浅灰背板上几乎看不见
    /// （用户实机提的）。拖影画的是"这颗按钮被拎起来"的常规样子，不是它的激活态，
    /// 所以前景该跟着背板一起退回常规那一档（<c>ForgeSquareButtonBase.IdleForeground</c>）。
    /// </para>
    /// </param>
    /// <returns>位图；控件小于 1px 时为 <see langword="null"/>。</returns>
    public static global::Avalonia.Media.Imaging.RenderTargetBitmap? CaptureThumb(Control control, IBrush? foreground = null)
    {
        ArgumentNullException.ThrowIfNull(control);

        var size = control.Bounds.Size;
        if (size.Width < 1 || size.Height < 1)
        {
            return null;
        }

        var bitmap = new global::Avalonia.Media.Imaging.RenderTargetBitmap(
            new PixelSize((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height)));

        // ⚠ 拍之前把文本渲染改成**灰度**抗锯齿。
        //   字体图标（条带上的 SymbolIcon 就是）默认走 LCD 子像素抗锯齿，那套算法**假定目标
        //   有一层不透明的底**；画进这张透明位图里，笔画有一部分会落成**不透明纯黑**——
        //   实测一个 24×24 的图标里 60 个像素是 (0,0,0,255)。屏幕上看不出来（窗口底是实的），
        //   一进拖影就露馅：图标带一圈黑块（用户实机提的）。灰度抗锯齿不依赖底色，实测归零。
        // ⚠ 这里**曾经**是「先 CreateDrawingContext 清空一次」，按的是"新建位图不透明"那个猜测。
        //   那段是无效代码：RenderTargetBitmap.Render 自己就会清空目标——实测先填成纯白，
        //   Render 之后白也没了，而黑像素照旧。底色从来不是这件事的原因。
        // 拍完把整份 TextOptions 原样写回（它是一个结构，取回来的就是这颗控件此刻生效的那份）。
        var oldOptions = TextOptions.GetTextOptions(control);
        TextOptions.SetTextRenderingMode(control, TextRenderingMode.Antialias);

        // 前景同理：改成局部值拍一张，拍完撤回去 —— 撤成"没设过"而不是写回旧值，
        // 因为它本来是**继承**按钮的（宿主给的图标控件自己不设色），写回会把继承钉死。
        var hadForeground = control.IsSet(TextElement.ForegroundProperty);
        var oldForeground = TextElement.GetForeground(control);

        if (foreground is not null)
        {
            // 写下就能拍：属性继承是当场往下传的，不用再排一次布局（实测 SymbolIcon 上
            // 加不加 UpdateLayout 拍出来一样）——拖动起手这一帧本来就紧，能不排就不排。
            TextElement.SetForeground(control, foreground);
        }

        try
        {
            bitmap.Render(control);
        }
        finally
        {
            TextOptions.SetTextOptions(control, oldOptions);

            if (foreground is not null)
            {
                if (hadForeground)
                {
                    TextElement.SetForeground(control, oldForeground);
                }
                else
                {
                    control.ClearValue(TextElement.ForegroundProperty);
                }
            }
        }

        return bitmap;
    }

    /// <summary>什么都不用画的时候，别让它白占一层渲染。</summary>
    public bool IsIdle => AreaRect is null && DropRect is null && SplitBoxRect is null;

    /// <summary>
    /// 只撤掉「落点反馈」，**虚影留着**。
    /// </summary>
    /// <remarks>
    /// 拖到不能放的地方（两个落区之间那段空白）时用它。
    /// <para>
    /// ⚠ 这种时候**不能**调 <see cref="Clear"/> —— 那会把虚影一起清掉，
    /// 表现为「拖到侧边栏中间，拖着的图标就没了，再往上或往下又出现」（用户实测）。
    /// 上游的虚影是一个独立窗口，压根不受落点判定影响
    /// （<c>ToolWindowDragHelper.kt:299 · relocateImageView</c>），全程跟手。
    /// </para>
    /// </remarks>
    public void ClearDropFeedback()
    {
        AreaRect = null;
        DropRect = null;
        SplitBoxRect = null;
    }

    /// <summary>
    /// 手势结束了：反馈全撤。
    /// </summary>
    /// <remarks>
    /// 拖影不在这层里 —— 它是独立的幽灵窗（<see cref="ForgeDragGhost"/>），
    /// 由发起拖拽的那一方关掉。这层只画"松手会落到哪"。
    /// </remarks>
    public void Clear() => ClearDropFeedback();

    /// <summary><see cref="DropBackgroundBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> DropBackgroundBrushProperty =
        AvaloniaProperty.Register<ForgeDragOverlay, IBrush?>(nameof(DropBackgroundBrush));

    /// <summary><see cref="DropBorderBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> DropBorderBrushProperty =
        AvaloniaProperty.Register<ForgeDragOverlay, IBrush?>(nameof(DropBorderBrush));

    /// <summary><see cref="DropAreaBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> DropAreaBrushProperty =
        AvaloniaProperty.Register<ForgeDragOverlay, IBrush?>(nameof(DropAreaBrush));

    /// <summary>落点方框的底色，由 <c>ControlTheme</c> 从资源键给（REQ-067）。</summary>
    /// <remarks>
    /// <b>这个控件不套模板</b>：它画的是拖拽过程中**实时算出来的**落点方框与高亮区，
    /// 位置每帧都变，模板里的静态元素表达不了。对宿主而言用法一样（重定义同名键、
    /// 明暗照常跟随），只是库内部仍由 <c>Render</c> 拿着画刷画。
    /// <para>兜底回配色 —— 控件没进树时拿不到主题。</para>
    /// </remarks>
    public IBrush? DropBackgroundBrush
    {
        get => GetValue(DropBackgroundBrushProperty);
        set => SetValue(DropBackgroundBrushProperty, value);
    }

    /// <summary>落点方框的描边，来源同 <see cref="DropBackgroundBrush"/>。</summary>
    public IBrush? DropBorderBrush
    {
        get => GetValue(DropBorderBrushProperty);
        set => SetValue(DropBorderBrushProperty, value);
    }

    /// <summary>目标停靠区高亮的底色，来源同 <see cref="DropBackgroundBrush"/>。</summary>
    public IBrush? DropAreaBrush
    {
        get => GetValue(DropAreaBrushProperty);
        set => SetValue(DropAreaBrushProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (AreaRect is { } area)
        {
            context.FillRectangle(DropAreaBrush ?? new SolidColorBrush(Palette.DragAreaBackground), area);
        }

        if (DropRect is { } drop)
        {
            // 落点方块与真按钮同圆角（AbstractDroppableStripe.kt:639 取的就是 stripeButtonArc）。
            // ⚠ 那是**直径**，画的时候要折半 —— 见 ForgeMetrics.StripeButtonArc 的说明。
            var arc = IsCompact ? ForgeCompactMetrics.StripeButtonArc : ForgeMetrics.StripeButtonArc;
            var radius = DropCornerRadius == default ? new CornerRadius(arc / 2) : DropCornerRadius;
            context.DrawRectangle(
                DropBackgroundBrush ?? new SolidColorBrush(Palette.ButtonDropBackground),
                null,
                new RoundedRect(drop, radius));
        }

        if (SplitBoxRect is { } box)
        {
            // BasicStroke(1f, CAP_SQUARE, JOIN_MITER, 10f, dash = [4, 4], 0f)
            var pen = new Pen(
                DropBorderBrush ?? new SolidColorBrush(Palette.ButtonDropBorder),
                thickness: 1,
                dashStyle: new DashStyle([4, 4], 0),
                lineCap: PenLineCap.Square,
                lineJoin: PenLineJoin.Miter,
                miterLimit: 10);

            context.DrawRectangle(null, pen, box);
        }

    }
}
