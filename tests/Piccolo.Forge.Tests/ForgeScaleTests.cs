using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// JBUIScale.kt 的缩放规则。
public class ForgeScaleTests
{
    [Theory]
    // JBUIScale.kt:399-401 —— (factor * i).roundToInt()
    [InlineData(40, 1.0f, 40)]
    [InlineData(40, 1.25f, 50)]
    [InlineData(41, 1.25f, 51)]
    [InlineData(13, 1.75f, 23)]      // 22.75 → 23
    [InlineData(11, 1.5f, 17)]       // 16.5  → 17（.5 向上，不是银行家舍入）
    [InlineData(5, 1.5f, 8)]         // 7.5   → 8
    [InlineData(59, 1.25f, 74)]      // 73.75 → 74
    public void Scale_rounds_half_up(int value, float factor, int expected)
        => Assert.Equal(expected, ForgeScale.Scale(value, factor));

    [Fact]
    public void Scale_is_not_bankers_rounding()
    {
        // .NET 的 Math.Round 默认是银行家舍入，16.5 会变成 16；上游是 17
        Assert.Equal(17, ForgeScale.Scale(11, 1.5f));
        Assert.Equal(8, ForgeScale.Scale(5, 1.5f));
    }

    [Theory]
    // JBUIScale.kt:421-426 —— 两个特例 + 截断
    [InlineData(12f, 1.25f, 16)]     // 12 * 1.34 = 16.08 → 16
    [InlineData(12f, 1.75f, 20)]     // 12 * 1.67 = 20.04 → 20
    [InlineData(12f, 1.5f, 18)]
    [InlineData(13f, 1.0f, 13)]
    public void ScaleFontSize_has_two_special_cases_and_truncates(float size, float factor, int expected)
        => Assert.Equal(expected, ForgeScale.ScaleFontSize(size, factor));

    [Theory]
    // JBUIScale.kt:490-495 —— 对齐到 0.25 的整数倍
    [InlineData(1.0f, 1.0f)]
    [InlineData(1.1f, 1.0f)]
    [InlineData(1.2f, 1.25f)]
    [InlineData(1.33f, 1.25f)]
    [InlineData(1.4f, 1.5f)]
    [InlineData(2.0f, 2.0f)]
    public void Discretize_snaps_to_quarter_steps(float raw, float expected)
        => Assert.Equal(expected, ForgeScale.Discretize(raw));

    [Fact]
    public void GetFontScale_is_relative_to_12pt()
    {
        // JBUIScale.kt:270 + :113
        Assert.Equal(1f, ForgeScale.GetFontScale(12f));
        Assert.Equal(1.25f, ForgeScale.GetFontScale(15f));
    }

    [Fact]
    public void Stripe_button_is_50px_at_125_percent()
    {
        // 40（ForgeMetrics）在 1.25 倍下应当正好是 50
        Assert.Equal(50, ForgeScale.Scale((int)ForgeMetrics.StripeButtonSize, 1.25f));
        Assert.Equal(40, ForgeScale.Scale((int)ForgeCompactMetrics.StripeButtonSize, 1.25f));
    }

    [Fact]
    public void Header_height_matches_upstream_in_both_densities()
    {
        // JBUI.java:1178-1180 与 darcula.theme.json 的 ToolWindow.Header.height.compact
        Assert.Equal(41, ForgeMetrics.HeaderHeight);
        Assert.Equal(31, ForgeCompactMetrics.HeaderHeight);
    }
}
