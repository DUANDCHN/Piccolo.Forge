using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;
using Piccolo.Forge.Model;
using Piccolo.Forge.Themes;

namespace Piccolo.Forge.Avalonia;

/// <summary>
/// 停靠区：四条边各一格，中间是编辑区。
/// </summary>
/// <remarks>
/// 对应 <c>platform/platform-impl/src/com/intellij/toolWindow/ToolWindowPane.kt</c> 的两层
/// <c>ThreeComponentsSplitter</c>（<c>度量与主题键.md</c> §6.1）：
/// <list type="bullet">
/// <item><c>wideScreenSupport = false</c>（**默认**，<c>UISettingsState.kt:93</c>）→
/// 外层是竖分隔器，**TOP/BOTTOM 通栏，LEFT/RIGHT 夹在中间**；</item>
/// <item><c>= true</c> → 反过来，LEFT/RIGHT 通栏。</item>
/// </list>
/// <para>
/// 尺寸换算照抄 <c>ToolWindowPane.kt:315-355 · setWeight</c>：
/// **权重乘的是整个 pane 的宽/高，不是剩余空间**。
/// 横边（TOP/BOTTOM）乘高，竖边（LEFT/RIGHT）乘宽。
/// 所有下发的权重都先过 <see cref="ForgeMetrics.NormalizeWeight"/>（<c>:85-90</c>）。
/// </para>
/// </remarks>
[ForgeThemeless("纯布局面板：只排子元素的位置，不画、不取色。")]
public class ForgeDockPane : Panel
{
    /// <summary><see cref="IsWideScreen"/> 的属性定义。</summary>
    public static readonly StyledProperty<bool> IsWideScreenProperty =
        AvaloniaProperty.Register<ForgeDockPane, bool>(nameof(IsWideScreen));

    /// <summary>四条边上的槽位。</summary>
    public static readonly AttachedProperty<ToolWindowAnchor?> AnchorProperty =
        AvaloniaProperty.RegisterAttached<ForgeDockPane, Control, ToolWindowAnchor?>("Anchor");

    /// <summary>该槽位占整个 pane 的比例。</summary>
    public static readonly AttachedProperty<double> WeightProperty =
        AvaloniaProperty.RegisterAttached<ForgeDockPane, Control, double>("Weight", WindowInfo.DefaultWeight);

    /// <summary>是不是同一条边的「另一半」。</summary>
    public static readonly AttachedProperty<bool> IsSplitProperty =
        AvaloniaProperty.RegisterAttached<ForgeDockPane, Control, bool>("IsSplit");

    /// <summary>同一格里两个窗口的分配比例。</summary>
    public static readonly AttachedProperty<double> SideWeightProperty =
        AvaloniaProperty.RegisterAttached<ForgeDockPane, Control, double>("SideWeight", 0.5);

    /// <summary>
    /// 这个面板是**盖在编辑区上**的（SLIDING / 停靠不固定），不占编辑区的地方。
    /// </summary>
    /// <remarks>
    /// 上游把 SLIDING 的装饰器加进 layeredPane 的 PALETTE_LAYER
    /// （<c>ToolWindowPane.kt:432-459 · setComponent</c> 那一支），所以它**不参与**
    /// 四条边的尺寸分配 —— 编辑区该多大还是多大，面板浮在上面。
    /// </remarks>
    public static readonly AttachedProperty<bool> IsOverlayProperty =
        AvaloniaProperty.RegisterAttached<ForgeDockPane, Control, bool>("IsOverlay");

    /// <summary>读一个子控件是不是覆盖层面板。</summary>
    /// <param name="control">子控件。</param>
    /// <returns>是则为 <see langword="true"/>。</returns>
    public static bool GetIsOverlay(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(IsOverlayProperty);
    }

    /// <summary>设一个子控件是不是覆盖层面板。</summary>
    /// <param name="control">子控件。</param>
    /// <param name="value">是不是。</param>
    public static void SetIsOverlay(Control control, bool value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(IsOverlayProperty, value);
    }

    /// <summary>这个子控件是哪条边的拖拽热区（<see langword="null"/> 表示不是）。</summary>
    public static readonly AttachedProperty<ToolWindowAnchor?> IsResizerProperty =
        AvaloniaProperty.RegisterAttached<ForgeDockPane, Control, ToolWindowAnchor?>("IsResizer");

    /// <summary>这个子控件是哪条边**格内那条分割线**的热区（<see langword="null"/> 表示不是）。</summary>
    public static readonly AttachedProperty<ToolWindowAnchor?> IsSplitResizerProperty =
        AvaloniaProperty.RegisterAttached<ForgeDockPane, Control, ToolWindowAnchor?>("IsSplitResizer");

    /// <summary>读一个子控件是不是某条边的格内分割线热区。</summary>
    /// <param name="control">子控件。</param>
    /// <returns>是哪条边的；不是则 <see langword="null"/>。</returns>
    public static ToolWindowAnchor? GetIsSplitResizer(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(IsSplitResizerProperty);
    }

    /// <summary>把一个子控件标成某条边的格内分割线热区。</summary>
    /// <param name="control">子控件。</param>
    /// <param name="value">哪条边。</param>
    public static void SetIsSplitResizer(Control control, ToolWindowAnchor? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(IsSplitResizerProperty, value);
    }

    /// <summary>
    /// 左右两条边**默认多宽**（像素）；<see cref="double.NaN"/>（默认）= 按 <c>weight</c> 算。
    /// </summary>
    /// <remarks>
    /// 只在这条边**还没有像素记录、且它的 weight 就是那个默认值**时起作用 ——
    /// 也就是"用户从没拉过、也没有存档"的那一份。用户拉过或读了档，走的都是台账里的实际值，
    /// 这个默认宽度不会把它盖掉。
    /// <para>
    /// 上下两条边不受影响：那儿的默认高度仍按 <c>weight</c> 来（宿主没有提过要改）。
    /// </para>
    /// </remarks>
    public static readonly StyledProperty<double> DefaultAnchorWidthProperty =
        AvaloniaProperty.Register<ForgeDockPane, double>(nameof(DefaultAnchorWidth), double.NaN);

    /// <summary>见 <see cref="DefaultAnchorWidthProperty"/>。</summary>
    public double DefaultAnchorWidth
    {
        get => GetValue(DefaultAnchorWidthProperty);
        set => SetValue(DefaultAnchorWidthProperty, value);
    }

    /// <summary>
    /// 某条边的尺寸被本控件改成了新的像素值——请宿主按当前 pane 反算成 weight 写回台账。
    /// </summary>
    /// <remarks>
    /// **异步**发（排到下一轮消息）：布局过程中改台账会当场再触发一次布局，那是重入。
    /// 宿主写回去只是为了存档（格式仍是 weight），写回来的值**不会**反过来改本控件的像素 ——
    /// 那条回路正是"拖动时边界每帧跳一格"的来源。
    /// </remarks>
    public event EventHandler<DockWeightChangedEventArgs>? AnchorWeightRebased;

    /// <summary>
    /// 四条边当前的像素尺寸 —— <b>排布的真值</b>。
    /// </summary>
    /// <remarks>
    /// 没有记录的边，下一轮按它的 <c>weight</c> 播种一次（首次布局、读档、恢复默认布局都是这条路）。
    /// </remarks>
    private readonly Dictionary<ToolWindowAnchor, double> _pixels = [];

    /// <summary>已经排队等着回写的边，避免同一条边连发多次。</summary>
    private readonly HashSet<ToolWindowAnchor> _rebasePending = [];

    /// <summary>
    /// 直接给某条边定一个像素尺寸（拖分隔条走这儿）。
    /// </summary>
    /// <param name="anchor">哪条边。</param>
    /// <param name="pixels">新尺寸，像素。</param>
    public void SetAnchorSize(ToolWindowAnchor anchor, double pixels)
    {
        if (double.IsNaN(pixels))
        {
            return;
        }

        _pixels[anchor] = Math.Max(0, pixels);
        InvalidateMeasure();
    }

    /// <summary>这条边上那块面板的 <c>weight</c>（播种判据用）；没有就当默认值。</summary>
    /// <param name="anchor">哪条边。</param>
    /// <returns>权重。</returns>
    private double GetSeedWeight(ToolWindowAnchor anchor)
    {
        var panel = Children.FirstOrDefault(x => x.IsVisible && GetAnchor(x) == anchor);
        return panel is null ? WindowInfo.DefaultWeight : GetWeight(panel);
    }

    /// <summary>
    /// 丢掉记着的像素尺寸，下一轮按各边的 <c>weight</c> 重新播种。
    /// </summary>
    /// <remarks>
    /// 只在**布局意图真的换了**的时候调：读档、恢复默认布局、面板换边 / 开合
    /// （宿主在 <c>ForgeControl</c> 的状态变更那一处调）。
    /// 窗口改大小、以及把像素反算成 weight 写回台账，都**不**走这儿 ——
    /// 那正是像素固定要守住的东西。
    /// </remarks>
    public void InvalidateAnchorSizes()
    {
        _pixels.Clear();
        InvalidateMeasure();
    }

    static ForgeDockPane()
    {
        AffectsMeasure<ForgeDockPane>(IsWideScreenProperty);
        AnchorProperty.Changed.AddClassHandler<Control>((c, _) => (c.Parent as ForgeDockPane)?.InvalidateMeasure());
        WeightProperty.Changed.AddClassHandler<Control>((c, _) => (c.Parent as ForgeDockPane)?.InvalidateMeasure());
        IsSplitProperty.Changed.AddClassHandler<Control>((c, _) => (c.Parent as ForgeDockPane)?.InvalidateMeasure());
        SideWeightProperty.Changed.AddClassHandler<Control>((c, _) => (c.Parent as ForgeDockPane)?.InvalidateMeasure());
        IsOverlayProperty.Changed.AddClassHandler<Control>((c, _) => (c.Parent as ForgeDockPane)?.InvalidateMeasure());
    }

    /// <summary>
    /// 是否宽屏模式，决定两层分隔器谁在外。默认 <see langword="false"/>。
    /// </summary>
    public bool IsWideScreen
    {
        get => GetValue(IsWideScreenProperty);
        set => SetValue(IsWideScreenProperty, value);
    }

    /// <summary>读一个子控件停在哪条边。</summary>
    /// <param name="control">子控件。</param>
    /// <returns>停靠边；<see langword="null"/> 表示中间的编辑区。</returns>
    public static ToolWindowAnchor? GetAnchor(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(AnchorProperty);
    }

    /// <summary>设一个子控件停在哪条边。</summary>
    /// <param name="control">子控件。</param>
    /// <param name="value">停靠边；<see langword="null"/> 表示中间的编辑区。</param>
    public static void SetAnchor(Control control, ToolWindowAnchor? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(AnchorProperty, value);
    }

    /// <summary>读一个子控件的权重。</summary>
    /// <param name="control">子控件。</param>
    /// <returns>权重。</returns>
    public static double GetWeight(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(WeightProperty);
    }

    /// <summary>设一个子控件的权重。</summary>
    /// <param name="control">子控件。</param>
    /// <param name="value">权重。</param>
    public static void SetWeight(Control control, double value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(WeightProperty, value);
    }

    /// <summary>读一个子控件是不是同一条边的另一半。</summary>
    /// <param name="control">子控件。</param>
    /// <returns>是则 <see langword="true"/>。</returns>
    public static bool GetIsSplit(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(IsSplitProperty);
    }

    /// <summary>设一个子控件是不是同一条边的另一半。</summary>
    /// <param name="control">子控件。</param>
    /// <param name="value">是否。</param>
    public static void SetIsSplit(Control control, bool value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(IsSplitProperty, value);
    }

    /// <summary>读一个子控件在同一格里的分配比例。</summary>
    /// <param name="control">子控件。</param>
    /// <returns>比例。</returns>
    public static double GetSideWeight(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(SideWeightProperty);
    }

    /// <summary>设一个子控件在同一格里的分配比例。</summary>
    /// <param name="control">子控件。</param>
    /// <param name="value">比例。</param>
    public static void SetSideWeight(Control control, double value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(SideWeightProperty, value);
    }

    /// <summary>读一个子控件是不是某条边的拖拽热区。</summary>
    /// <param name="control">子控件。</param>
    /// <returns>是哪条边的热区；不是热区则 <see langword="null"/>。</returns>
    public static ToolWindowAnchor? GetIsResizer(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetValue(IsResizerProperty);
    }

    /// <summary>把一个子控件标成某条边的拖拽热区。</summary>
    /// <param name="control">子控件。</param>
    /// <param name="value">哪条边。</param>
    public static void SetIsResizer(Control control, ToolWindowAnchor? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(IsResizerProperty, value);
    }

    /// <summary>
    /// 一条边被两个窗口分掉时，非 split 的那个占多大比例。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowPane.kt:722 · addAndSplitDockedComponentCmd</c>：
    /// <c>splitter.proportion = normalizeWeight(info.sideWeight)</c>，
    /// <c>info</c> 是 <c>isSplit=false</c> 的那个（它是 splitter 的 firstComponent，<c>:721</c>）。
    /// </remarks>
    /// <param name="nonSplitSideWeight">非 split 那个窗口的 sideWeight。</param>
    /// <returns>非 split 那半占的比例。</returns>
    public static double ComputeSplitProportion(double nonSplitSideWeight)
        => ForgeMetrics.NormalizeWeight((float)nonSplitSideWeight);

    /// <summary>
    /// 按权重算出四条边各自的像素尺寸。
    /// </summary>
    /// <remarks>
    /// 单独拆出来是为了能不建 UI 就测。规则见类注释。
    /// </remarks>
    /// <param name="paneSize">整个 pane 的尺寸。</param>
    /// <param name="weights">每条边的权重；没给的边算作 0。</param>
    /// <returns>四条边各自的像素尺寸。</returns>
    internal static DockSizes ComputeSizes(Size paneSize, IReadOnlyDictionary<ToolWindowAnchor, double> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        double Pixels(ToolWindowAnchor anchor, double total)
            => weights.TryGetValue(anchor, out var w)
                ? Math.Floor(total * ForgeMetrics.NormalizeWeight((float)w))
                : 0;

        return new DockSizes(
            Top: Pixels(ToolWindowAnchor.Top, paneSize.Height),
            Bottom: Pixels(ToolWindowAnchor.Bottom, paneSize.Height),
            Left: Pixels(ToolWindowAnchor.Left, paneSize.Width),
            Right: Pixels(ToolWindowAnchor.Right, paneSize.Width));
    }

    /// <summary>
    /// 四条边此刻各自的厚度：**像素是真值**，按 weight 算出来的那份只当播种用。
    /// </summary>
    /// <remarks>
    /// 第一次遇到某条边时按 weight 播一次种，之后这条边就一直用记着的像素 ——
    /// 窗口增减的空间全落到编辑区身上。记录只在 <see cref="InvalidateAnchorSizes"/> 时丢掉
    /// （读档 / 恢复默认布局 / 面板换边或开合），拖分隔条则直接改记录（<see cref="SetAnchorSize"/>）。
    /// <para>
    /// 仍然夹在 pane 之内：窗口缩到比面板还窄时，面板跟着缩（不然编辑区会被挤成负数）。
    /// </para>
    /// </remarks>
    /// <param name="paneSize">整个 pane 的尺寸。</param>
    /// <param name="weights">每条边的权重（只在播种时用）。</param>
    /// <param name="byWeight">按权重算出来的尺寸（同上）。</param>
    /// <returns>实际要用的尺寸。</returns>
    private DockSizes ResolveSizes(
        Size paneSize,
        IReadOnlyDictionary<ToolWindowAnchor, double> weights,
        DockSizes byWeight)
    {
        double Resolve(ToolWindowAnchor anchor, double fromWeight, double total)
        {
            if (!weights.ContainsKey(anchor))
            {
                // 这条边此刻没有占位的面板。覆盖层（取消停靠）不在 weights 里，它自己走
                // OverlayRect → Thickness，所以这儿也别动记录，否则滑出的面板每帧重新播种。
                return fromWeight;
            }

            return Thickness(anchor, fromWeight, total);
        }

        return new DockSizes(
            Top: Resolve(ToolWindowAnchor.Top, byWeight.Top, paneSize.Height),
            Bottom: Resolve(ToolWindowAnchor.Bottom, byWeight.Bottom, paneSize.Height),
            Left: Resolve(ToolWindowAnchor.Left, byWeight.Left, paneSize.Width),
            Right: Resolve(ToolWindowAnchor.Right, byWeight.Right, paneSize.Width));
    }

    /// <summary>
    /// 一条边此刻的厚度：**像素是真值**，没有记录时才按 weight 播种一次。
    /// </summary>
    /// <remarks>
    /// 停靠（<see cref="ResolveSizes"/>）与取消停靠（覆盖层，<see cref="OverlayRect"/>）走同一处，
    /// 两种形态共用同一份记录 —— 面板切成取消停靠再切回来，宽度不会自己变一次。
    /// <para>
    /// ⚠ <b>算尺寸只有这一条路。</b>weight 只在两处露面：没有记录时播一次种，以及反算回去写台账
    /// （存档格式仍是 weight）。<b>排布过程中绝不再经过 weight</b> —— 像素→weight→floor 回像素
    /// 的那一来一回带着量化误差，窄尺寸下足够让边界每帧跳一格，实机表现就是
    /// "拖到小一点时标题栏一跳一跳，拖过某个宽度就好了"。
    /// </para>
    /// </remarks>
    /// <param name="anchor">哪条边。</param>
    /// <param name="byWeight">按 weight 算出来的厚度（只在播种时用）。</param>
    /// <param name="total">这条边的换算基数（横边取高、竖边取宽）。</param>
    /// <returns>实际厚度。</returns>
    private double Thickness(ToolWindowAnchor anchor, double byWeight, double total)
    {
        if (total <= 0)
        {
            return byWeight;
        }

        if (!_pixels.TryGetValue(anchor, out var pixels))
        {
            // 播种：宿主给了默认宽度就用它（竖边才有），否则按 weight 算。
            // 台账里存过用户自己拉的宽度时，读档已经把 weight 改成对应值，播出来就是那个宽度 ——
            // 默认宽度只在"从没拉过"的那份默认 weight 上起作用。
            var seed = !anchor.IsHorizontal()
                && !double.IsNaN(DefaultAnchorWidth)
                && Math.Abs(GetSeedWeight(anchor) - WindowInfo.DefaultWeight) < 0.0005
                    ? Math.Min(DefaultAnchorWidth, total)
                    : byWeight;

            _pixels[anchor] = seed;
            return seed;
        }

        // 夹在 pane 之内：窗口缩到比面板还窄时面板跟着缩，不然编辑区会被挤成负数。
        // ⚠ 夹出来的结果**不写回记录**：窗口再拉大时，面板要回到用户拉的那个宽度。
        var used = Math.Clamp(pixels, 0, Math.Floor(total * ForgeMetrics.NormalizeWeight(1f)));

        // 台账里那份 weight 跟不上了（窗口改了大小）——请宿主按当前 pane 反算着写回去，
        // 否则关窗存下的还是旧比例，下次开起来宽度又变了。
        RequestRebase(anchor, used, total);
        return used;
    }

    /// <summary>锁定的像素与台账里的 weight 对不上时，异步请宿主写回新 weight。</summary>
    /// <param name="anchor">哪条边。</param>
    /// <param name="pixels">锁定的像素。</param>
    /// <param name="total">这条边的换算基数（横边取高、竖边取宽）。</param>
    private void RequestRebase(ToolWindowAnchor anchor, double pixels, double total)
    {
        var current = Children.FirstOrDefault(x => x.IsVisible && GetAnchor(x) == anchor);
        if (current is null)
        {
            return;
        }

        var weight = ForgeMetrics.NormalizeWeight((float)(pixels / total));

        // 差不到一个像素就别折腾了 —— 取整误差会让它每帧都想写一次。
        if (Math.Abs(Math.Floor(total * weight) - Math.Floor(total * ForgeMetrics.NormalizeWeight((float)GetWeight(current)))) < 1)
        {
            return;
        }

        if (!_rebasePending.Add(anchor))
        {
            return;
        }

        // ⚠ 必须异步：这里还在布局里，当场改台账会立刻再触发一次布局（重入）。
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _rebasePending.Remove(anchor);
            AnchorWeightRebased?.Invoke(this, new DockWeightChangedEventArgs(anchor, weight));
        });
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size(
            double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);

        foreach (var (child, rect) in Layout(size))
        {
            child.Measure(rect.Size);
        }

        return size;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var placed = Layout(finalSize);

        foreach (var (child, rect) in placed)
        {
            child.Arrange(rect);
        }

        ClipUnderOverlays(placed, finalSize);
        return finalSize;
    }

    /// <summary>
    /// 把覆盖层压着的那条带，从**所有被它压着的**子控件的绘制里减掉。
    /// </summary>
    /// <remarks>
    /// 取消停靠的面板是**一座浮在底板上的岛**：它四周那条缝，露的应当与停靠时两岛之间那条缝
    /// 是同一层东西 —— 宿主的窗口底板（可能是云母那种活的材质）。可缝底下压着编辑区，
    /// 编辑区是不透明的，底板根本透不上来 —— 拿任何纯色去"填"那条缝，都只会是一条色带。
    /// 唯一做得对的办法是让被压着的那些在那块**不画**，下面的窗口自己露出来。
    /// <para>
    /// 这条规则属于**停靠区这一层**：谁被覆盖层压着，谁就在那条带上不画。
    /// 不是"每块面板各自去处理" —— 面板不知道自己有没有被谁盖住，
    /// 知道的是排布它们的这里。
    /// </para>
    /// <para>
    /// 减的是**整条边的矩形带**（面板 + 它四周的缝），不是面板的圆角形状：
    /// 滑出的面板本来就贴边铺满整条边长，矩形足够；而非矩形裁剪要开离屏缓冲做遮罩，
    /// 图像区每帧都得多合成一次 —— 那儿跑的是相机实时画面，这个开销不能要。
    /// 圆角缺口那几小块照样露底板，因为整条带都不画了。
    /// </para>
    /// </remarks>
    private void ClipUnderOverlays(List<(Control Child, Rect Rect)> placed, Size paneSize)
    {
        var remaining = new Rect(paneSize);

        foreach (var (child, rect) in placed)
        {
            if (child.IsVisible && GetIsOverlay(child) && GetAnchor(child) is { } anchor)
            {
                // 让开的宽度要够**一整条缝**：岛与岛并排时两边各出一半，
                // 而被压着的这块整个被裁掉了，出不了它那一半 —— 由裁剪线替它让出来，
                // 否则挨着覆盖层的那条缝只有半条宽，比别处窄一半。
                remaining = Exclude(remaining, rect.Inflate(child.Margin), anchor);
            }
        }

        var whole = new Rect(paneSize);

        foreach (var (child, rect) in placed)
        {
            // **所有被压着的都要让开**，不只是编辑区：滑出的面板贴边铺满整条边长，
            // 一块底部的滑出面板会同时压住左右停靠的面板 —— 它们不让开，
            // 那条缝里露的就是它们，底板照样透不上来。
            // 拖手不裁：它本来就该骑在覆盖层的边上（还得抓得到）。
            if (GetIsOverlay(child) || GetIsResizer(child) is not null || GetIsSplitResizer(child) is not null)
            {
                child.Clip = null;
                continue;
            }

            // ⚠ Clip 用的是**子控件自己的坐标系**，remaining 算在停靠区的坐标里 —— 要换算过去。
            //   不换算的话，左边有滑出面板时 remaining.X 是几百，套进右面板（自身才两百来宽）
            //   的坐标就成了空矩形，整块面板被裁没。
            // ⚠ 平移的基准是控件的 **Bounds**（排布矩形去掉外边距之后的那个），不是排布矩形本身：
            //   Clip 就长在 Bounds 的坐标系里。用排布矩形去平移，谁有外边距谁就多让一截 ——
            //   表现为同一条缝在挨着不同面板的地方宽窄不一。
            child.Clip = remaining == whole
                ? null
                : new RectangleGeometry(remaining.Translate(-(Vector)child.Bounds.Position));
        }
    }

    /// <summary>从一块矩形里减掉贴着某条边的一条带。</summary>
    private static Rect Exclude(Rect area, Rect band, ToolWindowAnchor anchor) => anchor switch
    {
        ToolWindowAnchor.Left => area.WithX(Math.Max(area.X, band.Right))
            .WithWidth(Math.Max(0, area.Right - Math.Max(area.X, band.Right))),
        ToolWindowAnchor.Right => area.WithWidth(Math.Max(0, Math.Min(area.Right, band.X) - area.X)),
        ToolWindowAnchor.Top => area.WithY(Math.Max(area.Y, band.Bottom))
            .WithHeight(Math.Max(0, area.Bottom - Math.Max(area.Y, band.Bottom))),
        _ => area.WithHeight(Math.Max(0, Math.Min(area.Bottom, band.Y) - area.Y)),
    };

    /// <summary>覆盖层面板贴边的矩形：按自己的 weight 算厚度，沿边铺满。</summary>
    /// <remarks>
    /// ⚠ **厚度与贴的那条边都落在整像素上**，规则与停靠分支的 <see cref="ComputeSizes"/> 同一条
    /// （那边是 <c>Math.Floor</c>）。停靠区自己的宽高由外层 <c>DockPanel</c> 分，带小数是常态：
    /// 不取整的话覆盖层的外沿就停在半个像素上，绘制时糊进**隔壁条带的第一列**，
    /// 而条带内缘那条 1px 分隔线正好住在那一列 —— 表现为"取消停靠时条带与面板之间那条线没了"，
    /// 且只在某些 weight 下发作（边界恰好落回整数时线又回来）。
    /// 两条分支各自取整只会让"停靠时对、滑出时错"这种事再来一次，所以规则写死在同一处语义上。
    /// </remarks>
    private Rect OverlayRect(Size paneSize, ToolWindowAnchor anchor, double weight)
    {
        var w = Math.Clamp(weight, 0.05, 0.95);

        // 贴边的那条外沿：停靠区自己的右 / 下边界也要先落到整像素上，
        // 否则厚度取了整、外沿仍然带半格，照样糊到隔壁。
        var right = Math.Floor(paneSize.Width);
        var bottom = Math.Floor(paneSize.Height);

        // 厚度与停靠那条路同一份记录：滑出的面板同样是"拉多宽就多宽"，
        // 窗口放大时多出来的地方归编辑区。
        // ⚠ 换算基数**按这条边的朝向取一次**（横边取高、竖边取宽，同 ComputeSizes）：
        //   两个方向各算一次会拿同一把锁存两个不相干的数，互相盖来盖去 ——
        //   实机表现是滑出的面板宽度在两个值之间来回跳，还把回写的 weight 一起带偏。
        var total = anchor.IsHorizontal() ? paneSize.Height : paneSize.Width;
        var thickness = Thickness(anchor, Math.Floor(total * w), total);

        return anchor switch
        {
            ToolWindowAnchor.Top => new Rect(0, 0, right, thickness),
            ToolWindowAnchor.Bottom => new Rect(0, bottom - thickness, right, thickness),
            ToolWindowAnchor.Left => new Rect(0, 0, thickness, bottom),
            _ => new Rect(right - thickness, 0, thickness, bottom),
        };
    }

    // 热区骑在边界上，中心对准分界线；宽度 6（registry.properties:290）
    // overlayWeight 有值时这条边站着一块覆盖层（SLIDING）：边界不在停靠尺寸上，
    // 而在覆盖层自己那块的内沿 —— 上游同样给 SLIDING 配了 resizer
    //（ToolWindowPane.kt:565-607 · Resizer.LayeredPane.Left/Right/Top/Bottom）。
    private Rect ResizerRect(
        Size paneSize,
        DockSizes sizes,
        ToolWindowAnchor anchor,
        (double Weight, Thickness Margin)? overlay,
        double verticalBandX,
        double verticalBandWidth,
        double horizontalBandY,
        double horizontalBandHeight)
    {
        // 骑在**看得见的**那条边上：岛让出的缝里没有东西可抓，
        // 抓手对准几何边界的话，一半落在缝里、一半落在编辑区上，就是不在面板边上。
        var slot = overlay is { } o
            ? OverlayRect(paneSize, anchor, o.Weight).Deflate(o.Margin)
            : SlotRect(paneSize, sizes, anchor, verticalBandX, verticalBandWidth, horizontalBandY, horizontalBandHeight);

        return EdgeZone(slot, anchor);
    }

    /// <summary>
    /// 骑在一块矩形朝编辑区那条内沿上的热区：中心对准边界，**长度取这块矩形自己**。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 长度跟着矩形而不是铺满整条边，是本方法存在的理由。非宽屏档下 LEFT/RIGHT
    /// 只占中间那段（上下被 TOP/BOTTOM 夹着），旧写法用 <c>paneSize.Height</c> 铺满，
    /// 热区会比面板本人长出上下两截 —— 摸到 BOTTOM 面板那一截左边界，拖的却是
    /// LEFT 面板的宽度。它平时不发作，只是因为那截上面正好压着别的面板把命中挡掎了 ——
    /// 而遮挡关系取决于 ZIndex 与子控件顺序，那是两件不相干的事。靠遮挡换来的正确不算正确。
    /// </para>
    /// <para>
    /// 合并停靠与覆盖层两条分支：两者只在“该骑在哪块矩形上”上不同（一个是自己那格，
    /// 一个是滑出来那块），“怎么骑”是同一条规则。旧代码把规则写了两遍，
    /// 于是只修其中一遍的人会把另一遍留在原地。
    /// </para>
    /// </remarks>
    private static Rect EdgeZone(Rect slot, ToolWindowAnchor anchor)
    {
        var zone = ForgeMetrics.SplitterMouseZone;
        var half = zone / 2;

        return anchor switch
        {
            ToolWindowAnchor.Left => new Rect(slot.Right - half, slot.Y, zone, slot.Height),
            ToolWindowAnchor.Right => new Rect(slot.X - half, slot.Y, zone, slot.Height),
            ToolWindowAnchor.Top => new Rect(slot.X, slot.Bottom - half, slot.Width, zone),
            _ => new Rect(slot.X, slot.Y - half, slot.Width, zone),
        };
    }

    /// <summary>
    /// 一条边上要是有两个可见窗口，把这一格切成两半。
    /// </summary>
    /// <remarks>
    /// 对应 <c>ToolWindowPane.kt:611-741 · addAndSplitDockedComponentCmd</c>：
    /// <c>isSplit=false</c> 的是 firstComponent、<c>isSplit=true</c> 的是 secondComponent（<c>:703-706</c>、<c>:721-722</c>）。
    /// 切的方向是 <c>anchor.isSplitVertically</c>（<c>:655</c>）——
    /// LEFT/RIGHT 默认竖着切（上下两块，<c>ToolWindowAnchor.java:63-66</c> 加上
    /// `leftHorizontalSplit`/`rightHorizontalSplit` 默认 false），横边则左右分。
    /// </remarks>
    private Rect SplitSlot(Rect slot, ToolWindowAnchor anchor, Control child)
    {
        Control? other = null;
        foreach (var candidate in Children)
        {
            if (!ReferenceEquals(candidate, child)
                && candidate.IsVisible
                && GetAnchor(candidate) == anchor
                && GetIsSplit(candidate) != GetIsSplit(child))
            {
                other = candidate;
                break;
            }
        }

        if (other is null)
        {
            return slot;
        }

        var isSplit = GetIsSplit(child);
        var nonSplit = isSplit ? other : child;
        var proportion = ComputeSplitProportion(GetSideWeight(nonSplit));

        // 竖边默认竖着切（上下），横边左右分
        if (anchor.IsHorizontal())
        {
            var firstWidth = Math.Floor(slot.Width * proportion);
            return isSplit
                ? new Rect(slot.X + firstWidth, slot.Y, slot.Width - firstWidth, slot.Height)
                : new Rect(slot.X, slot.Y, firstWidth, slot.Height);
        }

        var firstHeight = Math.Floor(slot.Height * proportion);
        return isSplit
            ? new Rect(slot.X, slot.Y + firstHeight, slot.Width, slot.Height - firstHeight)
            : new Rect(slot.X, slot.Y, slot.Width, firstHeight);
    }

    /// <summary>某条边那一整格的矩形（还没被 split 切开之前）。</summary>
    private static Rect SlotRect(
        Size paneSize,
        DockSizes sizes,
        ToolWindowAnchor anchor,
        double verticalBandX,
        double verticalBandWidth,
        double horizontalBandY,
        double horizontalBandHeight)
        => anchor switch
        {
            ToolWindowAnchor.Top => new Rect(verticalBandX, 0, verticalBandWidth, sizes.Top),
            ToolWindowAnchor.Bottom => new Rect(
                verticalBandX, paneSize.Height - sizes.Bottom, verticalBandWidth, sizes.Bottom),
            ToolWindowAnchor.Left => new Rect(0, horizontalBandY, sizes.Left, horizontalBandHeight),
            _ => new Rect(paneSize.Width - sizes.Right, horizontalBandY, sizes.Right, horizontalBandHeight),
        };

    /// <summary>这条边当前的切分比例（非 split 那半占多少）。</summary>
    private double SplitProportionFor(ToolWindowAnchor anchor)
    {
        foreach (var child in Children)
        {
            if (child.IsVisible && GetAnchor(child) == anchor && !GetIsSplit(child))
            {
                return ComputeSplitProportion(GetSideWeight(child));
            }
        }

        return 0.5;
    }

    /// <summary>
    /// 格内分割线的热区：骑在分界上，中心对准分界线。
    /// </summary>
    /// <remarks>
    /// 方向与 <see cref="SplitSlot"/> 一致：横边左右分（竖着的一条线），竖边上下分（横着的一条线）。
    /// </remarks>
    private static Rect SplitResizerRect(Rect slot, ToolWindowAnchor anchor, double proportion)
    {
        var zone = ForgeMetrics.SplitterMouseZone;
        var half = zone / 2;

        if (anchor.IsHorizontal())
        {
            var x = slot.X + Math.Floor(slot.Width * proportion);
            return new Rect(x - half, slot.Y, zone, slot.Height);
        }

        var y = slot.Y + Math.Floor(slot.Height * proportion);
        return new Rect(slot.X, y - half, slot.Width, zone);
    }

    private List<(Control Child, Rect Rect)> Layout(Size paneSize)
    {
        // 一条边上有两个窗口时，整格的宽/高取**非 split 那个**的 weight
        // （ToolWindowPane.kt:723-728：竖边竖切走 normalizeWeight(info.weight) 分支）
        var weights = new Dictionary<ToolWindowAnchor, double>();
        var overlays = new Dictionary<ToolWindowAnchor, (double Weight, Thickness Margin)>();
        foreach (var child in Children)
        {
            // 覆盖层（SLIDING）不占地方：不进边尺寸的计算，编辑区照旧铺满 ——
            // 但它那条内沿仍要能拖，所以单记一份自己的 weight。
            if (!child.IsVisible || GetAnchor(child) is not { } anchor)
            {
                continue;
            }

            if (GetIsOverlay(child))
            {
                overlays[anchor] = (GetWeight(child), child.Margin);
                continue;
            }

            if (!weights.ContainsKey(anchor) || !GetIsSplit(child))
            {
                weights[anchor] = GetWeight(child);
            }
        }

        var sizes = ResolveSizes(paneSize, weights, ComputeSizes(paneSize, weights));
        var result = new List<(Control, Rect)>(Children.Count);

        // 默认（非宽屏）：竖分隔器在外 —— TOP/BOTTOM 通栏，LEFT/RIGHT 夹在中间
        var horizontalBandY = IsWideScreen ? 0 : sizes.Top;
        var horizontalBandHeight = IsWideScreen
            ? paneSize.Height
            : Math.Max(0, paneSize.Height - sizes.Top - sizes.Bottom);

        var verticalBandX = IsWideScreen ? sizes.Left : 0;
        var verticalBandWidth = IsWideScreen
            ? Math.Max(0, paneSize.Width - sizes.Left - sizes.Right)
            : paneSize.Width;

        var centerX = sizes.Left;
        var centerY = sizes.Top;
        var centerWidth = Math.Max(0, paneSize.Width - sizes.Left - sizes.Right);
        var centerHeight = Math.Max(0, paneSize.Height - sizes.Top - sizes.Bottom);

        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }

            if (GetIsResizer(child) is { } resizerAnchor)
            {
                var overlay = overlays.TryGetValue(resizerAnchor, out var o) ? o : ((double, Thickness)?)null;

                // 覆盖层盖在编辑区上（ZIndex 1），它的 resizer 得再高一层才抓得到
                child.ZIndex = overlay is null ? 0 : 2;
                result.Add((child, ResizerRect(
                    paneSize, sizes, resizerAnchor, overlay,
                    verticalBandX, verticalBandWidth, horizontalBandY, horizontalBandHeight)));
                continue;
            }

            // 格内分割线：骑在「非 split 那半」与「split 那半」的分界上
            if (GetIsSplitResizer(child) is { } splitAnchor)
            {
                var splitSlot = SlotRect(paneSize, sizes, splitAnchor, verticalBandX, verticalBandWidth, horizontalBandY, horizontalBandHeight);
                if (child is ForgeDockResizer dockResizer)
                {
                    dockResizer.SlotBounds = splitSlot;
                }

                result.Add((child, SplitResizerRect(splitSlot, splitAnchor, SplitProportionFor(splitAnchor))));
                continue;
            }

            var anchor = GetAnchor(child);

            if (GetIsOverlay(child) && anchor is { } overlayAnchor)
            {
                // 盖在编辑区上：尺寸按它自己的 weight 算（而不是与别人分摊的那一份），
                // 贴着自己那条边、铺满整条边的长度；ZIndex 抬高才盖得住编辑区。
                child.ZIndex = 1;
                result.Add((child, OverlayRect(paneSize, overlayAnchor, GetWeight(child))));
                continue;
            }

            child.ZIndex = 0;
            var slot = anchor switch
            {
                ToolWindowAnchor.Top => new Rect(verticalBandX, 0, verticalBandWidth, sizes.Top),
                ToolWindowAnchor.Bottom => new Rect(
                    verticalBandX, paneSize.Height - sizes.Bottom, verticalBandWidth, sizes.Bottom),
                ToolWindowAnchor.Left => new Rect(0, horizontalBandY, sizes.Left, horizontalBandHeight),
                ToolWindowAnchor.Right => new Rect(
                    paneSize.Width - sizes.Right, horizontalBandY, sizes.Right, horizontalBandHeight),
                _ => new Rect(centerX, centerY, centerWidth, centerHeight),
            };

            result.Add((child, anchor is { } a ? SplitSlot(slot, a, child) : slot));
        }

        return result;
    }
}

/// <summary>
/// 四条边各自的像素尺寸。
/// </summary>
/// <param name="Top">上边的高。</param>
/// <param name="Bottom">下边的高。</param>
/// <param name="Left">左边的宽。</param>
/// <param name="Right">右边的宽。</param>
internal readonly record struct DockSizes(double Top, double Bottom, double Left, double Right);
