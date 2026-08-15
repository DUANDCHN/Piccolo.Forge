using Avalonia;
using Avalonia.Headless.XUnit;
using Piccolo.Forge.Avalonia;
using Piccolo.Forge.Model;
using Piccolo.Forge.Themes;
using Xunit;

namespace Piccolo.Forge.Tests;

/// 控件的度量与状态色。
public class ForgeControlsTests
{
    [AvaloniaFact]
    public void Stripe_button_is_40_by_40_and_32_when_compact()
    {
        // JBUI.java:1322-1324 与 darcula.theme.json 的 StripeToolbar.Button.size.compact
        var button = new ForgeStripeButton();
        button.Measure(Size.Infinity);
        Assert.Equal(new Size(40, 40), button.DesiredSize);

        button.IsCompact = true;
        button.InvalidateMeasure();
        button.Measure(Size.Infinity);
        Assert.Equal(new Size(32, 32), button.DesiredSize);
    }

    [AvaloniaFact]
    public void Stripe_button_metrics_follow_density()
    {
        var button = new ForgeStripeButton();
        Assert.Equal(20, button.IconSize);
        Assert.Equal(5, button.IconPadding);
        Assert.Equal(12, button.Arc);

        button.IsCompact = true;
        Assert.Equal(16, button.IconSize);
        Assert.Equal(4, button.IconPadding);
        Assert.Equal(8, button.Arc);
    }

    [AvaloniaFact]
    public void Visible_beats_hover_in_the_background_priority()
    {
        // SquareStripeButton.kt:208-219 —— 焦点 > 窗口可见 > 悬停 > 无
        var button = new ForgeStripeButton { Palette = ForgePalette.ExpUiDark };

        Assert.Null(button.ResolveBackground());

        button.IsToolWindowVisible = true;
        Assert.Equal(ForgePalette.ExpUiDark.ButtonPressedBackground, button.ResolveBackground());

        button.IsToolWindowActive = true;
        Assert.Equal(ForgePalette.ExpUiDark.ButtonSelectedBackground, button.ResolveBackground());
    }

    [AvaloniaFact]
    public void Active_button_uses_the_selected_foreground()
    {
        var button = new ForgeStripeButton { Palette = ForgePalette.ExpUiLight };
        Assert.Equal(ForgePalette.ExpUiLight.ButtonForeground, button.ResolveForeground());

        button.IsToolWindowActive = true;
        Assert.Equal(ForgePalette.ExpUiLight.ButtonSelectedForeground, button.ResolveForeground());
    }

    [AvaloniaFact]
    public void Separator_reserves_32_by_11()
    {
        // StripeButtonSeparator.kt:22-24
        var separator = new ForgeStripeSeparator();
        separator.Measure(Size.Infinity);
        Assert.Equal(new Size(32, 11), separator.DesiredSize);
    }

    [AvaloniaFact]
    public void Toolbar_is_button_width_plus_one_pixel_border()
    {
        // ToolWindowLeftToolbar.kt:28 —— 右缘一条 1px 线
        var toolbar = new ForgeToolWindowToolbar { Side = StripeSide.Left };
        toolbar.TopGroup.Children.Add(new ForgeStripeButton());

        toolbar.Measure(new Size(double.PositiveInfinity, 600));

        Assert.Equal(41, toolbar.DesiredSize.Width);
        Assert.True(toolbar.HasButtons);
    }

    [AvaloniaFact]
    public void Empty_toolbar_reports_no_buttons()
    {
        // 没有按钮的条带整根隐藏（ToolWindowPaneNewButtonManager.kt:76-99）
        Assert.False(new ForgeToolWindowToolbar().HasButtons);
    }

    [AvaloniaFact]
    public void Header_is_41_high_and_31_when_compact()
    {
        // JBUI.java:1178-1180 与 ToolWindow.Header.height.compact
        var header = new ForgeToolWindowHeader { Title = "Project" };
        header.Measure(new Size(300, double.PositiveInfinity));
        Assert.Equal(41, header.DesiredSize.Height);

        header.IsCompact = true;
        header.Measure(new Size(300, double.PositiveInfinity));
        Assert.Equal(31, header.DesiredSize.Height);
    }
}

/// 颜色字面量的位序。
public class ForgeColorTests
{
    [Fact]
    public void Six_digit_hex_is_opaque_rgb()
    {
        var color = ForgeColor.Parse("#3574F0");
        Assert.Equal(255, color.A);
        Assert.Equal(0x35, color.R);
        Assert.Equal(0x74, color.G);
        Assert.Equal(0xF0, color.B);
    }

    [Fact]
    public void Eight_digit_hex_puts_alpha_last()
    {
        // 上游是 #RRGGBBAA，与 Avalonia 的 #AARRGGBB 相反。
        // 证据：ToolWindow.DragAndDrop.areaBackground = #A0BDF84D，兜底是 JBColor(0xA0BDF8, ...)
        var color = ForgeColor.Parse("#A0BDF84D");
        Assert.Equal(0xA0, color.R);
        Assert.Equal(0xBD, color.G);
        Assert.Equal(0xF8, color.B);
        Assert.Equal(0x4D, color.A);
    }

    [Fact]
    public void Action_button_hover_is_a_translucent_overlay()
    {
        // ActionButton.hoverBackground = #00000012（light）/ #FFFFFF16（dark）
        Assert.Equal(0x12, ForgePalette.ExpUiLight.ButtonHoverBackground.A);
        Assert.Equal(0, ForgePalette.ExpUiLight.ButtonHoverBackground.R);
        Assert.Equal(0x16, ForgePalette.ExpUiDark.ButtonHoverBackground.A);
        Assert.Equal(255, ForgePalette.ExpUiDark.ButtonHoverBackground.R);
    }

    [Fact]
    public void Bad_length_is_rejected()
        => Assert.Throws<FormatException>(() => ForgeColor.Parse("#ABC"));
}
