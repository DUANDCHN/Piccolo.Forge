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
/// </remarks>
public class StripeItemsPanel : Panel
{
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

            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
            height += child.DesiredSize.Height + ForgeMetrics.StripeButtonGap;
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
        var fitWidth = 0.0;
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                fitWidth = Math.Max(fitWidth, child.DesiredSize.Width);
            }
        }

        var y = 0.0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }

            var height = child.DesiredSize.Height;
            child.Arrange(new Rect(0, y, fitWidth, height));
            y += height + ForgeMetrics.StripeButtonGap;
        }

        return new Size(Math.Max(fitWidth, finalSize.Width), Math.Max(y, finalSize.Height));
    }
}
