using Avalonia;
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

    static ForgeDockPane()
    {
        AffectsMeasure<ForgeDockPane>(IsWideScreenProperty);
        AnchorProperty.Changed.AddClassHandler<Control>((c, _) => (c.Parent as ForgeDockPane)?.InvalidateMeasure());
        WeightProperty.Changed.AddClassHandler<Control>((c, _) => (c.Parent as ForgeDockPane)?.InvalidateMeasure());
        IsSplitProperty.Changed.AddClassHandler<Control>((c, _) => (c.Parent as ForgeDockPane)?.InvalidateMeasure());
        SideWeightProperty.Changed.AddClassHandler<Control>((c, _) => (c.Parent as ForgeDockPane)?.InvalidateMeasure());
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
    public static DockSizes ComputeSizes(Size paneSize, IReadOnlyDictionary<ToolWindowAnchor, double> weights)
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
        foreach (var (child, rect) in Layout(finalSize))
        {
            child.Arrange(rect);
        }

        return finalSize;
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

    private List<(Control Child, Rect Rect)> Layout(Size paneSize)
    {
        // 一条边上有两个窗口时，整格的宽/高取**非 split 那个**的 weight
        // （ToolWindowPane.kt:723-728：竖边竖切走 normalizeWeight(info.weight) 分支）
        var weights = new Dictionary<ToolWindowAnchor, double>();
        foreach (var child in Children)
        {
            if (!child.IsVisible || GetAnchor(child) is not { } anchor)
            {
                continue;
            }

            if (!weights.ContainsKey(anchor) || !GetIsSplit(child))
            {
                weights[anchor] = GetWeight(child);
            }
        }

        var sizes = ComputeSizes(paneSize, weights);
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

            var anchor = GetAnchor(child);
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
public readonly record struct DockSizes(double Top, double Bottom, double Left, double Right);
