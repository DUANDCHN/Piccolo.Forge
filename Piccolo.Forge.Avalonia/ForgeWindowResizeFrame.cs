using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 无装饰窗口四周那圈「抓着改大小」的边条。
/// </summary>
/// <remarks>
/// 对应上游 <c>FloatingDecorator.java:102-108</c>：浮动窗口 <c>setUndecorated(true)</c> 之后，
/// 它自己往四边塞了四个 <c>BorderItem</c>（<c>ANCHOR_TOP/LEFT/BOTTOM/RIGHT</c>），
/// 宽度取 <c>DIVIDER_WIDTH</c>(3, <c>:66</c>)。
/// <para>
/// 为什么不用系统边框（<c>WindowDecorations.BorderOnly</c>）：那会在窗口四周画一圈边线，
/// 暗色下顶上那条尤其显眼，而且把标题条整体往下顶一像素 —— 标题条此时**就是**窗口的标题栏，
/// 上面多一条线就不像标题栏了。
/// </para>
/// <para>八块而不是四块：四角要能斜着拉。上游把角的判断塞在 BorderItem 内部（按指针落点算 anchor），
/// 这里直接摆八块，省掉那套坐标判断。</para>
/// </remarks>
public sealed class ForgeWindowResizeFrame : Panel
{
    /// <summary>边条宽度 3（<c>FloatingDecorator.java:66 · DIVIDER_WIDTH</c>）。</summary>
    public const double GripThickness = 3;

    private readonly Grip[] _grips =
    [
        new(WindowEdge.NorthWest), new(WindowEdge.North), new(WindowEdge.NorthEast),
        new(WindowEdge.West), new(WindowEdge.East),
        new(WindowEdge.SouthWest), new(WindowEdge.South), new(WindowEdge.SouthEast),
    ];

    /// <summary>把内容裹进来，四周加上抓手。</summary>
    /// <param name="content">窗口内容。</param>
    public ForgeWindowResizeFrame(Control content)
    {
        Children.Add(content);
        foreach (var grip in _grips)
        {
            Children.Add(grip);
        }
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        // 内容铺满 —— 抓手是**盖在**它上面的一圈，不挤占布局
        // （上游是 BorderLayout 的四边 + CENTER，等价于内容让出 3px；
        //  这里盖着放，面板内容不会因为浮动就少 6px 宽）。
        foreach (var child in Children)
        {
            if (child is not Grip)
            {
                child.Arrange(new Rect(finalSize));
            }
        }

        var t = GripThickness;
        var w = finalSize.Width;
        var h = finalSize.Height;
        var midWidth = Math.Max(0, w - (2 * t));
        var midHeight = Math.Max(0, h - (2 * t));

        Place(0, new Rect(0, 0, t, t));
        Place(1, new Rect(t, 0, midWidth, t));
        Place(2, new Rect(w - t, 0, t, t));
        Place(3, new Rect(0, t, t, midHeight));
        Place(4, new Rect(w - t, t, t, midHeight));
        Place(5, new Rect(0, h - t, t, t));
        Place(6, new Rect(t, h - t, midWidth, t));
        Place(7, new Rect(w - t, h - t, t, t));

        return finalSize;

        void Place(int index, Rect rect) => _grips[index].Arrange(rect);
    }

    /// <summary>一条边（或一个角）的抓手。</summary>
    private sealed class Grip(WindowEdge edge) : Control
    {
        /// <inheritdoc />
        /// <remarks>⚠ 必须画点东西，否则收不到指针事件 —— 同 <see cref="ForgeHitArea"/> 记的那条。</remarks>
        public override void Render(global::Avalonia.Media.DrawingContext context)
            => ForgeHitArea.Fill(context, Bounds.Size);

        /// <inheritdoc />
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
                && TopLevel.GetTopLevel(this) is Window window)
            {
                window.BeginResizeDrag(edge, e);
            }
        }
    }
}
