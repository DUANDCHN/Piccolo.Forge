using Avalonia;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 条带的「显示名字」模式。
public class ForgeShowNamesTests
{
    [Theory]
    // SquareStripeButton.kt:331-334 —— split(" ", limit = 2)
    [InlineData("Project", new[] { "Project" })]
    [InlineData("Version Control", new[] { "Version", "Control" })]
    [InlineData("Problems View Extra", new[] { "Problems", "View Extra" })]
    [InlineData("  Terminal  ", new[] { "Terminal" })]
    [InlineData("", new string[0])]
    public void The_title_splits_on_the_first_space_into_at_most_two_lines(string title, string[] expected)
        => Assert.Equal(expected, ForgeStripeButton.SplitTitle(title));

    [AvaloniaFact]
    public void Showing_names_makes_the_button_taller_and_as_wide_as_the_stripe()
    {
        var plain = new ForgeStripeButton { Title = "Project" };
        plain.Measure(Size.Infinity);
        Assert.Equal(new Size(40, 40), plain.DesiredSize);

        var named = new ForgeStripeButton
        {
            Title = "Project",
            ShowName = true,
            StripeWidth = ForgeMetrics.StripeNamedDefaultWidth,
        };
        named.Measure(Size.Infinity);

        Assert.Equal(59, named.DesiredSize.Width);
        Assert.True(named.DesiredSize.Height > 40, "显示名字时按钮要变高");
    }

    [AvaloniaFact]
    public void Two_line_titles_are_taller_than_one_line_ones()
    {
        // 高度 = 基础高 + 间距 + 行数 × 行高（SquareStripeButton.kt:320-327）
        var one = Named("Terminal");
        var two = Named("Version Control");

        Assert.True(two.DesiredSize.Height > one.DesiredSize.Height);
    }

    [AvaloniaFact]
    public void The_stripe_takes_the_custom_width()
    {
        // ResizeStripeManager.kt:215-218 —— 打开时默认 59
        var toolbar = new ForgeToolWindowToolbar { Side = StripeSide.Left, CustomWidth = 59 };
        toolbar.TopGroup.Children.Add(Named("Project"));

        toolbar.Measure(new Size(double.PositiveInfinity, 600));

        Assert.Equal(59, toolbar.DesiredSize.Width);
    }

    [Theory]
    // ResizeStripeManager.kt:138-149 · checkMinMax
    [InlineData(10, false, 40)]
    [InlineData(10, true, 33)]
    [InlineData(59, false, 59)]
    [InlineData(500, false, 100)]
    public void The_custom_width_is_clamped(double width, bool compact, double expected)
        => Assert.Equal(expected, ForgeToolWindowToolbar.ClampCustomWidth(width, compact));

    [AvaloniaFact]
    public void The_frame_switches_the_whole_stripe_at_once()
    {
        var manager = new ToolWindowManager(DefaultToolWindowLayout.CreateV2());
        var frame = new ForgeToolWindowFrame { ShowToolWindowNames = true };
        frame.Attach(manager);

        Assert.Equal(ForgeMetrics.StripeNamedDefaultWidth, frame.LeftToolbar.CustomWidth);
        Assert.All(
            frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>(),
            x => Assert.True(x.ShowName));

        frame.ShowToolWindowNames = false;

        Assert.Equal(0, frame.LeftToolbar.CustomWidth);
        Assert.All(
            frame.LeftToolbar.TopGroup.Children.OfType<ForgeStripeButton>(),
            x => Assert.False(x.ShowName));
    }

    private static ForgeStripeButton Named(string title)
    {
        var button = new ForgeStripeButton
        {
            Title = title,
            ShowName = true,
            StripeWidth = ForgeMetrics.StripeNamedDefaultWidth,
        };
        button.Measure(Size.Infinity);
        return button;
    }
}
