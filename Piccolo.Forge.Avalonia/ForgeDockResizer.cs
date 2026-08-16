using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Piccolo.Forge.Model;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 停靠区边界上的拖拽热区。
/// </summary>
/// <remarks>
/// 上游的主分隔器**可见宽度是 0**（<c>ToolWindowPane.kt:197 · dividerWidth = 0</c>），
/// 靠一条单独的鼠标热区拖，宽度取 Registry <c>ide.splitter.mouseZone</c> = 6
/// （<c>:198</c>、<c>registry.properties:290</c>）。所以这个控件不画任何东西，只吃鼠标。
/// <para>
/// 拖出来的新权重按 <c>ToolWindowManagerImpl.kt:228-232 · getAdjustedRatio</c> 换算，
/// 并夹在 <c>ide.mainSplitter.min.size</c>（30，<c>registry.properties:160</c>）之内。
/// </para>
/// </remarks>
public class ForgeDockResizer : Control
{
    private bool _dragging;

    /// <summary><see cref="Anchor"/> 的属性定义。</summary>
    public static readonly StyledProperty<ToolWindowAnchor> AnchorProperty =
        AvaloniaProperty.Register<ForgeDockResizer, ToolWindowAnchor>(nameof(Anchor));

    /// <summary>建一个热区。</summary>
    public ForgeDockResizer()
    {
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
    }

    /// <summary>这是哪条边的边界。</summary>
    public ToolWindowAnchor Anchor
    {
        get => GetValue(AnchorProperty);
        set
        {
            SetValue(AnchorProperty, value);
            UpdateCursor();
        }
    }

    /// <summary>热区宽度（未缩放）：6。</summary>
    public static double MouseZone => ForgeMetrics.SplitterMouseZone;

    /// <summary><see cref="IsSplitDivider"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsSplitDividerProperty =
        AvaloniaProperty.Register<ForgeDockResizer, bool>(nameof(IsSplitDivider));

    /// <summary>
    /// 这是**同一条边里两个窗口之间**的那条分割线，不是停靠区与编辑区之间的边界。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowPane.kt:617 · MySplitter</c>（<c>OnePixelSplitter</c>）——
    /// 一条边被两个窗口分掉时，中间那条可拖的线，拖出来的是 <c>sideWeight</c>。
    /// <para>
    /// 切分方向与 <c>ForgeDockPane.SplitSlot</c> 一致：**横边（TOP/BOTTOM）左右分，
    /// 竖边（LEFT/RIGHT）上下分** —— 所以它的拖动轴与同 anchor 的停靠边界**正好垂直**，
    /// 光标形状也相反。
    /// </para>
    /// </remarks>
    public bool IsSplitDivider
    {
        get => GetValue(IsSplitDividerProperty);
        set
        {
            SetValue(IsSplitDividerProperty, value);
            UpdateCursor();
        }
    }

    private void UpdateCursor()
    {
        // 分割线的轴与停靠边界垂直：横边的停靠边界是上下拖，它的分割线是左右拖
        var horizontalDrag = IsSplitDivider ? !Anchor.IsHorizontal() : Anchor.IsHorizontal();
        Cursor = new Cursor(horizontalDrag ? StandardCursorType.SizeNorthSouth : StandardCursorType.SizeWestEast);
    }

    /// <summary>
    /// 分割线所在那一格在 pane 坐标系里的矩形；由 <see cref="ForgeDockPane"/> 排布时写入。
    /// </summary>
    /// <remarks>比例要按**这一格**算，不是整个 pane —— 底边那一格只占 pane 的下面一条。</remarks>
    public Rect? SlotBounds { get; set; }

    /// <summary>用户把边界拖到了新的权重。</summary>
    public event EventHandler<DockWeightChangedEventArgs>? WeightChanged;

    /// <summary>
    /// 把「这条边现在有多少像素」换算成权重。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowManagerImpl.kt:228-232 · getAdjustedRatio</c>：
    /// <c>ratio = part/total</c>，再 <c>ratio += ((part+direction)/total - ratio) / 2</c>。
    /// 然后按 <c>ide.mainSplitter.min.size</c>=30 夹住两端，最后过
    /// <see cref="ForgeMetrics.NormalizeWeight"/>。
    /// </remarks>
    /// <param name="partSize">这条边占的像素。</param>
    /// <param name="totalSize">整个 pane 的对应边长。</param>
    /// <param name="direction">上游传的方向，取 ±1。</param>
    /// <returns>换算出的权重。</returns>
    public static float ComputeWeight(double partSize, double totalSize, int direction = 1)
    {
        if (totalSize <= 0)
        {
            return WindowInfo.DefaultWeight;
        }

        var min = ForgeMetrics.MainSplitterMinSize;
        var clamped = Math.Clamp(partSize, min, Math.Max(min, totalSize - min));

        var ratio = clamped / totalSize;
        ratio += (((clamped + direction) / totalSize) - ratio) / 2;

        return ForgeMetrics.NormalizeWeight((float)ratio);
    }

    /// <summary>
    /// 画一条 1px 的分界线；<see langword="null"/> 表示不画（停靠边界本来就不画线）。
    /// </summary>
    /// <remarks>
    /// 停靠的面板与编辑区之间不需要线：两块颜色本来就不同，上游那条分隔器可见宽度是 0
    /// （<c>ToolWindowPane.kt:197</c>）。**覆盖层（SLIDING）要**：它浮在编辑区上面，
    /// 不画线就分不出哪儿是面板、哪儿是被盖住的内容 —— 上游为此单给 SLIDING 加了一条 divider
    /// （<c>InternalDecoratorImpl.kt:599-612</c>，按 anchor 加在 EAST/WEST/NORTH/SOUTH）。
    /// </remarks>
    public IBrush? LineBrush
    {
        get => _lineBrush;
        set
        {
            if (Equals(_lineBrush, value)) return;
            _lineBrush = value;
            InvalidateVisual();
        }
    }

    private IBrush? _lineBrush;

    /// <inheritdoc />
    /// <remarks>
    /// <b>必须画点东西，哪怕是透明的。</b>Avalonia 的命中测试基于渲染内容 ——
    /// 一个什么都不画的 <see cref="Control"/> 收不到任何指针事件，
    /// 于是热区在那儿、光标也变了，就是拖不动。
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        if (LineBrush is not { } brush)
        {
            return;
        }

        // 线画在热区正中 —— 热区骑在边界上，中线就是边界（见 ForgeDockPane.ResizerRect）
        var horizontalBar = IsSplitDivider ? !Anchor.IsHorizontal() : Anchor.IsHorizontal();
        var rect = horizontalBar
            ? new Rect(0, Math.Round((Bounds.Height - 1) / 2), Bounds.Width, 1)
            : new Rect(Math.Round((Bounds.Width - 1) / 2), 0, 1, Bounds.Height);

        context.FillRectangle(brush, rect);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // 分割线的轴与停靠边界垂直（见 IsSplitDivider 的说明）
        var horizontalBar = IsSplitDivider ? !Anchor.IsHorizontal() : Anchor.IsHorizontal();
        return horizontalBar ? new Size(0, MouseZone) : new Size(MouseZone, 0);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _dragging = true;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging || Parent is not Visual parent)
        {
            return;
        }

        var point = e.GetPosition(parent);
        var bounds = parent.Bounds;

        if (IsSplitDivider)
        {
            // sideWeight 是**非 split 那半占的比例**（ForgeDockPane.ComputeSplitProportion）。
            // 横边左右分：非 split 在左；竖边上下分：非 split 在上。
            var slot = SlotBounds ?? new Rect(bounds.Size);
            var (first, span) = Anchor.IsHorizontal()
                ? (point.X - slot.X, slot.Width)
                : (point.Y - slot.Y, slot.Height);

            WeightChanged?.Invoke(
                this,
                new DockWeightChangedEventArgs(Anchor, ComputeWeight(first, span), isSideWeight: true));
            e.Handled = true;
            return;
        }

        var (part, total) = Anchor switch
        {
            ToolWindowAnchor.Left => (point.X, bounds.Width),
            ToolWindowAnchor.Right => (bounds.Width - point.X, bounds.Width),
            ToolWindowAnchor.Top => (point.Y, bounds.Height),
            _ => (bounds.Height - point.Y, bounds.Height),
        };

        WeightChanged?.Invoke(this, new DockWeightChangedEventArgs(Anchor, ComputeWeight(part, total)));
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }
}

/// <summary>
/// <see cref="ForgeDockResizer.WeightChanged"/> 的参数。
/// </summary>
/// <param name="anchor">被拖的是哪条边。</param>
/// <param name="weight">新的权重。</param>
/// <param name="isSideWeight">拖的是不是格内那条分割线（改 <c>sideWeight</c>）。</param>
public sealed class DockWeightChangedEventArgs(ToolWindowAnchor anchor, float weight, bool isSideWeight = false)
    : EventArgs
{
    /// <summary>被拖的是哪条边。</summary>
    public ToolWindowAnchor Anchor { get; } = anchor;

    /// <summary>新的权重。</summary>
    public float Weight { get; } = weight;

    /// <summary>
    /// 拖的是不是**同一条边里两个窗口之间**那条线（改的是 <c>sideWeight</c>，不是这条边的整体权重）。
    /// </summary>
    public bool IsSideWeight { get; } = isSideWeight;
}
