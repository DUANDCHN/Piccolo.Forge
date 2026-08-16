using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

        return new Size(width, height);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 放不下时按两条规矩排：
    /// <list type="number">
    /// <item>
    /// <b>第一个越界的切一半，不是压成 0</b> —— 照抄
    /// <c>TabContentLayout.java:249-272 · layout</c> 里的 <c>reachedBounds</c> 那一支
    /// （<c>width = bounds.width - eachX - toolbarWidth</c>）。只有它**之后**的才真压成 0 宽
    /// （<c>:161</c> 靠 <c>width == 0</c> 挑出被藏起来的）。
    /// </item>
    /// <item>
    /// <b>选中的那个一定看得见</b> —— 从头排会把它挤到行外时，整行的起点往后挪，
    /// 挪到它排得下为止（等价于「滚动到选中项」，与 <c>JBTabsImpl</c> 的单行布局同义）。
    /// </item>
    /// </list>
    /// <para>
    /// ⚠ 这两条以前都没有，只剩「放不下 → 0 宽」一句。后果不是"少显示几个"，
    /// 而是**一个都不显示**：窄窗格里第一个标签就放不下，于是从它起全被压成 0，
    /// 标签行整条空白 —— 文档还开着，看上去却像没开。
    /// </para>
    /// <para>
    /// <b>有意不照抄上游的「淘汰」那一段</b>（<c>:223-247</c>：总宽超了就从两端往下丢）：
    /// 上游丢掉的那些能从标签行末尾的「更多」弹窗里选回来
    /// （<c>:161-168 · showMorePopup</c>），而本仓的标签行**还没有那个弹窗**——
    /// 照抄就等于把文档丢进一个打不开的抽屉。改成挪起点，一个都不会失联。
    /// 将来补上「更多」弹窗时，这里可以换回上游那一段。
    /// </para>
    /// </remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        var hidden = 0;

        var laying = Children.Where(c => c.IsVisible).ToList();

        // 起点：默认从第一个排；选中的那个若排不进来，起点往后挪到它排得下为止
        var start = 0;
        var selected = laying.FindIndex(IsSelected);
        if (selected >= 0)
        {
            var span = 0.0;
            for (var i = selected; i >= 0; i--)
            {
                span += WidthOf(laying[i]);
                if (span > finalSize.Width)
                {
                    // i 这一个已经挤不进来了，起点取它的下一个（选中的那个自己也可能被切一半）
                    start = Math.Min(i + 1, selected);
                    break;
                }
            }
        }

        for (var i = 0; i < start; i++)
        {
            var empty = new Rect(0, 0, 0, finalSize.Height);
            _slots[laying[i]] = empty;
            laying[i].Arrange(empty);
            hidden++;
        }

        var reachedBounds = false;

        foreach (var child in laying.Skip(start))
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

    /// <summary>这一个标签排布时该占多宽（封顶 <see cref="ForgeMetrics.TabMaxWidth"/>）。</summary>
    private static double WidthOf(Control child) => Math.Min(child.DesiredSize.Width, ForgeMetrics.TabMaxWidth);

    /// <summary>是不是当前选中的那一个 —— 淘汰阶段它有豁免权。</summary>
    private static bool IsSelected(Control child) => child is TabStripItem { IsSelected: true };

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
