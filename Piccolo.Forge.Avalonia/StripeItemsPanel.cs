using Avalonia;
using Avalonia.Controls;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 条带内一组按钮的排布：竖着堆，间隙 0，整组宽度取最宽的那个。
/// </summary>
/// <remarks>
/// 对应上游 <c>platform/platform-impl/src/com/intellij/toolWindow/ToolWindowToolbar.kt:196</c>：
/// 条带组用的是 <c>VerticalFlowLayout(0, 0)</c>，横纵间隙都是 0
/// （<see cref="ForgeMetrics.StripeButtonGap"/>）。
/// <para>
/// 这里只做排布，不做次序。次序是模型的事，见
/// <c>Piccolo.Forge.Model.StripePlacementResolver.CreateButtonLayoutComparer</c>。
/// </para>
/// <para>
/// <b>拖拽期间的排布</b>见 <see cref="DraggedChild"/> 与 <see cref="DropIndex"/>：
/// 被拖的那个从队列里拿掉、落点处留一个等大的空槽——这是上游
/// <c>AbstractDroppableStripe.kt</c> 的做法，不是额外发明的效果。
/// </para>
/// </remarks>
public class StripeItemsPanel : Panel
{
    /// <summary><see cref="DraggedChild"/> 的属性定义。</summary>
    public static readonly StyledProperty<Control?> DraggedChildProperty =
        AvaloniaProperty.Register<StripeItemsPanel, Control?>(nameof(DraggedChild));

    /// <summary><see cref="DropIndex"/> 的属性定义。</summary>
    public static readonly StyledProperty<int> DropIndexProperty =
        AvaloniaProperty.Register<StripeItemsPanel, int>(nameof(DropIndex), defaultValue: -1);

    /// <summary><see cref="DropSlotHeight"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> DropSlotHeightProperty =
        AvaloniaProperty.Register<StripeItemsPanel, double>(nameof(DropSlotHeight));

    static StripeItemsPanel()
    {
        AffectsMeasure<StripeItemsPanel>(DraggedChildProperty, DropIndexProperty, DropSlotHeightProperty);
        AffectsArrange<StripeItemsPanel>(DraggedChildProperty, DropIndexProperty, DropSlotHeightProperty);
    }

    /// <summary>
    /// 正在被拖走的那个子项——**排布时当它不存在**。
    /// </summary>
    /// <remarks>
    /// 对应 <c>AbstractDroppableStripe.kt:601 · getButtonsToLayOut</c> 里的
    /// <c>tools.remove(dragButton)</c>。
    /// <para>
    /// 不拿掉的后果不只是"少了个空位"：后面所有按钮的累计坐标都多算一个按钮高，
    /// 于是**落点判定整体偏一格**——用户表现为「明明放在下面，蓝框提示在上面」。
    /// </para>
    /// <para>不属于本面板也没关系，找不到就什么都不发生，所以宿主可以四个组一起设。</para>
    /// </remarks>
    public Control? DraggedChild
    {
        get => GetValue(DraggedChildProperty);
        set => SetValue(DraggedChildProperty, value);
    }

    /// <summary>
    /// 在**排除被拖项之后**的序列里，第几个位置前面留出空槽；<c>-1</c> 表示不留。
    /// </summary>
    /// <remarks>
    /// 等于序列长度时留在末尾。对应 <c>layoutDragButton</c> 把 <c>eachY</c> 往前推一个按钮高，
    /// 后面的按钮因此让位。
    /// </remarks>
    public int DropIndex
    {
        get => GetValue(DropIndexProperty);
        set => SetValue(DropIndexProperty, value);
    }

    /// <summary>空槽的高度，一般就是被拖按钮的高度。</summary>
    public double DropSlotHeight
    {
        get => GetValue(DropSlotHeightProperty);
        set => SetValue(DropSlotHeightProperty, value);
    }

    /// <summary>
    /// 上一次排布算出来的空槽矩形（面板自己的坐标系）；没有空槽时是 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 蓝色落点框要画在**这里**，而不是另算一遍——上游那两件事本来就是同一个
    /// <c>layoutDragButton</c> 干的（它同时写 <c>drawRectangle</c> 和推进 <c>eachY</c>），
    /// 分两处算迟早对不上。
    /// </remarks>
    public Rect? DropSlotRect { get; private set; }

    /// <summary>参与排布的子项：可见、且不是正在被拖走的那个。</summary>
    private IEnumerable<Control> LayoutChildren
    {
        get
        {
            var dragged = DraggedChild;
            foreach (var child in Children)
            {
                if (child.IsVisible && !ReferenceEquals(child, dragged))
                {
                    yield return child;
                }
            }
        }
    }

    /// <summary>空槽实际会插在第几个之前（夹到合法范围；<c>-1</c> 表示没有）。</summary>
    private int EffectiveDropIndex(int count)
        => DropIndex < 0 || DropSlotHeight <= 0 ? -1 : Math.Clamp(DropIndex, 0, count);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        var count = 0;

        foreach (var child in LayoutChildren)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
            height += child.DesiredSize.Height + ForgeMetrics.StripeButtonGap;
            count++;
        }

        // 被拖走的那个仍要 Measure：它没参与排布，但还是这棵树上的控件，
        // 不量的话 DesiredSize 是 0，拖回来时会闪一下。
        DraggedChild?.Measure(new Size(availableSize.Width, double.PositiveInfinity));

        if (EffectiveDropIndex(count) >= 0)
        {
            height += DropSlotHeight;
        }

        return new Size(width, height);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 宽度统一取所有可见子项 DesiredSize 的最大值（上游的 <c>data.fitSize</c>，
    /// <c>AbstractDroppableStripe.kt:296-301</c>），高度各按各的
    /// （<c>:482-495 · layoutButton</c>）。**不是**铺满容器宽度。
    /// </remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var laidOut = LayoutChildren.ToList();

        var fitWidth = 0.0;
        foreach (var child in laidOut)
        {
            fitWidth = Math.Max(fitWidth, child.DesiredSize.Width);
        }

        var dropIndex = EffectiveDropIndex(laidOut.Count);
        DropSlotRect = null;

        var y = 0.0;
        for (var i = 0; i <= laidOut.Count; i++)
        {
            if (i == dropIndex)
            {
                DropSlotRect = new Rect(0, y, Math.Max(fitWidth, 1), DropSlotHeight);
                y += DropSlotHeight;
            }

            if (i == laidOut.Count)
            {
                break;
            }

            var child = laidOut[i];
            var height = child.DesiredSize.Height;
            child.Arrange(new Rect(0, y, fitWidth, height));
            y += height + ForgeMetrics.StripeButtonGap;
        }

        // 被拖走的那个排成**零尺寸**：既不占位、也不画出来。
        // ⚠ 早先是排到 y 为负的地方（面板外），那是错的 —— 面板不裁剪，于是它会画在面板**上方**。
        //    上组因为紧贴窗口顶、负坐标落到窗外看不见，下组却正好落在界面里 ——
        //    表现成"左下角拖动时上面多出一个图标，别的角没有"。
        //    不能靠 IsVisible=false：那样连拖拽虚影都取不到它的视觉。虚影现在走
        //    ForgeToolWindowFrame 在拖拽开始时拍的位图快照，与这个控件解耦。
        DraggedChild?.Arrange(default);

        return new Size(Math.Max(fitWidth, finalSize.Width), Math.Max(y, finalSize.Height));
    }
}
