using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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
[ForgeThemeless("纯布局面板：只把标签排成一行。")]
internal class HeaderTabsPanel : Panel
{
    /// <summary>
    /// 有多少个 tab 因为放不下被压成了 0 宽。
    /// </summary>
    /// <remarks>上游据此决定「更多」按钮显不显示（<c>TabContentLayout.java:155-168</c>）。</remarks>
    public int HiddenTabCount { get; private set; }

    /// <summary>被"摘起来"的那一个；<see langword="null"/> 表示没人在拖。</summary>
    /// <remarks>
    /// ★ <b>摘起 = 排布跳过它、把它摆成空。</b>它的视觉由**拖影**承担
    /// （编辑区：覆盖层缩略图 / 幽灵窗；面板标题条：幽灵窗）——
    /// 真控件出不了这条行（会被祖先裁掉半截），夹在行里又会被读成"吸附"。
    /// 缝由 <see cref="DropIndex"/> 表达，投影吸附在缝上；拖影永不吸附。
    /// </remarks>
    public Control? DraggedChild { get; set; }

    /// <summary>
    /// 空档占位落在第几个位置（0 = 最前面）；<see langword="null"/> 表示不留空档。
    /// </summary>
    /// <remarks>
    /// 上游拖动中是**当场 reallocate**，其余标签跟着让位、空出来的那一格就是"松手会落在哪"
    /// （<c>JBTabsImpl.kt:3331</c>）。本仓的模型变更**推迟到松手**（拖动中改模型会让
    /// TabStrip 回收/换父当前容器，那正是"拖影卡住、标签变空白"那一串 bug 的来源），
    /// 所以改成排布这一层留空档：视觉上与上游一致，模型不动。
    /// <para>
    /// 索引按**除被拖那一个之外**的次序算 —— 被拖的那一个此刻是浮在上面跟手的，
    /// 不占行内位置。
    /// </para>
    /// </remarks>
    public int? DropIndex { get; set; }

    /// <summary>
    /// 落点那块**投影矩形**在哪（排布时算出来）；<see langword="null"/> 表示此刻没有落点。
    /// </summary>
    /// <remarks>
    /// 分工是这样的：<b>被拖的那一个跟着光标自由走（不吸附）</b>，
    /// <b>投影矩形吸附到格位上</b> —— 一个说"我现在拿着它"，一个说"松手会落在这儿"。
    /// 两个都吸附就没法瞄准，两个都不吸附就说不清落点。
    /// <para>
    /// ⚠ <b>画它的是外面那层 TabStrip，不是本面板</b>：<c>Panel.Render</c> 是密封的。
    /// 好在 <c>Control.Render</c> 画在模板子元素之下 —— 那块地方此刻正好是空的（空档），
    /// 被拖的那个影子浮在上面，两者不打架。
    /// </para>
    /// </remarks>
    public Rect? DropRect { get; private set; }

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

        var placed = 0;
        var gapWidth = DraggedChild is { } dragged && DropIndex is not null ? WidthOf(dragged) : 0;
        DropRect = null;

        foreach (var child in laying)
        {
            var width = WidthOf(child);

            // 被摘起来的那一个：不占行内位置、也不在这里画（视觉由拖影承担）；
            // 它的格子照旧留在 _slots 里（SlotOf 的契约：排布格子，不是画在哪）
            if (ReferenceEquals(child, DraggedChild))
            {
                child.Arrange(default);
                continue;
            }

            // 轮到空档了：先空出一格再放这一个（那一格就是投影矩形的位置）
            if (DropIndex == placed && gapWidth > 0)
            {
                DropRect = new Rect(x, 0, gapWidth, finalSize.Height);
                x += gapWidth;
            }

            placed++;

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
            child.ZIndex = 0;
            child.Opacity = 1;
            child.Arrange(slot);

            x += width;
        }

        // 空档在最后一格：上面的循环没机会加，这里补上（否则拖到行尾看不到落点）
        if (DropIndex == placed && gapWidth > 0)
        {
            DropRect = new Rect(x, 0, gapWidth, finalSize.Height);
            x += gapWidth;
        }

        HiddenTabCount = hidden;
        return finalSize;
    }

    /// <summary>换行排布：一个都不藏，放不下就往下一行放。</summary>
    /// <remarks>
    /// ⚠ <b>空档与投影这两件事，两条排布分支都要做。</b>
    /// 文档标签走的正是这一条（<see cref="WrapRows"/> 开着）——
    /// 早先只在不换行那支里写了，于是"投影矩形"在文档区**一点效果都没有**。
    /// </remarks>
    private Size ArrangeWrapped(Size finalSize)
    {
        var rowHeight = Children.Where(c => c.IsVisible).Select(c => c.DesiredSize.Height).DefaultIfEmpty(0).Max();
        var gapWidth = DraggedChild is { } floating && DropIndex is not null ? WidthOf(floating) : 0;

        DropRect = null;

        // 被拖的那一个不占行内位置（浮在上面跟手），所以先按"除它之外"的次序折行，
        // 并在 DropIndex 处塞一个与它同宽的占位，折行结果才与松手后一致。
        var laying = Children.Where(c => c.IsVisible && !ReferenceEquals(c, DraggedChild)).ToList();
        var placed = 0;
        var x = 0.0;
        var row = 0;

        void Advance(double width, out Rect slot)
        {
            if (x > 0 && x + width > finalSize.Width)
            {
                row++;
                x = 0;
            }

            slot = new Rect(x, row * rowHeight, Math.Min(width, finalSize.Width), rowHeight);
            x += slot.Width;
        }

        foreach (var child in laying)
        {
            if (DropIndex == placed && gapWidth > 0)
            {
                Advance(gapWidth, out var gap);
                DropRect = gap;
            }

            placed++;
            Advance(WidthOf(child), out var slot);
            _slots[child] = slot;
            child.ZIndex = 0;
            child.Opacity = 1;
            child.Arrange(slot);
        }

        // 落点在最后：循环里没机会摆
        if (DropIndex == placed && gapWidth > 0)
        {
            Advance(gapWidth, out var gap);
            DropRect = gap;
        }

        // 被摘起来的那一个：不画（视觉由拖影承担）
        if (DraggedChild is { } dragged && dragged.IsVisible)
        {
            dragged.ZIndex = 0;
            dragged.Opacity = 1;
            dragged.Arrange(default);
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
