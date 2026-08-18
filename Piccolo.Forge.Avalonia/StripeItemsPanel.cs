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
/// <b>拖拽期间的排布</b>见 <see cref="DraggedChild"/> 与 <see cref="DropOffset"/>：
/// 被拖的那个从队列里拿掉、落点处留一个等大的空槽——这是上游
/// <c>AbstractDroppableStripe.kt</c> 的做法，不是额外发明的效果。
/// </para>
/// </remarks>
public class StripeItemsPanel : Panel
{
    /// <summary><see cref="DraggedChild"/> 的属性定义。</summary>
    public static readonly StyledProperty<Control?> DraggedChildProperty =
        AvaloniaProperty.Register<StripeItemsPanel, Control?>(nameof(DraggedChild));

    /// <summary><see cref="DropOffset"/> 的属性定义。</summary>
    public static readonly StyledProperty<double?> DropOffsetProperty =
        AvaloniaProperty.Register<StripeItemsPanel, double?>(nameof(DropOffset));

    /// <summary><see cref="DropSlotHeight"/> 的属性定义。</summary>
    public static readonly StyledProperty<double> DropSlotHeightProperty =
        AvaloniaProperty.Register<StripeItemsPanel, double>(nameof(DropSlotHeight));

    static StripeItemsPanel()
    {
        AffectsMeasure<StripeItemsPanel>(DraggedChildProperty, DropOffsetProperty, DropSlotHeightProperty);
        AffectsArrange<StripeItemsPanel>(DraggedChildProperty, DropOffsetProperty, DropSlotHeightProperty);
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
    /// 落点在**排除被拖项、也不含空槽**的那条序列里的纵向偏移；<see langword="null"/> 表示没在拖。
    /// </summary>
    /// <remarks>
    /// 宿主交进来的是**落点本身**，不是插入位置——插到第几个由本面板在排布期算
    /// （<see cref="DropIndex"/>）。对应上游 <c>processDropButton</c> 只存下 <c>dropRectangle</c>、
    /// 判定留给 <c>recomputeBounds</c>（<c>AbstractDroppableStripe.kt:197-223</c>）。
    /// <para>
    /// ★ <b>为什么不是宿主直接给插入位置</b>：那样宿主必须先排一次布才能读回空槽位置，
    /// 于是每个指针事件都要同步排一次整棵树。落点交给排布去解，就只剩"标脏、等下一帧"，
    /// 与上游的 <c>revalidate()</c> 同形。
    /// </para>
    /// <para>
    /// ⚠ 口径是「**去掉空槽之后**那条序列的顶」，不是面板当前的顶边——面板的顶边**会动**：
    /// 下组贴底排布（<c>ForgeToolWindowToolbar</c> 里的 <c>Dock.Bottom</c>），
    /// 插一个空槽就把整组往上顶一个按钮高。拿会动的顶边换算落点，判定恒偏一格，
    /// 表现为下组「放在这一格、却插到隔壁那格」。换算见 <c>ForgeControl.ApplyDropOffset</c>。
    /// </para>
    /// </remarks>
    public double? DropOffset
    {
        get => GetValue(DropOffsetProperty);
        set => SetValue(DropOffsetProperty, value);
    }

    /// <summary>空槽的高度，一般就是被拖按钮的高度。</summary>
    public double DropSlotHeight
    {
        get => GetValue(DropSlotHeightProperty);
        set => SetValue(DropSlotHeightProperty, value);
    }

    /// <summary>
    /// 上一次排布定下的插入位置：在**排除被拖项之后**的序列里插在第几个之前；
    /// <c>-1</c> 表示没有空槽。
    /// </summary>
    /// <remarks>
    /// 等于序列长度时表示接在末尾。松手时按它定次序——**不要**在排布之外再算一遍：
    /// 那样"看见的"和"落到的"就成了两处各算的结果（回归见 <c>InteractionWiringTests</c>）。
    /// 对应上游 <c>LayoutData.dragInsertPosition</c>，同样是排布算完、<c>finishDrop</c> 直接取用。
    /// </remarks>
    public int DropIndex { get; private set; } = -1;

    /// <summary>
    /// 上一次排布算出来的空槽矩形（面板自己的坐标系）；没有空槽时是 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 蓝色落点框要画在**这里**，而不是另算一遍——上游那两件事本来就是同一个
    /// <c>layoutDragButton</c> 干的（它同时写 <c>drawRectangle</c> 和推进 <c>eachY</c>），
    /// 分两处算迟早对不上。
    /// </remarks>
    public Rect? DropSlotRect { get; private set; }

    /// <summary>这一组现在要不要留空槽。</summary>
    private bool HasDropSlot => DropOffset is not null && DropSlotHeight > 0;

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

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;

        foreach (var child in LayoutChildren)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
            height += child.DesiredSize.Height + ForgeMetrics.StripeButtonGap;
        }

        // 被拖走的那个仍要 Measure：它没参与排布，但还是这棵树上的控件，
        // 不量的话 DesiredSize 是 0，拖回来时会闪一下。
        DraggedChild?.Measure(new Size(availableSize.Width, double.PositiveInfinity));

        // 空槽在与不在改变整组高度，插在第几个则不改变——所以量的时候只问"有没有"。
        // 拖动全程这个答案只在进出本组时翻一次，往上传播的重排也就只有那么两次。
        if (HasDropSlot)
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
    /// <para>
    /// 落点判定也在这里：一次遍历同时定出插入位置、让位、空槽矩形——
    /// 与上游 <c>recomputeBounds</c> 同一个形（<c>:341-399</c>）。
    /// </para>
    /// </remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var laidOut = LayoutChildren.ToList();

        var fitWidth = 0.0;
        foreach (var child in laidOut)
        {
            fitWidth = Math.Max(fitWidth, child.DesiredSize.Width);
        }

        DropIndex = HasDropSlot
            ? StripeDropResolver.ResolveInsertIndex(
                DropOffset!.Value,
                laidOut.Select(x => x.DesiredSize.Height).ToList())
            : -1;
        DropSlotRect = null;

        var y = 0.0;
        for (var i = 0; i <= laidOut.Count; i++)
        {
            if (i == DropIndex)
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
        //    ForgeControl 在拖拽开始时拍的位图快照，与这个控件解耦。
        DraggedChild?.Arrange(default);

        return new Size(Math.Max(fitWidth, finalSize.Width), Math.Max(y, finalSize.Height));
    }
}
