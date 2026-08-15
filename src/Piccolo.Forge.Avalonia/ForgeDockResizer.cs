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
            Cursor = new Cursor(value.IsHorizontal()
                ? StandardCursorType.SizeNorthSouth
                : StandardCursorType.SizeWestEast);
        }
    }

    /// <summary>热区宽度（未缩放）：6。</summary>
    public static double MouseZone => ForgeMetrics.SplitterMouseZone;

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

    /// <inheritdoc />
    /// <remarks>
    /// <b>必须画点东西，哪怕是透明的。</b>Avalonia 的命中测试基于渲染内容 ——
    /// 一个什么都不画的 <see cref="Control"/> 收不到任何指针事件，
    /// 于是热区在那儿、光标也变了，就是拖不动。
    /// 上游那条分隔器<b>可见宽度是 0</b>（<c>ToolWindowPane.kt:197</c>），
    /// 所以这里也只画透明色：看不见，但吃得到鼠标。
    /// </remarks>
    public override void Render(DrawingContext context)
        => context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
        => Anchor.IsHorizontal()
            ? new Size(0, MouseZone)
            : new Size(MouseZone, 0);

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
public sealed class DockWeightChangedEventArgs(ToolWindowAnchor anchor, float weight) : EventArgs
{
    /// <summary>被拖的是哪条边。</summary>
    public ToolWindowAnchor Anchor { get; } = anchor;

    /// <summary>新的权重。</summary>
    public float Weight { get; } = weight;
}
