using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 条带组的排布：竖着堆、间隙 0（ToolWindowToolbar.kt:196）。
public class StripeItemsPanelTests
{
    [AvaloniaFact]
    public void Stacks_children_vertically_without_gaps()
    {
        var panel = new StripeItemsPanel();
        for (var i = 0; i < 3; i++)
        {
            panel.Children.Add(new Border
            {
                Width = ForgeMetrics.StripeButtonSize,
                Height = ForgeMetrics.StripeButtonSize,
            });
        }

        panel.Measure(Size.Infinity);
        panel.Arrange(new Rect(panel.DesiredSize));

        Assert.Equal(ForgeMetrics.StripeButtonSize, panel.DesiredSize.Width);
        Assert.Equal(ForgeMetrics.StripeButtonSize * 3, panel.DesiredSize.Height);

        for (var i = 0; i < panel.Children.Count; i++)
        {
            Assert.Equal(ForgeMetrics.StripeButtonSize * i, panel.Children[i].Bounds.Y);
        }
    }

    [AvaloniaFact]
    public void Collapsed_children_take_no_space()
    {
        var panel = new StripeItemsPanel();
        panel.Children.Add(new Border { Width = 40, Height = 40, IsVisible = false });
        panel.Children.Add(new Border { Width = 40, Height = 40 });

        panel.Measure(Size.Infinity);

        Assert.Equal(40, panel.DesiredSize.Height);
    }
}
