using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 编辑区两格之间那条可拖的分割线 —— 看得见 1px，抓得住 6px。
/// </summary>
/// <remarks>
/// 对应上游的 <c>OnePixelSplitter</c>：**可见宽度 1px**（<c>OnePixelSplitter.java:73 · init</c>
/// 的 <c>setDividerWidth(1)</c>），鼠标热区 6px（Registry <c>ide.splitter.mouseZone</c>）。
/// 两者不是一个数：线细才好看，热区细就抓不住。
/// <para>
/// 颜色是 <c>EditorPane.splitBorder</c>（<c>EditorsSplitters.kt:1863-1876 · createSplitter</c>：
/// <c>divider.background = JBColor.namedColor("EditorPane.splitBorder", JBColor.border())</c>）——
/// expUI Dark 定义为 Gray3、Islands 定义为 <c>editor-border</c>、expUI Light 未定义故回落
/// <c>Borders.color</c>，四套算下来正好都等于 <see cref="ForgePalette.EditorTabsBorder"/>
/// （标签条下面那条线同源）。<b>不是 <c>WindowBorder</c></b>：那是窗口/工具区的边框，
/// 在 expUI Dark 恰好等于编辑区底色，画上去等于没画。
/// </para>
/// <para>
/// <b>为什么不用 Avalonia 自带的 <c>GridSplitter</c>（原实现用的就是它）</b>：
/// <list type="number">
/// <item>
/// <b>它的长相由宿主的主题决定。</b>Forge 所有控件都是代码自绘、颜色只认
/// <see cref="ForgePalette"/>，唯独这一条线的模板来自宿主装了哪套主题 ——
/// 换一套（或那套的 GridSplitter 模板不吃 <c>Background</c>）就**整条线看不见**，
/// 而分屏之间没有缝在视觉上等于「这两块是一块」。
/// </item>
/// <item>
/// <b>它拖完不进模型。</b>GridSplitter 改的是 Grid 的行列定义，而编辑区的真值是
/// <see cref="Piccolo.Forge.Model.EditorSplit.Proportion"/>；下一次重建（开关标签、
/// 拖标签、排宫格）按模型重画，用户刚拖出来的比例当场被打回原样。
/// </item>
/// </list>
/// </para>
/// <para>
/// 拖动中只发 <see cref="Preview"/>（宿主就地改 Grid，图像不重建），松手才发
/// <see cref="Committed"/> 写回模型 —— 与标签拖拽「行内仅预览、松手才提交」同一条规矩。
/// </para>
/// </remarks>
public sealed class ForgeEditorSplitter : Control
{
    /// <summary>建一条分割线。</summary>
    /// <param name="isVertical">竖着切（上下两块）—— 那么线本身是横的一条。</param>
    public ForgeEditorSplitter(bool isVertical)
    {
        IsVertical = isVertical;
        Cursor = new Cursor(isVertical ? StandardCursorType.SizeNorthSouth : StandardCursorType.SizeWestEast);
    }

    /// <summary>鼠标热区。</summary>
    public static double MouseZone => ForgeMetrics.SplitterMouseZone;

    /// <summary>竖着切（上下两块）。</summary>
    public bool IsVertical { get; }

    /// <summary>配色。</summary>
    public ForgePalette Palette { get; set; } = ForgePalette.ExpUiDark;

    /// <summary>拖动中的新比例 —— 只该用来改视图，别写模型。</summary>
    public event EventHandler<float>? Preview;

    /// <summary>松手了：这是要写回模型的比例。</summary>
    public event EventHandler<float>? Committed;

    /// <inheritdoc />
    /// <remarks>
    /// ⚠ <b>整块热区都要画上东西，哪怕是透明的</b>：Avalonia 的命中测试基于渲染内容，
    /// 什么都不画的 <see cref="Control"/> 收不到指针事件 —— 光标变了却拖不动
    /// （同 <see cref="ForgeDockResizer.Render"/> 记过的那一条）。
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        context.FillRectangle(
            LineBrush ?? new SolidColorBrush(Palette.EditorTabsBorder),
            LineRect(Bounds.Size, IsVertical));
    }

    /// <summary><see cref="LineBrush"/> 的属性定义。</summary>
    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<ForgeEditorSplitter, IBrush?>(nameof(LineBrush));

    /// <summary>那条 1px 线的颜色，由 <c>ControlTheme</c> 从资源键给（REQ-067）。</summary>
    /// <remarks>
    /// <b>不套模板</b>：热区比线宽得多（6px 抓取区 vs 1px 线），
    /// "热区里居中的一条线"要用模板表达就得把 <see cref="LineRect"/> 也搬成绑定属性，
    /// 收益抵不上绕的这一道。对宿主而言用法一样：重定义同名键即可，明暗照常跟随。
    /// <para>兜底回配色 —— 控件还没进树时拿不到主题（见 <c>ThemeResourceSpikeTests</c>）。</para>
    /// </remarks>
    public IBrush? LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    /// <summary>热区里那条真正画出来的 1px 线（居中）。</summary>
    public static Rect LineRect(Size size, bool isVertical)
    {
        var thickness = ForgeMetrics.SplitDividerWidth;
        return isVertical
            ? new Rect(0, (size.Height - thickness) / 2, size.Width, thickness)
            : new Rect((size.Width - thickness) / 2, 0, thickness, size.Height);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 版面上占的是**热区**的 6px，画出来的只有正中那 1px —— 剩下 5px 是透明的，
    /// 而分割线两侧就是编辑区底色，所以看上去仍然只是一条 1px 的线（同 OnePixelSplitter）。
    /// <para>
    /// 曾经试过「只占 1px，靠负边距把命中区撑到 6px」：负边距会连 <c>DesiredSize</c> 一起减，
    /// Auto 行列量出来是 0，最后既没有 6px 热区也不是 1px 线。
    /// </para>
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
        => IsVertical ? new Size(0, MouseZone) : new Size(MouseZone, 0);

    private bool _dragging;

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragging = true;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging && Resolve(e) is { } proportion)
        {
            Preview?.Invoke(this, proportion);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);

        if (Resolve(e) is { } proportion)
        {
            Committed?.Invoke(this, proportion);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    /// <summary>指针现在落在父容器的百分之几处。</summary>
    private float? Resolve(PointerEventArgs e)
    {
        if (Parent is not Visual parent)
        {
            return null;
        }

        var point = e.GetPosition(parent);
        var (part, total) = IsVertical
            ? (point.Y, parent.Bounds.Height)
            : (point.X, parent.Bounds.Width);

        return total <= 0 ? null : (float)(part / total);
    }
}
