using Avalonia;
using Avalonia.Controls;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 一排 tab 的排布：横着排，间隙 0。
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

    /// <summary>
    /// 放不下就<b>换行</b>，一个都不藏。
    /// </summary>
    /// <remarks>
    /// 编辑区的文档标签行用这个：文档是用户自己开的，藏起来就等于找不回来
    /// （标签行还没有「更多」弹窗，见 <see cref="ArrangeOverride"/> 的说明）。
    /// <para>
    /// 工具窗口的标题条**不能**换行（那一行还有标题和工具按钮，高度是固定的），
    /// 所以默认关着，仍走上游 <c>TabContentLayout</c> 的「压成 0 宽 + 更多弹窗」。
    /// </para>
    /// </remarks>
    public bool WrapRows { get; set; }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;

        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }

            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += Math.Min(child.DesiredSize.Width, ForgeMetrics.TabMaxWidth);
            height = Math.Max(height, child.DesiredSize.Height);
        }

        if (!WrapRows || double.IsInfinity(availableSize.Width) || width <= availableSize.Width)
        {
            return new Size(width, height);
        }

        // 换行：宽度吃满给的，高度按排出来的行数长
        var wrapped = Wrap(availableSize.Width, height);
        var rows = wrapped.Count == 0 ? 1 : wrapped[^1].Row + 1;
        return new Size(availableSize.Width, height * rows);
    }

    /// <summary>
    /// 按给定宽度把标签折成若干行。
    /// </summary>
    /// <remarks>
    /// 一个标签比整行还宽时**独占一行**并按行宽收窄——否则这一行永远排不下，会死循环。
    /// 这是换行布局里唯一还会"切"的地方。
    /// </remarks>
    private List<(Control Child, int Row, Rect Slot)> Wrap(double width, double rowHeight)
    {
        var result = new List<(Control, int, Rect)>();
        var x = 0.0;
        var row = 0;

        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }

            var w = WidthOf(child);
            if (x > 0 && x + w > width)
            {
                row++;
                x = 0;
            }

            var slot = new Rect(x, row * rowHeight, Math.Min(w, width), rowHeight);
            result.Add((child, row, slot));
            x += slot.Width;
        }

        return result;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 两条路：<see cref="WrapRows"/> 开着就**换行**（编辑区的文档标签），
    /// 关着走上游 <c>TabContentLayout.java:249-272 · layout</c> 的单行规则
    /// （工具窗口的标题条，那一行高度固定、换不了行）。
    /// <para>
    /// 单行这一支照抄上游的 <c>reachedBounds</c>：**第一个越界的用剩下的宽度画出来**
    /// （<c>width = bounds.width - eachX - toolbarWidth</c>），只有它之后的才压成 0 宽
    /// （<c>:161</c> 靠 <c>width == 0</c> 挑出被藏起来的）。
    /// ⚠ 本仓原来只留了「放不下 → 0 宽」那一句，后果不是"少显示几个"，而是
    /// **一个都不显示**：窄行里第一个标签就放不下，从它起全被压成 0。
    /// </para>
    /// <para>
    /// <b>两支都不照抄上游的「淘汰」那一段</b>（<c>:223-247</c>：总宽超了就从两端往下丢、
    /// 只保选中的那个）：上游丢掉的能从标签行末尾的「更多」弹窗里选回来
    /// （<c>:161-168 · showMorePopup</c>），而本仓还没有那个弹窗 ——
    /// 照抄等于把标签丢进一个打不开的抽屉，而且会让**左边的标签凭空消失**。
    /// </para>
    /// </remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (WrapRows)
        {
            return ArrangeWrapped(finalSize);
        }

        var x = 0.0;
        var hidden = 0;

        var laying = Children.Where(c => c.IsVisible).ToList();
        var reachedBounds = false;

        foreach (var child in laying)
        {
            var width = WidthOf(child);

            if (x + width >= finalSize.Width)
            {
                // 第一个越界的：把剩下的宽度全给它 —— 看到的是半个标签，而不是没有标签
                var rest = reachedBounds ? 0 : Math.Max(0, finalSize.Width - x);
                var clipped = new Rect(x, 0, rest, finalSize.Height);
                _slots[child] = clipped;
                child.Arrange(clipped);

                if (rest <= 0)
                {
                    hidden++;
                }

                x += rest;
                reachedBounds = true;
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

    /// <summary>换行排布：一个都不藏，放不下就往下一行放。</summary>
    private Size ArrangeWrapped(Size finalSize)
    {
        var rowHeight = Children.Where(c => c.IsVisible).Select(c => c.DesiredSize.Height).DefaultIfEmpty(0).Max();

        foreach (var (child, _, slot) in Wrap(finalSize.Width, rowHeight))
        {
            _slots[child] = slot;

            // 被拖的那一个只改 x，其余照排布来（同非换行那一支）
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
        }

        HiddenTabCount = 0;
        return finalSize;
    }

    /// <summary>这一个标签排布时该占多宽（封顶 <see cref="ForgeMetrics.TabMaxWidth"/>）。</summary>
    private static double WidthOf(Control child) => Math.Min(child.DesiredSize.Width, ForgeMetrics.TabMaxWidth);

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
