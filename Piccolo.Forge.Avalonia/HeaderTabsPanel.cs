using Avalonia;
using System.Linq;
using Avalonia.Controls;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 标题条里那一排 tab 的排布：横着排，间隙 0，放不下的压成 0 宽。
/// </summary>
/// <remarks>
/// 对应 <c>platform/platform-impl/src/com/intellij/openapi/wm/impl/content/TabContentLayout.java:171-230
/// · layout</c>。
/// 「放不下就压成 0 宽、改从『更多』弹窗里选」是上游的原话
/// （<c>:161</c> 用 <c>tab.getWidth() == 0</c> 挑出被藏起来的 tab）。
/// </remarks>
public class HeaderTabsPanel : Panel
{
    /// <summary>
    /// 有多少个 tab 因为放不下被压成了 0 宽。
    /// </summary>
    /// <remarks>上游据此决定「更多」按钮显不显示（<c>TabContentLayout.java:155-168</c>）。</remarks>
    public int HiddenTabCount { get; private set; }

    /// <summary>
    /// 按什么次序排；<see langword="null"/> 就按 <see cref="Panel.Children"/> 的次序。
    /// </summary>
    /// <remarks>
    /// ⚠ <b>次序与"谁是子控件"是两件事，别混。</b>原来重排是靠 <c>Children.Move</c>，
    /// 而 Avalonia 的 Move 是摘下来再挂回去 —— 顺带把**指针捕获**丢掉，
    /// 正在拖的标签页就此收不了尾。把次序单独拿出来，可视树在拖动全程纹丝不动。
    /// <para>
    /// 说到底这是<b>数据源该管的事</b>：换成 <c>TabStrip</c> + <c>ItemsSource</c> 之后
    /// 这个属性连同 <c>Reconcile</c> 都该消失。现阶段先把捕获这条止住。
    /// </para>
    /// </remarks>
    public IReadOnlyList<Control>? Order { get; set; }

    private IEnumerable<Control> InOrder =>
        Order is null ? Children : Order.Where(Children.Contains);

    /// <summary>正在被拖的那一个；<see langword="null"/> 表示没人在拖。</summary>
    /// <remarks>见 <see cref="DraggedX"/>。</remarks>
    public Control? DraggedChild { get; set; }

    /// <summary>
    /// 被拖那一个画在哪（左边缘，相对本面板）。
    /// </summary>
    /// <remarks>
    /// 对应 <c>JBTabsImpl.kt:2280-2294 · moveDraggedTabLabel</c>：横向标签行
    /// <b>只改 x，y / 宽 / 高全部保持排布算出来的值</b>。所以被拖的那一页是"沿着行滑"，
    /// 不会边拖边变形，也不会飘到行外面 —— 飘不出去还额外靠
    /// <see cref="ForgeEditorTabDragTarget.MoveToFit"/> 夹一道。
    /// <para>
    /// 注意它<b>仍然占着自己那一格</b>：拖动过程中模型已经被 <c>reallocate</c> 换过位了，
    /// 那一格就是它"排完之后该在的地方"，松手时它落回去正好严丝合缝。
    /// </para>
    /// </remarks>
    public double DraggedX { get; set; }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;

        foreach (var child in InOrder)
        {
            if (!child.IsVisible)
            {
                continue;
            }

            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += Math.Min(child.DesiredSize.Width, ForgeMetrics.TabMaxWidth);
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        var hidden = 0;

        foreach (var child in InOrder)
        {
            if (!child.IsVisible)
            {
                continue;
            }

            var width = Math.Min(child.DesiredSize.Width, ForgeMetrics.TabMaxWidth);

            // 放不下就压成 0 宽（TabContentLayout.java:161 靠这个判断哪些被藏了）
            if (x + width > finalSize.Width)
            {
                _slots[child] = new Rect(x, 0, 0, finalSize.Height);
                child.Arrange(new Rect(x, 0, 0, finalSize.Height));
                hidden++;
                continue;
            }

            var slot = new Rect(x, 0, width, finalSize.Height);
            _slots[child] = slot;

            // 被拖的那一个：只把 x 换成鼠标处，其余照排布来（moveDraggedTabLabel）。
            // ZIndex 抬高是 Avalonia 这边的补充 —— Swing 那边靠 z 序天然在上，
            // 这里不抬的话它会被后面的标签盖住半截。
            if (ReferenceEquals(child, DraggedChild))
            {
                child.ZIndex = 1;
                child.Arrange(slot.WithX(DraggedX));
            }
            else
            {
                child.ZIndex = 0;
                child.Arrange(slot);
            }

            x += width;
        }

        HiddenTabCount = hidden;
        return finalSize;
    }

    private readonly Dictionary<Control, Rect> _slots = [];

    /// <summary>
    /// 某一个孩子<b>排布算出来的格子</b>（不是它当前画在哪）。
    /// </summary>
    /// <remarks>
    /// 拖拽判定要的是格子而不是控件当前的 <c>Bounds</c>：被拖的那一个
    /// <c>Bounds</c> 已经跟着鼠标跑了，拿它去算"该和谁换位"会自己咬自己。
    /// </remarks>
    /// <param name="child">哪一个。</param>
    /// <returns>格子；还没排过则为 <see langword="null"/>。</returns>
    public Rect? SlotOf(Control child) => _slots.TryGetValue(child, out var slot) ? slot : null;
}
